# Everything2Everything TDD Theoretical SSOT

이 디렉터리는 **Everything2Everything** 프로젝트의 테스트 주도 개발(TDD, Test-Driven Development)과 초엄격 디자인/UX 감사 및 릴리즈 엔지니어링의 **단일 진실 원천(Single Source of Truth, SSOT)**이다.

2026년 9월 기준 최신 학술 논문, 산업계 프랙티스(Agentic TDD, LLM Invariant Verification), 그리고 TDD 최고 권위자(Kent Beck, Martin Fowler, Gerard Meszaros, Michael Feathers)의 정통 이론을 집대성하여 시스템에 체화했다.

---

## 📚 목차 (Architecture & Index)

1. **[01. TDD 이론 캐넌 & 정통 원칙 (THEORY CANON)](01_THEORY_CANON.md)**
   - Kent Beck의 *Canonical TDD* 3대 법칙 및 *Tidy First?* 구조 분리
   - Martin Fowler의 *Practical Test Pyramid* & Mockist vs Classicist 철학
   - Gerard Meszaros의 *xUnit Test Patterns* (4단계 테스트, Fixture 관리, 18대 Test Smells)
   - Michael Feathers의 *Working Effectively with Legacy Code* (Seam과 특성화 테스트)

2. **[02. 2025–2026 AI AGENTIC TDD 최신 동향 (AGENTIC TDD)](02_AGENTIC_TDD.md)**
   - LLM 에이전트의 환각 및 코드 퇴행(Vibe Coding)을 막는 TDD 가드레일
   - 적대적 명세-검증 루프 (Adversarial Specification & Verification)
   - Pre-Write / Pre-Test Invariant Hooks 및 자가 치유(Fail-Guided Self-Repair)
   - 뮤테이션 테스팅(Mutation Testing)을 통한 테스트 자체의 무결성 검증

3. **[03. 초엄격 데스크톱 UI & 디자인 감사 TDD (DESIGN AUDIT TDD)](03_DESIGN_AUDIT_TDD.md)**
   - XAML 정적 AST 분석을 통한 디자인 토큰 및 레이아웃 결함 사전 감지
   - STA 스레드 기반 In-Memory Visual Tree 배치(Measure/Arrange) 및 오버플로 검증
   - 8px 그리드 시스템, 아이콘-텍스트 수직 기준선(Baseline) 정렬 원칙
   - WCAG 2.2 AA 기준 명도 대비, 인풋창 내부 패딩 규격, 빈 상태(Empty State) 필수 원칙

4. **[04. 테스트 라이프사이클 관리: 가지치기 & 통폐합 (TEST LIFECYCLE & PRUNING)](04_TEST_LIFECYCLE_PRUNING.md)**
   - 테스트 스위트 비대화(Test Sprawl) 및 플레이키(Flaky) 테스트 방지
   - Gerard Meszaros의 테스트 리팩토링 및 저가치/취약 테스트 가지치기(Pruning)
   - xUnit `[Theory]`와 파라미터화를 통한 유사 RED들의 유즈케이스 통폐합(Consolidation)
   - 10초 이내 전체 스위트 실행 원칙과 최소 회귀 방어선(Coverage Floor)

5. **[05. 릴리즈 엔지니어링 & 배포 체계 (RELEASE & DEPLOYMENT)](05_RELEASE_AND_DEPLOYMENT.md)**
   - Forgejo(`git.chanpaca.net`) 기반 원격 저장소 및 Actions CI/CD 워크플로
   - SemVer 2.0 시맨틱 버전 태깅 및 `Package.appxmanifest` 버전 동기화
   - MSIX 서명 패키지 및 Portable EXE 듀얼 바이너리 릴리즈 게이트

---

## 🎯 불변의 헌법: TDD 절대주의 (TDD as the Absolute Law)

```
[ RED ] 실패하는 테스트를 먼저 작성한다 (정확한 실패 원인 증명)
   │
   ▼
[ GREEN ] 테스트를 통과시키는 최소한의 프로덕션 코드를 작성한다
   │
   ▼
[ REFACTOR ] 중복을 제거하고, 디자인 규격을 준수하며, 성능을 최적화한다 (스위트 전체 GREEN 유지)
   │
   ▼
[ PRUNE & CONSOLIDATE ] 비대해진 테스트를 통폐합하고 실제 유즈케이스 중심으로 정제한다
```

어떠한 에이전트나 작업자도 **실패하는 테스트(RED) 없이 프로덕션 코드를 단 1줄도 수정하거나 작성할 수 없다.**
UI 디자인, 레이아웃, 패딩, 타이포그래피, 정렬 오류 역시 모두 **자동화된 테스트 코드로 사전에 감지하고 자가 수정**한다.
