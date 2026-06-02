#Requires -Version 5.1
#Requires -RunAsAdministrator
# MSIX 사이드로드 설치 — 1회 셋업 스크립트.
# 흐름: 인증서 신뢰 → MSIX 설치 → (대화형) 선택 외부 도구 + 바탕화면 바로가기.
# 사용법:
#   PowerShell (관리자) > .\Install-Everything2Everything.ps1 -PfxPath .\Everything2Everything-DevCert.pfx -MsixPath .\dist\Everything2Everything-x64.msix
#   무인 설치(프롬프트 생략):  ... -NoPrompt

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$PfxPath,
    [Parameter(Mandatory)] [string]$MsixPath,
    [securestring]$PfxPassword,
    [string]$Password = 'Everything2EverythingDev',
    [switch]$NoPrompt
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $PfxPath)) { throw "PFX 파일을 찾을 수 없습니다: $PfxPath" }
if (-not (Test-Path $MsixPath)) { throw "MSIX 파일을 찾을 수 없습니다: $MsixPath" }

if (-not $PfxPassword) {
    $PfxPassword = ConvertTo-SecureString -String $Password -AsPlainText -Force
}

Write-Host '[1/4] 인증서를 LocalMachine\TrustedPeople에 임포트…'
$importResult = Import-PfxCertificate `
    -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' `
    -FilePath $PfxPath `
    -Password $PfxPassword
Write-Host "      Thumbprint: $($importResult.Thumbprint)"

Write-Host '[2/4] 인증서를 LocalMachine\Root에도 임포트 (체인 신뢰)…'
Import-PfxCertificate `
    -CertStoreLocation 'Cert:\LocalMachine\Root' `
    -FilePath $PfxPath `
    -Password $PfxPassword | Out-Null

Write-Host '[3/4] MSIX 패키지 설치…'
Add-AppxPackage -Path $MsixPath -ForceApplicationShutdown

# ─────────────────────────────────────────────────────────────────────────────
# [4/4] 선택 구성요소 — 외부 변환 도구 + 바탕화면 바로가기 (대화형)
# 외부 도구는 해당 형식 변환에만 필요. 앱 설정(⚙)·Diagnose에서도 나중에 설치 안내를 받을 수 있음.
# ─────────────────────────────────────────────────────────────────────────────
if (-not $NoPrompt) {
    Write-Host ''
    Write-Host '[4/4] 선택 구성요소 설치 ─ 필요한 것만 Y를 입력하세요 (없으면 Enter)'

    $hasWinget = $null -ne (Get-Command winget -ErrorAction SilentlyContinue)
    if (-not $hasWinget) {
        Write-Host '      ⚠️ winget을 찾지 못했습니다(Windows 11 권장). 선택 시 다운로드 페이지를 대신 엽니다.'
    }

    function Confirm-YesNo([string]$prompt) {
        $answer = Read-Host "  $prompt [Y/N]"
        return ($answer -match '^\s*(y|yes|예|ㅛ)\s*$')
    }

    function Install-Tool([string]$name, [string]$wingetId, [string]$url) {
        if ($hasWinget) {
            Write-Host "    → winget으로 $name 설치 중… (수 분 걸릴 수 있습니다)"
            try {
                & winget install --id $wingetId -e --source winget `
                    --accept-package-agreements --accept-source-agreements
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "    ✅ $name 설치/확인 완료."
                } else {
                    Write-Host "    ⚠️ winget 실패(코드 $LASTEXITCODE). 수동 설치: $url"
                }
            } catch {
                Write-Host "    ⚠️ winget 오류($_). 수동 설치: $url"
            }
        } else {
            Write-Host "    다운로드 페이지를 엽니다: $url"
            Start-Process $url
        }
    }

    if (Confirm-YesNo '영상·오디오 변환용 FFmpeg를 설치하시겠습니까?') {
        Install-Tool 'FFmpeg' 'Gyan.FFmpeg' 'https://www.gyan.dev/ffmpeg/builds/'
    }
    if (Confirm-YesNo 'HWP·DOCX → PDF 변환용 LibreOffice를 설치하시겠습니까? (용량 ~350MB)') {
        Install-Tool 'LibreOffice' 'TheDocumentFoundation.LibreOffice' 'https://www.libreoffice.org/download/download/'
    }
    if (Confirm-YesNo 'PDF 고급 압축용 Ghostscript를 설치하시겠습니까?') {
        Install-Tool 'Ghostscript' 'ArtifexSoftware.GhostScript' 'https://www.ghostscript.com/releases/gsdnld.html'
    }

    if (Confirm-YesNo '바탕화면에 바로가기를 만들겠습니까?') {
        try {
            $pkg = Get-AppxPackage -Name 'Everything2Everything.YunChan' | Select-Object -First 1
            if ($pkg) {
                $target = "shell:AppsFolder\$($pkg.PackageFamilyName)!Everything2Everything"
                $desktop = [Environment]::GetFolderPath('CommonDesktopDirectory')
                $lnkPath = Join-Path $desktop 'Everything2Everything.lnk'
                $wshell = New-Object -ComObject WScript.Shell
                $lnk = $wshell.CreateShortcut($lnkPath)
                $lnk.TargetPath = Join-Path $env:WINDIR 'explorer.exe'
                $lnk.Arguments = $target
                $lnk.Description = 'Everything2Everything — 모든 것을 모든 것으로 (양방향 변환)'
                $lnk.Save()
                Write-Host "    ✅ 바탕화면 바로가기 생성: $lnkPath"
            } else {
                Write-Host '    ⚠️ 설치된 패키지를 찾지 못해 바로가기를 만들 수 없습니다.'
            }
        } catch {
            Write-Host "    ⚠️ 바로가기 생성 실패: $_"
        }
    }
}

Write-Host ''
Write-Host '✅ 설치 완료. Win11 메인 우클릭 메뉴에 "Everything2Everything으로 변환" 카스케이드가 보일 겁니다.'
Write-Host '   서브메뉴: 이미지·PDF·문서·HWP·영상·오디오·데이터·벡터 변환 (입력 형식별 추천 출력).'
Write-Host '   (탐색기 재시작이 필요할 수 있음: 작업 관리자 → "Windows 탐색기" 다시 시작)'
Write-Host ''
Write-Host '⚠️ 중복 방지: Portable EXE 카스케이드를 이미 등록했다면 unregister 권장 — 메인 메뉴와 추가 옵션 메뉴 중복 노출 방지:'
Write-Host '       <publish 폴더>\Everything2Everything.exe unregister'
