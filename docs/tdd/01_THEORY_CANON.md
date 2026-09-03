# 01. TDD 이론 캐넌 & 정통 원칙 (Theoretical Foundations & Canon)

> "Clean code that works, in Ron Jeffries' pithy phrase, is the goal of Test-Driven Development (TDD)."
> — **Kent Beck**, *Test-Driven Development: By Example* (Addison-Wesley)

이 문서는 소프트웨어 공학 30년간 검증된 TDD의 본질과 최고 권위자들의 핵심 정통 이론을 집대성한 SSOT 캐넌이다.

---

## 1. Kent Beck의 정통 TDD 체계 (Canonical TDD)

### 1.1 TDD의 불변의 3대 원칙 (Three Laws of TDD)
Robert C. Martin(Uncle Bob)과 Kent Beck이 정립한 TDD의 나노 사이클 규칙:

1. **제1법칙 (The First Law)**: 실패하는 단위 테스트를 작성하기 전에는 어떠한 프로덕션 코드도 작성할 수 없다.
2. **제2법칙 (The Second Law)**: 컴파일 실패를 포함하여 실패를 나타내기에 충분한 정도까지만 단위 테스트를 작성해야 하며, 그 이상 작성해서는 안 된다.
3. **제3법칙 (The Third Law)**: 현재 실패하는 단 하나의 단위 테스트를 통과시키기에 충분한 정도까지만 프로덕션 코드를 작성해야 하며, 그 이상 작성해서는 안 된다.

### 1.2 Red-Green-Refactor 라이프사이클의 본질
*   **RED (실패의 명세화)**:
    - 작성하고자 하는 기능의 "의도(Intent)"와 "계약(Contract)"을 테스트 코드로 먼저 선언한다.
    - **핵심 주의점**: 단순 컴파일 에러가 아닌, **올바른 실패(Expected vs Actual 불일치)**가 출력창에 나타나는 것을 반드시 눈으로 확인해야 한다.
*   **GREEN (최소 구현)**:
    - 가장 빠르고 간결한 방법으로 테스트를 통과시킨다.
    - 필요하다면 상수를 하드코딩(Fake It Till You Make It)하거나, 단순한 삼항 연산자를 사용해도 좋다. 중요한 것은 즉각적인 피드백과 안전망 후보다.
*   **REFACTOR (구조적 정제)**:
    - 테스트가 초록불(GREEN)인 상태에서만 리팩토링을 수행한다.
    - 중복 코드(DRY) 제거, 매직 넘버 제거, 디자인 패턴 적용, 성능 최적화, 가독성 향상.
    - **Kent Beck의 불변식**: 리팩토링 단계에서는 절대로 새로운 동작(Behavior)을 추가해서는 안 된다.

### 1.3 Kent Beck의 *Tidy First?* (정리 우선 원칙)
구조적 변경(Structural Change)과 기능적 변경(Behavioral Change)을 철저히 분리한다:
*   **B 커밋**: 기능 추가/변경 (반드시 테스트를 동반)
*   **S 커밋**: 코드 정리/이름 변경/인터페이스 추출 (동작 변경 0%)
두 가지를 한 커밋이나 한 사이클에 섞는 순간, 버그의 원인을 추적할 수 없는 혼돈이 발생한다.

---

## 2. Martin Fowler: 테스트 피라미드 & Classicist vs Mockist

### 2.1 The Practical Test Pyramid (실용적 테스트 피라미드)
Martin Fowler와 Mike Cohn이 제안한 계층 구조:

```
        /   UI / E2E   \       (느림, 고비용, 높은 취약성, 핵심 유즈케이스 집중)
       /----------------\
      /   Integration    \     (중간 속도, 컴포넌트 간 협력 검증, DI/그래프 검증)
     /--------------------\
    /      Unit Tests      \   (초고속, 밀리초 단위, 비즈니스 로직 및 알고리즘 100%)
   /------------------------\
```

*   **피라미드 붕괴 안티패턴**:
    - **아이스크림 콘 (Ice Cream Cone)**: 단위 테스트가 거의 없고 느리고 깨지기 쉬운 UI/E2E 테스트만 가득한 구조. 피드백 루프가 수십 분으로 늘어나며 TDD가 마비된다.
    - **모래시계 (Hourglass)**: 단위 테스트와 E2E는 많으나 컴포넌트 결합 검증(Integration)이 전무하여 경계 지점에서 런타임 크래시가 빈발함.
*   **Everything2Everything의 황금 비율**:
    - **Unit/Engine (Core)**: 70% (Dijkstra 그래프 라우팅, 손실 등급 산출, 포맷 변환 순수 로직)
    - **Integration/AST (Audit)**: 20% (XAML 정적 파싱, DI 컨테이너 결합, 인코더 옵션 매핑)
    - **E2E/Visual Tree (App)**: 10% (WPF In-Memory 창 렌더링, 큐 조작, 취소/진행률 시나리오)

### 2.2 Classicist (Detroit) vs Mockist (London) 학파의 조화
*   **Classicist 학파 (Detroit/Chicago)**:
    - 실제 객체(Real Collaborators)를 사용하여 상태(State)의 변화를 검증.
    - 리팩토링 시 내부 구현이 바뀌어도 테스트가 깨지지 않는 강건함(Robustness)을 제공.
    - **우리의 원칙**: `ConversionGraph`, `ProviderRegistry`, `ConvertOptions` 등 핵심 도메인은 Classicist 방식으로 실제 객체 그래프를 구동하여 검증한다.
