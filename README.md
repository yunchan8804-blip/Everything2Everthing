# Everything2Everything

Windows 우클릭 한 방에 **모든 것을 모든 것으로** 양방향 변환. 이미지 ↔ 이미지, 이미지 ↔ PDF, 문서 ↔ PDF/이미지, HTML → PDF/이미지를 통합 매트릭스로 처리합니다.

> 이 프로젝트는 단방향 *EverythingToJpeg* 에서 양방향 *Everything2Everything* 으로 피보팅된 결과물입니다.

- **카스케이드 컨텍스트 메뉴** — 입력 파일에 따라 가능한 출력 형식만 자동 노출
- **양방향 이미지 매트릭스** — PNG ↔ JPEG ↔ WebP ↔ AVIF ↔ TIFF ↔ BMP ↔ GIF
- **HTML → PDF**, **DOCX/HWPX ↔ PDF** 같은 문서 변환 포함
- 출력 형식별 인코딩 옵션(JPEG quality, WebP lossless, AVIF speed 등) 분리
- 진행 상황 + 썸네일 + 드래그 & 드롭 메인 창

## 변환 매트릭스

| Provider | 입력 | 출력 |
|---|---|---|
| **MagickProvider** | PNG · JPEG · WebP · AVIF · BMP · TIFF · GIF · PSD · RAW (NEF/CR2/CR3/ARW/DNG/RAF/ORF/RW2/SRW/PEF) | PNG · JPEG · WebP · AVIF · BMP · TIFF · GIF |
| **HeicProvider** | HEIC · HEIF | PNG · JPEG · WebP · AVIF · BMP · TIFF · GIF |
| **PdfProvider** | PDF | PNG · JPEG · WebP · AVIF · BMP · TIFF |
| **HtmlProvider** | HTML · HTM | PNG · JPEG · WebP · AVIF · BMP · TIFF · **PDF** |
| **DocxProvider** | DOCX · DOC | **PDF** + 이미지 7종 |
| **HwpxProvider** | HWP · HWPX | **PDF** + 이미지 7종 |

총 200+ 변환 쌍 지원. 알파 채널은 출력 포맷이 지원할 때 자동 보존, 미지원이면 흰색(또는 설정된 배경색)으로 평탄화합니다.

## 빠른 시작

### 1) 빌드

```powershell
# 솔루션 빌드
dotnet build Everything2Everything.slnx -c Release

# 단일 폴더 publish (framework-dependent, .NET 9 Desktop Runtime 필요)
dotnet publish src\Everything2Everything.App\Everything2Everything.App.csproj `
    -c Release -r win-x64 --self-contained false -o publish

# .NET 런타임 동봉 (단일 사용자 배포가 편함)
dotnet publish src\Everything2Everything.App\Everything2Everything.App.csproj `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -o publish-self
```

산출물: `publish\Everything2Everything.exe`

### 2) 컨텍스트 메뉴 등록

```powershell
.\Everything2Everything.exe register
.\Everything2Everything.exe unregister
```

> Windows 11 기본 우클릭에서는 **추가 옵션 표시(Shift+우클릭)** 안에 노출됩니다. 메인 메뉴 노출은 MSIX + IExplorerCommand DLL이 필요합니다 — `packaging/BuildAndSign.ps1` 참고.

### 3) 사용

#### 우클릭 카스케이드
파일 우클릭 → **Everything2Everything으로 변환** → 서브메뉴에서 출력 형식 선택. 각 입력의 매트릭스에 따라 가능한 출력만 표시됩니다.

| 입력 → 노출되는 서브메뉴 항목 |
|---|
| `.png` → JPEG · WebP · AVIF · GIF · TIFF · BMP · 변환… |
| `.heic` → JPEG · PNG · WebP · AVIF · GIF · TIFF · BMP · 변환… |
| `.pdf` → JPEG · PNG · WebP · AVIF · TIFF · BMP · 변환… |
| `.docx` → PDF · JPEG · PNG · WebP · AVIF · TIFF · BMP · 변환… |
| `.html` → PDF · JPEG · PNG · WebP · AVIF · TIFF · BMP · 변환… |

"변환…" 항목은 메인 창을 띄워 사이드바에서 출력 형식 ComboBox로 직접 선택할 수 있습니다.

#### 메인 창
파일을 드래그 & 드롭하거나 Ctrl+O. 사이드바의 **TARGET FORMAT** ComboBox는 **큐의 모든 파일이 변환 가능한 출력의 교집합**만 보여줍니다 (이종 입력을 섞으면 자동 필터링).

#### CLI

```powershell
# <ext>로 변환 (배치 가능)
.\Everything2Everything.exe to png photo.heic shot.jpg banner.webp
.\Everything2Everything.exe to pdf doc.docx report.html
.\Everything2Everything.exe to webp *.png

# 빠른 변환 (기본 출력 .jpg, 컨텍스트 메뉴 호환)
.\Everything2Everything.exe quick photo.heic

# 메인 창에서 출력 형식 선택
.\Everything2Everything.exe dialog photo.heic

# 진단
.\Everything2Everything.exe diagnose
```

## 외부 도구 의존성

