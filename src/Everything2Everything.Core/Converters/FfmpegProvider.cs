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

            // 진행률 best-effort: ffprobe로 길이를 알면 시간 기반 보고
            TimeSpan total = TimeSpan.Zero;
            try { total = (await FFProbe.AnalyseAsync(sourcePath, ffOptions, cancellationToken).ConfigureAwait(false)).Duration; }
            catch { /* 길이 미상이면 진행률 생략 */ }

            // GPU(NVENC) 가속은 H.264 컨테이너(mp4/mkv/mov)에만 적용 시도하고, 실패하면 CPU로 폴백
            var tryGpu = options.VideoPreferGpu && (outExt is ".mp4" or ".mkv" or ".mov");

            async Task<bool> RunAsync(bool gpu)
            {
                var processor = FFMpegArguments
                    .FromFileInput(sourcePath)
                    .OutputToFile(outPath, overwrite: true, o =>
                    {
                        if (gpu) o.WithVideoCodec("h264_nvenc");
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
