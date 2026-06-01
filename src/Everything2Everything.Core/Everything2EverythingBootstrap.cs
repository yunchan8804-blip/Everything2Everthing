using Everything2Everything.Core.Providers;

namespace Everything2Everything.Core;

public static class Everything2EverythingBootstrap
{
    public static ConversionEngine CreateDefault()
    {
        var settings = new DpapiSettingsStore();
        var magick = new Converters.MagickProvider();
        var pdf = new Converters.PdfProvider();
        var providers = new IConverterProvider[]
        {
            magick,
            new Converters.HeicProvider(magick),
            pdf,
            new Converters.PdfToolProvider(),
            new Converters.DocxProvider(pdf),
            new Converters.HtmlProvider(),
            new Converters.HwpxProvider(),
            new Converters.OcrProvider(pdf),
            new Converters.DocumentProvider(),
            new Converters.DataProvider(),
            new Converters.VectorProvider(),
            new Converters.ImageOptimProvider(),
            new Converters.LlmProvider(settings),
            new Converters.FfmpegProvider(),
        };
        return new ConversionEngine(new ProviderRegistry(providers));
    }
}
