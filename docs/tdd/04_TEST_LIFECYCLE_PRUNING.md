# 04. 테스트 라이프사이클 관리: 가지치기 & 통폐합 (Test Lifecycle & Pruning)

> "A test suite that only grows and is never pruned will eventually collapse under its own weight, slowing down the feedback loop until developers stop running it."
> — **Gerard Meszaros**, *xUnit Test Patterns*

이 문서는 테스트 스위트의 비대화(Test Sprawl)와 유지보수 비용 폭증을 방지하고, 초고속 피드백 루프(< 10초)를 영구히 유지하기 위한 **테스트 가지치기(Pruning) 및 통폐합(Consolidation) 규약**을 정의한다.

---

## 1. 테스트 비대화(Test Sprawl)의 위협

### 1.1 무분별한 테스트 증식의 3대 부작용
1. **피드백 루프 지연 (Feedback Degradation)**: 전체 테스트 실행 시간이 30초, 1분, 5분으로 늘어나면 개발자와 AI 에이전트가 코드를 수정할 때마다 테스트를 돌리지 않고 건너뛰게 됨.
2. **리팩토링 저항성 (Refactoring Friction)**: 내부 구현 세부사항에 과도하게 결합된 수백 개의 테스트가 작은 구조 개선에도 동시에 깨져, 코드 개선 자체를 포기하게 만듦.
3. **가짜 안전감 (False Sense of Security)**: 테스트 개수는 500개인데 실질적으로는 같은 분기를 중복 검증하거나 사소한 getter/setter만 확인하고 있어 치명적인 시스템 결함을 놓침.

---

## 2. 테스트 가지치기 원칙 (The Pruning Protocol)

어떤 테스트를 유지하고 어떤 테스트를 제거할 것인가? 아래 기준에 따라 정기적으로 스위트를 정제한다.

### 2.1 즉각 제거(Delete) 대상
*   **구현 결합 테스트 (Implementation-Coupled Tests)**:
    - 공개 계약(Public API/Output)이 아닌, 내부 private 헬퍼의 호출 여부나 로컬 변수 상태를 리플렉션으로 검증하는 테스트.
*   **중복 검증 테스트 (Redundant Tests)**:
    - 동일한 로직 분기를 거의 유사한 일반 데이터(예: "abc", "def", "xyz")로 중복 테스트하는 개별 메서드들.
*   **플레이키/타이머 테스트 (Flaky/Timing-Dependent Tests)**:
    - `Thread.Sleep()`이나 비동기 타이밍 경쟁 상태에 의존하여 가끔씩 실패하는 불안정한 테스트. 결정론적 동기화 메커니즘(`TaskCompletionSource`, 가짜 시계)으로 전환할 수 없다면 제거한다.
*   **프레임워크 자체 검증 테스트**:
    - WPF `Grid`가 2개의 행을 나누는지, .NET BCL `Dictionary`가 키를 저장하는지와 같이 프레임워크 자체의 기본 동작을 검증하는 무의미한 테스트.

### 2.2 회귀 방어선 (Coverage Floor)
*   가지치기를 수행할 때 핵심 비즈니스 로직의 커버리지는 다음의 **최소 방어선(Coverage Floor)** 이하로 내려가서는 안 된다:
    - `Everything2Everything.Core.Routing` (Dijkstra 최단 경로): **95% 이상**
    - `Everything2Everything.Core.Providers` (각 포맷별 변환 옵션 및 지원 매트릭스): **90% 이상**
    - `Everything2Everything.App.Views` (XAML 디자인 AST 및 레이아웃 무결성): **100% 필수 뷰 커버리지**

---

## 3. 테스트 통폐합 기법 (Consolidation Patterns)

### 3.1 개별 `[Fact]`의 `[Theory]` 파라미터화 통합
*   **Bad Pattern (스위트 파편화)**:
    ```csharp
    [Fact] public void PngToJpg_Supported() => Assert.True(graph.CanConvert(".png", ".jpg"));
    [Fact] public void PngToWebp_Supported() => Assert.True(graph.CanConvert(".png", ".webp"));
    [Fact] public void PngToAvif_Supported() => Assert.True(graph.CanConvert(".png", ".avif"));
    // 50개의 개별 Fact 메서드...
    ```
*   **Good Pattern (파라미터화 통폐합)**:
    ```csharp
    [Theory]
    [InlineData(".png", ".jpg")]
    [InlineData(".png", ".webp")]
    [InlineData(".png", ".avif")]
    [InlineData(".heic", ".png")]
    [InlineData(".docx", ".pdf")]
    public void SupportedMatrix_RoutesSuccessfully(string input, string output)
    {
        var plan = _graph.FindRoute(input, output);
        Assert.NotNull(plan);
        Assert.True(plan.Steps.Count >= 1);
    }
    ```
    - 단 하나의 잘 다듬어진 테스트 본체로 수십 가지의 입출력 매트릭스를 간결하게 검증.
    - 실패 시 어떤 파라미터 조합이 실패했는지 테스트 탐색기에서 즉시 식별 가능.

### 3.2 사용자 시나리오 기반 통합 (User Journey Tests)
*   사용자의 실제 행동 흐름을 하나의 완결된 시나리오로 통합하여, 10개의 파편화된 테스트보다 훨씬 강력하고 현실적인 검증을 제공한다:
    - **Step 1**: 큐에 복합 파일(이미지, 문서) 드롭
    - **Step 2**: 상호 교집합 포맷 필터링 확인
    - **Step 3**: 인코딩 옵션(품질, 비트레이트) 변경
    - **Step 4**: 변환 실행 및 프로그레스 갱신
    - **Step 5**: 결과 이력(Past Results) 생성 및 큐 비우기

---

## 4. 10초 룰 (The 10-Second Invariant)

*   **원칙**: `dotnet test Everything2Everything.Tests.csproj`의 전체 실행 시간은 로컬 개발 머신에서 **10초 이내**여야 한다.
*   만약 테스트 스위트 실행 시간이 10초를 초과하기 시작하면:
    1. I/O 작업(실제 대용량 파일 생성)을 메모리 스트림(`MemoryStream`)으로 전환한다.
    2. 무거운 외부 프로세스(FFmpeg, LibreOffice)를 호출하는 테스트는 스모크/인프라 카테고리(`[Trait("Category", "Smoke")]`)로 분리한다.
    3. 중복된 XAML 파싱 과정을 픽스처(`IClassFixture`)로 캐싱하여 재사용한다.
