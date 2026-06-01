using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Everything2Everything.Core;
using Everything2Everything.Core.Providers;
using ImageMagick;
using Xunit;

namespace Everything2Everything.Tests;

/// <summary>
/// 엔진 실행경로(ConvertOneAsync / ConvertManyAsync / ExecuteChainAsync / CombineAsync)의 특성화(골든마스터) 테스트.
/// 리팩토링(P2~P6) 이전의 "현재 동작"을 실파일 end-to-end로 박제한다 — 이후 모든 PR의 회귀 머지 게이트.
///
/// 단순 파일 존재 체크 금지(적대적 리뷰 핵심 지적): 출력의 구조 메타(치수/HasAlpha/포맷/프레임수/압축)와
/// 내용 지문(솔리드 컬러의 평균색 우세)을 함께 박제해 alpha 처리·압축·인코딩 정책 회귀까지 잡는다.
/// 외부 도구(Ghostscript/LibreOffice/FFmpeg) 없이 동작하는 순수 .NET 경로만 사용한다.
/// </summary>
public class EngineCharacterizationTests
{
    private static string TempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "e2e_char_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    private static string MakeSolidPng(string dir, string name, MagickColor color, uint w, uint h, bool withAlpha = false)
    {
        var path = Path.Combine(dir, name);
        using var img = new MagickImage(color, w, h);
        if (withAlpha) img.Alpha(AlphaOption.Set);
        img.Write(path);
        return path;
    }

    private const string SvgRedSquare =
        "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><rect width='100' height='80' fill='red'/></svg>";

    /// <summary>출력 이미지의 평균색(1x1 리샘플) RGB. 솔리드 컬러 입력의 내용 보존을 결정적으로 검증한다.</summary>
    private static (double R, double G, double B) MeanRgb(IMagickImage<ushort> img)
    {
        using var c = (IMagickImage<ushort>)img.Clone();
        c.Resize(new MagickGeometry(1, 1) { IgnoreAspectRatio = true });
        using var px = c.GetPixels();
        var color = px.GetPixel(0, 0).ToColor()!;
        return (color.R, color.G, color.B);
    }

    private static (double R, double G, double B) MeanRgb(string path)
    {
        using var img = new MagickImage(path);
        return MeanRgb(img);
    }

    private static ConvertOptions CustomOut(string dir)
        => new() { OutputLocation = OutputLocation.Custom, CustomOutputDirectory = dir };

    // ── 시나리오 1: png→jpg (1홉) — 치수 보존 + alpha 평탄화 + 내용(빨강) 보존 ──────────────
    [Fact]
    public async Task Png_To_Jpg_SingleHop_LocksStructureAndContent()
    {
        var dir = TempDir();
        var png = MakeSolidPng(dir, "red.png", MagickColors.Red, 64, 48, withAlpha: true);
        var engine = Everything2EverythingBootstrap.CreateDefault();

        var r = await engine.ConvertOneAsync(png, ".jpg", CustomOut(dir));

        Assert.Equal(ConvertStatus.Success, r.Status);
        Assert.Single(r.OutputPaths);
        var outPath = r.OutputPaths[0];
        Assert.Equal(".jpg", Path.GetExtension(outPath).ToLowerInvariant());

        using var img = new MagickImage(outPath);
        Assert.Equal(MagickFormat.Jpeg, img.Format);
        Assert.Equal(64u, img.Width);
        Assert.Equal(48u, img.Height);
        Assert.False(img.HasAlpha); // jpg는 alpha 비대응 → 평탄화되어야 한다
        var (rr, gg, bb) = MeanRgb(img);
        Assert.True(rr > gg && rr > bb, $"빨강 우세 기대, 실제 R={rr} G={gg} B={bb}");
    }

