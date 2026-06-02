using CommunityToolkit.Mvvm.ComponentModel;
using Everything2Everything.Core;

namespace Everything2Everything.App.ViewModels;

/// <summary>
/// 변환 옵션 패널의 상태 + 불변 ConvertOptions 구성 로직(MVVM ViewModel).
/// ToConvertOptions는 순수 함수라 WPF 없이 헤드리스 단위 테스트가 가능하다(P5b: 코드비하인드 BuildOptions 추출).
/// ObservableProperty로 노출되어 향후 옵션 패널 XAML을 이 VM에 직접 바인딩할 수 있다.
/// </summary>
public partial class OptionsViewModel : ObservableObject
{
    /// <summary>JPEG/WebP 품질(1~100). AVIF는 -30 보정.</summary>
    [ObservableProperty] private int _quality = 85;

    /// <summary>비우면 원본 옆 서브폴더. 값이 있으면 사용자 지정 출력 폴더.</summary>
    [ObservableProperty] private string? _customOutputDirectory;

    [ObservableProperty] private NameCollision _conflictRule = NameCollision.AppendNumber;

    /// <summary>0=요약, 1=번역, 2=교정 (AI 텍스트 변환).</summary>
    [ObservableProperty] private int _aiTaskIndex;

    [ObservableProperty] private string? _targetLanguage;

    [ObservableProperty] private bool _videoPreferGpu = true;

    /// <summary>현재 상태로 불변 ConvertOptions를 구성한다(기존 MainWindow.BuildOptions와 동일 동작).</summary>
    public ConvertOptions ToConvertOptions()
    {
        var hasCustom = !string.IsNullOrWhiteSpace(CustomOutputDirectory);
        var aiTask = AiTaskIndex switch
        {
            1 => "translate",
            2 => "proofread",
            _ => "summarize",
        };

        return new ConvertOptions
        {
            OnCollision = ConflictRule,
            OutputLocation = hasCustom ? OutputLocation.Custom : OutputLocation.SubfolderBesideSource,
            CustomOutputDirectory = hasCustom ? CustomOutputDirectory!.Trim() : null,
            Jpeg = new JpegEncodingOptions { Quality = Quality },
            Webp = new WebpEncodingOptions { Quality = Quality },
            Avif = new AvifEncodingOptions { Quality = Math.Clamp(Quality - 30, 1, 100) },
            Ai = new AiOptions
            {
                Task = aiTask,
                TargetLanguage = string.IsNullOrWhiteSpace(TargetLanguage) ? null : TargetLanguage.Trim(),
            },
            VideoPreferGpu = VideoPreferGpu,
        };
    }
}
