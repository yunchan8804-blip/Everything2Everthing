using Everything2Everything.Core.Providers;
using FFMpegCore;

namespace Everything2Everything.Core.Converters;

/// <summary>
/// FFmpeg로 영상/오디오를 변환·트랜스코딩한다. ffmpeg/ffprobe가 없으면 NotReady로 비활성되며,
/// 사용자가 바이너리를 PATH 또는 앱 폴더에 두면 자동 활성화된다(LGPL 분리 호출 — 본체에 번들하지 않음).
/// </summary>
public sealed class FfmpegProvider : IConverterProvider
{
    private static readonly string[] Video = { ".mp4", ".mkv", ".webm", ".mov", ".avi", ".gif" };
    private static readonly string[] Audio = { ".mp3", ".aac", ".m4a", ".opus", ".ogg", ".flac", ".wav" };
    private static readonly string[] ImageFromVideo = { ".png", ".jpg", ".jpeg", ".webp", ".bmp" };

    public ProviderCapability Capability { get; } = new(
        Id: "ffmpeg",
        DisplayName: "영상·오디오 (FFmpeg)",
        SupportedConversions: BuildPairs(),
        Status: ProviderStatus.RequiresExternal,
        Summary: "FFmpeg로 영상/오디오를 변환·트랜스코딩하고 영상에서 오디오를 추출합니다. ffmpeg가 없으면 비활성됩니다.",
        ExternalDependencies: new[]
        {
            new ExternalDependency(
                Name: "FFmpeg (LGPL)",
                Description: "ffmpeg.exe와 ffprobe.exe를 시스템 PATH 또는 %LOCALAPPDATA%\\Everything2Everything\\ffmpeg 에 두면 활성화됩니다. BtbN의 win64-lgpl-shared 빌드를 권장합니다.",
                DownloadUrl: "https://github.com/BtbN/FFmpeg-Builds/releases",
                IsRequired: true),
        },
        RoadmapNote: "하드웨어 인코더(nvenc/qsv) 우선·자동 다운로드·정밀 진행률은 후속 확장.");

    private static IReadOnlyList<ConversionPair> BuildPairs()
    {
        var pairs = new List<ConversionPair>();
        pairs.AddRange(ProviderCapability.PairsFromMatrix(Video, Video, LossClass.Recode));
        pairs.AddRange(ProviderCapability.PairsFromMatrix(Audio, Audio, LossClass.Recode));
        pairs.AddRange(ProviderCapability.PairsFromMatrix(Video, Audio, LossClass.Recode)); // 영상 → 오디오 추출
        pairs.AddRange(ProviderCapability.PairsFromMatrix(Video, ImageFromVideo, LossClass.Rasterize)); // 영상 → 대표 프레임 이미지
        // 동일 포맷 self-edge: 같은 컨테이너로 재인코딩(=압축). mp4→mp4로 6GB 8K를 줄이는 사용 사례.
        // self-edge는 ProviderRegistry에서 '다른 형식으로 변환' 목록에선 제외되지만, 명시 요청 시 엔진이 실행한다.
        // gif는 제외 — 이미지로도 다뤄지므로(Magick) ffmpeg 미설치 시 무해한 Skip이 하드 실패로 바뀌는 회귀를 막는다.
        foreach (var v in Video) if (!string.Equals(v, ".gif", StringComparison.OrdinalIgnoreCase)) pairs.Add(new ConversionPair(v, v, LossClass.Recode));
        foreach (var a in Audio) pairs.Add(new ConversionPair(a, a, LossClass.Recode));
        return pairs;
    }

