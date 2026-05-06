using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using EverythingToJpeg.Core.Providers;
using ImageMagick;
using Microsoft.Web.WebView2.Core;

namespace EverythingToJpeg.Core.Converters;

public sealed class HtmlProvider : IConverterProvider
{
    public ProviderCapability Capability { get; } = new(
        Id: "html",
        DisplayName: "HTML / 웹 페이지",
        Extensions: new[] { ".html", ".htm" },
        Status: ProviderStatus.Available,
        Summary: "HTML/HTM 파일을 WebView2로 헤드리스 렌더링하여 풀페이지 JPEG로 캡처합니다.",
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
        ConvertOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var path = OutputPathHelper.ResolveOutputPath(outputDirectory, baseName, null, options.OnCollision);
        if (OutputPathHelper.ShouldSkip(path, options.OnCollision))
            return ConvertResult.Skip(sourcePath, "기존 파일이 있어 건너뜁니다.");

        var pngBytes = await CapturePngAsync(sourcePath, options, progress, cancellationToken)
            .ConfigureAwait(false);

        progress?.Report(0.85);

        await Task.Run(() =>
        {
            using var image = new MagickImage(pngBytes);
            if (options.FlattenTransparency && image.HasAlpha)
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
            image.Quality = (uint)Math.Clamp(options.Quality, 1, 100);
            image.Format = MagickFormat.Jpeg;
            image.Write(path);
        }, cancellationToken).ConfigureAwait(false);

        progress?.Report(1.0);
        return ConvertResult.Ok(sourcePath, new[] { path });
    }

    private static Task<byte[]> CapturePngAsync(
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
                _ = RunCaptureOnDispatcher(dispatcher, sourcePath, options, progress, cancellationToken, tcs);
                Dispatcher.Run();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Name = "EverythingToJpeg.HtmlCapture";
        thread.Start();

        return tcs.Task;
    }

    private static async Task RunCaptureOnDispatcher(
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
                "EverythingToJpeg", "WebView2");
            Directory.CreateDirectory(userDataFolder);

            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder).ConfigureAwait(true);
            progress?.Report(0.15);

            // HWND_MESSAGE = (IntPtr)(-3) → headless message-only parent
            controller = await env.CreateCoreWebView2ControllerAsync(new IntPtr(-3)).ConfigureAwait(true);

            int width = options.HtmlViewportWidth > 0 ? options.HtmlViewportWidth : 1280;
            int height = options.HtmlViewportHeight ?? 720;
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

            if (options.HtmlWaitMilliseconds > 0)
                await Task.Delay(options.HtmlWaitMilliseconds, cancellationToken).ConfigureAwait(true);

            progress?.Report(0.65);

            // Use CDP for full-page screenshot beyond viewport
            var captureParams = options.HtmlFullPage
                ? "{\"captureBeyondViewport\":true,\"format\":\"png\"}"
                : "{\"format\":\"png\"}";

            var resultJson = await web
                .CallDevToolsProtocolMethodAsync("Page.captureScreenshot", captureParams)
                .ConfigureAwait(true);
            progress?.Report(0.8);

            using var doc = JsonDocument.Parse(resultJson);
            var b64 = doc.RootElement.GetProperty("data").GetString()
                ?? throw new InvalidOperationException("CDP captureScreenshot이 빈 결과를 반환했습니다.");
            var pngBytes = Convert.FromBase64String(b64);

            tcs.TrySetResult(pngBytes);
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
}
