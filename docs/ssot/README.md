# Everything2Everything — SSOT (Single Source of Truth)

이 폴더는 프로젝트의 **장기 마스터플랜 SSOT**다. "변환 프로그램의 극한 — 진짜 양방향·다방향 변환 + AI + 미디어"라는 목표를 향한 아키텍처 비전·핵심 결정(ADR)·8단계 로드맵·리스크·근거(인터넷 리서치 + 코드 분석)를 한곳에 모았다.

생성 방법: 멀티에이전트 Workflow 오케스트레이션 (5 코드분석 + 7 인터넷리서치 → 3 독립 아키텍트 → 심사 랭킹 → 마스터플랜 종합). 2026-06-01, 17 agents / 1.43M tokens.

## 파일 구조

```
docs/ssot/
├─ index.html        ← 웹 대시보드 (브라우저로 열기). _data 에서 생성됨, 직접 편집 금지
├─ PLAN.md           ← 마크다운 SSOT (사람·다음 세션이 읽는 텍스트). 생성됨, 직접 편집 금지
├─ build.py          ← 제너레이터. _data/*.json → index.html + PLAN.md
├─ README.md         ← 이 파일
└─ _data/            ← ★진짜 SSOT 원천 데이터 (여기를 고친다)
   ├─ master.json    ← 마스터플랜 (비전·원칙·아키텍처·ADR·로드맵·매트릭스·AI·미디어·리스크·지표·인계노트)
   ├─ status.json    ← 로드맵 단계별 진행 상태 (planned|in_progress|done)
   ├─ analyses.json  ← 5개 서브시스템 코드 심층 분석
   ├─ researches.json← 7개 토픽 인터넷 리서치 (라이브러리·라이선스·출처)
   ├─ designs.json   ← 3개 독립 설계안
   ├─ ranking.json   ← 설계안 심사 점수 + 흡수 아이디어 + 종합 권고
   └─ meta.json      ← 생성 메타데이터
```

## 갱신 워크플로 (다음 세션)

1. **진행 표시**: 단계를 시작/완료하면 `_data/status.json` 의 해당 단계를 `in_progress` / `done` 으로 바꾼다.
2. **계획 수정**: 로드맵·ADR이 바뀌면 `_data/master.json` 을 편집한다 (HTML/MD를 직접 고치지 말 것).
3. **재생성**: `python docs/ssot/build.py` → `index.html` 과 `PLAN.md` 가 다시 만들어진다.

## 핵심 요약 (TL;DR)

- **승자 종합**: idx2(AI·미디어 우선, 89점)를 실행 골격 + idx0(그래프 코어, 84점)을 아키텍처 영혼.
- **북극성**: 모든 변환을 단일 홉 "엣지"로 등록 → 엔진(자체 Dijkstra)이 멀티홉 A→Z를 자동 합성하는 변환 그래프 OS.
- **P1부터**: ① xUnit 테스트 프로젝트 신설(현재 0개) → ② `ProviderRegistry`를 `ConversionGraph`로 승격 + 자체 Dijkstra → ③ `DocumentProvider.RouteAsync`(손그림 멀티홉) 삭제를 회귀 테스트로 "동일 동작" 증명 → ④ PDF 압축(PDFsharp) 즉시 체감 가치.
- **불변식**: AI는 키 없으면 조용히 비활성(기존 변환 100% 동작), 무거운 외부 도구(FFmpeg/Ghostscript)는 GPL/AGPL 라이선스 게이트로 분리 프로세스 호출만.

자세한 내용은 `PLAN.md` 또는 `index.html` 참조.