    public Task<ProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        if (!ExternalToolDetector.TryFindFfmpeg(out _))
            return Task.FromResult(ProviderAvailability.NotReady(
                "FFmpeg가 설치되어 있지 않습니다. ffmpeg.exe/ffprobe.exe를 PATH 또는 %LOCALAPPDATA%\\Everything2Everything\\ffmpeg 에 두면 활성화됩니다.",
                Capability.ExternalDependencies));
        return Task.FromResult(ProviderAvailability.Ready);
    }

    public async Task<ConvertResult> ConvertAsync(
        string sourcePath, string outputDirectory, string outputExtension,
        ConvertOptions options, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (!ExternalToolDetector.TryFindFfmpeg(out var dir))
            return ConvertResult.Fail(sourcePath, "FFmpeg를 찾을 수 없습니다.");

        var outExt = ConversionPair.Normalize(outputExtension);
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var outPath = OutputPathHelper.ResolveOutputPath(outputDirectory, baseName, null, outExt, options.OnCollision);
        if (OutputPathHelper.ShouldSkip(outPath, options.OnCollision))
            return ConvertResult.Skip(sourcePath, "기존 파일이 있어 건너뜁니다.");

        var ffOptions = new FFOptions { BinaryFolder = dir };
        var inExt = ConversionPair.Normalize(Path.GetExtension(sourcePath));
        var isImageOut = outExt is ".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp";
        try
        {
            progress?.Report(0.1);

            // 영상 → 이미지: 대표 프레임 1장 추출 (gif 경유 멀티홉을 피해 즉시 처리)
            if (isImageOut && Array.IndexOf(Video, inExt) >= 0)
            {
                var okImg = await FFMpegArguments
                    .FromFileInput(sourcePath)
                    .OutputToFile(outPath, overwrite: true, o => o.WithCustomArgument("-frames:v 1 -update 1"))
                    .CancellableThrough(cancellationToken)
                    .ProcessAsynchronously(throwOnError: true, ffOptions)
                    .ConfigureAwait(false);
                progress?.Report(1.0);
                return okImg && File.Exists(outPath)
                    ? ConvertResult.Ok(sourcePath, new[] { outPath })
                    : ConvertResult.Fail(sourcePath, "영상에서 프레임 추출에 실패했습니다.");
            }

            // 진행률 best-effort + 코덱 선택용 해상도 파악: ffprobe로 길이·가로해상도 조회
            TimeSpan total = TimeSpan.Zero;
            int videoWidth = 0;
            try
            {
                var info = await FFProbe.AnalyseAsync(sourcePath, ffOptions, cancellationToken).ConfigureAwait(false);
                total = info.Duration;
                videoWidth = info.PrimaryVideoStream?.Width ?? 0;
            }
            catch { /* 분석 실패 시 진행률·코덱은 기본값 사용 */ }

            var v = options.Video;
            var isAudioOut = FfmpegArgBuilder.IsAudioContainer(outExt);
            var resolvedCodec = FfmpegArgBuilder.ResolveVideoCodec(v.Codec, outExt, videoWidth);

            // 입력측 트림(시킹) 적용 — VideoEncodeOptions.TrimStart/TrimEnd는 영상·오디오 출력 공통.
            void AddInput(FFMpegArgumentOptions inOpt)
            {
                if (v.TrimStart is TimeSpan ss && ss > TimeSpan.Zero) inOpt.Seek(ss);
            }
            void AddDuration(FFMpegArgumentOptions o)
            {
                if (v.TrimEnd is TimeSpan en && en > TimeSpan.Zero)
                {
                    var start = v.TrimStart ?? TimeSpan.Zero;
                    if (en > start) o.WithDuration(en - start);
                }
            }

            // 한 계층(인코더)으로 단일 패스 실행. 인자는 FfmpegArgBuilder가 계층별로 생성.
            async Task<bool> RunSingleAsync(EncoderTier tier)
            {
                var outputArgs = FfmpegArgBuilder.BuildOutputArguments(options, outExt, tier, videoWidth);
                var processor = FFMpegArguments
                    .FromFileInput(sourcePath, true, AddInput)
                    .OutputToFile(outPath, overwrite: true, o => { o.WithCustomArgument(outputArgs); AddDuration(o); })
                    .CancellableThrough(cancellationToken);
                if (total > TimeSpan.Zero)
                    processor = processor.NotifyOnProgress(p => progress?.Report(Math.Clamp(p / 100.0, 0, 1)), total);
                return await processor.ProcessAsynchronously(throwOnError: true, ffOptions).ConfigureAwait(false);
            }

            // 2패스(목표 비트레이트 최고 품질) — ffmpeg 2회 호출, passlog는 임시 경로로 격리.
            async Task<bool> RunTwoPassAsync()
            {
                var passLog = Path.Combine(Path.GetTempPath(), "e2e_2pass_" + Guid.NewGuid().ToString("N"));
                try
                {
                    var args = FfmpegArgBuilder.BuildOutputArguments(options, outExt, EncoderTier.Cpu, videoWidth);
                    await FFMpegArguments
                        .FromFileInput(sourcePath, true, AddInput)
                        .OutputToFile("NUL", overwrite: true, o =>
                        {
                            o.WithCustomArgument(args);
                            o.WithCustomArgument($"-pass 1 -passlogfile \"{passLog}\" -an -f null");
                            AddDuration(o);
                        })
                        .CancellableThrough(cancellationToken)
                        .ProcessAsynchronously(throwOnError: true, ffOptions).ConfigureAwait(false);
                    progress?.Report(0.5);

                    var proc2 = FFMpegArguments
                        .FromFileInput(sourcePath, true, AddInput)
                        .OutputToFile(outPath, overwrite: true, o =>
                        {
                            o.WithCustomArgument(args);
                            o.WithCustomArgument($"-pass 2 -passlogfile \"{passLog}\"");
                            AddDuration(o);
                        })
                        .CancellableThrough(cancellationToken);
                    if (total > TimeSpan.Zero)
                        proc2 = proc2.NotifyOnProgress(p => progress?.Report(Math.Clamp(0.5 + p / 200.0, 0, 1)), total);
                    return await proc2.ProcessAsynchronously(throwOnError: true, ffOptions).ConfigureAwait(false);
                }
                finally
                {
                    try
                    {
                        foreach (var f in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(passLog) + "*"))
                            File.Delete(f);
                    }
                    catch { /* 임시 로그 정리 실패 무시 */ }
                }
            }

            var twoPass = !isAudioOut && v.RateControl == RateControlMode.TwoPass && v.VideoBitrateKbps is > 0;

            // 시도할 인코더 계층: GPU 적용 가능하면 NVENC→QSV→AMF→CPU, 아니면 CPU만.
            var gpuOk = !isAudioOut && options.VideoPreferGpu
                        && FfmpegArgBuilder.GpuApplicable(v, resolvedCodec, outExt) && !twoPass;
            var tiers = gpuOk
                ? new[] { EncoderTier.Nvenc, EncoderTier.Qsv, EncoderTier.Amf, EncoderTier.Cpu }
                : new[] { EncoderTier.Cpu };

            bool ok = false;
            Exception? lastEx = null;
            for (var i = 0; i < tiers.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    ok = twoPass ? await RunTwoPassAsync().ConfigureAwait(false)
                                 : await RunSingleAsync(tiers[i]).ConfigureAwait(false);
                    if (ok) break;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) when (i < tiers.Length - 1)
                {
                    // 이 계층(예: NVENC GPU 없음)이 실패하면 다음 계층으로 폴백하며 인자를 재생성.
                    lastEx = ex;
                    progress?.Report(0.05);
                }
            }

            progress?.Report(1.0);
            if (ok && File.Exists(outPath))
                return ConvertResult.Ok(sourcePath, new[] { outPath });
            return ConvertResult.Fail(sourcePath,
                lastEx is null ? "FFmpeg 변환에 실패했습니다." : $"FFmpeg 변환 실패: {lastEx.Message}", lastEx);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return ConvertResult.Fail(sourcePath, $"FFmpeg 변환 실패: {ex.Message}", ex);
        }
    }
}
