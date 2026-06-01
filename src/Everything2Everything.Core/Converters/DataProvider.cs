using System.Globalization;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using CsvHelper;
using Everything2Everything.Core.Providers;

namespace Everything2Everything.Core.Converters;

/// <summary>
/// 표(tabular) 데이터 양방향 변환기. 원자 엣지 csv↔json, csv↔xlsx만 선언하고
/// json↔xlsx는 그래프가 csv 경유로 자동 합성한다(도그푸딩). 순수 .NET, 외부 도구 불필요.
/// </summary>
public sealed class DataProvider : IConverterProvider
{
    public ProviderCapability Capability { get; } = new(
        Id: "data",
        DisplayName: "표 데이터 (CSV/JSON/XLSX)",
        SupportedConversions: new[]
        {
            new ConversionPair(".csv", ".json", LossClass.Container),
            new ConversionPair(".json", ".csv", LossClass.Container),
            new ConversionPair(".csv", ".xlsx", LossClass.Container),
            new ConversionPair(".xlsx", ".csv", LossClass.Container),
        },
        Status: ProviderStatus.Available,
        Summary: "CSV·JSON·XLSX 표 데이터를 양방향 변환합니다 (json↔xlsx는 csv를 거쳐 자동 합성). 평면 표 데이터 기준.",
        ExternalDependencies: Array.Empty<ExternalDependency>(),
        RoadmapNote: "중첩 JSON·다중 시트·Parquet는 후속 확장.");

    public Task<ProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(ProviderAvailability.Ready);

    public Task<ConvertResult> ConvertAsync(
        string sourcePath, string outputDirectory, string outputExtension,
        ConvertOptions options, IProgress<double>? progress, CancellationToken cancellationToken)
        => Task.Run(() => Convert(sourcePath, outputDirectory, outputExtension, options, progress, cancellationToken), cancellationToken);

    private static ConvertResult Convert(
        string sourcePath, string outputDirectory, string outputExtension,
        ConvertOptions options, IProgress<double>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var inExt = ConversionPair.Normalize(Path.GetExtension(sourcePath));
        var outExt = ConversionPair.Normalize(outputExtension);
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var outPath = OutputPathHelper.ResolveOutputPath(outputDirectory, baseName, null, outExt, options.OnCollision);
        if (OutputPathHelper.ShouldSkip(outPath, options.OnCollision))
            return ConvertResult.Skip(sourcePath, "기존 파일이 있어 건너뜁니다.");

        progress?.Report(0.1);
        var tmp = outPath + ".tmp";
        try
        {
            switch (inExt, outExt)
            {
                case (".csv", ".json"): CsvToJson(sourcePath, tmp, ct); break;
                case (".json", ".csv"): JsonToCsv(sourcePath, tmp, ct); break;
                case (".csv", ".xlsx"): CsvToXlsx(sourcePath, tmp, ct); break;
                case (".xlsx", ".csv"): XlsxToCsv(sourcePath, tmp, ct); break;
                default:
                    return ConvertResult.Fail(sourcePath, $"{inExt} → {outExt} 변환을 지원하지 않습니다.");
            }

            if (File.Exists(outPath)) File.Delete(outPath);
            File.Move(tmp, outPath);
        }
        catch (Exception)
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* 정리 실패 무시 */ }
            throw;
        }

        progress?.Report(1.0);
        return ConvertResult.Ok(sourcePath, new[] { outPath });
    }

    private static void CsvToJson(string source, string target, CancellationToken ct)
    {
        var rows = new List<Dictionary<string, object?>>();
        using (var reader = new StreamReader(source))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            csv.Read();
            csv.ReadHeader();
            var headers = csv.HeaderRecord ?? Array.Empty<string>();
            while (csv.Read())
            {
                ct.ThrowIfCancellationRequested();
                var row = new Dictionary<string, object?>(headers.Length);
                foreach (var h in headers)
                    row[h] = InferValue(csv.GetField(h));
                rows.Add(row);
            }
        }
        var json = JsonSerializer.Serialize(rows, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        File.WriteAllText(target, json, new UTF8Encoding(false));
    }

    private static void JsonToCsv(string source, string target, CancellationToken ct)
    {
        var json = File.ReadAllText(source);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new NotSupportedException("JSON 최상위가 객체 배열이어야 CSV로 변환할 수 있습니다.");

        // 헤더 = 모든 객체 키의 합집합(첫 등장 순서 보존)
        var headers = new List<string>();
        var seen = new HashSet<string>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object)
                throw new NotSupportedException("JSON 배열 원소는 모두 객체여야 합니다.");
            foreach (var prop in el.EnumerateObject())
                if (seen.Add(prop.Name)) headers.Add(prop.Name);
        }

        using var writer = new StreamWriter(target, false, new UTF8Encoding(false));
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        foreach (var h in headers) csv.WriteField(h);
        csv.NextRecord();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            ct.ThrowIfCancellationRequested();
            foreach (var h in headers)
                csv.WriteField(el.TryGetProperty(h, out var v) ? JsonToField(v) : "");
            csv.NextRecord();
        }
    }

    private static void CsvToXlsx(string source, string target, CancellationToken ct)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("Sheet1");
        using (var reader = new StreamReader(source))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            csv.Read();
            csv.ReadHeader();
            var headers = csv.HeaderRecord ?? Array.Empty<string>();
            for (var c = 0; c < headers.Length; c++)
                ws.Cell(1, c + 1).Value = headers[c];

            var row = 2;
            while (csv.Read())
            {
                ct.ThrowIfCancellationRequested();
                for (var c = 0; c < headers.Length; c++)
                {
                    var field = csv.GetField(headers[c]);
                    var cell = ws.Cell(row, c + 1);
                    // 숫자/불리언은 형식 유지, 나머지는 문자열
                    if (InferValue(field) is { } val && val is not string)
                        cell.Value = val switch
                        {
                            bool b => b,
                            long l => l,
                            double d => d,
                            _ => field ?? string.Empty,
                        };
                    else
                        cell.Value = field ?? string.Empty;
                }
                row++;
            }
        }
        using (var fs = File.Create(target))
            workbook.SaveAs(fs);
    }

    private static void XlsxToCsv(string source, string target, CancellationToken ct)
    {
        using var workbook = new XLWorkbook(source);
        var ws = workbook.Worksheets.First();
        var range = ws.RangeUsed();

        using var writer = new StreamWriter(target, false, new UTF8Encoding(false));
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        if (range is not null)
        {
            foreach (var r in range.Rows())
            {
                ct.ThrowIfCancellationRequested();
                foreach (var cell in r.Cells())
                    csv.WriteField(cell.GetString());
                csv.NextRecord();
            }
        }
    }

    private static object? InferValue(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        if (bool.TryParse(s, out var b)) return b;
        // 선행 0이 있는 값(우편번호 등)은 문자열로 보존
        if (s.Length > 1 && s[0] == '0' && char.IsDigit(s[1])) return s;
        if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l;
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
        return s;
    }

    private static string JsonToField(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString() ?? "",
        JsonValueKind.Null => "",
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Number => el.GetRawText(),
        _ => el.GetRawText(),
    };
}
