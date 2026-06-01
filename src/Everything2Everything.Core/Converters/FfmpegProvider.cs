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
        try
        {
            progress?.Report(0.05);

            // 진행률 best-effort: ffprobe로 길이를 알면 시간 기반 보고
            TimeSpan total = TimeSpan.Zero;
            try { total = (await FFProbe.AnalyseAsync(sourcePath, ffOptions, cancellationToken).ConfigureAwait(false)).Duration; }
            catch { /* 길이 미상이면 진행률 생략 */ }

            var processor = FFMpegArguments
                .FromFileInput(sourcePath)
                .OutputToFile(outPath, overwrite: true)
                .CancellableThrough(cancellationToken);

            if (total > TimeSpan.Zero)
                processor = processor.NotifyOnProgress(
                    percent => progress?.Report(Math.Clamp(percent / 100.0, 0, 1)), total);

            var ok = await processor.ProcessAsynchronously(throwOnError: true, ffOptions).ConfigureAwait(false);
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
