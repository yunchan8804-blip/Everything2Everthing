using Everything2Everything.Core.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Everything2Everything.Core;

/// <summary>
/// 변환 엔진의 DI 컴포지션 루트. 모든 IConverterProvider 구현을 어셈블리 스캔(Scrutor)으로 자동 등록해
/// 컴파일타임 하드코딩(Bootstrap의 14개 new + 수동 생성자 와이어링)을 제거한다.
/// 새 Provider는 클래스만 추가하면 자동 등록되고, 그래프가 그 변환 엣지를 자동 흡수한다(확장성 토대).
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEverything2Everything(
        this IServiceCollection services,
        ISettingsStore? settings = null)
    {
        // 설정 저장소: 호출자가 공유 인스턴스(App의 DPAPI 저장소)를 주면 그걸 등록 — 키 변경이 LlmProvider에 즉시 반영.
        // 없으면 기본 DPAPI 저장소를 단 1회 등록.
        if (settings is not null)
            services.AddSingleton<ISettingsStore>(settings);
        else
            services.TryAddSingleton<ISettingsStore, DpapiSettingsStore>();

        // IConverterProvider 구현 전수 자동 등록. AsSelfWithInterfaces =
        // 구체 타입을 단일 싱글턴으로 등록 + IConverterProvider는 그 인스턴스로 포워드한다.
        // → Heic(MagickProvider)/Docx·Ocr·Hwpx(PdfProvider)/Llm(ISettingsStore) 생성자 의존이
        //   동일 싱글턴 인스턴스로 자동 해소된다(다중 생성자는 MS.DI가 해소 가능한 최다 매개변수 생성자 선택).
        services.Scan(scan => scan
            .FromAssembliesOf(typeof(IConverterProvider))
            .AddClasses(c => c.AssignableTo<IConverterProvider>(), publicOnly: false)
            .AsSelfWithInterfaces()
            .WithSingletonLifetime());

        // 그래프 빌더: 등록된 모든 IConverterProvider로부터 변환 그래프를 합성.
        services.AddSingleton<ProviderRegistry>(sp => new ProviderRegistry(sp.GetServices<IConverterProvider>()));
        services.AddSingleton<ConversionEngine>();

        return services;
    }
}