    // ── 시나리오 2: svg→jpg (멀티홉 래스터, svg→png→jpg) — 치수/포맷 보존 ────────────────────
    [Fact]
    public async Task Svg_To_Jpg_MultiHop_LocksStructure()
    {
        var dir = TempDir();
        var svg = Path.Combine(dir, "r.svg");
        await File.WriteAllTextAsync(svg, SvgRedSquare);
        var engine = Everything2EverythingBootstrap.CreateDefault();

        var r = await engine.ConvertOneAsync(svg, ".jpg", CustomOut(dir));

        Assert.Equal(ConvertStatus.Success, r.Status);
        Assert.Single(r.OutputPaths);
        using var img = new MagickImage(r.OutputPaths[0]);
        Assert.Equal(MagickFormat.Jpeg, img.Format);
        // Svg.Skia는 내부 스케일로 래스터화하므로 정확한 픽셀수(렌더 스케일 상수)는 박제하지 않고,
        // 의미 있는 불변식인 종횡비 보존(100:80=1.25)과 양수 치수, 내용(빨강)을 박제한다.
        Assert.True(img.Width > 0 && img.Height > 0);
        var aspect = (double)img.Width / img.Height;
        Assert.True(System.Math.Abs(aspect - 1.25) < 0.05, $"종횡비 1.25 기대, 실제 {aspect} ({img.Width}x{img.Height})");
        var (rr, gg, bb) = MeanRgb(img);
        Assert.True(rr > gg && rr > bb, $"빨강 우세 기대, 실제 R={rr} G={gg} B={bb}");
    }

    // ── 시나리오 3: PDF→페이지별 이미지 (직접 변환의 다중 산출물) ───────────────────────────────
    [Fact]
    public async Task Pdf_To_Images_ProducesPerPageOutputs()
    {
        var dir = TempDir();
        var pdf = await BuildTwoPagePdf(dir);
        var engine = Everything2EverythingBootstrap.CreateDefault();

        var r = await engine.ConvertOneAsync(pdf, ".png", CustomOut(dir));

        Assert.Equal(ConvertStatus.Success, r.Status);
        Assert.Equal(2, r.OutputPaths.Count); // 2페이지 → 페이지당 1 png
        foreach (var p in r.OutputPaths)
        {
            using var img = new MagickImage(p);
            Assert.Equal(MagickFormat.Png, img.Format);
            Assert.True(img.Width > 0 && img.Height > 0);
        }
    }

    // ── 시나리오 4: 멀티홉 중간 단계의 다중 산출물 거부 (ConversionEngine.cs:200-205) ───────────
    [Fact]
    public async Task MultiHop_RejectsMultiOutputIntermediate()
    {
        var dir = TempDir();
        var src = Path.Combine(dir, "x.a");
        await File.WriteAllTextAsync(src, "dummy");

        // a→b(2 산출물)→c : 중간 홉 a→b가 여러 파일을 만들면 자동 합성 체인을 이어갈 수 없어야 한다.
        var reg = new ProviderRegistry(new IConverterProvider[]
        {
            new FakeMultiOutputProvider("multi", 2, new ConversionPair(".a", ".b", LossClass.Recode)),
            new FakeProvider("single", new ConversionPair(".b", ".c", LossClass.Recode)),
        });
        var engine = new ConversionEngine(reg);

        var r = await engine.ConvertOneAsync(src, ".c", new ConvertOptions());

        Assert.Equal(ConvertStatus.Failed, r.Status);
        Assert.Contains("여러 파일", r.Message);
    }