| 변환 | 필요 도구 |
|---|---|
| 모든 이미지 ↔ 이미지, RAW/PSD 디코딩 | (내장) Magick.NET 14 |
| HEIC/HEIF 디코딩 | (내장) PhotoSauce + libheif |
| PDF ↔ 이미지 | (내장) PDFtoImage(PDFium) |
| HTML → 이미지/PDF | Microsoft Edge **WebView2 Runtime** (Win11 기본 포함) |
| DOCX/DOC → PDF/이미지 | **Microsoft Word** 또는 **LibreOffice** (자동 감지) |
| HWP/HWPX → PDF/이미지 | **LibreOffice** + [**H2Orestart**](https://github.com/ebandal/H2Orestart) 확장 |

메인 창 시작 시 가용성을 자동 점검해 사이드바에 안내하고, 미설치 도구는 다이얼로그에서 다운로드 링크를 제공합니다.

## 기술 스택

- **.NET 9 + WPF**, Windows 10.0.19041.0+
- UI: **WPF-UI 4.3** (Win11 Fluent 2)
- 변환 엔진: Magick.NET, PDFtoImage, PhotoSauce.MagicScaler + Libheif, WebView2 (CDP)

## 아키텍처

```
입력 파일 → ProviderRegistry.TryGet(input ext, output ext)
                     ↓
       (input, output) → IConverterProvider 매트릭스 인덱스
                     ↓
       provider.ConvertAsync(source, outDir, outExt, options, ...)
```

핵심 추상화:
- `ConversionPair(InputExtension, OutputExtension)` — 단방향 쌍
- `ProviderCapability.SupportedConversions: IReadOnlyList<ConversionPair>`
- `ProviderRegistry` — `Dictionary<(input, output), Provider>` 매트릭스 인덱싱 + `OutputsForFile()` 쿼리
- `ConversionEngine.ConvertOneAsync(source, outputExt, options)` — 동일 입출력 자동 skip + 출력 ext 기반 서브폴더 suffix(`<base>_png/`, `<base>_pdf/`)
- `ConvertOptions` — 형식별 sub-record (`Jpeg.Quality`, `Webp.Lossless`, `Avif.Speed`, `PdfRender.Dpi`, `HtmlRender.FullPage` 등)

## 키보드 단축키

| 키 | 동작 |
|---|---|
| Ctrl+O | 파일 추가 |
| Ctrl+Enter | Process Queue (변환 시작) |
| Esc | 창 닫기 |
| F5 | 통계 새로고침 |

## 두 가지 사용 방식

### A) Portable EXE — 가장 가벼움
- `dotnet publish` 산출물 그대로 사용
- 우클릭 → **추가 옵션 표시** → "Everything2Everything으로 변환" → 카스케이드 서브메뉴
- 인증서·서명 불필요

### B) MSIX 패키지 — Win11 메인 메뉴 노출
- `packaging/BuildMsix.ps1`로 MSIX 빌드
- 자체 서명 인증서를 `LocalMachine\TrustedPeople`에 임포트 후 사이드로드
- 우클릭 → 메인 메뉴 바로 노출 (IExplorerCommand DLL 사용)
- 자세한 절차는 [packaging/README.md](packaging/README.md)

## 프로젝트 구조

```
Everything2Everything/
├── Everything2Everything.slnx
└── src/
    ├── Everything2Everything.Core/        — 변환 엔진, Provider 추상화
    │   ├── Providers/
    │   │   ├── ConversionPair.cs          — (입력ext, 출력ext) 단방향 쌍
    │   │   ├── ProviderCapability.cs      — SupportedConversions + 입출력 헬퍼
    │   │   ├── ProviderRegistry.cs        — N×M 매트릭스 인덱싱
    │   │   └── IConverterProvider.cs
    │   ├── Converters/                    — Magick / Heic / Pdf / Docx / Html / Hwpx
    │   ├── ConversionEngine.cs            — outputExt 라우팅 + 서브폴더 suffix
    │   ├── ConvertOptions.cs              — 형식별 sub-options
    │   └── Everything2EverythingBootstrap.cs
    └── Everything2Everything.App/         — WPF + CLI 통합 진입점
        ├── App.xaml(.cs)
        ├── Cli/CliRouter.cs               — verb: to / quick / dialog / register / diagnose
        ├── Shell/ContextMenuRegistrar.cs  — HKCU 카스케이드 등록
        └── Views/                         — Fluent UI 화면
```

## Provider 확장

새 변환 쌍을 추가하려면 `IConverterProvider`를 구현하고 `Everything2EverythingBootstrap.CreateDefault()`에 등록합니다. `ProviderCapability.SupportedConversions`에 (입력ext, 출력ext) 쌍 리스트를 정의하면 `ProviderRegistry`가 매트릭스 인덱스에 자동 편입하며, 컨텍스트 메뉴 카스케이드 + 메인 창 ComboBox에 자동 반영됩니다.

```csharp
// 매트릭스 헬퍼 — 모든 입력 × 모든 출력 카르테시안 곱
SupportedConversions: ProviderCapability.PairsFromMatrix(
    new[] { ".png", ".jpg", ".webp" },
    new[] { ".png", ".jpg", ".pdf" }),
```

## 라이선스

MIT.
