using System.Windows.Media.Imaging;
using ImageMagick;
using PDFtoImage;
using PhotoSauce.MagicScaler;
using PhotoSauce.NativeCodecs.Libheif;
using SkiaSharp;

namespace EverythingToJpeg.Core;

public sealed record PreviewResult(BitmapSource? Image, string? Reason, string? Dimensions, int? PageCount);

public static class PreviewService
{
    private static int _heifConfigured;

    public static async Task<PreviewResult> CreateAsync(string path, int maxLongEdge = 720, CancellationToken ct = default)
    {
        if (!File.Exists(path)) return new PreviewResult(null, "파일을 찾을 수 없습니다.", null, null);

        var ext = Path.GetExtension(path).ToLowerInvariant();
        try
        {
            return ext switch
            {
                ".pdf" => await Task.Run(() => RenderPdf(path, maxLongEdge), ct).ConfigureAwait(false),
                ".heic" or ".heif" => await Task.Run(() => RenderHeic(path, maxLongEdge), ct).ConfigureAwait(false),
                ".html" or ".htm" => new PreviewResult(null, "HTML 미리보기는 변환 시점에 렌더됩니다.", null, null),
                ".doc" or ".docx" or ".hwp" or ".hwpx" =>
                    new PreviewResult(null, "문서 미리보기는 다음 업데이트에서 지원합니다.", null, null),
                _ => await Task.Run(() => RenderViaMagick(path, maxLongEdge), ct).ConfigureAwait(false),
            };
        }
        catch (Exception ex)
        {
            return new PreviewResult(null, "미리보기 생성 실패: " + ex.Message, null, null);
        }
    }

    private static PreviewResult RenderViaMagick(string path, int maxLongEdge)
    {
        using var image = new MagickImage(path);
        var w = (int)image.Width;
        var h = (int)image.Height;
        try { image.AutoOrient(); } catch { }
        if (image.HasAlpha)
        {
            image.BackgroundColor = MagickColors.White;
            image.Alpha(AlphaOption.Remove);
            image.Alpha(AlphaOption.Off);
        }
        if (w > maxLongEdge || h > maxLongEdge)
        {
            var geom = new MagickGeometry((uint)maxLongEdge, (uint)maxLongEdge) { IgnoreAspectRatio = false };
            image.Resize(geom);
        }
        image.Quality = 88;
        image.Format = MagickFormat.Jpeg;
        var bytes = image.ToByteArray();
        return new PreviewResult(BytesToBitmap(bytes), null, $"{w} × {h}", null);
    }

    private static PreviewResult RenderHeic(string path, int maxLongEdge)
    {
        if (Interlocked.Exchange(ref _heifConfigured, 1) == 0)
            CodecManager.Configure(c => c.UseLibheif());

        var tempPng = Path.Combine(Path.GetTempPath(), $"e2j_pv_{Guid.NewGuid():N}.png");
        try
        {
            MagicImageProcessor.ProcessImage(path, tempPng, ProcessImageSettings.Default);
            return RenderViaMagick(tempPng, maxLongEdge);
        }
        finally
        {
            try { if (File.Exists(tempPng)) File.Delete(tempPng); } catch { }
        }
    }

    private static PreviewResult RenderPdf(string path, int maxLongEdge)
    {
        int pageCount;
        using (var probe = File.OpenRead(path))
        {
            pageCount = Conversion.GetPageCount(probe);
        }
        if (pageCount <= 0) return new PreviewResult(null, "PDF 페이지가 없습니다.", null, 0);

        var ms = new MemoryStream();
        using (var input = File.OpenRead(path))
        {
            var renderOptions = new RenderOptions
            {
                Dpi = 144,
                BackgroundColor = SKColors.White,
                WithAnnotations = true,
                WithFormFill = true,
            };
            Conversion.SaveJpeg(ms, input, page: 0, leaveOpen: false, password: null, options: renderOptions);
        }
        ms.Position = 0;
        var bytes = ms.ToArray();

        // optional resize via Magick
        using var image = new MagickImage(bytes);
        var w = (int)image.Width;
        var h = (int)image.Height;
        if (w > maxLongEdge || h > maxLongEdge)
        {
            var geom = new MagickGeometry((uint)maxLongEdge, (uint)maxLongEdge) { IgnoreAspectRatio = false };
            image.Resize(geom);
            image.Quality = 88;
            image.Format = MagickFormat.Jpeg;
            bytes = image.ToByteArray();
        }
        return new PreviewResult(BytesToBitmap(bytes), null, $"{w} × {h}", pageCount);
    }

    private static BitmapSource BytesToBitmap(byte[] bytes)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = new MemoryStream(bytes);
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }
}
