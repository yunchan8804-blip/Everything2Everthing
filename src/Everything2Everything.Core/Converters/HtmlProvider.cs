using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Everything2Everything.Core.Providers;
using ImageMagick;
using Microsoft.Web.WebView2.Core;

namespace Everything2Everything.Core.Converters;

public sealed class HtmlProvider : IConverterProvider
{
    private static readonly string[] HtmlInputs = { ".html", ".htm" };

    private static readonly string[] HtmlOutputs =
        { ".png", ".jpg", ".jpeg", ".webp", ".avif", ".bmp", ".tif", ".tiff", ".pdf" };

    public ProviderCapability Capability { get; } = new(
        Id: "html",
        DisplayName: "HTML / 웹 페이지",
        SupportedConversions: ProviderCapability.PairsFromMatrix(HtmlInputs, HtmlOutputs),
        Status: ProviderStatus.Available,
        Summary: "HTML/HTM을 WebView2로 헤드리스 렌더링하여 이미지 또는 PDF로 저장합니다.",
        ExternalDependencies: new[]
        {
            new ExternalDependency(
                Name: "Microsoft Edge WebView2 Runtime",
                Description: "Windows 11에는 기본 포함되어 있습니다.",
                DownloadUrl: "https://developer.microsoft.com/microsoft-edge/webview2/",
                IsRequired: true),
        },
        RoadmapNote: null);

