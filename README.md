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

## 로드맵 (Phase 2)

1. **IExplorerCommand 셸 익스텐션 + MSIX Sparse Package** — Win11 메인 컨텍스트 메뉴 직접 노출
2. **HTML 변환** — WebView2 헤드리스, viewport 옵션
3. **HWP/HWPX 변환** — LibreOffice + H2Orestart 자동 설치 가이드
4. **GitHub Releases CI/CD** — 태그 푸시 시 자동 빌드 + MSIX 패키징
5. **자체 서명 인증서 자동 생성·배포** — 내부 5대 PC 신뢰 체인 자동화 (현재는 unsigned MSIX → 개발자 모드 필요)

## 미서명 빌드를 신뢰할 PC에 설치하기 (Phase 2 미리보기)

본인 PC 5대에만 설치할 계획이므로 정식 코드사이닝 인증서 없이도 사용 가능합니다.

### 옵션 A — Portable EXE (지금 바로 가능)
1. `publish` 폴더 통째로 PC에 복사
2. `EverythingToJpeg.exe` 실행 → 한 번만 "컨텍스트 메뉴 등록"
3. 끝. SmartScreen 경고가 뜨면 "추가 정보" → "실행"

### 옵션 B — MSIX Sparse Package (Phase 2)
1. `EverythingToJpeg.Package` 프로젝트로 unsigned MSIX 빌드
2. 각 PC에서 **개발자 모드 켜기** (설정 → 개인 정보 및 보안 → 개발자용)
3. PowerShell:
   ```powershell
   Add-AppxPackage -AllowUnsigned -Path EverythingToJpeg.msix
   ```
4. 또는 Group Policy로 사이드로딩 허용 후 자체 서명 인증서를 Local Machine\Trusted People에 임포트

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
