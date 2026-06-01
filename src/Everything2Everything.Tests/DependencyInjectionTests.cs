using System.Linq;
using Everything2Everything.Core;
using Everything2Everything.Core.Converters;
using Everything2Everything.Core.Providers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Everything2Everything.Tests;

/// <summary>
/// DI 컴포지션 루트(AddEverything2Everything) 등록 규약 검증. Scrutor 자동 등록이 하드코딩 14개와
/// 동일하게 모든 Provider를 해소하고, 생성자 의존(Magick/Pdf/Settings)이 자동 와이어링되는지 보증한다.
/// 적대적 리뷰 지적: AsSelf 누락 시 Heic/Docx/Ocr 의존 해소가 silent 실패 → 일부 Provider 런타임 누락.
/// </summary>
public class DependencyInjectionTests
{
    [Fact]
    public void AddEverything2Everything_RegistersAll14Providers()
    {
        var services = new ServiceCollection();
        services.AddEverything2Everything();
        using var sp = services.BuildServiceProvider();

        var providers = sp.GetServices<IConverterProvider>().ToList();
        Assert.Equal(14, providers.Count); // 하드코딩 14개와 동일 — Scrutor 누락/초과 방어

        Assert.NotNull(sp.GetRequiredService<ProviderRegistry>());
        Assert.NotNull(sp.GetRequiredService<ConversionEngine>());
    }

    [Fact]
    public void ConstructorDependencies_AreAutoWired()
    {
        var services = new ServiceCollection();
        services.AddEverything2Everything();
        using var sp = services.BuildServiceProvider();

        // 이들이 해소되면 생성자 의존이 DI로 연결된 것(AsSelf 미등록 시 여기서 실패).
        Assert.NotNull(sp.GetRequiredService<HeicProvider>());  // ← MagickProvider 주입
        Assert.NotNull(sp.GetRequiredService<DocxProvider>());  // ← PdfProvider 주입
        Assert.NotNull(sp.GetRequiredService<OcrProvider>());   // ← PdfProvider 주입
        Assert.NotNull(sp.GetRequiredService<HwpxProvider>());  // ← PdfProvider 주입
        Assert.NotNull(sp.GetRequiredService<LlmProvider>());   // ← ISettingsStore 주입

        // AsSelfWithInterfaces: 구체 타입과 인터페이스가 동일 싱글턴 인스턴스를 공유.
        var magickAsSelf = sp.GetRequiredService<MagickProvider>();
        var magickAsInterface = sp.GetServices<IConverterProvider>().OfType<MagickProvider>().Single();
        Assert.Same(magickAsSelf, magickAsInterface);
    }

    [Fact]
    public void SharedSettings_AreRegisteredInstance()
    {
        // App이 공유 ISettingsStore를 주입하면 그 인스턴스가 등록되어야 한다(LlmProvider 키 공유).
        var settings = new DpapiSettingsStore();
        var services = new ServiceCollection();
        services.AddEverything2Everything(settings);
        using var sp = services.BuildServiceProvider();
        Assert.Same(settings, sp.GetRequiredService<ISettingsStore>());
    }

    [Fact]
    public void MagickProvider_IsDiscoverableAsMultiInputCombiner()
    {
        // P3: 엔진은 결합을 _registry.All.OfType<IMultiInputConverter>()로 찾는다.
        // 이 seam이 깨지면(MagickProvider가 인터페이스 미구현/Scrutor 미등록) 결합이 전부 실패한다.
        var reg = Everything2EverythingBootstrap.CreateDefault().Providers;
        var combiners = reg.All.OfType<IMultiInputConverter>().ToList();
        Assert.NotEmpty(combiners);
        Assert.Contains(combiners, c => c.CanCombineTo(".pdf"));
        Assert.Contains(combiners, c => c.CanCombineTo(".tif"));
        Assert.Contains(combiners, c => c.CanCombineTo(".gif"));
        Assert.DoesNotContain(combiners, c => c.CanCombineTo(".png")); // png은 결합 출력이 아님
    }

    [Fact]
    public void CreateDefault_Facade_StillWorks()
    {
        // 파사드 하위호환: DI 내부전환 후에도 CreateDefault가 동일하게 엔진/그래프를 구성.
        var engine = Everything2EverythingBootstrap.CreateDefault();
        Assert.Equal(14, engine.Providers.All.Count);
        Assert.NotNull(engine.Providers.Graph.FindBestPath(".png", ".jpg"));
        Assert.NotNull(engine.Providers.Graph.FindBestPath(".svg", ".jpg", maxHops: 3));
    }
}
