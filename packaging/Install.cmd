@echo off
chcp 65001 > nul
title Everything2Everything 1-클릭 설치기

:: 1. 관리자 권한 확인 및 자동 승격 (UAC)
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [안내] 인증서 신뢰 등록 및 MSIX 패키지 설치를 위해 관리자 권한을 요청합니다...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process cmd.exe -ArgumentList '/c cd /d \"\"%~dp0\"\" && \"\"%~f0\"\"' -Verb RunAs"
    exit /b
)

cd /d "%~dp0"

echo ==========================================================
echo   Everything2Everything 1-클릭 자동 설치 프로그램
echo ==========================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$ErrorActionPreference = 'Stop';" ^
    "try {" ^
    "    Write-Host '[1/2] 개발자 인증서 신뢰 저장소(TrustedPeople) 등록 중...' -ForegroundColor Cyan;" ^
    "    $cer = Get-ChildItem -Path . -Filter '*.cer' | Select-Object -First 1;" ^
    "    if ($cer) {" ^
    "        Import-Certificate -FilePath $cer.FullName -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null;" ^
    "        Import-Certificate -FilePath $cer.FullName -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null;" ^
    "        Write-Host '      인증서 신뢰 등록 성공: ' $cer.Name -ForegroundColor Green;" ^
    "    } else {" ^
    "        Write-Host '      [주의] 폴더 내 .cer 인증서가 없습니다. 건너뜁니다.' -ForegroundColor Yellow;" ^
    "    }" ^
    "    Write-Host '[2/2] Everything2Everything MSIX 패키지 설치 중...' -ForegroundColor Cyan;" ^
    "    $msix = Get-ChildItem -Path . -Filter '*.msix' | Select-Object -First 1;" ^
    "    if (-not $msix) { throw 'MSIX 패키지 파일(*.msix)을 찾을 수 없습니다.' }" ^
    "    Add-AppxPackage -Path $msix.FullName -ForceApplicationShutdown;" ^
    "    Write-Host '      패키지 설치 성공: ' $msix.Name -ForegroundColor Green;" ^
    "    Write-Host '';" ^
    "    Write-Host '==========================================================' -ForegroundColor Green;" ^
    "    Write-Host '  Everything2Everything 설치가 성공적으로 완료되었습니다!' -ForegroundColor Green;" ^
    "    Write-Host '  - 파일 우클릭 시 최상위 컨텍스트 메뉴가 활성화됩니다.' -ForegroundColor Gray;" ^
    "    Write-Host '==========================================================' -ForegroundColor Green;" ^
    "} catch {" ^
    "    Write-Host '';" ^
    "    Write-Host '❌ 설치 중 오류가 발생했습니다:' -ForegroundColor Red;" ^
    "    Write-Host $_.Exception.Message -ForegroundColor Red;" ^
    "    Write-Host '';" ^
    "    Write-Host '동일 버전이 이미 설치되어 있는 경우 기존 앱을 삭제 후 다시 시도해 주세요.' -ForegroundColor Yellow;" ^
    "}"

echo.
pause
