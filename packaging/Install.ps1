#Requires -Version 5.1
# Everything2Everything 1-클릭 자동 설치 스크립트

$ErrorActionPreference = 'Stop'

# 1. 관리자 권한 확인 및 승격
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[안내] 인증서 등록 및 MSIX 설치를 위해 관리자 권한을 요청합니다..." -ForegroundColor Yellow
    $scriptPath = $MyInvocation.MyCommand.Definition
    Start-Process powershell.exe -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`""
    exit
}

$workingDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $workingDir

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Everything2Everything 1-클릭 자동 설치 프로그램" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

try {
    # 2. 인증서 등록
    Write-Host "[1/3] 개발자 인증서 신뢰 저장소 등록 중..." -ForegroundColor Cyan
    $cer = Get-ChildItem -Path $workingDir -Filter "*.cer" | Select-Object -First 1
    if ($cer) {
        Import-Certificate -FilePath $cer.FullName -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
        Import-Certificate -FilePath $cer.FullName -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
        Write-Host "      인증서 신뢰 등록 성공: $($cer.Name)" -ForegroundColor Green
    } else {
        Write-Host "      [주의] 폴더 내 .cer 인증서가 없습니다. 건너뜁니다." -ForegroundColor Yellow
    }

    # 3. 기존 버전 감지 및 정리 (충돌 방지)
    Write-Host "[2/3] 기존 설치 확인 중..." -ForegroundColor Cyan
    $existing = Get-AppxPackage -Name "Everything2Everything.YunChan" -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Host "      기존 버전 감지 ($($existing.Version)). 업그레이드를 위해 정리 중..." -ForegroundColor Yellow
        Remove-AppxPackage -Package $existing.PackageFullName -ErrorAction SilentlyContinue
    }

    # 4. MSIX 패키지 설치
    Write-Host "[3/3] MSIX 패키지 설치 중..." -ForegroundColor Cyan
    $msix = Get-ChildItem -Path $workingDir -Filter "*.msix" | Select-Object -First 1
    if (-not $msix) {
        throw "MSIX 패키지 파일(*.msix)을 찾을 수 없습니다."
    }
    Add-AppxPackage -Path $msix.FullName -ForceApplicationShutdown
    Write-Host "      패키지 설치 성공: $($msix.Name)" -ForegroundColor Green

    Write-Host ""
    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host "  Everything2Everything 설치가 성공적으로 완료되었습니다!" -ForegroundColor Green
    Write-Host "  - 파일 우클릭 시 최상위 컨텍스트 메뉴가 활성화됩니다." -ForegroundColor Gray
    Write-Host "==========================================================" -ForegroundColor Green
} catch {
    Write-Host ""
    Write-Host "❌ 설치 중 오류가 발생했습니다:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
}

Write-Host "`n아무 키나 누르면 창을 닫습니다..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