    // ── 시나리오 5: 이미지 N장 결합 → TIFF (CombineToSingle) — 프레임수/압축/치수/내용 박제 ──────
    [Fact]
    public async Task Combine_ImagesToTiff_LocksFramesCompressionAndContent()
    {
        var dir = TempDir();
        var red = MakeSolidPng(dir, "1.png", MagickColors.Red, 40, 30);
        var green = MakeSolidPng(dir, "2.png", MagickColors.Lime, 40, 30);
        var blue = MakeSolidPng(dir, "3.png", MagickColors.Blue, 40, 30);
        var engine = Everything2EverythingBootstrap.CreateDefault();

        var results = await engine.ConvertManyAsync(
            new[] { red, green, blue }, ".tif", CustomOut(dir),
            progress: null, batchMode: BatchMode.CombineToSingle);

        Assert.Single(results);
        Assert.Equal(ConvertStatus.Success, results[0].Status);
        Assert.Single(results[0].OutputPaths);

        using var coll = new MagickImageCollection(results[0].OutputPaths[0]);
        Assert.Equal(3, coll.Count); // 3장 → 3프레임 TIFF
        // 현재 동작 박제: ConvertOptions.Tiff.Compression 기본 "lzw"를 SetDefine하지만, combine 경로의
        // 멀티프레임 TIFF 쓰기에서는 실제로 압축이 적용되지 않고 NoCompression으로 저장된다.
        // ⚠️ 잠재 부채(combine의 Tiff 압축 옵션 미적용) — P3 combine 추출 시 정책 통일을 의식적 결정으로 다룬다.
        // (지금 LZW로 '고치면' 동작 보존 원칙 위반이자 골든마스터의 의미 상실 → 현재 동작을 그대로 박제)
        Assert.Equal(CompressionMethod.NoCompression, coll[0].Compression);
        Assert.Equal(40u, coll[0].Width);
        Assert.Equal(30u, coll[0].Height);

        var (r0, g0, b0) = MeanRgb(coll[0]);
        var (r1, g1, b1) = MeanRgb(coll[1]);
        var (r2, g2, b2) = MeanRgb(coll[2]);
        Assert.True(r0 > g0 && r0 > b0, "프레임0 빨강 우세 기대");
        Assert.True(g1 > r1 && g1 > b1, "프레임1 초록 우세 기대");
        Assert.True(b2 > r2 && b2 > g2, "프레임2 파랑 우세 기대");
    }

