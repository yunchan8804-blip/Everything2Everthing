# Phase 2 — MSIX 패키징 (placeholder)

이 폴더는 향후 IExplorerCommand 셸 익스텐션 + MSIX Sparse Package 작업을 위한 자리입니다. 현재 구현되지 않았습니다.

## 다음 단계 체크리스트

- [ ] `EverythingToJpeg.Shell` C++/WinRT 또는 C#(WinRT projection) 프로젝트 생성 → `IExplorerCommand` 구현
- [ ] `Package.appxmanifest` 작성 — `<uap3:Extension Category="windows.fileExplorerContextMenus">` 사용
- [ ] Windows Application Packaging Project (.wapproj) 생성 — App + Shell DLL 묶기
- [ ] 자체 서명 인증서 생성 스크립트:
  ```powershell
  New-SelfSignedCertificate -Type CodeSigningCert `
      -Subject "CN=EverythingToJpegDev" `
      -KeyAlgorithm RSA -KeyLength 2048 `
      -CertStoreLocation "Cert:\CurrentUser\My"
  ```
- [ ] MakeAppx + SignTool로 MSIX 빌드 + 서명
- [ ] 5대 PC에 인증서를 `Cert:\LocalMachine\TrustedPeople`에 임포트
- [ ] GitHub Actions: 태그 푸시 시 unsigned MSIX 자동 빌드 + Release 첨부

## 참고

- [PowerToys 컨텍스트 메뉴 개발 문서](https://github.com/microsoft/PowerToys/blob/main/doc/devdocs/common/context-menus.md)
- [IExplorerCommand C# 예제](https://github.com/cjee21/IExplorerCommand-Examples)
- [Microsoft: Sparse package 등록](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps)
