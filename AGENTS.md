# Everything2Everything — AI AGENT GUIDELINES (AGENTS.md)

> **[최고 지침] TDD 절대주의 (TDD as the Absolute Law)**  
> 본 저장소에서 작업하는 모든 AI 에이전트와 개발자는 예외 없이 **테스트 주도 개발(TDD)** 원칙을 신성한 불변의 헌법으로 준수해야 한다.  
> 실패하는 단위/통합/디자인 테스트(RED)가 작성되기 전에는 어떠한 프로덕션 코드도 단 한 줄 작성하거나 수정할 수 없다.  
> 이론적 배경 및 세부 규약은 [docs/tdd/](docs/tdd/README.md)의 SSOT 문서를 반드시 따른다.

---

## 1. TDD 4대 불변 법칙 (Inviolable Invariants)

### Rule 1: No Code Without RED (테스트 선행)
- 어떠한 버그 수정, 기능 추가, UI 개편도 **실패하는 테스트(RED)를 먼저 작성**하는 것으로 시작해야 한다.
- 테스트는 단순한 컴파일 오류가 아니라, **의도한 동작이 실패함을 알리는 명확한 Assertion 실패**여야 한다.

### Rule 2: Minimal Code for GREEN (최소 구현)
- 작성된 RED 테스트를 통과시키는 데 필요한 **가장 간결하고 정갈한 코드**만을 작성한다.
- 테스트 범위를 넘어서는 불필요한 기능이나 과도한 엔지니어링을 사전에 추가하지 않는다.

### Rule 3: Strict REFACTOR (무결점 리팩토링)
- 모든 테스트가 초록불(GREEN)인 상태에서만 리팩토링을 수행한다.
- 코드 중복 제거, 명확한 네이밍, 디자인 패턴 적용, 성능 최적화를 진행하며, 리팩토링 중에는 어떠한 새로운 동작도 추가하지 않는다.

### Rule 4: Test Suite Pruning & Consolidation (가지치기 및 통폐합)
- RED 체계를 무작정 확장하기만 하지 않는다. 의미 없는 세부 구현 결합 테스트나 중복 테스트는 즉각 가지치기(Pruning)한다.
- 유사한 입출력 분기는 xUnit `[Theory]`와 `[InlineData]`로 파라미터화 통폐합(Consolidation)한다.
- 실제 사용자의 복합적 인터랙션(입력 필드 작성, 버튼 클릭, 취소, 프로그레스) 중심의 완결된 유즈케이스 시나리오 테스트를 지향한다.
- 전체 테스트 스위트 실행 시간은 **10초 이내**를 유지해야 한다.

---

## 2. 초엄격 디자인 & UX 감사 규약 (Design Audit Invariants)

Everything2Everything은 최고 수준의 Windows 11 Fluent 2 미학을 추구한다. 다음 7가지 디자인 결함은 **자동화된 정적 AST 테스트 및 In-Memory Visual Tree 테스트로 검출(RED)**되며, 발견 즉시 자가 수정(GREEN)해야 한다.

### 1) 아이콘-텍스트 수직 기준선 중앙 정렬 (Vertical Alignment)
- 가로 방향 `StackPanel` 내부에 아이콘(`Image`, `Path`, `ui:SymbolIcon`, 상태 `Ellipse`)과 `TextBlock`이 함께 배치될 때, **반드시 `VerticalAlignment="Center"`**를 선언해야 한다.
- 아이콘이 텍스트보다 위에 뜨거나 아래로 가라앉는 시각적 치우침을 절대 허용하지 않는다.

### 2) 인풋창 내부 요소 패딩 (Input Padding)
- `TextBox`, `PasswordBox`, `ComboBox`는 내부 텍스트 글리프가 테두리에 닿지 않도록 **수평 최소 8px 이상, 수직 최소 4px 이상**의 패딩을 가져야 한다. (권장: `10,8` 또는 `12,8`).
- 패딩이 0이거나 비대칭적인 기형적 값(`0,6`, `20,1` 등)을 금지한다.

