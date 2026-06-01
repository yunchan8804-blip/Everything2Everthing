# Everything2Everything — 변환 그래프 OS 마스터플랜

> 전체 소스 심층 분석 · 방법론 인터넷 리서치 · 아키텍처 종합 (Single Source of Truth)

**생성** 2026-06-01 · **방법** Workflow 멀티에이전트 오케스트레이션 (5 코드분석 + 7 인터넷리서치 → 3 독립 아키텍트 → 심사 랭킹 → 마스터플랜 종합)  
**규모** 17 agents · 1,426,291 subagent tokens · 367 tool calls  
**종합** idx2(AI·미디어 우선, 89점) 실행 골격 + idx0(그래프 코어, 84점) 아키텍처 영혼

> 이 문서는 SSOT다. `docs/ssot/_data/*.json` 을 갱신하고 `python docs/ssot/build.py` 로 재생성한다. 웹 버전은 `docs/ssot/index.html`.

---
## 엘리베이터 피치

손으로 짠 변환 switch를 자동 경로 탐색 그래프로 교체하고, 그 위에 PDF 압축·영상·HWP·AI를 엣지로 얹어, 코드 한 줄당 N×M 매트릭스가 발현하는 '변환하면서 더 좋아지는' 만능 변환기.

## 북극성 비전

Everything2Everything의 북극성은 "세상의 모든 변환을 원자(atomic) 엣지로 등록하면, 엔진이 그 조합으로 임의의 A→Z를 스스로 합성하고, 변환하면서 AI가 결과를 더 좋게 만드는 변환 그래프 OS"다. 핵심 통찰은 현재 DocumentProvider.RouteAsync(92-205)가 사실상 '사람이 손으로 그린 Dijkstra'(md→html→docx, docx→html→md, hwp→html→md를 switch에 박아넣음)라는 점이며, 이 손그림을 삭제하고 엔진이 같은 경로를 '계산'하게 만드는 것이 모든 확장의 열쇠다. 그래프가 코어가 되면 FFmpeg(미디어), PDF 압축, HWP 양방향, AI 요약/번역/캡션이 전부 '엣지 추가'로 환원되고, OutputsForInput은 1-hop 직접 출력에서 도달 가능한 모든 포맷(transitive closure)으로 폭발한다. 동시에 사용자가 명시한 신규 가치(PDF 압축·HWP·영상·AI)를 인프라 완성을 기다리지 않고 빠르게 출시해 체감 차별화를 먼저 만든다. AI는 핵심 엔진이 아니라 '키 없으면 조용히 비활성되는 부가가치 엣지'로, 변환의 로컬 예측가능성이라는 신뢰를 절대 깨지 않는다.

## 설계 원칙

1. **변환은 엣지, 엔진은 라우터** — 모든 Provider는 단일 홉 원자 변환(md→html, png→pdf)만 선언한다. 멀티홉(md→docx)은 절대 Provider 내부에 손으로 짜지 않고 엔진의 그래프 탐색이 자동 합성한다. DocumentProvider.RouteAsync의 switch 지옥이 재발하지 않도록 이를 불변식으로 강제한다.
2. **기능이 그래프를 견인하되, 그래프가 기능을 받친다** — 사용자 체감 가치(PDF 압축·HWP·영상·AI)를 빠르게 출시하되, 신규 기능은 반드시 '그래프 엣지'로만 추가한다. Phase 0에 심은 그래프 코어가 하드코딩 유혹을 구조적으로 차단한다.
3. **손실은 가중치다** — 품질 손실을 ConversionPair.LossClass(Lossless/Container/Recode/Rasterize)로 SSOT화하고 -log(보존율)+홉페널티로 환산한다. 멀티홉 경로 선택과 UI '손실 변환' 경고 배지가 모두 이 단일 출처를 소비한다.
4. **AI는 끄면 사라지는 부가 엣지** — AI는 절대 기본 경로를 점유하지 않는다. 키가 없으면 모든 기존 변환은 100% 동작하고 AI 페어만 자동 비활성(NotReady)되며 ✨AI 배지로만 opt-in 노출된다. 변환의 로컬 예측가능성 신뢰를 깨지 않는다.
5. **무거운 외부 도구는 분리 호출로만** — FFmpeg(GPL 정적링크 금지·LGPL 분리호출만), Ghostscript/MuPDF(AGPL·사용자 설치본 감지만), H2Orestart/Calibre(GPL·외부 프로세스 분리)를 본체에 절대 정적 링크하지 않는다. 라이선스 경계를 코드 리뷰 게이트로 강제해 상업 배포 오염을 원천 차단한다.
6. **순수 .NET 우선, 외부 바이너리 차선** — 단일 포터블 EXE 부담을 줄이기 위해 SharpCompress·Parquet.Net·PDFsharp·Svg.Skia 같은 순수 관리 코드를 EXE에 직접 포함하고, FFmpeg/Pandoc/Calibre 같은 무거운 바이너리는 '외부 설치 감지 + 미설치 시 안내/자동조달' 모델로만 통합한다.
7. **점진 마이그레이션, 무중단** — IConverterProvider/ConvertResult/ConvertOptions 일반화는 기존 8개 Provider를 어댑터로 감싸 한 번에 깨지지 않게 한다. 모든 코어 변경은 회귀 테스트(현재 0개에서 출발)로 '동일 동작'을 객관 증명한다.

## 타깃 아키텍처

4계층 변환 그래프 아키텍처. (1) Abstractions 계층이 Provider 계약을 담고, (2) 그래프 코어가 모든 원자 변환을 방향 그래프로 합성해 Dijkstra로 멀티홉 경로를 푼다. (3) Provider 계층은 in-box 코드 Provider(이미지/문서/미디어/AI)와 manifest 기반 외부 도구 어댑터로 나뉘며, (4) 실행 계층(ExternalProcessRunner·ISettingsStore)이 외부 프로세스·설정·키를 횡단 관리한다. 핵심은 ProviderRegistry를 단일 홉 딕셔너리에서 ConversionGraph로 승격하는 것이다.

- **Abstractions 계층 (Everything2Everything.Abstractions)** — Provider 계약을 별도 어셈블리로 분리해 타입 동일성을 보장하고 향후 플러그인의 안정적 참조점을 제공  
  `IConverterProvider`, `ConvertRequest/ConvertContext`, `ConvertResult(비파일 산출물 포함)`, `ProviderCapability`, `ConversionPair+LossClass`, `ExternalDependency`
- **그래프 코어 계층 (Core.Graph)** — 모든 Provider Capability를 순회해 방향 그래프(노드=확장자, 엣지=Provider+LossClass 가중치)를 빌드하고, 자체 Dijkstra로 최저손실 멀티홉 경로를 탐색·실행  
  `ConversionGraph`, `PathFinder(자체 Dijkstra)`, `ChainExecutor(ExecuteChainAsync)`, `ConversionEngine(라우터로 축소)`, `ProviderRegistry(증분 등록 Register/Rebuild)`
- **Provider 계층** — 단일 홉 원자 변환 능력을 선언·실행. in-box 코드 Provider와 manifest 어댑터 Provider 공존  
  `MagickProvider/PdfProvider/HtmlProvider(기존)`, `LlmProvider(AI)`, `FfmpegProvider(미디어)`, `PdfToolProvider(압축)`, `ImageCombineProvider(N→1)`, `ExternalToolProvider(manifest 어댑터 베이스)`
- **실행/인프라 계층** — 외부 프로세스 실행·설정 영속화·키 보안·미리보기를 횡단 제공  
  `ExternalProcessRunner(CliWrap, 타임아웃+stderr+Kill)`, `ISettingsStore(DPAPI 암호화)`, `ExternalToolDetector(번들 경로 폴백)`, `IPreviewRenderer(프리뷰 캐시)`, `ManifestLoader`

