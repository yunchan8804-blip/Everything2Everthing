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

            // H.264는 4096px 초과(>4K, 예: 8K 7680px)를 인코딩하지 못해 "encoder를 열 수 없음"으로 즉시 실패한다.
            // → 고해상도 입력은 8K까지 지원하는 HEVC(H.265)로 인코딩한다.
            var highRes = videoWidth > 4096;
            var h264Container = outExt is ".mp4" or ".mkv" or ".mov";
            // GPU(NVENC) 가속은 mp4/mkv/mov 컨테이너에만 적용 시도하고, 실패하면 CPU로 폴백
            var tryGpu = options.VideoPreferGpu && h264Container;

            async Task<bool> RunAsync(bool gpu)
            {
                var processor = FFMpegArguments
                    .FromFileInput(sourcePath)
                    .OutputToFile(outPath, overwrite: true, o =>
                    {
                        if (gpu) o.WithVideoCodec(highRes ? "hevc_nvenc" : "h264_nvenc");
                        else if (h264Container) o.WithVideoCodec(highRes ? "libx265" : "libx264");
                        // webm/avi/gif 등 비(非) H.264/HEVC 컨테이너는 컨테이너 기본 코덱(vp9 등)을 사용
                    })
                    .CancellableThrough(cancellationToken);
                if (total > TimeSpan.Zero)
                    processor = processor.NotifyOnProgress(
                        percent => progress?.Report(Math.Clamp(percent / 100.0, 0, 1)), total);
                return await processor.ProcessAsynchronously(throwOnError: true, ffOptions).ConfigureAwait(false);
            }

            bool ok;
            try
            {
                ok = await RunAsync(tryGpu).ConfigureAwait(false);
            }
            catch when (tryGpu && !cancellationToken.IsCancellationRequested)
            {
                // NVENC 미지원(GPU 없음 등) → CPU 인코더로 폴백
                progress?.Report(0.05);
                ok = await RunAsync(false).ConfigureAwait(false);
            }

            progress?.Report(1.0);
            return ok && File.Exists(outPath)
                ? ConvertResult.Ok(sourcePath, new[] { outPath })
                : ConvertResult.Fail(sourcePath, "FFmpeg 변환에 실패했습니다.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return ConvertResult.Fail(sourcePath, $"FFmpeg 변환 실패: {ex.Message}", ex);
        }
    }
}
