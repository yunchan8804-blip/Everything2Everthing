namespace EverythingToJpeg.Core;

public enum OutputLocation
{
    SubfolderBesideSource,
    SameFolderAsSource,
    Custom
}

public enum NameCollision
{
    AppendNumber,
    Overwrite,
    Skip
}

public sealed class ConvertOptions
{
    public int Quality { get; set; } = 92;

    public OutputLocation OutputLocation { get; set; } = OutputLocation.SubfolderBesideSource;

    public string SubfolderSuffix { get; set; } = "_jpeg";

    public string? CustomOutputDirectory { get; set; }

    public NameCollision OnCollision { get; set; } = NameCollision.AppendNumber;

    public int? MaxLongEdgePixels { get; set; }

    public int PdfDpi { get; set; } = 200;

    public bool KeepExifWhenPossible { get; set; } = true;

    public bool FlattenTransparency { get; set; } = true;

    public string TransparencyBackground { get; set; } = "#FFFFFF";

    public static ConvertOptions Quick() => new();
}