**데이터 흐름:** 파일 입력 → ConversionEngine.ConvertOneAsync가 입력/출력 확장자 정규화 → ConversionGraph.FindBestPath(in,out,options)로 경로 탐색(직접 엣지 있으면 1홉, 없으면 손실가중치 기반 멀티홉) → ChainExecutor가 경로의 각 홉을 순차 실행하며 중간 산출물을 공용 workDir(Temp/e2e_{Guid})에 체이닝 → 각 홉은 Provider.ConvertAsync(ConvertRequest) 호출, 진행률은 홉 수로 분할 매핑 → 마지막 홉 산출물을 OutputPathHelper로 충돌 해결 후 최종 출력 → ConvertResult(출력 경로 + 비파일 산출물) 반환, 중간 산출물 정리. AI/외부도구 엣지는 CheckAvailabilityAsync 게이트를 먼저 통과해야 그래프에 활성 노드로 참여.

## 핵심 아키텍처 결정 (ADR)

### ADR-1 · ProviderRegistry를 단일 홉 딕셔너리에서 ConversionGraph로 승격
- **결정:** _byPair 단일 룩업(ProviderRegistry.cs:6,43)을 유지하되 그 위에 인접 리스트 그래프(Dictionary<string,List<Edge>>)를 빌드하고, 외부 의존성 없는 자체 Dijkstra(80~120줄, .NET 9 PriorityQueue 사용)로 멀티홉 경로를 탐색한다. DocumentProvider.RouteAsync의 손그림 멀티홉을 엔진 합성으로 대체.
- **근거:** 현재 멀티홉이 Provider 내부 switch에 하드코딩되어 형식 N개에 O(N²)로 수동 증식한다. NCSA Polyglot 모델(노드=포맷, 엣지=Provider, 가중치=손실)은 학계 검증된 best practice이며, 그래프가 수십 노드·수백 엣지 규모라 성능 이슈가 없다.
- **대안:** QuikGraph(MS-PL, 2022 이후 정체)·Pandoc식 단일 AST 허브(이질적 도메인에 부적합). 자체 구현이 단일 EXE/AOT/라이선스 검토 모두 무부담이라 1순위.
- **트레이드오프:** 멀티홉은 중간 임시파일 I/O가 늘고 손실이 누적될 수 있다. 완화: 직접 엣지 우선, MaxHops=3 제한, 손실 블랙리스트, 손실 경로 UI 경고 배지.

### ADR-2 · 손실을 ConversionPair.LossClass 가중치로 SSOT화
- **결정:** ConversionPair에 LossClass(Lossless=0/Container=0.05/Recode=0.4/Rasterize=0.8) 필드를 추가하고, 엣지 가중치를 -log(품질보존율)+홉페널티+실행비용 합산으로 계산한다. UI '손실 변환' 배지도 이 가중치를 소비.
- **근거:** 손실은 본래 곱셈적(0.9×0.8)이므로 -log 변환으로 덧셈 최단경로(Dijkstra)가 곧 최대 품질보존 경로가 된다. 래스터화(텍스트/벡터→PNG)는 단방향 손실 절벽이므로 큰 페널티로 자연 회피.
- **대안:** 동적 손실 측정(Versus식 실측). 초기엔 정적 가중치 테이블로 시작하고 동적 측정은 처음부터 넣지 않는다(과도한 복잡도).
- **트레이드오프:** 정적 가중치는 추정값이라 일부 쌍에서 비최적 경로 가능. 완화: 보수적으로 직접 엣지 우선, 멀티홉은 fallback으로만 운영.

### ADR-3 · 레지스트리 충돌을 조용한 first-wins에서 Priority 기반 명시 선택으로 교체
- **결정:** _byPair.TryAdd(ProviderRegistry.cs:22)의 '조용한 첫 등록자 우선'을 ProviderCapability.Priority 필드 + 다중 Provider 공존 모델 + 충돌 시 진단 경고로 교체한다. 같은 (input,output)에 빠른변환/고품질/AI 등 복수 전략 등록 허용.
- **근거:** PDF압축 vs PDF렌더, AI변환 vs 일반변환처럼 한 쌍에 복수 전략이 필연적으로 생긴다. 현재는 부트스트랩 순서에 따라 비결정적으로 한쪽이 조용히 사라져 데이터 손실이다.
- **대안:** 현 first-wins 유지(확장 불가). 비용 기반 자동 선택만(사용자 전략 선택 불가). Priority+공존이 그래프 가중치와도 자연 연결.
- **트레이드오프:** 같은 쌍에 복수 Provider가 등록되면 UI에서 전략 선택지를 노출해야 하는 추가 복잡도. 완화: 기본은 최저비용 자동 선택, 고급 모드에서만 명시 선택.

### ADR-4 · IConverterProvider 시그니처를 ConvertRequest/ConvertContext로 일반화
- **결정:** 단일 sourcePath/단일 outputExtension/IProgress<double> 고정 시그니처(IConverterProvider.cs:9-15)를 ConvertRequest(다중 입력·옵션 백·미디어 메타) + ConvertContext로 일반화하고, ConvertResult(ConvertResult.cs)에 ExtractedText/AiResponse/Metadata/IntermediateArtifacts 필드를 추가한다. 기존 8개 Provider는 어댑터로 감싸 무중단 마이그레이션.
- **근거:** 현 시그니처는 N→1 결합, AI 비파일 응답, 영상 메타데이터 프로빙, 멀티홉 중간 컨텍스트를 표현할 수 없다. 미디어/AI 엣지가 들어올 '그릇'을 코어에 먼저 판다.
- **대안:** 시그니처 유지하고 옵션에 모든 것 욱여넣기(갓 오브젝트 가속). 점진 어댑터 전략이 8개 Provider 동시 파괴를 방지.
- **트레이드오프:** 어댑터 계층이 일시적 중복을 만든다. 완화: 회귀 테스트로 동일 동작 보장 후 어댑터를 점진 제거.

### ADR-5 · AI는 IAiProvider 특수 인터페이스가 아니라 그래프의 부가 엣지로 편입
- **결정:** LlmProvider를 일반 IConverterProvider로 구현하고 Microsoft.Extensions.AI(IChatClient) 추상화 위에 OpenAI/Anthropic 공식 SDK를 연결한다. AI는 로컬 변환이 없는 신규 페어(요약/번역/캡션/메타데이터)에만 노출되고, 키 부재 시 CheckAvailabilityAsync가 NotReady를 반환해 그래프에서 자동 비활성된다.
- **근거:** AI를 특수 카테고리로 두면 그래프·레지스트리 밖에 별도 배관이 생긴다. 엣지로 환원하면 OcrProvider가 Windows OCR을 흡수한 선례처럼 매트릭스에 자연 편입되고, AI 후처리 파이프(OCR→LLM 교정)도 멀티홉으로 자동 합성된다.
- **대안:** 별도 IAiConverterProvider 확장(추상화 분기 증가). 통합 IConverterProvider가 단순하고 그래프와 정합.
- **트레이드오프:** AI는 비결정적·유료·네트워크 의존이라 '재현 가능한 변환'과 충돌. 완화: ✨AI 배지·기본 경로 불점유·키 없으면 비활성 불변식.

