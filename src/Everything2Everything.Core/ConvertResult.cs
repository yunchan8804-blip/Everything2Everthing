namespace Everything2Everything.Core;

public enum ConvertStatus
{
    Success,
    Skipped,
    Failed
}

public sealed record ConvertResult(
    string SourcePath,
    IReadOnlyList<string> OutputPaths,
    ConvertStatus Status,
    string? Message = null,
    Exception? Error = null)
{
    public static ConvertResult Ok(string source, IReadOnlyList<string> outputs)
        => new(source, outputs, ConvertStatus.Success);

    public static ConvertResult Fail(string source, string message, Exception? ex = null)
        => new(source, Array.Empty<string>(), ConvertStatus.Failed, message, ex);

    public static ConvertResult Skip(string source, string message)
        => new(source, Array.Empty<string>(), ConvertStatus.Skipped, message);
}