    public Task<ProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            if (string.IsNullOrEmpty(version))
                return Task.FromResult(ProviderAvailability.NotReady(
                    "WebView2 Runtime이 설치되어 있지 않습니다.",
                    Capability.ExternalDependencies));
            return Task.FromResult(ProviderAvailability.Ready);
        }
        catch (Exception ex)
        {
            return Task.FromResult(ProviderAvailability.NotReady(
                "WebView2 감지 실패: " + ex.Message,
                Capability.ExternalDependencies));
        }
    }

    public async Task<ConvertResult> ConvertAsync(
        string sourcePath,
        string outputDirectory,
        string outputExtension,
        ConvertOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var outExt = ConversionPair.Normalize(outputExtension);
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var path = OutputPathHelper.ResolveOutputPath(outputDirectory, baseName, null, outExt, options.OnCollision);
        if (OutputPathHelper.ShouldSkip(path, options.OnCollision))
            return ConvertResult.Skip(sourcePath, "기존 파일이 있어 건너뜁니다.");

        if (outExt == ".pdf")
        {
            var pdfBytes = await CapturePdfAsync(sourcePath, options, progress, cancellationToken)
                .ConfigureAwait(false);
            await File.WriteAllBytesAsync(path, pdfBytes, cancellationToken).ConfigureAwait(false);
            progress?.Report(1.0);
            return ConvertResult.Ok(sourcePath, new[] { path });
        }

        var pngBytes = await CapturePngAsync(sourcePath, options, progress, cancellationToken)
            .ConfigureAwait(false);

        progress?.Report(0.85);

        await Task.Run(() =>
        {
            using var image = new MagickImage(pngBytes);
            var alphaCapable = outExt is ".png" or ".webp" or ".avif" or ".tif" or ".tiff";
            if ((!alphaCapable || options.FlattenTransparency) && image.HasAlpha)
            {
                image.BackgroundColor = new MagickColor(options.TransparencyBackground);
                image.Alpha(AlphaOption.Remove);
                image.Alpha(AlphaOption.Off);
            }
            if (options.MaxLongEdgePixels is int maxLong && maxLong > 0
                && (image.Width > (uint)maxLong || image.Height > (uint)maxLong))
            {
                image.Resize(new MagickGeometry((uint)maxLong, (uint)maxLong) { IgnoreAspectRatio = false });
            }
            ApplyEncoding(image, outExt, options);
            image.Write(path);
        }, cancellationToken).ConfigureAwait(false);

        progress?.Report(1.0);
        return ConvertResult.Ok(sourcePath, new[] { path });
    }

    private static void ApplyEncoding(IMagickImage<ushort> image, string outputExtension, ConvertOptions options)
    {
        switch (outputExtension)
        {
            case ".jpg":
            case ".jpeg":
                image.Quality = (uint)Math.Clamp(options.Jpeg.Quality, 1, 100);
                image.Format = MagickFormat.Jpeg;
                break;
            case ".png":
                image.Format = MagickFormat.Png;
                break;
            case ".webp":
                image.Quality = (uint)Math.Clamp(options.Webp.Quality, 1, 100);
                if (options.Webp.Lossless)
                    image.Settings.SetDefine(MagickFormat.WebP, "lossless", "true");
                image.Format = MagickFormat.WebP;
                break;
            case ".avif":
                image.Quality = (uint)Math.Clamp(options.Avif.Quality, 1, 100);
                image.Settings.SetDefine(MagickFormat.Avif, "speed", Math.Clamp(options.Avif.Speed, 0, 10).ToString());
                image.Format = MagickFormat.Avif;
                break;
            case ".bmp":
                image.Format = MagickFormat.Bmp;
                break;
            case ".tif":
            case ".tiff":
                if (!string.IsNullOrWhiteSpace(options.Tiff.Compression))
                    image.Settings.SetDefine(MagickFormat.Tiff, "compression", options.Tiff.Compression);
                image.Format = MagickFormat.Tiff;
                break;
        }
    }

    private static Task<byte[]> CapturePngAsync(
        string sourcePath,
        ConvertOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
        => RunOnDispatcherThreadAsync((web, p) => CaptureScreenshotAsync(web, options, p), sourcePath, options, progress, cancellationToken);

    private static Task<byte[]> CapturePdfAsync(
        string sourcePath,
        ConvertOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
        => RunOnDispatcherThreadAsync((web, p) => PrintToPdfAsync(web, options, p), sourcePath, options, progress, cancellationToken);

    private static Task<byte[]> RunOnDispatcherThreadAsync(
        Func<CoreWebView2, IProgress<double>?, Task<byte[]>> capture,
        string sourcePath,
        ConvertOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            try
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                _ = RunCaptureOnDispatcher(capture, dispatcher, sourcePath, options, progress, cancellationToken, tcs);
                Dispatcher.Run();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Name = "Everything2Everything.HtmlCapture";
        thread.Start();

        return tcs.Task;
    }

    private static async Task RunCaptureOnDispatcher(
        Func<CoreWebView2, IProgress<double>?, Task<byte[]>> capture,
        Dispatcher dispatcher,
        string sourcePath,
        ConvertOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken,
        TaskCompletionSource<byte[]> tcs)
    {
        CoreWebView2Controller? controller = null;
        try
        {
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Everything2Everything", "WebView2");
            Directory.CreateDirectory(userDataFolder);

            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder).ConfigureAwait(true);
            progress?.Report(0.15);

            controller = await env.CreateCoreWebView2ControllerAsync(new IntPtr(-3)).ConfigureAwait(true);

            int width = options.HtmlRender.ViewportWidth > 0 ? options.HtmlRender.ViewportWidth : 1280;
            int height = options.HtmlRender.ViewportHeight ?? 720;
            controller.Bounds = new System.Drawing.Rectangle(0, 0, width, height);
            controller.IsVisible = false;

            var web = controller.CoreWebView2;
            progress?.Report(0.3);

            var navTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<CoreWebView2NavigationCompletedEventArgs>? navHandler = null;
            navHandler = (_, e) =>
            {
                web.NavigationCompleted -= navHandler!;
                if (e.IsSuccess) navTcs.TrySetResult(true);
                else navTcs.TrySetException(new InvalidOperationException(
                    $"내비게이션 실패: {e.WebErrorStatus}"));
            };
            web.NavigationCompleted += navHandler;

            var fileUri = new Uri(sourcePath).AbsoluteUri;
            web.Navigate(fileUri);

            using (cancellationToken.Register(() => navTcs.TrySetCanceled()))
            {
                await navTcs.Task.ConfigureAwait(true);
            }
            progress?.Report(0.5);

            if (options.HtmlRender.WaitMilliseconds > 0)
                await Task.Delay(options.HtmlRender.WaitMilliseconds, cancellationToken).ConfigureAwait(true);

            progress?.Report(0.65);

            var bytes = await capture(web, progress).ConfigureAwait(true);
            tcs.TrySetResult(bytes);
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }
        finally
        {
            try { controller?.Close(); } catch { }
            dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
        }
    }

    private static async Task<byte[]> CaptureScreenshotAsync(CoreWebView2 web, ConvertOptions options, IProgress<double>? progress)
    {
        var captureParams = options.HtmlRender.FullPage
            ? "{\"captureBeyondViewport\":true,\"format\":\"png\"}"
            : "{\"format\":\"png\"}";

        var resultJson = await web
            .CallDevToolsProtocolMethodAsync("Page.captureScreenshot", captureParams)
            .ConfigureAwait(true);
        progress?.Report(0.8);

        using var doc = JsonDocument.Parse(resultJson);
        var b64 = doc.RootElement.GetProperty("data").GetString()
            ?? throw new InvalidOperationException("CDP captureScreenshot이 빈 결과를 반환했습니다.");
        return Convert.FromBase64String(b64);
    }

    private static async Task<byte[]> PrintToPdfAsync(CoreWebView2 web, ConvertOptions options, IProgress<double>? progress)
    {
        var resultJson = await web
            .CallDevToolsProtocolMethodAsync("Page.printToPDF", "{\"printBackground\":true,\"preferCSSPageSize\":true}")
            .ConfigureAwait(true);
        progress?.Report(0.8);

        using var doc = JsonDocument.Parse(resultJson);
        var b64 = doc.RootElement.GetProperty("data").GetString()
            ?? throw new InvalidOperationException("CDP printToPDF가 빈 결과를 반환했습니다.");
        return Convert.FromBase64String(b64);
    }
}
