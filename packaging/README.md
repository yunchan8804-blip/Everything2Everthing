# MSIX 패키징

Win11 메인 우클릭 메뉴에 "Everything2Everything으로 변환" 카스케이드 메뉴를 띄우는 정공법. 레지스트리 기반 카스케이드 메뉴는 [Portable EXE 방식](../README.md#a-portable-exe--가장-가벼움)을 참고하세요.

## 구성

```
packaging/
├── Package.appxmanifest           — IExplorerCommand 등록 (com:Class + desktop4:FileExplorerContextMenus)
├── Assets/                        — 앱 아이콘 (placeholder, GenerateAssets.ps1로 생성)
├── GenerateAssets.ps1             — placeholder PNG 일괄 생성
├── CreateDevCert.ps1              — 자체 서명 코드사이닝 인증서 생성 + PFX export
├── BuildMsix.ps1                  — .NET publish + C++ DLL 빌드 + makeappx + (선택) signtool
└── Install-Everything2Everything.ps1   — 5대 PC 1회 설치 스크립트
```

C++ Shell DLL은 `src/Everything2Everything.Shell/` 에 있고 `BuildMsix.ps1` 안에서 자동 빌드됩니다.

## 1회: 자체 서명 인증서 만들기

```powershell
cd packaging
.\CreateDevCert.ps1
# Subject 기본값: CN=Everything2EverythingDev (Package.appxmanifest의 Publisher와 일치)
# 비밀번호 입력 → Everything2Everything-DevCert.pfx 생성
```

출력된 Thumbprint를 `BuildMsix.ps1 -CertThumbprint <값>` 으로 사용하거나, PFX 파일을 5대 PC에 복사해서 설치 시 사용합니다.

## 빌드

### 미서명 (Phase 1 그대로 사용 가능, 메인 메뉴 노출은 안 됨)
```powershell
.\BuildMsix.ps1
# 산출: packaging/dist/Everything2Everything-x64.msix
```

### 서명
```powershell
# 방법 1: PFX 사용
.\BuildMsix.ps1 -Sign -PfxPath .\Everything2Everything-DevCert.pfx

# 방법 2: 인증서 저장소의 Thumbprint
.\BuildMsix.ps1 -Sign -CertThumbprint AABBCCDD...
```

## 5대 PC 설치 (관리자 PowerShell)

```powershell
.\Install-Everything2Everything.ps1 `
    -PfxPath .\Everything2Everything-DevCert.pfx `
    -MsixPath .\Everything2Everything-x64.msix
```

스크립트가 자동으로:
1. PFX를 `LocalMachine\TrustedPeople` 에 임포트
2. PFX를 `LocalMachine\Root` 에도 임포트 (체인 신뢰)
3. `Add-AppxPackage` 로 MSIX 사이드로드

설치 후 PNG/JPG/HEIC/PDF/DOCX 등을 우클릭하면 **메인 메뉴에 직접** "Everything2Everything으로 변환" 카스케이드 메뉴가 보입니다.

## 미서명 사이드로드 (Phase 2 임시 사용)

자체 서명 만들기조차 귀찮을 때:
```powershell
# 개발자 모드 켜기: 설정 → 개인 정보 및 보안 → 개발자용 → 켜기
Add-AppxPackage -AllowUnsigned -Path .\Everything2Everything-x64.msix
```
> Win11 24H2부터 `-AllowUnsigned` 지원. 이전 버전은 자체 서명 권장.

## CI/CD

`.github/workflows/release.yml` — 태그 푸시(`v1.0.0` 등) 시 자동:
1. .NET / MSBuild 셋업
2. `BuildMsix.ps1` 실행 (미서명)
3. GitHub Release 생성 + MSIX 첨부

서명까지 자동화하려면 GitHub Secrets에 `PFX_BASE64`, `PFX_PASSWORD`를 등록하고 워크플로에 단계 추가 (별도 보안 검토 후).

## 트러블슈팅

| 증상 | 원인 / 해결 |
|---|---|
| `Add-AppxPackage`: "신뢰할 수 없는 인증서" | PFX를 `LocalMachine\TrustedPeople`에 임포트했는지 확인 (Install 스크립트 자동 수행) |
| 메뉴가 안 뜸 | 탐색기 재시작: 작업관리자 → "Windows 탐색기" 다시 시작 |
| Publisher 불일치 오류 | `Package.appxmanifest`의 `Publisher=` 와 인증서 `Subject` 가 정확히 일치해야 함 |
| `App identity required` | MSIX 패키지로 설치된 경우에만 IExplorerCommand 작동. portable EXE는 Phase 1 레지스트리 방식 사용 |