*   **Mockist 학파 (London)**:
    - Mock/Stub을 사용하여 객체 간의 상호작용(Behavior/Interaction)을 검증.
    - 격리성이 뛰어나며 TDD 설계 유도에 유리하나, 내부 메서드 시그니처 변경에 민감함.
    - **우리의 원칙**: 외부 프로세스(FFmpeg CLI, LibreOffice), OS 파일시스템, 클라우드 AI API(OpenAI/Anthropic) 경계 지점에서만 철저히 Test Double을 사용한다.

---

## 3. Gerard Meszaros: *xUnit Test Patterns* & Test Smells

### 3.1 4단계 테스트 패턴 (Four-Phase Test)
모든 단위 테스트는 다음 4개의 구별된 단계를 가져야 하며, 순서가 뒤섞여서는 안 된다:

1. **Setup (준비)**: 테스트 픽스처(Fixture)를 구성한다. SUT(System Under Test)와 협력자를 인스턴스화한다.
2. **Exercise (실행)**: SUT의 단 하나의 메서드나 행위를 실행한다.
3. **Verify (검증)**: 기대한 결과와 실제 결과를 단언(Assert)한다.
4. **Teardown (해제)**: 생성된 임시 파일, 메모리, STA 스레드 리소스를 원래 상태로 복원한다.

### 3.2 픽스처 관리 전략 (Fixture Strategies)
*   **Fresh Fixture (신선한 픽스처)**: 각 테스트마다 새로운 객체와 환경을 생성. 테스트 간 오염(Pollution)이 없어 독립성이 보장됨 (기본 채택).
*   **Shared Fixture (공유 픽스처)**: 무거운 자원(예: 테스트용 폰트 로딩, DI 빌드)을 테스트 클래스 전체에서 공유. xUnit의 `IClassFixture<T>` 사용. 단, 불변(Immutable) 상태로 유지해야 테스트 순서 의존성이 발생하지 않음.

### 3.3 18대 테스트 스멜 (Test Smells) 및 제거법
| 테스트 스멜 | 징후 및 문제점 | Everything2Everything 해결책 |
|---|---|---|
| **Fragile Test (취약한 테스트)** | 코드의 내부 private 변수나 구현 디테일을 검증하여 작은 리팩토링에도 우수수 깨짐 | 인터페이스와 공개 계약(Public API/Output)만 검증 |
| **Obscure Test (모호한 테스트)** | 테스트 코드가 길고 복잡하여 무엇을 검증하는지 한눈에 알 수 없음 | Setup을 헬퍼 메서드로 캡슐화, 명확한 네이밍 |
| **Mystery Guest (불가사의한 손님)** | 테스트 파일 외부에 있는 마법 같은 파일/환경에 의존하여 실패 원인을 알 수 없음 | 인라인 테스트 데이터 빌더 및 명시적 픽스처 선언 |
| **Eager Test (욕심쟁이 테스트)** | 하나의 테스트 메서드에서 5~10개의 서로 다른 기능을 한꺼번에 검증함 | 하나의 테스트는 단 하나의 논리적 개념(Single Concept)만 단언 |
| **Assertion Roulette (단언 룰렛)** | 메시지 없는 수많은 Assert가 나열되어 어느 라인에서 왜 죽었는지 알 수 없음 | 설명적 실패 메시지 포함 및 FluentAssertion 스타일 분리 |
| **Conditional Test Logic (조건부 테스트 로직)** | 테스트 코드 안에 `if`, `for`, `switch`가 들어있어 테스트 자체가 버그를 가짐 | 테스트 내 분기문 완전 금지. `[Theory]`와 `[InlineData]`로 분리 |
| **Hardcoded Test Data (하드코딩된 테스트 데이터)** | 실제 파일 경로나 운영 체제 종속적 경로(`C:\...`)를 하드코딩 | `Path.Combine` 및 임시 디렉터리(`Path.GetTempPath()`) 추상화 |

---

## 4. Michael Feathers: 레거시 코드와 특성화 테스트 (Characterization Tests)

> "Legacy code is simply code without tests."
> — **Michael Feathers**, *Working Effectively with Legacy Code*

### 4.1 접합부 (Seam) 기법
테스트 대상 코드를 직접 수정하지 않고도 그 행위를 가로채거나 변경할 수 있는 장소:
*   **객체 접합부 (Object Seam)**: 생성자 주입(DI) 또는 가상 메서드 오버라이드를 통해 가짜 인스턴스를 주입.
*   **링크 접합부 (Link Seam)**: 인터페이스 기반 참조를 분리하여 테스트 러너에서 대체 어셈블리를 로드.

### 4.2 골든 마스터 & 특성화 테스트 (Characterization Testing)
*   동작을 모르는 복잡한 기존 코드(예: `MainWindow.xaml.cs`의 수많은 이벤트 핸들러)를 리팩토링하기 전, 현재 시스템의 실제 입출력 동작을 그대로 기록(Snapshot)하는 테스트를 먼저 작성한다.
*   이 특성화 테스트가 **회귀 방어선(Regression Safety Net)**이 되어, 내부 코드를 MVVM이나 별도 커맨드로 분해할 때 기존 동작이 단 1%도 변하지 않았음을 수학적으로 증명한다.