### ADR-6 · 무거운 외부 도구는 분리 프로세스 호출 + 라이선스 게이트로만 통합
- **결정:** FFmpeg는 BtbN lgpl-shared 빌드를 별도 프로세스로 호출(LGPL 준수), Ghostscript/MuPDF는 AGPL이라 사용자 설치본 감지만, H2Orestart/Calibre/Pandoc은 GPL이라 외부 프로세스 분리. 공통 ExternalProcessRunner(CliWrap, 타임아웃+stderr+Kill)로 통일하고, 라이선스 경계를 코드 리뷰 게이트로 강제한다.
- **근거:** 단일 포터블 EXE 상업 배포에서 GPL/AGPL 바이너리 정적 링크는 즉시 라이선스 오염이다. 이미 LibreOffice를 외부 도구로 다루는 검증된 패턴을 그대로 확장.
- **대안:** GPL 빌드 번들(라이선스 위반)·상업 라이선스 구매(비용). 분리 호출 + 사용자 설치 감지/LGPL 자동조달이 안전.
- **트레이드오프:** 진정한 자족 EXE가 아니라 외부 의존 체인이 길어진다. 완화: 순수 .NET 라이브러리 우선, 외부 도구는 NotReady로 친절히 안내.

### ADR-7 · CombineAsync를 ImageCombineProvider(N→1 엣지)로 분리
- **결정:** ConversionEngine.CombineAsync의 ImageMagick 직접 의존(ConversionEngine.cs:2,164-257)과 정적 HashSet(CombinableInputs/Outputs:14-23)을 IMultiInputProvider 추상화로 분리한다. 엔진은 라이브러리 중립이 되고 결합 가능 형식은 Provider 능력 선언으로 통합.
- **근거:** 현재 '결합'이 Provider 추상화 밖에 있어 엔진이 ImageMagick에 결합되고, PDF 병합·동영상 concat·오디오 믹스 같은 비이미지 결합으로 확장 불가하다. 정적 HashSet과 능력 선언의 이중 관리도 해소.
- **대안:** 현 구조 유지(이미지 결합만 영구 고착). N→1 추상화가 모든 결합을 동일 패턴으로 흡수.
- **트레이드오프:** 결합 진행률 보고가 단일 출력 가정과 달라 재설계 필요. 완화: ConvertProgress를 N→1 케이스로 확장.

### ADR-8 · ConvertOptions 갓 오브젝트를 그래프 옵션 + 형식별 옵션 백으로 분해
- **결정:** 11개 sub-record 갓 오브젝트(ConvertOptions.cs:35-55)를 그래프 옵션(AllowMultiHop/MaxHops/AvoidLossy) + 형식별 옵션 백(IReadOnlyDictionary 또는 Provider 선언형 스키마)으로 분해한다. Video/Audio/Ai/PdfCompress를 sub-record 증식 없이 수용하고 ISettingsStore(DPAPI 암호화)로 영속화.
- **근거:** 형식 추가마다 sub-record가 비대해지고 모든 Provider가 무관한 옵션을 끌고 다닌다. 영상 코덱·AI 프롬프트·PDF 압축 레벨을 담을 자리가 코어 record 증식 없이 필요하다.
- **대안:** sub-record 계속 추가(god object 가속). 옵션 백이 형식별 옵션만 주입해 확장성 확보.
- **트레이드오프:** 강타입 안전성이 약화된다. 완화: Provider가 옵션 스키마(이름/타입/범위/기본값)를 선언하고 UI가 동적 생성·검증.

### ADR-9 · manifest는 풀 DSL이 아니라 단순 CLI용 선언적 인자 템플릿으로 제한 채택
- **결정:** manifest를 ExternalProcessRunner 위의 '선언적 인자 템플릿({input}/{output}/{outdir}/{format})'으로만 좁게 채택해 qpdf/Ghostscript 같은 단순 CLI 압축 도구를 코드 없이 추가한다. 복잡 로직(FFmpeg HW가속 폴백·AI)은 in-box 코드 Provider 원칙을 P1부터 못박는다.
- **근거:** Provider 8개·테스트 0개 단일 개발자 프로젝트에 풀 manifest DSL·동적 ALC 로더는 ROI가 낮다. FFmpeg의 nvenc→AV1 조건부 폴백은 manifest로 표현 불가하므로 하이브리드 경계가 필수.
- **대안:** 풀 플러그인 생태계(과잉 엔지니어링)·전부 코드(확장 비용). 좁은 manifest가 단순 도구 추가 비용만 제거.
- **트레이드오프:** manifest가 또 다른 갓 오브젝트가 될 위험. 완화: '90% 단순 CLI만 manifest, 복잡 로직은 in-box' 경계를 P1 불변식으로 명문화.

## 실행 로드맵

### P1 · 그래프 엔진 도입 + 즉시 체감 가치(PDF 압축)  `effort:L` `risk:medium` `status:done`
**목표:** ProviderRegistry를 ConversionGraph로 승격하고 멀티홉 경로 탐색을 엔진에 내장한다. 동시에 PDF 압축이라는 즉시 체감 신기능을 출시해 '보이지 않는 리팩터링의 함정'을 회피한다.

**산출물:**
- ConversionGraph + 자체 Dijkstra PathFinder(외부 의존성 0, .NET 9 PriorityQueue)
- ConversionPair.LossClass 필드 + 정적 가중치 테이블
- ConversionEngine.ConvertOneAsync 그래프 위임 + ChainExecutor(공용 workDir 헬퍼)
- PdfToolProvider 신설: PDF 압축(Light=PDFsharp 구조최적화, Strong=PDFium 렌더+Magick 재인코딩, Max=Ghostscript 외부폴백) + 병합/분할
- xUnit 테스트 프로젝트 신설(현재 0개) + 그래프 경로탐색 회귀 테스트

**핵심 코드 변경:**
- `ProviderRegistry.cs` — _byPair 위에 인접 리스트 그래프 빌드, 증분 등록 Register/Rebuild 추가
- `ConversionEngine.cs:91` — TryGet 직접 매핑에서 그래프 FindBestPath→ExecuteChainAsync 위임으로 전환
- `ConversionPair` — LossClass 필드 추가, 엣지 가중치 SSOT
- `신규 PdfToolProvider` — 동일포맷 pdf→pdf Skip(ConversionEngine.cs:88) 우회, 3단계 압축

**Exit Criteria:** 기존 모든 변환이 그래프 경로로 동일 동작(회귀 테스트 통과)하고, PDF 파일을 3단계 레벨로 압축해 출력 용량 감소를 GUI에서 확인 가능.

### P2 · 손그림 멀티홉 제거 + HWP 한글 양방향  `effort:M` `risk:medium` `status:planned` · depends: P1
**목표:** DocumentProvider.RouteAsync의 손코딩 switch를 삭제하고 원자 엣지만 선언하게 해 그래프를 도그푸딩한다. HWP→DOCX/HTML/TXT 출력 매트릭스를 확장해 한글 사용자 핵심 요구를 충족.

**산출물:**
- DocumentProvider.RouteAsync(92-205) 삭제 → md→html, html→docx 등 원자 엣지만 선언, md→docx는 엔진 자동 합성
- HWP/HWPX 출력 확장: HwpxProvider Outputs에 .docx/.html/.txt/.odt 추가(soffice --convert-to 파라미터화)
- .hwp 입력 시 --infilter='Hwp2002_File' 조건부 지정 + 함초롬/맑은고딕 폰트 누락 감지 경고
- DocumentProvider 입력에 .pdf 추가 → pdf→docx/html/txt 역변환(soffice) + pdf→txt 무외부 폴백(PdfPig)
- RouteAsync 삭제가 손그림과 동일 동작함을 회귀 테스트로 증명

**핵심 코드 변경:**
- `DocumentProvider.cs:92-205` — 멀티홉 switch 삭제, 단일 홉 원자 변환만 선언
- `HwpxProvider` — Outputs 배열에 .docx/.html/.txt/.odt 추가, soffice 타깃 파라미터화
- `DocumentProvider Inputs` — .pdf 추가로 PDF 역변환 엣지 개통

**Exit Criteria:** HWP/HWPX 파일을 DOCX/HTML/TXT/PDF로 변환 가능하고, md→docx 같은 멀티홉이 RouteAsync 없이 그래프 합성으로 동일하게 동작.

