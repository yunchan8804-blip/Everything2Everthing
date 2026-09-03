# 05. 릴리즈 엔지니어링 & 배포 체계 (Release & Deployment to git.chanpaca.net)

이 문서는 **Everything2Everything**의 무결점 배포를 위한 **릴리즈 엔지니어링, 시맨틱 버전 태깅, 그리고 Forgejo(`git.chanpaca.net`) 배포 파이프라인 규약**을 정의한다.

---

## 1. 배포 인프라 및 아키텍처 개요

### 1.1 대상 저장소 (Target Remote)
*   **플랫폼**: Forgejo (자체 호스팅 Git 서버)
*   **도메인**: `https://git.chanpaca.net`
*   **원격 저장소 경로**: `https://git.chanpaca.net/yunchan/Everything2Everything.git`
*   **SSH 경로**: `ssh://git@git.chanpaca.net:2222/yunchan/Everything2Everything.git`
*   **인증 방식**: Windows Credential Manager에 등록된 `yunchan` 계정 자격증명 및 API 토큰.

### 1.2 배포 산출물 (Dual Binary Deliverables)
1. **Portable EXE (`publish/Everything2Everything.exe`)**:
   - .NET 9 프레임워크 의존형 x64 단일 실행 파일.
   - 우클릭 컨텍스트 메뉴 자체 등록/해제 (`.\Everything2Everything.exe register`) 지원.
   - 무설치 초경량 배포용.
2. **MSIX 패키지 (`packaging/dist/Everything2Everything-x64.msix`)**:
   - Windows 11 Fluent 2 메인 메뉴(Shift 없이 바로 노출) 지원용 IExplorerCommand DLL 동봉 패키지.
   - 개발자 인증서 서명 패키지.

---

## 2. 릴리즈 게이트 (Release Gates)

배포 스크립트 실행 시 다음 4단계 게이트를 하나라도 통과하지 못하면 릴리즈는 즉각 중단(Aborted)된다:

```
[ Gate 1: Test Suite 100% Pass ]
   │  dotnet test Everything2Everything.Tests.csproj (실패 0개, 경고 0개)
   ▼
[ Gate 2: Version Synchronization ]
   │  Package.appxmanifest, App.csproj, Git Tag 버전 일치 확인 (예: 1.0.6.0 <-> v1.0.6)
   ▼
[ Gate 3: Release Build & Artifacts ]
   │  dotnet publish (Release win-x64) + BuildMsix.ps1 산출물 생성 확인
   ▼
[ Gate 4: Git Tag & Forgejo Release ]
   │  git tag 생성 -> git.chanpaca.net 푸시 -> Forgejo API 릴리즈 등록 및 바이너리 첨부
```

---

## 3. 시맨틱 버저닝 (Semantic Versioning 2.0.0)

모든 릴리즈 태그는 `vMAJOR.MINOR.PATCH` 형식을 엄격히 준수한다:
*   **MAJOR**: 아키텍처 전면 개편 또는 호환되지 않는 CLI/매트릭스 변경.
*   **MINOR**: 신규 변환 프로바이더 추가, 새로운 GUI 기능 추가.
*   **PATCH**: 버그 수정, 디자인/정렬 결함 패치, 성능 최적화.

### 버전 동기화 대상 파일
1. `packaging/Package.appxmanifest`: `<Identity Version="1.0.6.0" ... />`
2. `src/Everything2Everything.App/Everything2Everything.App.csproj`: `<Version>1.0.6</Version>`
3. `docs/ssot/_data/meta.json` 또는 릴리즈 노트

---

## 4. 자동화 배포 스크립트 (`tools/Publish-Release.ps1`)

릴리즈 엔지니어링은 단 1줄의 PowerShell 스크립트로 전자동 수행된다:

```powershell
# 사용법 예시: 버전 1.0.6 릴리즈 배포
pwsh tools/Publish-Release.ps1 -Version "1.0.6" -ReleaseNotes "디자인 감사 TDD 적용 및 UI 수직 정렬/패딩 결함 수정"
```

스크립트의 동작 절차:
1. `git status` 작업 디렉터리 청결 상태 확인.
2. `dotnet test` 전체 테스트 스위트 실행.
3. 버전 번호 자동 동기화 (`1.0.6.0` / `1.0.6`).
4. Portable EXE 빌드 및 MSIX 패키징.
5. 로컬 커밋 및 Git 태그 `v1.0.6` 생성.
6. `origin`(GitHub) 및 `forgejo`(`https://git.chanpaca.net/yunchan/Everything2Everything.git`) 양방향 푸시.
7. Forgejo REST API를 호출하여 태그 기반 정식 릴리즈 생성 및 바이너리 업로드.

---

## 5. Forgejo Actions 워크플로 (`.forgejo/workflows/release.yaml`)

원격 서버 자체 CI/CD를 위한 워크플로를 구성하여, 태그 푸시(`v*`) 시 원격 러너에서도 자동으로 빌드와 검증이 수행되도록 보장한다.