    // ── 시나리오 6: 이미지 N장 결합 → PDF (CombineToSingle) — 단일 PDF 산출 ──────────────────────
    [Fact]
    public async Task Combine_ImagesToPdf_ProducesSinglePdf()
    {
        var dir = TempDir();
        var pdf = await BuildTwoPagePdf(dir); // 내부적으로 combine→pdf 사용
        Assert.True(File.Exists(pdf));
        Assert.Equal(".pdf", Path.GetExtension(pdf).ToLowerInvariant());

        var header = new byte[5];
        using (var fs = File.OpenRead(pdf)) _ = fs.Read(header, 0, 5);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(header)); // 유효 PDF 헤더
        Assert.True(new FileInfo(pdf).Length > 200);
    }

    // ── 시나리오 7: 멀티홉 마지막 홉 Skip 존중 (ConversionEngine.cs:182-192) ──────────────────────
    [Fact]
    public async Task MultiHop_LastHopSkip_IsRespected()
    {
        var dir = TempDir();
        var svg = Path.Combine(dir, "r.svg");
        await File.WriteAllTextAsync(svg, SvgRedSquare);
        var engine = Everything2EverythingBootstrap.CreateDefault();

        // 1차: svg→png→jpg 로 jpg 생성
        var first = await engine.ConvertOneAsync(svg, ".jpg", CustomOut(dir));
        Assert.Equal(ConvertStatus.Success, first.Status);

        // 2차: 동일 출력이 존재 + OnCollision=Skip → 마지막 홉(png→jpg)이 Skip을 반환, 엔진은 Skip으로 존중
        var skipOpts = CustomOut(dir);
        skipOpts.OnCollision = NameCollision.Skip;
        var second = await engine.ConvertOneAsync(svg, ".jpg", skipOpts);
        Assert.Equal(ConvertStatus.Skipped, second.Status);
    }

    // ── 시나리오 8: 단일 홉 Skip 통과 (ConversionEngine.cs:133-145) ────────────────────────────────
    [Fact]
    public async Task SingleHop_Skip_PassesThrough()
    {
        var dir = TempDir();
        var png = MakeSolidPng(dir, "red.png", MagickColors.Red, 32, 32);
        var engine = Everything2EverythingBootstrap.CreateDefault();

        var first = await engine.ConvertOneAsync(png, ".jpg", CustomOut(dir));
        Assert.Equal(ConvertStatus.Success, first.Status);

        var skipOpts = CustomOut(dir);
        skipOpts.OnCollision = NameCollision.Skip;
        var second = await engine.ConvertOneAsync(png, ".jpg", skipOpts);
        Assert.Equal(ConvertStatus.Skipped, second.Status);
    }

    // ── 시나리오 9: 동일포맷 + self-edge 없음 → Skip (ConversionEngine.cs:94-95) ───────────────────
    [Fact]
    public async Task SameFormat_NoSelfEdge_Skips()
    {
        var dir = TempDir();
        var csv = Path.Combine(dir, "t.csv");
        await File.WriteAllTextAsync(csv, "a,b\n1,2\n");
        var engine = Everything2EverythingBootstrap.CreateDefault();

        // csv는 csv→csv self-edge가 없으므로 동일포맷 변환은 Skip 되어야 한다.
        var r = await engine.ConvertOneAsync(csv, ".csv", CustomOut(dir));

        Assert.Equal(ConvertStatus.Skipped, r.Status);
        Assert.Contains("동일", r.Message);
    }

    // ── 시나리오 10: png→png 재압축 (imageoptim self-edge) — alpha 보존 ────────────────────────────
    [Fact]
    public async Task Png_To_Png_Optimize_PreservesAlpha()
    {
        var dir = TempDir();
        // 진짜 반투명(alpha=0.5) 빨강 — 불투명 alpha는 PNG 저장 시 제거되므로 실제 투명도가 있어야 보존을 검증할 수 있다.
        var png = Path.Combine(dir, "a.png");
        using (var src = new MagickImage(new MagickColor(65535, 0, 0, 32768), 50, 50))
            src.Write(png);
        var engine = Everything2EverythingBootstrap.CreateDefault();

        var r = await engine.ConvertOneAsync(png, ".png", CustomOut(dir));

        Assert.Equal(ConvertStatus.Success, r.Status);
        Assert.Single(r.OutputPaths);
        using var img = new MagickImage(r.OutputPaths[0]);
        Assert.Equal(MagickFormat.Png, img.Format);
        Assert.True(img.HasAlpha); // png self-edge 최적화는 alpha 채널을 보존해야 한다
    }

    /// <summary>combine→pdf 경로로 2페이지 PDF를 만든다(순수 .NET, Ghostscript 불필요).</summary>
    private static async Task<string> BuildTwoPagePdf(string dir)
    {
        var p1 = MakeSolidPng(dir, "pg1.png", MagickColors.White, 120, 160);
        var p2 = MakeSolidPng(dir, "pg2.png", MagickColors.White, 120, 160);
        var engine = Everything2EverythingBootstrap.CreateDefault();
        var results = await engine.ConvertManyAsync(
            new[] { p1, p2 }, ".pdf", CustomOut(dir),
            progress: null, batchMode: BatchMode.CombineToSingle);
        Assert.Single(results);
        Assert.Equal(ConvertStatus.Success, results[0].Status);
        return results[0].OutputPaths[0];
    }
}

/// <summary>중간 홉의 다중 산출물 거부(line 200-205) 특성화용 — 지정 개수의 더미 출력을 반환한다.</summary>
internal sealed class FakeMultiOutputProvider : IConverterProvider
{
    private readonly int _count;

    public ProviderCapability Capability { get; }

    public FakeMultiOutputProvider(string id, int outputCount, params ConversionPair[] pairs)
    {
        _count = outputCount;
        Capability = new ProviderCapability(
            Id: id, DisplayName: id, SupportedConversions: pairs,
            Status: ProviderStatus.Available, Summary: id,
            ExternalDependencies: Array.Empty<ExternalDependency>());
    }

    public Task<ProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(ProviderAvailability.Ready);

    public Task<ConvertResult> ConvertAsync(string sourcePath, string outputDirectory, string outputExtension,
        ConvertOptions options, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var outs = Enumerable.Range(0, _count)
            .Select(i => Path.Combine(outputDirectory, $"{baseName}_{i}{outputExtension}"))
            .ToArray();
        return Task.FromResult(ConvertResult.Ok(sourcePath, outs));
    }
}