### P3 · 외부 프로세스 통합 + 인터페이스 일반화  `effort:L` `risk:medium` `status:in_progress` · depends: P2
**목표:** 3중 복제된 LibreOffice 호출을 단일 ExternalProcessRunner로 통합하고(타임아웃·stderr·Kill), IConverterProvider/ConvertResult를 일반화해 미디어·AI 엣지가 들어올 그릇을 판다.

**산출물:**
- ExternalProcessRunner(CliWrap): 타임아웃+stderr수집+Kill 통합, LibreOffice 3중 복제 흡수
- Abstractions 어셈블리 분리(IConverterProvider/ConvertResult 이전, 타입 동일성)
- IConverterProvider→ConvertRequest/ConvertContext 일반화, 기존 8개 Provider 어댑터로 무중단 마이그레이션
- ConvertResult에 ExtractedText/AiResponse/Metadata/IntermediateArtifacts 필드
- ISettingsStore(DPAPI 암호화) 신설 — API 키·도구 경로 영속화 토대
- Priority 기반 충돌 모델로 _byPair.TryAdd first-wins 교체

**핵심 코드 변경:**
- `3개 Provider` — ConvertWithLibreOfficeAsync 복붙을 ExternalProcessRunner로 통합
- `IConverterProvider.cs:9-15` — ConvertRequest/ConvertContext로 일반화
- `ConvertResult.cs` — 비파일 산출물 필드 추가
- `신규 ISettingsStore` — DPAPI ProtectedData 암호화 JSON 영속화

**Exit Criteria:** LibreOffice가 멈춰도 타임아웃으로 복구되고 stderr가 에러 메시지에 포함되며, 기존 변환이 일반화된 시그니처로 무중단 동작(회귀 테스트 통과).

### P4 · 미디어 레이어 — 영상/오디오 코덱·압축  `effort:XL` `risk:high` `status:in_progress` · depends: P3
**목표:** FFmpeg로 카테고리를 '미디어 변환기'로 점프시킨다. 영상/오디오 N×M 코덱·압축을 라이선스 안전하게 통합하고 배치 병렬화로 트랜스코딩 병목을 해소.

**산출물:**
- FfmpegProvider: FFMpegCore(MIT) + 영상(mp4/mkv/webm/mov/avi/gif)·오디오(mp3/aac/m4a/opus/flac/wav) N×M
- 바이너리 조달: ExternalToolDetector.TryFindFfmpeg + BtbN lgpl-shared 자동 다운로드(SHA256 검증), GlobalFFOptions 경로 고정
- HW 인코더(nvenc/qsv/amf) 우선 + SW 폴백, NotifyOnProgress→IProgress 직결, CancellableThrough(ct)
- 배치 병렬화: ConvertManyAsync 순차 for-loop(57-70)를 Parallel.ForEachAsync로 교체(MaxDegreeOfParallelism)
- PreviewService→IPreviewRenderer 추상화 + FFmpeg 프레임 추출 + 프리뷰 캐시
- ImageMagick ResourceLimits 전역 설정(decompression bomb 방어) + NU190x 취약점 경고 재활성화

**핵심 코드 변경:**
- `신규 FfmpegProvider` — FFMpegCore 래퍼, HW 가속 폴백, RequiresExternal
- `ConversionEngine.cs:57-70` — 순차 for-loop를 Parallel.ForEachAsync로 교체
- `PreviewService.cs:23` — 닫힌 switch를 IPreviewRenderer 레지스트리로, 영상 프레임 추출 추가

**신규 Provider:** FfmpegProvider

**Exit Criteria:** mp4→webm, wav→mp3 등 영상/오디오 변환이 HW 가속으로 동작하고 진행률·취소가 정확하며, 100개 배치가 멀티코어를 활용.

### P5 · AI 부가가치 레이어 — Codex OAuth + API  `effort:L` `risk:high` `status:in_progress` · depends: P3
**목표:** 변환에 'AI가 더 좋게 만든다'는 해자를 얹는다. 기본은 API 키 + 공식 SDK, Codex CLI는 구독자용 opt-in. 키 없으면 AI 페어만 비활성, 기존 변환 무영향.

**산출물:**
- LlmProvider: Microsoft.Extensions.AI(IChatClient)로 OpenAI/Anthropic 공식 SDK 연결 + Codex CLI opt-in(codex exec --json --output-schema)
- AI 매트릭스: 요약(pdf/docx/txt→txt/md), 번역(→대상언어), OCR교정(OcrProvider 출력 2단계 파이프), 이미지 캡션(png/jpg→txt 비전), 메타데이터(→json Structured Output)
- 키 관리: ISettingsStore DPAPI 암호화 + OPENAI_API_KEY/ANTHROPIC_API_KEY 환경변수 폴백, CheckAvailabilityAsync 게이트
- UI: AI 출력 페어에 ✨AI 배지(종량과금·네트워크 명시) + 설정에서 백엔드/모델/키 입력
- Codex 경로 SemaphoreSlim(1) 직렬화(auth.json refresh 토큰 race 방지) 또는 --ephemeral

**핵심 코드 변경:**
- `신규 LlmProvider` — IConverterProvider로 구현, AI는 로컬 변환 없는 신규 엣지로만
- `CheckAvailabilityAsync` — 키/codex --version 게이트로 키 부재 시 NotReady→그래프 자동 비활성
- `UI` — AI 페어 ✨ 배지, 등록 순서로 기본 경로 불점유 보장

**신규 Provider:** LlmProvider

**Exit Criteria:** API 키 입력 시 PDF 요약·이미지 캡션·번역이 동작하고, 키가 없으면 AI 페어만 사라지고 모든 기존 변환은 100% 동작.

### P6 · 매트릭스 자동 극대화 + 헤드리스 CLI  `effort:L` `risk:medium` `status:planned` · depends: P5
**목표:** 앞 단계에서 쌓인 모든 엣지를 그래프가 자동 합성해 진짜 N×M·다방향을 완성하고(video→mp3→txt AI전사 등), 헤드리스 CLI로 자동화·스크립팅을 개방한다.

**산출물:**
- OutputsForInput을 transitive closure로 확장 — '이 파일로 만들 수 있는 모든 포맷' UI 노출 + 손실 경로 경고 배지
- 멀티홉 도그푸딩 검증: hwp→pdf→png, video→mp3→txt(AI) 같은 신규 합성 경로 동작 확인
- 헤드리스 CLI 분리: --json/--output-dir/--quality/--prompt/--codec/--recursive 플래그 + stdout JSON 결과 + exit code
- 워치폴더 모드(FileSystemWatcher + 디바운스 + 파일잠금 재시도, 출력 디렉터리 분리로 무한루프 방지)
- QuickProgressWindow 취소 토큰 전파 + 케이퍼빌리티 사전 점검

**핵심 코드 변경:**
- `ProviderRegistry.cs:52` — OutputsForInput을 그래프 reachability로 확장
- `CliRouter.cs:21` — 옵션 플래그 파싱 + stdout JSON + exit code
- `App.xaml.cs:96` — Quick 경로에 취소 토큰 전파

**Exit Criteria:** HWP 파일에서 PNG까지(멀티홉) 변환 가능하고, CLI가 WPF 창 없이 JSON 결과를 stdout으로 반환해 스크립트가 파싱 가능.

### P7 · 순수 .NET 카테고리 보강 + manifest 어댑터  `effort:L` `risk:low` `status:in_progress` · depends: P6
**목표:** EXE 번들 가능한 순수 관리 라이브러리로 빈 카테고리를 채우고, 단순 CLI 도구를 코드 없이 추가하는 좁은 manifest 어댑터를 도입한다.

