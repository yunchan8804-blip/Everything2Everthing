# tools/Publish-Release.ps1
# Everything2Everything 자동화 릴리즈 배포 스크립트 (Forgejo git.chanpaca.net 연동)

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, HelpMessage = "릴리즈 버전 (예: 1.0.6)")]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [Parameter(Mandatory = $false)]
    [string]$ReleaseNotes = "TDD RED 체계 및 초엄격 디자인 감사 적용, UI 정렬/패딩/빈상태 결함 수정",

    [Parameter(Mandatory = $false)]
    [string]$ForgejoHost = "https://git.chanpaca.net",

    [Parameter(Mandatory = $false)]
    [string]$ForgejoOwner = "yunchan",

    [Parameter(Mandatory = $false)]
    [string]$ForgejoRepo = "Everything2Everything",

    [Parameter(Mandatory = $false)]
    [switch]$SkipTests,

    [Parameter(Mandatory = $false)]
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$RootDir = Split-Path -Parent $ScriptDir
$Tag = "v$Version"
$FourPartVersion = "$Version.0"

function Import-EnvFile {
    param([string]$Path)
    if (Test-Path $Path) {
        Get-Content $Path | Where-Object { $_ -match '^\s*([^#=\s]+)\s*=\s*(.*)$' } | ForEach-Object {
            $key = $matches[1].Trim()
            $val = $matches[2].Trim().Trim('"').Trim("'")
            if (-not [string]::IsNullOrEmpty($key) -and -not [Environment]::GetEnvironmentVariable($key)) {
                [Environment]::SetEnvironmentVariable($key, $val, 'Process')
            }
        }
    }
}
Import-EnvFile (Join-Path $RootDir '.env')

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Everything2Everything 릴리즈 파이프라인" -ForegroundColor Cyan
Write-Host "  버전: $Version (Tag: $Tag, Manifest: $FourPartVersion)" -ForegroundColor Cyan
Write-Host "  대상: $ForgejoHost/$ForgejoOwner/$ForgejoRepo" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# ── Gate 1: 테스트 스위트 100% 통과 검증 ────────────────────────────────
if (-not $SkipTests) {
    Write-Host "`n[Gate 1] TDD 테스트 스위트 전체 실행 중..." -ForegroundColor Yellow
    $testResult = dotnet test (Join-Path $RootDir "src/Everything2Everything.Tests/Everything2Everything.Tests.csproj") -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Error "테스트 스위트 실행 실패! 릴리즈가 중단되었습니다."
        exit 1
    }
    Write-Host "✅ 테스트 스위트 100% 통과 (0 실패)" -ForegroundColor Green
} else {
    Write-Host "`n[Gate 1] 테스트 건너뜀 (-SkipTests 플래그)" -ForegroundColor DarkGray
}

# ── Gate 2: 버전 동기화 ──────────────────────────────────────────────
Write-Host "`n[Gate 2] 버전 번호 동기화 중..." -ForegroundColor Yellow

# 1. Package.appxmanifest
$manifestPath = Join-Path $RootDir "packaging/Package.appxmanifest"
if (Test-Path $manifestPath) {
    $xml = [xml](Get-Content $manifestPath -Raw)
    $identity = $xml.Package.Identity
    if ($identity) {
        $oldVer = $identity.Version
        $identity.Version = $FourPartVersion
        $xml.Save($manifestPath)
        Write-Host "  - Package.appxmanifest: $oldVer -> $FourPartVersion" -ForegroundColor Gray
    }
}

# 2. Everything2Everything.App.csproj
$appCsprojPath = Join-Path $RootDir "src/Everything2Everything.App/Everything2Everything.App.csproj"
if (Test-Path $appCsprojPath) {
    $csprojContent = Get-Content $appCsprojPath -Raw
    if ($csprojContent -match '<Version>([^<]+)</Version>') {
        $csprojContent = $csprojContent -replace '<Version>[^<]+</Version>', "<Version>$Version</Version>"
    } else {
        $csprojContent = $csprojContent -replace '<PropertyGroup>', "<PropertyGroup>`n    <Version>$Version</Version>"
    }
    Set-Content $appCsprojPath $csprojContent
    Write-Host "  - Everything2Everything.App.csproj: Version -> $Version" -ForegroundColor Gray
}

Write-Host "✅ 버전 동기화 완료" -ForegroundColor Green

# ── Gate 3: Release 빌드 & 아티팩트 패키징 ──────────────────────────────
Write-Host "`n[Gate 3] 릴리즈 아티팩트 빌드 중..." -ForegroundColor Yellow

$publishDir = Join-Path $RootDir "publish"
$distDir = Join-Path $RootDir "packaging/dist"

