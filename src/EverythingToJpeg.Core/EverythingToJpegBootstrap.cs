using EverythingToJpeg.Core.Providers;

namespace EverythingToJpeg.Core;

public static class EverythingToJpegBootstrap
{
    public static ConversionEngine CreateDefault()
    {
        var magick = new Converters.MagickProvider();
        var pdf = new Converters.PdfProvider();
        var providers = new IConverterProvider[]
        {
            magick,
            new Converters.HeicProvider(magick),
            pdf,
            new Converters.DocxProvider(pdf),
            new Converters.HtmlProvider(),
            new Converters.HwpxProvider(),
        };
        return new ConversionEngine(new ProviderRegistry(providers));
    }
}