**산출물:**
- ArchiveProvider(SharpCompress, 순수관리) — zip/7z/tar/gz/bz2
- DataProvider(Parquet.Net/ClosedXML/CsvHelper) — csv↔json↔xlsx↔parquet
- VectorProvider(Svg.Skia) — svg→png/jpg/webp/pdf, EPS는 Magick+Ghostscript
- PandocProvider(외부 CLI) — md/rst/latex/ipynb/epub 마크업 매트릭스, LibreOffice 겹침은 Priority 라우팅
- EbookProvider(Calibre ebook-convert, 외부) — epub↔mobi↔azw3↔pdf
- manifest 어댑터(ExternalProcessRunner 위 인자 템플릿): qpdf/gs 같은 단순 CLI 코드 없이 추가

**핵심 코드 변경:**
- `신규 4-5개 Provider` — 순수 .NET은 EXE 직접 포함, 외부 CLI는 분리 호출
- `ManifestLoader` — tools/*.manifest.json으로 단순 CLI 엣지 추가
- `Bootstrap` — 하드코딩 배열에 신규 Provider 등록 + manifest 동적 등록

**신규 Provider:** ArchiveProvider, DataProvider, VectorProvider, PandocProvider, EbookProvider

**Exit Criteria:** zip 압축/해제, csv→xlsx, svg→png가 외부 도구 없이 동작하고, manifest 파일 하나로 새 CLI 변환 도구를 코어 재컴파일 없이 추가 가능.

### P8 · 확장성·신뢰성·배포 굳히기  `effort:L` `risk:low` `status:planned` · depends: P7
**목표:** 기능이 다 들어온 뒤 회귀 방지·UI 분해·배포를 다진다. 차별화는 끝났으니 여기서부터는 깨지지 않게 유지.

**산출물:**
- UI MVVM 분해(MainWindow.xaml.cs 1171줄) + Provider 선언형 옵션 스키마 기반 동적 옵션 UI 생성
- 히스토리 도메인 로직을 Core로 분리 + 데모 시드 제거 + 스트리밍 로드/회전 정책
- CI 강화: NuGet 캐시 + self-contained portable EXE 산출 + 외부 바이너리 번들링 파이프라인(FFmpeg LGPL 고지) + dotnet test 게이트
- 출력 형식 매트릭스 3중 중복(AllFormats/PopularOutputs/파일다이얼로그)을 단일 FormatCatalog로 통합
- 테스트 확대: OutputPathHelper 충돌·결합 로직·JSONL round-trip 순수 함수 커버

**핵심 코드 변경:**
- `MainWindow.xaml.cs` — MVVM 분해, 동적 옵션 UI
- `BuildMsix.ps1` — 외부 바이너리 번들 + 라이선스 고지 단계
- `build.yml/release.yml` — 캐시+테스트 게이트+self-contained 산출물

**Exit Criteria:** PR마다 테스트가 게이트로 동작하고, self-contained portable EXE가 자동 산출되며, 새 형식 추가가 단일 FormatCatalog 한 곳 수정으로 끝남.

## 변환 매트릭스 · 그래프 라우팅

- **현재:** 8개 Provider가 PairsFromMatrix로 N×M 쌍을 선언하지만 ProviderRegistry는 (input,output) 단일 홉 딕셔너리(_byPair)만 매핑한다. 멀티홉(md→docx)은 DocumentProvider.RouteAsync(92-205)에 손코딩되어 형식 N개에 O(N²)로 수동 증식한다. 매트릭스는 '거의 모든 것→이미지/PDF/텍스트' 단방향으로만 풍부하고, 역방향(이미지/PDF→편집문서, HWP 출력, 미디어/아카이브)이 구조적으로 비어 있다. 동일포맷(pdf→pdf 압축)은 ConversionEngine.cs:88에서 무조건 Skip된다.
- **목표:** ConversionGraph가 모든 Provider Capability를 순회해 방향 그래프를 빌드하고, Dijkstra가 임의의 A→Z를 원자 엣지 조합으로 자동 합성한다. OutputsForInput은 transitive closure로 확장되어 '이 파일로 만들 수 있는 모든 포맷'을 노출한다. PDF/HWP 양방향, 영상/오디오, 아카이브/데이터/벡터, AI 후처리가 모두 엣지로 편입되고, 동일포맷 압축(pdf→pdf)도 옵션으로 허용되는 엣지가 된다.

**구조적 공백:**
- PDF 압축(pdf→pdf): 어떤 Provider도 수행 못 함 — PdfToolProvider 신설 필요
- PDF→DOCX/HTML 역변환: 편집가능 역변환 경로 전무 — LibreOffice 경유 추가
- HWP/HWPX 출력: H2Orestart import 전용이라 →HWP 불가, →DOCX/HTML/TXT도 미노출
- 영상/오디오 전 카테고리: mp4/mp3/flac 등 미디어 Provider 0개
- 아카이브/폰트/벡터/데이터/전자책: 빈 카테고리(ComingSoon enum 미사용)
- AI 변환(요약/번역/캡션): 추상화·옵션·Provider 어디에도 자리 없음
- 동일포맷 최적화(이미지 리인코딩, PDF 압축): ConversionEngine.cs:88에서 Skip되어 표현 불가

**그래프 라우팅 설계:** 1) 그래프 빌드(앱 시작 1회): ProviderRegistry 생성자 루프(16-30)에서 각 Provider의 Capability.SupportedConversions를 순회해 인접 리스트 Dictionary<string,List<Edge>>를 구축한다. 노드=정규화된 확장자(.png/.pdf/.docx), 엣지=Edge{Provider, ConversionPair, Weight}. 노드 수십·엣지 수백 규모라 그래프는 매우 작다. 2) 가중치: 각 ConversionPair.LossClass(Lossless=0/Container=0.05/Recode=0.4/Rasterize=0.8)를 -log(품질보존율)로 환산하고 홉페널티(작은 상수)와 실행비용(외부 프로세스>in-process)을 가중합한다. 손실은 곱셈적이므로 -log 변환으로 덧셈 최단경로가 곧 최대 품질보존 경로가 된다. 3) 탐색: .NET 9 System.Collections.Generic.PriorityQueue로 Dijkstra(O(E log V), 80~120줄)를 자체 구현한다. ConversionEngine.ConvertOneAsync(91)에서 직접 엣지가 있으면 1홉(기존 동작 호환), 없으면 FindBestPath(inExt,outExt,options)로 멀티홉 경로를 구한다. AllowMultiHop(기본 true)/MaxHops(기본 3)/AvoidLossy 옵션으로 게이트. 4) 실행: ChainExecutor가 경로의 각 홉을 순차 실행하며 중간 산출물을 공용 workDir(Temp/e2e_{Guid})에 체이닝하고, 진행률을 홉 수로 분할해 IProgress에 매핑한다. 각 홉은 기존 provider.ConvertAsync를 그대로 호출(인터페이스 변경 불필요). 5) 안전장치: 멀티홉은 직접 엣지가 없을 때만 발동, 손실 블랙리스트(텍스트→래스터 같은 도메인 경계 전이 통제), 한 홉 실패 시 어느 홉에서 실패했는지 사용자에게 전달. 6) UI: OutputsForInput을 reachability(transitive closure)로 확장하고, 손실 경로로만 도달하는 출력에 '손실 변환' 경고 배지를 붙인다.

## AI 통합

- **Codex OAuth:** Codex CLI를 PATH에서 감지될 때만 활성화되는 구독자용 opt-in 보조 백엔드로 둔다. 핵심 제약: ChatGPT 구독 OAuth 토큰(auth.json의 access/refresh)은 Codex 백엔드 전용이라 api.openai.com에 직접 Bearer로 붙일 수 없다 — 구독 재사용은 오직 codex CLI 프로세스 호출로만 가능. 실행은 ExternalProcessRunner로 `codex exec --skip-git-repo-check --json --output-schema schema.json -o out.json --cd <tempdir> "<프롬프트 + 파일경로>"` 형태. --skip-git-repo-check는 변환 앱에 필수(git 저장소 아닌 폴더 허용), --output-schema로 응답을 JSON Schema로 강제해 메타데이터 추출, --json으로 JSONL 이벤트 스트림 파싱. CheckAvailabilityAsync에서 `codex --version` 프로브 + auth.json 존재 확인. auth.json refresh 토큰 race를 막기 위해 SemaphoreSlim(1) 직렬화 또는 --ephemeral 사용.
- **API 모드:** 기본 경로는 API 키 + 공식 SDK다. Microsoft.Extensions.AI(IChatClient, MIT) 단일 추상화로 OpenAI(공식 OpenAI 패키지, MIT)와 Anthropic(공식 Anthropic 패키지, MIT)을 동일 인터페이스로 다룬다. 사용자는 설정에서 'OpenAI / Claude / Codex CLI / auto'를 고르고 API 키만 입력한다. 키는 ISettingsStore에서 System.Security.Cryptography.ProtectedData(DPAPI, CurrentUser)로 암호화해 %LOCALAPPDATA%에 저장하고, OPENAI_API_KEY/ANTHROPIC_API_KEY 환경변수도 폴백으로 읽어 CI/파워유저 친화. CheckAvailabilityAsync가 키 부재 시 NotReady(키 발급 URL을 ExternalDependency로 안내)를 반환해 그래프에서 자동 비활성.
- **아키텍처:** LlmProvider를 별도 IAiProvider가 아닌 일반 IConverterProvider로 구현해 그래프의 부가 엣지로 편입한다(ADR-5). AI는 로컬 변환이 없는 신규 페어(요약/번역/캡션/메타데이터)에만 노출되며, 등록 순서로 '로컬 변환이 이미 있는 페어는 로컬 Provider가 우선, AI는 신규 페어만'을 보장한다(Priority 충돌 모델). 불변식: 키가 없어도 모든 기존 변환은 100% 동작하고 AI 페어만 비활성, AI는 절대 기본 경로를 점유하지 않으며 UI에 ✨AI 배지(종량과금·네트워크 명시)로만 opt-in 노출된다. 텍스트 추출이 필요하면 DocumentProvider/PdfProvider/OcrProvider를 주입받아 '추출→LLM' 2단계로 구성(OcrProvider가 PdfProvider를 주입받는 선례). 프라이버시: 로컬 문서가 외부 서버로 전송되므로 명시적 동의 토글 필수(기본 OFF), 미래에 Ollama 로컬 모델 경로를 IChatClient로 열어둔다.
- **활용 사례:**
  - 요약: pdf/docx/txt/md → txt/md (긴 문서를 LLM이 요약)
  - 번역: txt/docx/md → txt/docx (대상 언어는 옵션, 비파일 입력 LLM 왕복)
  - OCR 교정: OcrProvider 출력(.txt)을 받아 LLM이 오탈자/줄바꿈 정리 (그래프가 OCR→LLM 2단계 멀티홉으로 자동 합성)
  - 이미지 캡션/대체텍스트: png/jpg → txt (비전 모델)
  - 문서 언어 번역 + 포맷 정규화: csv→md(표), txt→md
  - 메타데이터 생성: 임의 입력 → json (제목/태그/요약, Structured Outputs로 구조화)

## 미디어 레이어

- **영상:** FfmpegProvider(FFMpegCore 5.4.0, MIT)로 mp4/mkv/webm/mov/avi/gif N×M 트랜스코딩. H.264/H.265는 HW 인코더(h264_nvenc/qsv/amf) 우선, LGPL 빌드엔 libx264/x265(GPL)가 없으므로 HW 미지원 시 AV1(libaom)/VP9(libvpx, 둘 다 BSD-like royalty-free)로 폴백. FFprobe로 duration 확보 후 NotifyOnProgress(Action<double>,TimeSpan)을 IProgress에 직결, CancellableThrough(ct)로 취소.
- **오디오:** 오디오는 mp3/aac/m4a/opus/ogg/flac/wav N×M. AAC는 FFmpeg 네이티브 aac 인코더(LGPL, libfdk-aac=nonfree 회피), Opus/FLAC/MP3는 LGPL 빌드로 직접 처리. 오디오 전용 출력(flac/mp3)은 영상 입력에서 오디오 트랙만 추출.
- **PDF 압축:** PdfToolProvider 3단계: Light=PDFsharp(MIT, in-process) 또는 qpdf(Apache 2.0) 구조 최적화(object stream 압축·linearize), Strong=PDFium 렌더+ImageMagick 재인코딩(텍스트 선택성 잃지만 라이선스 안전), Max=Ghostscript(-dPDFSETTINGS /screen)는 AGPL이라 번들 금지·사용자 설치본 감지만. 병합/분할/암호화는 PDFsharp 또는 qpdf.
- **이미지 최적화:** 기존 MagickProvider의 ApplyEncoding(jpg/png/webp/avif/tiff 품질·알파평탄화·MaxLongEdge)을 공용 ImageEncoder 헬퍼로 추출해 PdfProvider/HtmlProvider/CombineAsync의 4중 복제를 제거. 동일포맷 이미지 리인코딩(품질 조절)도 엣지로 허용.
- **외부 바이너리·라이선스 전략:** 단일 포터블 EXE 부담을 줄이기 위해 무거운 바이너리(FFmpeg ~100MB)는 절대 번들하지 않고 'RequiresExternal + 최초 사용 시 자동 다운로드' 모델. 라이선스 게이트(코드 리뷰 강제): FFmpeg는 BtbN lgpl-shared 빌드(--enable-gpl/nonfree 없음)를 별도 프로세스로 호출(동적 분리)해 LGPL 준수 — gyan.dev/BtbN gpl 빌드(GPLv3) 번들 절대 금지. ExternalToolDetector.TryFindFfmpeg가 (a)%LOCALAPPDATA%\Everything2Everything\ffmpeg, (b)시스템 PATH 순 탐지, 없으면 lgpl-shared zip을 SHA256 검증 후 다운로드. GlobalFFOptions.Configure로 경로 고정. NVENC는 LGPL 빌드에서 --enable-nonfree 없이 합법 사용 가능(NVIDIA 공식 확인). About 화면에 'uses FFmpeg under LGPLv2.1' 고지 + 소스 다운로드 링크(LGPL 의무). MSIX 변형에서는 샌드박스 정책상 lgpl-shared DLL을 패키지 동봉(여전히 LGPL 준수). Ghostscript/MuPDF(AGPL)는 사용자 설치본 감지만, codec 특허(H.264/AAC) 위험을 줄이려 AV1/VP9/Opus/FLAC(royalty-free)를 기본 권장 출력으로.

## 리스크 레지스터

| 리스크 | 발생 | 영향 | 완화책 |
|---|---|---|---|
| '보이지 않는 리팩터링의 함정' — 그래프 코어 재설계가 사용자 체감 변화 0인 상태로 길어짐 | medium | high | P1에서 그래프 도입과 PDF 압축(즉시 체감 신기능)을 묶고, transitive closure로 늘어나는 '만들 수 있는 포맷 목록'을 가시 성과로 노출. DocumentProvider.RouteAsync 삭제를 회귀 테스트로 동일 동작 증명. |
| '최단 경로' 압박으로 LlmProvider/FfmpegProvider를 또 하드코딩 switch로 끼워넣어 RouteAsync 지옥 재생산 | medium | high | P1에 그래프 코어를 먼저 심어 하드코딩을 구조적으로 차단. '신규 기능은 그래프 엣지로만 추가'를 불변식으로 명문화하고 코드 리뷰 게이트로 강제. |
| GPL/AGPL 바이너리(FFmpeg gpl빌드·Ghostscript·H2Orestart) 정적 링크로 상업 배포 라이선스 오염 | medium | high | 모든 무거운 외부 도구를 별도 프로세스 분리 호출 + 사용자 설치 감지/LGPL 빌드 자동조달로만 통합. 라이선스 경계를 코드 리뷰 게이트로 강제(ADR-6). |
| 멀티홉 손실 누적·은폐 — HWP→PDF(래스터화)→DOCX가 '편집가능'을 약속하나 이미지 덩어리 반환 | medium | medium | LossClass 가중치로 래스터화에 큰 페널티, 멀티홉은 직접 엣지 없을 때만, MaxHops=3, 손실 블랙리스트, 손실 경로 UI 경고 배지 3겹 가드레일. |
| 인터페이스 일반화(ConvertRequest)가 8개 기존 Provider를 한 번에 깸 | medium | high | 기존 시그니처를 어댑터로 감싸 점진 마이그레이션, 무중단을 회귀 테스트로 보장. P3에 배치해 미디어/AI 동기가 코드에 들어온 뒤 일반화. |
| AI 비결정성·종량과금·네트워크 의존이 '로컬 예측가능 변환' 신뢰를 깸 | high | medium | AI는 기본 경로 불점유, ✨AI 배지 opt-in, 키 없으면 조용히 비활성을 설계 불변식으로 박음. 토큰/비용 표시, 사용자 확인 게이트, 재시도·백오프. |
| 테스트 0개 상태에서 대규모 코어 변경이 회귀를 탐지 못 함 | high | high | P1에서 xUnit 테스트 프로젝트를 최우선 신설하고 그래프 경로탐색·DocumentProvider 회귀를 첫 안전망으로. CI에 dotnet test 게이트 추가. |
| manifest가 또 다른 갓 오브젝트화 — FFmpeg HW가속 폴백 같은 복잡 로직을 manifest로 표현 시도 | low | medium | manifest는 '90% 단순 CLI(qpdf/gs)만, 복잡 로직은 in-box 코드 Provider' 경계를 P1부터 불변식으로 명문화. |

## 성공 지표

| 지표 | 현재 | 목표 |
|---|---|---|
| 멀티홉 경로 자동 합성 | DocumentProvider.RouteAsync에 손코딩된 3-4개 체인만 동작 | 엔진이 임의 A→Z를 그래프 탐색으로 자동 합성, RouteAsync 0줄 |
| 입력당 도달 가능 출력 포맷 수 | 1-hop 직접 출력만(OutputsForInput 직접 매핑) | transitive closure로 확장된 도달 가능 전체 포맷 + 손실 배지 |
| 지원 카테고리 수 | 이미지/PDF/문서/HEIC/OCR (약 5) | +영상/오디오/아카이브/데이터/벡터/전자책/AI (약 12) |
| PDF 압축 기능 | 어떤 Provider도 수행 불가 | 3단계 레벨(Light/Strong/Max) 압축 + 병합/분할 |
| HWP 출력 매트릭스 | →PDF/이미지만, →DOCX/HTML/TXT 미노출 | HWP→DOCX/HTML/TXT/PDF 완성 |
| 코어 테스트 커버리지 | 테스트 프로젝트 0개 | 그래프 탐색·OutputPathHelper·결합·JSONL round-trip 커버 + CI 게이트 |
| 배치 처리 동시성 | 순차 for-loop(코어 1개만 사용) | Parallel.ForEachAsync(MaxDegreeOfParallelism)로 멀티코어 활용 |
| CLI 자동화 가능성 | WPF 창만 띄우고 stdout 무반환 | --json/--codec/--prompt 플래그 + stdout JSON + exit code |

## 다음 세션 인계 노트 (Handoff)

다음 세션은 P1(그래프 엔진 도입 + PDF 압축)부터 시작한다. 시작 순서와 검증 포인트:

1) 가장 먼저 xUnit 테스트 프로젝트를 신설하라(현재 0개). 이게 모든 코어 변경의 안전망이며, 특히 DocumentProvider.RouteAsync 삭제가 '손그림과 동일 동작'임을 증명할 회귀 테스트의 전제다. 먼저 현재 RouteAsync의 모든 경로(md→docx, docx→md, hwp→html 등)에 대한 골든 테스트를 작성해 baseline을 고정하라.

2) ConversionGraph + 자체 Dijkstra를 ProviderRegistry 옆에 얇게 얹어라. ProviderRegistry.cs:16-30 생성자 루프에 그래프 빌드 한 단계만 추가. _byPair는 유지(직접 엣지 1홉 호환). ConversionPair에 LossClass 필드 추가가 선결.

3) 첫 검증: ConversionEngine.ConvertOneAsync(91)를 그래프 위임으로 바꾼 뒤, 기존 모든 변환이 동일 동작하는지 회귀 테스트로 확인. 그 다음에야 RouteAsync를 삭제하고 원자 엣지만 선언하게 바꿔 md→docx가 그래프 합성으로 동일하게 나오는지 검증.

4) PDF 압축(PdfToolProvider)은 ConversionEngine.cs:88의 동일포맷 Skip을 우회해야 한다 — pdf→pdf를 엣지로 허용하는 메커니즘이 그래프 도입과 함께 필요. PDFsharp(MIT) in-process 압축부터 시작하면 외부 의존성 0으로 즉시 체감 가치.

먼저 검증할 불변식: (a) 기존 8개 Provider 변환이 그래프 경로로 100% 동일 동작, (b) 멀티홉은 직접 엣지 없을 때만 발동, (c) 손실 경로에 가중치가 정확히 반영되는지. 라이선스 게이트(GPL/AGPL 분리 호출)는 P4(미디어)부터 본격 적용되지만, P1의 Ghostscript 폴백에서도 '사용자 설치본 감지만, 번들 금지' 원칙을 처음부터 지켜라.

참고: 빌드 후에는 메모리의 project_build_pipeline(publish + 카스케이드 재등록 PowerShell 시퀀스)를 따르고, 사용자가 직접 push & GUI 검증하는 워크플로이므로 큰 결정은 빠른 승인 후 단일 commit으로 진행.

---
## 심사 종합 권고

승자는 idx 2(AI·미디어 기능 우선, 89점)를 '실행 골격'으로, idx 0(그래프 코어, 84점)을 '아키텍처 영혼'으로 삼아 종합한다. 단독 채택이 아니라 두 안의 합성이 정답이다.

핵심 통찰: 세 안의 기술적 부품(자체 Dijkstra 멀티홉, ExternalProcessRunner 통합, ConvertRequest/ConvertResult 일반화, LossClass 가중치, Priority 충돌 모델, AI=엣지, DPAPI 키저장)은 사실상 동일하다. 진짜 차이는 '무엇을 북극성으로 삼아 순서를 짜느냐' 하나뿐이다. 이 프로젝트는 단일 개발자가 직접 push하고 GUI로 검증하며 큰 결정을 빠르게 승인하는 워크플로(프로젝트 메모리)이고, 사용자가 명시적으로 요구한 것은 AI/미디어/HWP/PDF압축이라는 '기능'이다. 따라서 '보이지 않는 리팩터링의 함정'(idx 0 본인이 인정한 최대 리스크)에 빠지는 그래프-우선 순서는 이 맥락에서 부적합하다.

그러나 idx 2의 최대 리스크('최단 경로 압박으로 LlmProvider/FfmpegProvider를 또 하드코딩 switch로 끼워넣어 RouteAsync 지옥 재생산')는 실재하고, idx 0의 그래프 코어가 바로 이 리스크의 백신이다. 그래서 둘을 봉합하는 마스터플랜은 다음 순서다:

Phase 0 (기반, 그러나 즉시 가치와 묶기 — idx 0의 자기 완화책 채택): ExternalProcessRunner 통합(3중 복제 제거) + Priority 충돌 모델 + ConvertRequest/ConvertResult 일반화(어댑터로 무중단). 동시에 DocumentProvider.RouteAsync를 원자 엣지로 분해하고 자체 Dijkstra를 넣어 '손그림=그래프 동일 동작'을 회귀 테스트로 증명(테스트 0개 탈출의 첫걸음). 이 단계의 가시 성과는 transitive closure로 늘어나는 '만들 수 있는 포맷 목록'.

Phase 1 (즉시 체감 — idx 2 순서): HWP 출력 매트릭스 확장(이미 깔린 LibreOffice+H2Orestart 배관 재사용 → DOCX/HTML/TXT) + PDF 압축. 이때 신규 기능은 반드시 '그래프 엣지'로만 추가한다는 것을 불변식으로 박아 idx 2의 하드코딩 유혹을 Phase 0 그래프가 구조적으로 차단.

Phase 2~3 (미디어 + AI 부가가치 레이어 — idx 2): FFmpeg/Ghostscript/qpdf를 idx 2의 라이선스 게이트(분리 프로세스·LGPL/AGPL 경계) 하에 엣지로 추가. AI는 idx 0의 '엣지' + idx 2의 '✨AI 배지·키 없으면 비활성·기본 경로 불점유' 이중 불변식으로 통합. 배치 병렬화는 이 시점에 필수.

idx 1(플러그인 생태계, 71점)은 베이스로는 과잉 엔지니어링이라 탈락하지만, 두 아이디어는 흡수한다: (1) Priority 기반 충돌 모델(이미 Phase 0에 편입), (2) manifest를 '풀 DSL'이 아니라 'ExternalProcessRunner 위 선언적 인자 템플릿'으로 축소해 qpdf/gs 같은 단순 CLI를 코드 없이 추가하는 좁은 용도로만 채택. 복잡 로직(FFmpeg HW가속·AI)은 in-box 코드 원칙을 P1부터 못박아 manifest 갓오브젝트화를 방지한다(idx 1 본인의 하이브리드 경계 그대로).

한 줄 요약: idx 2의 '기능이 견인하는 로드맵'에 idx 0의 '그래프가 받치는 코어'를 Phase 0에 심어, 사용자 체감 가치를 빠르게 내면서도 RouteAsync 지옥의 재발을 그래프로 원천 차단한다.

### 마스터플랜에 흡수한 최고의 아이디어

- [idx 0의 핵심] DocumentProvider.RouteAsync(92-205) 손그림 멀티홉을 '삭제'하고 엔진이 Dijkstra로 동일 경로를 계산하게 만드는 도그푸딩 — 이것을 회귀 테스트로 '그래프=손그림 동일 동작' 객관 증명. 테스트 0개인 현 상태에서 이 변환의 첫 안전망이 된다. 어떤 마스터플랜이 채택되든 이 검증 루프는 필수.
- [idx 0의 핵심] 손실을 -log(보존율)+홉페널티 단일 가중치로 ConversionPair.LossClass(Lossless=0/Container=0.05/Recode=0.4/Rasterize=0.8) 필드에 SSOT화. 이것이 멀티홉 경로 선택과 UI '⚠손실' 배지의 단일 출처. idx 2의 '손실 변환 경고 배지'도 이 가중치를 그대로 소비.
- [idx 0의 핵심] AI를 IAiProvider 특수 인터페이스가 아니라 '로컬 변환이 없는 신규 엣지(요약/번역/캡션)'로 그래프에 환원 — idx 2의 '후처리 부가가치 레이어'와 결합하면, AI는 그래프상 엣지이면서 동시에 키 없으면 자동 비활성 노드 + ✨AI 배지로 노출되는 이중 안전장치를 얻는다.
- [idx 2의 핵심] AI 불변식: 키가 없어도 모든 기존 변환 100% 동작, AI는 절대 기본 경로를 점유하지 않고 ✨AI 배지 페어로만 opt-in, 키 부재 시 등록 순서·게이트로 조용히 비활성. '변환은 로컬에서 예측가능' 신뢰를 깨지 않는 설계 불변식.
- [idx 2의 핵심] 라이선스 경계를 코드 리뷰 게이트로 강제: FFmpeg는 GPL 정적링크 금지·LGPL 분리호출만, Ghostscript/MuPDF는 AGPL이라 사용자 설치본 감지만, H2Orestart/Calibre는 GPL이라 외부 프로세스 분리. 모든 무거운 외부 도구 = '별도 프로세스 분리 호출 + 사용자 설치 감지 또는 LGPL 빌드 자동조달'. .NET 9 단일 EXE 상업 배포 오염 방지의 핵심.
- [idx 2의 핵심] 단계 순서: PDF 압축 + HWP 출력 매트릭스 확장을 최우선 출시(이미 HwpxProvider의 LibreOffice+H2Orestart 배관 존재 → DOCX/HTML/TXT 출력만 추가하면 즉시 신규 가치). 초기 체감 가치를 그래프 리팩터링보다 먼저.
- [idx 2의 핵심] 배치 병렬화: ConversionEngine 순차 for-loop(57-70)를 Parallel.ForEachAsync(동시성 제한 포함)로 교체. AI 네트워크 왕복·영상 트랜스코딩의 치명적 병목 해소. 더불어 ImageMagick ResourceLimits 전역 설정 + decompression bomb 방어로 미디어 공격면 차단.
- [idx 1의 핵심] 레지스트리 충돌 모델 교체: _byPair.TryAdd(22)의 조용한 first-wins를 ProviderCapability.Priority + 다중 Provider 공존으로 교체 + 충돌 시 진단 경고. 같은 (input,output)에 빠른변환/고품질/AI 등 복수 전략 등록 가능. idx 0/2 모두 이 교체가 전제 조건.
- [idx 1의 부분 채택] manifest는 '풀 생태계 비전'이 아니라 'ExternalProcessRunner 위의 선언적 인자 템플릿({input}/{output}/{outdir}/{format})'으로만 제한 채택 — qpdf/Ghostscript 같은 단순 CLI 압축 도구를 코드 없이 추가하는 용도. 단, 복잡 로직(FFmpeg HW가속 폴백/AI)은 in-box 코드 Provider 원칙을 P1부터 못박아 manifest 갓오브젝트화 방지.
- [3안 공통] ExternalProcessRunner 단일 추상화로 LibreOffice 3중 복제(DocumentProvider:238-281 / DocxProvider:113-157 / HwpxProvider:107-151) 통합 — 타임아웃·stderr 수집·Kill 일원화. 이후 모든 외부 엣지(FFmpeg/Ghostscript/qpdf/Codex CLI)가 이 러너 하나 공유. 세 안이 만장일치로 지목한 가장 안전하고 즉시 실행가능한 첫 리팩터링.
- [3안 공통] IConverterProvider 시그니처(9-15)를 ConvertRequest/ConvertContext로 일반화 + ConvertResult(10)에 비파일 산출물 필드(추출 텍스트·AI 응답·미디어 메타데이터) 추가. 단, 기존 8개 Provider는 어댑터로 감싸 점진 마이그레이션 + 회귀 테스트로 무중단 보장(idx 0의 마이그레이션 전략 채택).
- [3안 공통] ConvertOptions 갓 오브젝트(14+ sub-record)를 그래프 옵션(AllowMultiHop/MaxHops/AvoidLossy) + 형식별 옵션 백(IReadOnlyDictionary 또는 Provider 선언형 스키마)으로 분해 → Video/Audio/Ai/PdfCompress를 sub-record 증식 없이 수용. DPAPI(ProtectedData) 기반 ISettingsStore 신설로 API 키·도구 경로 안전 저장.