### 3) 텍스트 내 유니코드 이모지 혼용 금지 & 언어 통일
- 버튼 텍스트에 `Content="⚙ 설정"`처럼 날것의 유니코드 이모지나 특수문자를 인라인으로 포함하는 것을 엄격히 금지한다.
- 아이콘은 별도의 `ui:SymbolIcon` 또는 정제된 `Path` 벡터 엘리먼트로 분리한다.
- 상단 내비게이션 및 액션 버튼은 정제된 한국어로 통일한다 (`설정`, `컨텍스트 메뉴 등록`, `진단`, `로그 내보내기`, `목록 비우기`).

### 4) 컨테이너 오버플로 및 텍스트 생략 부호 (Overflow Safety)
- 긴 파일명이나 경로를 바인딩하는 모든 `TextBlock`은 `TextTrimming="CharacterEllipsis"` 또는 `TextWrapping="Wrap"`을 필수 적용한다.
- 화면 크기 축소 시 요소가 창 밖으로 삐져나가거나 인접 컨트롤을 덮어씌우는 Flex/Grid 정렬 오류를 방지한다.

### 5) 폰트 가독성 및 WCAG AA 명도 대비
- 다크 테마 배경(`#090A0C`, `#131417`) 위에서 텍스트는 최소 4.5:1 이상의 대비를 유지해야 한다 (`FsTextPrimary`, `FsTextSecondary`).
- 어두운 배경에 묻혀 보이지 않는 어두운 회색(`#3A3D45` 이하)의 텍스트 배치를 금지한다.

### 6) 제로-보이드 빈 상태 원칙 (Zero-Void Empty State)
- 데이터가 비어 있을 수 있는 모든 뷰(`ActiveQueueView`, `PastResultsView`)는 빈 상태 전용 UI(일러스트레이션 또는 아이콘 + 직관적인 안내 문구 + 행동 유도 버튼)를 무조건 포함해야 한다.
- 아무런 피드백 없는 칠흑 같은 빈 공간 노출을 금지한다.

### 7) 8px 그리드 시스템 준수
- 마진과 패딩은 4의 배수(`4, 8, 12, 16, 20, 24, 32px`)를 기본으로 사용하며, 홀수나 비표준 수치(`3, 7, 11, 13px`)의 남발을 금지한다.
- 동일한 위계의 형제 버튼은 일치하는 패딩과 높이를 가져야 한다.

---

## 3. 자율 피드백 루프 프로토콜 (Autonomous Loop Protocol)

에이전트는 요구사항을 구현할 때 다음 사이클을 자율적으로 회전시킨다:

1. **명세 & RED**:
   - `src/Everything2Everything.Tests/`에 실패하는 테스트 작성.
   - `dotnet test` 실행 후 실패 메시지(`Expected vs Actual`) 확인.
2. **구현 & GREEN**:
   - 최소한의 코드로 테스트 통과.
   - 전체 테스트 스위트 전수 실행 (`통과! - 실패: 0`).
3. **디자인 감사 & REFACTOR**:
   - XAML AST 정적 분석 및 Visual Tree 테스트 통과 여부 확인.
   - 결함 발견 시 XAML 뷰모델 및 스타일 즉각 보정.
4. **가지치기 (Pruning)**:
   - 중복되거나 과도하게 복잡한 테스트가 생기지 않았는지 점검 및 통폐합.

---

## 4. 릴리즈 및 배포 체계 (Release & Deployment Gate)

- **원격 저장소**: `https://git.chanpaca.net/yunchan/Everything2Everything.git`
- **배포 스크립트**: `pwsh tools/Publish-Release.ps1 -Version "<M.m.p>" -ReleaseNotes "<내용>"`
- **릴리즈 게이트**:
  1. 전체 단위/디자인/시나리오 테스트 100% 통과 (실패 0개, 경고 0개).
  2. `Package.appxmanifest`와 Git Tag 버전 동기화 (SemVer 2.0).
  3. Portable EXE + MSIX 패키지 빌드 성공.
  4. Forgejo API를 통한 릴리즈 생성 및 바이너리 자산 첨부.
