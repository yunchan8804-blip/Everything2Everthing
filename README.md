# EverythingToJpeg

Windows 우클릭 컨텍스트 메뉴에서 한 방에 JPEG로. PNG · GIF · BMP · TIFF · WebP · AVIF · HEIC · RAW · PSD · PDF · DOCX 를 지원합니다.

- **빠른 변환** — 다이얼로그 없이 원본 폴더의 `<원본명>_jpeg/` 하위에 즉시 저장
- **변환…** — 옵션 다이얼로그(품질, 출력 위치, 이름 충돌, 크기 제한, PDF DPI)
- 진행 상황 + 썸네일 + 드래그 & 드롭 (메인 창)

## 빠른 시작

### 1) 빌드

```powershell
# 솔루션 빌드
dotnet build EverythingToJpeg.slnx -c Release

# 단일 폴더 publish (framework-dependent, .NET 9 Desktop Runtime 필요)
dotnet publish src\EverythingToJpeg.App\EverythingToJpeg.App.csproj `
    -c Release -r win-x64 --self-contained false -o publish

# .NET 런타임 동봉 (단일 사용자 배포가 편함)
dotnet publish src\EverythingToJpeg.App\EverythingToJpeg.App.csproj `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -o publish-self
```

산출물: `publish\EverythingToJpeg.exe`

### 2) 컨텍스트 메뉴 등록

`EverythingToJpeg.exe`를 한 번 실행 → "컨텍스트 메뉴 등록" 클릭. 또는 CLI:

```powershell
.\EverythingToJpeg.exe register
.\EverythingToJpeg.exe unregister
```

> Windows 11 메인 우클릭 메뉴가 아니라 "추가 옵션 표시(Shift+우클릭)" 메뉴에 노출됩니다. 메인 메뉴 노출은 Phase 2에서 IExplorerCommand + MSIX 로 추가 예정.

### 3) 사용

- 파일 우클릭 → "추가 옵션 표시" → **JPEG로 빠른 변환** 또는 **JPEG로 변환…**
- 또는 메인 창에 파일/폴더를 끌어다 놓기

## 지원 현황

| 형식 | 상태 | 참고 |
|---|---|---|
| PNG · BMP · JPEG · WebP · AVIF · PSD · TIFF · GIF · RAW(NEF/CR2/CR3/ARW/DNG/RAF/ORF/RW2/SRW/PEF) | ✅ 준비됨 | Magick.NET 14.x |
| HEIC · HEIF | ✅ 준비됨 | PhotoSauce + libheif 디코드 |
| PDF | ✅ 준비됨 | PDFtoImage(PDFium) |
| DOCX · DOC | ⚙ 외부 도구 필요 | Microsoft Word 또는 LibreOffice 자동 감지 |
| HTML · HTM | 🕐 개발 중 | WebView2 헤드리스 캡처 예정 |
| HWP · HWPX | 🕐 개발 중 | LibreOffice + H2Orestart 파이프라인 예정 |

## 기술 스택

- **.NET 9 + WPF**, Windows 10.0.19041.0+
- UI: **WPF-UI 4.3** (Win11 Fluent 2 — Mica 백드롭, Segoe UI Variable 타입 램프)
- 변환 엔진: Magick.NET, PDFtoImage, PhotoSauce.MagicScaler + Libheif

## 로드맵

| 단계 | 상태 | 내용 |
|---|---|---|
| Phase 1 | ✅ | 레지스트리 컨텍스트 메뉴 (Win11 "추가 옵션 표시"), 핵심 변환(이미지·HEIC·RAW·PDF·DOCX), Fluent UI |
| Phase 2 | ✅ 빌드 가능 | C++ IExplorerCommand DLL, MSIX 패키징, 자체 서명 인증서, GitHub Releases 자동화 — `packaging/README.md` 참조 |
| Phase 3 | 🕐 | HTML(WebView2), HWP/HWPX(LibreOffice + H2Orestart) 실구현 |

## 두 가지 사용 방식

### A) Portable EXE — 가장 가벼움 (Phase 1)
- `dotnet publish` 산출물 그대로 사용
- 우클릭 → **추가 옵션 표시** → "JPEG로 빠른 변환" / "JPEG로 변환…"
- 인증서·서명 불필요

### B) MSIX 패키지 — Win11 메인 메뉴 노출 (Phase 2)
- `packaging/BuildMsix.ps1` 로 MSIX 빌드
- 자체 서명 인증서를 `LocalMachine\TrustedPeople`에 임포트 후 사이드로드
- 우클릭 → 바로 메인 메뉴에 항목 노출
- 자세한 절차는 [packaging/README.md](packaging/README.md)

## 프로젝트 구조

```
everythingToJpeg/
├── EverythingToJpeg.slnx
└── src/
    ├── EverythingToJpeg.Core/        — 변환 엔진, Provider 추상화
    │   ├── Providers/                — IConverterProvider + Capability 메타데이터
    │   ├── Converters/               — Magick / Heic / Pdf / Docx / Html / Hwpx
    │   ├── ConversionEngine.cs
    │   └── EverythingToJpegBootstrap.cs
    └── EverythingToJpeg.App/         — WPF + CLI 통합 진입점
        ├── App.xaml(.cs)             — CLI 라우터
        ├── Cli/CliRouter.cs          — verb: quick / dialog / register / diagnose
        ├── Shell/ContextMenuRegistrar.cs — HKCU 레지스트리 등록
        └── Views/                    — Fluent UI 화면
```

## Provider 전략 (확장 포인트)

새 형식을 지원하려면 `IConverterProvider`를 구현하고 `EverythingToJpegBootstrap.CreateDefault()`에 등록합니다. `ProviderCapability`에 다음을 명시하세요:

- `Status` — `Available` / `Preview` / `RequiresExternal` / `ComingSoon` / `Disabled`
- `Extensions` — 자동 라우팅 + 컨텍스트 메뉴 등록 키
- `ExternalDependencies` — UI에 자동 노출되는 외부 도구
- `RoadmapNote` — 사용자에게 보여줄 향후 계획

`ComingSoon` 상태는 메인 창의 "지원 형식" 섹션에 자동 노출되지만 컨텍스트 메뉴 등록에서는 자동 제외됩니다.

## 라이선스

MIT (예정).