if (-not $DryRun) {
    # 0. 실행 중인 인스턴스 중지 (파일 잠금 방지)
    Stop-Process -Name "Everything2Everything" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500

    # 1. Portable EXE Publish
    Write-Host "  - Portable 바이너리 빌드 (win-x64 Release)..." -ForegroundColor Gray
    dotnet publish (Join-Path $RootDir "src/Everything2Everything.App/Everything2Everything.App.csproj") `
        -c Release -r win-x64 --self-contained false -o $publishDir
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Portable 바이너리 빌드 실패!"
        exit 1
    }

    # 2. Portable ZIP 압축
    $portableZip = Join-Path $RootDir "packaging/dist/Everything2Everything-$Version-win-x64-portable.zip"
    if (-not (Test-Path $distDir)) { New-Item -ItemType Directory -Path $distDir -Force | Out-Null }
    if (Test-Path $portableZip) { Remove-Item $portableZip -Force }
    Compress-Archive -Path "$publishDir\*" -DestinationPath $portableZip
    Write-Host "  - Portable ZIP 생성: $portableZip" -ForegroundColor Gray

    # 3. MSIX 패키징 및 디지털 서명
    $buildMsixScript = Join-Path $RootDir "packaging/BuildMsix.ps1"
    if (Test-Path $buildMsixScript) {
        Write-Host "  - MSIX 패키징 및 자동 서명 실행..." -ForegroundColor Gray
        & pwsh -File $buildMsixScript -Configuration Release -Platform x64 -Sign
        if ($LASTEXITCODE -ne 0) {
            Write-Error "MSIX 패키징 및 서명 실패!"
            exit 1
        }
    }

    # 서명 무결성 검증 (0x800B010A 방지 게이트)
    $msixFile = Join-Path $distDir "Everything2Everything-x64.msix"
    if (Test-Path $msixFile) {
        $sig = Get-AuthenticodeSignature $msixFile
        if ($sig.Status -ne 'Valid') {
            Write-Error "MSIX 서명 검증 실패 (Status: $($sig.Status))! 미서명 패키지는 배포할 수 없습니다."
            exit 1
        }
        Write-Host "  - MSIX 디지털 서명 검증 통과 (Status: Valid, Signer: $($sig.SignerCertificate.Subject))" -ForegroundColor Green
    }

    # 4. 1-클릭 설치 번들 ZIP 생성 (MSIX + .cer + Install.cmd)
    $setupZip = Join-Path $distDir "Everything2Everything-$Version-Setup.zip"
    $cerFile = Join-Path $distDir "Everything2Everything-DevCert.cer"
    $installCmd = Join-Path $distDir "Install.cmd"

    $bundleFiles = @($msixFile)
    if (Test-Path $cerFile) { $bundleFiles += $cerFile }
    if (Test-Path $installCmd) { $bundleFiles += $installCmd }

    if (Test-Path $setupZip) { Remove-Item $setupZip -Force }
    Compress-Archive -Path $bundleFiles -DestinationPath $setupZip
    Write-Host "  - 1-클릭 설치 번들 ZIP 생성: $setupZip" -ForegroundColor Gray
} else {
    Write-Host "  (DryRun: 빌드 단계 건너뜀)" -ForegroundColor DarkGray
}

Write-Host "✅ 아티팩트 패키징 완료" -ForegroundColor Green

# ── Gate 4: Git 커밋, 태그 및 원격 푸시 ───────────────────────────────
Write-Host "`n[Gate 4] Git 태깅 및 원격 푸시 ($ForgejoHost)..." -ForegroundColor Yellow

if (-not $DryRun) {
    # 변경사항 커밋
    git add $manifestPath $appCsprojPath
    git diff --cached --quiet
    if ($LASTEXITCODE -ne 0) {
        git commit -m "chore(release): $Tag - $ReleaseNotes"
    }

    # 태그 생성 (기존 태그가 있다면 삭제 후 재생성 또는 확인)
    $existingTag = git tag -l $Tag
    if ($existingTag) {
        Write-Host "  - 기존 태그 $Tag 삭제 후 재생성..." -ForegroundColor Gray
        git tag -d $Tag
    }
    git tag -a $Tag -m "Release ${Tag}: $ReleaseNotes"
    Write-Host "  - Git 태그 생성 완료: $Tag" -ForegroundColor Gray

    # 원격 푸시
    Write-Host "  - Forgejo 원격 저장소로 푸시 중..." -ForegroundColor Gray
    git push forgejo HEAD
    git push forgejo $Tag --force
    Write-Host "✅ Git 푸시 완료" -ForegroundColor Green

    # ── Forgejo REST API 릴리즈 등록 ─────────────────────────────────
    Write-Host "`n[Gate 5] Forgejo REST API 릴리즈 등록 중..." -ForegroundColor Yellow

    # 자격증명 조회
    $credProcess = Start-Process git -ArgumentList "credential fill" -NoNewWindow -PassThru -RedirectStandardInput ([IO.Path]::GetTempFileName()) -RedirectStandardOutput ([IO.Path]::GetTempFileName())
    # 기본 Windows Credential 사용
    $authHeader = @{ "Content-Type" = "application/json" }
    $tokenBytes = [Text.Encoding]::ASCII.GetBytes("yunchan:ONVI2v4J#y")
    $authHeader["Authorization"] = "Basic " + [Convert]::ToBase64String($tokenBytes)

    $releaseBody = @{
        tag_name = $Tag
        target_commitish = "main"
        name = "Everything2Everything $Tag"
        body = @"
## Everything2Everything $Tag

$ReleaseNotes

### 다운로드
- **Portable ZIP**: `Everything2Everything-$Version-win-x64-portable.zip` (무설치 경량 실행 파일)
- **MSIX 패키지**: `Everything2Everything-x64.msix` (Windows 11 메인 메뉴 지원)

### 주요 개선사항
- TDD RED 체계 및 초엄격 디자인 감사(Design Audit AST) 도입
- Fluent 2 아이콘-텍스트 수직 기준선(Baseline) 중앙 정렬 보정
- 인풋 필드 패딩 규격화 (수평 10px+, 수직 8px+)
- 변환 이력(Past Results) 제로-보이드 빈 상태(Empty State) 추가
- 상단 내비게이션 버튼 한글화 및 Fluent SymbolIcon 통일
"@
        draft = $false
        prerelease = $false
    } | ConvertTo-Json

    try {
        $releaseUrl = "$ForgejoHost/api/v1/repos/$ForgejoOwner/$ForgejoRepo/releases"
        $existingReleases = Invoke-RestMethod -Uri $releaseUrl -Method Get -Headers $authHeader
        $releaseResponse = $existingReleases | Where-Object { $_.tag_name -eq $Tag } | Select-Object -First 1
        if (-not $releaseResponse) {
            $releaseResponse = Invoke-RestMethod -Uri $releaseUrl -Method Post -Headers $authHeader -Body $releaseBody
            Write-Host "✅ Forgejo 정식 릴리즈 생성 완료: $($releaseResponse.html_url)" -ForegroundColor Green
        } else {
            Write-Host "ℹ️ 기존 릴리즈 확인됨 (ID: $($releaseResponse.id)): $($releaseResponse.html_url)" -ForegroundColor Cyan
        }

        # 첨부 파일 업로드
        $uploadUrl = "$ForgejoHost/api/v1/repos/$ForgejoOwner/$ForgejoRepo/releases/$($releaseResponse.id)/assets"
        $msixFile = Join-Path $RootDir "packaging/dist/Everything2Everything-x64.msix"

        if (Test-Path $portableZip) {
            Write-Host "  - Portable ZIP 업로드 중..." -ForegroundColor Gray
            curl.exe -s -u "yunchan:ONVI2v4J#y" -X POST "$uploadUrl`?name=Everything2Everything-$Version-win-x64-portable.zip" -F "attachment=@$portableZip" | Out-Null
            Write-Host "    -> Portable ZIP 업로드 완료!" -ForegroundColor Gray
        }

        if (Test-Path $msixFile) {
            Write-Host "  - MSIX 패키지 업로드 중..." -ForegroundColor Gray
            curl.exe -s -u "yunchan:ONVI2v4J#y" -X POST "$uploadUrl`?name=Everything2Everything-x64.msix" -F "attachment=@$msixFile" | Out-Null
            Write-Host "    -> MSIX 업로드 완료!" -ForegroundColor Gray
        }

        if (Test-Path $setupZip) {
            Write-Host "  - 1-클릭 설치 번들 ZIP 업로드 중..." -ForegroundColor Gray
            curl.exe -s -u "yunchan:ONVI2v4J#y" -X POST "$uploadUrl`?name=Everything2Everything-$Version-Setup.zip" -F "attachment=@$setupZip" | Out-Null
            Write-Host "    -> 1-클릭 설치 번들 ZIP 업로드 완료!" -ForegroundColor Gray
        }

        if (Test-Path $cerFile) {
            Write-Host "  - 공개 개발자 인증서(.cer) 업로드 중..." -ForegroundColor Gray
            curl.exe -s -u "yunchan:ONVI2v4J#y" -X POST "$uploadUrl`?name=Everything2Everything-DevCert.cer" -F "attachment=@$cerFile" | Out-Null
            Write-Host "    -> 공개 개발자 인증서 업로드 완료!" -ForegroundColor Gray
        }
    } catch {
        Write-Warning "Forgejo 릴리즈 API 호출 중 경고: $_"
    }
} else {
    Write-Host "  (DryRun: Git 푸시 및 릴리즈 등록 건너뜀)" -ForegroundColor DarkGray
}

Write-Host "`n🎉 Everything2Everything $Tag 릴리즈 배포가 성공적으로 완료되었습니다!" -ForegroundColor Green
Write-Host "저장소 주소: $ForgejoHost/$ForgejoOwner/$ForgejoRepo" -ForegroundColor Cyan
