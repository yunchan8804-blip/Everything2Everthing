using Microsoft.Extensions.DependencyInjection;

namespace Everything2Everything.Core;

public static class Everything2EverythingBootstrap
{
    /// <summary>
    /// 기본 변환 엔진을 구성한다. 내부적으로 DI 컨테이너(<see cref="ServiceCollectionExtensions.AddEverything2Everything"/>)로
    /// 모든 Provider를 어셈블리 스캔 자동 등록하고 ConversionEngine을 해소한다.
    /// 기존 호출부(App/CLI/테스트) 하위호환을 위한 얇은 파사드 — 시그니처·동작 불변.
    /// </summary>
    public static ConversionEngine CreateDefault(ISettingsStore? settings = null)
    {
        var services = new ServiceCollection();
        services.AddEverything2Everything(settings);
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<ConversionEngine>();
    }
}
