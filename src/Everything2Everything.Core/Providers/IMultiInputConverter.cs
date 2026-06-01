namespace Everything2Everything.Core.Providers;

/// <summary>
/// 여러 입력을 단일 출력으로 결합하는 변환기(N→1). 변환 그래프의 1입력→1출력 엣지로는 표현할 수 없는
/// '결합'을 1급 시민으로 다룬다. ConversionEngine은 이 인터페이스에 결합을 위임하므로 엔진 자체는
/// 특정 이미지 라이브러리(ImageMagick)에 의존하지 않는다(추상화 누수 봉합).
/// </summary>
public interface IMultiInputConverter
{
    /// <summary>이 결합기가 해당 출력 확장자로의 결합을 지원하는가.</summary>
    bool CanCombineTo(string outputExtension);

    /// <summary>
    /// 여러 소스를 <paramref name="outputDirectory"/> 안에 단일 산출물로 결합한다.
    /// 출력 디렉터리 결정은 엔진이 수행하고, 파일명 해결·충돌 처리·실제 쓰기는 구현체가 담당한다.
    /// </summary>
    Task<ConvertResult> CombineAsync(
        IReadOnlyList<string> sources,
        string outputDirectory,
        string outputExtension,
        ConvertOptions options,
        IProgress<ConvertProgress>? progress,
        CancellationToken cancellationToken);
}
