#Requires -Version 5.1
#Requires -RunAsAdministrator
# 5대 PC에서 MSIX 사이드로드 설치 — 1회 셋업 스크립트.
# 사용법:
#   PowerShell (관리자) > .\Install-Everything2Everything.ps1 -PfxPath .\Everything2Everything-DevCert.pfx -MsixPath .\Everything2Everything.msix

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$PfxPath,
    [Parameter(Mandatory)] [string]$MsixPath,
    [securestring]$PfxPassword,
    [string]$Password = 'Everything2EverythingDev'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $PfxPath)) { throw "PFX 파일을 찾을 수 없습니다: $PfxPath" }
if (-not (Test-Path $MsixPath)) { throw "MSIX 파일을 찾을 수 없습니다: $MsixPath" }

if (-not $PfxPassword) {
    $PfxPassword = ConvertTo-SecureString -String $Password -AsPlainText -Force
}

Write-Host '[1/3] 인증서를 LocalMachine\TrustedPeople에 임포트…'
$importResult = Import-PfxCertificate `
    -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' `
    -FilePath $PfxPath `
    -Password $PfxPassword
Write-Host "      Thumbprint: $($importResult.Thumbprint)"

Write-Host '[2/3] 인증서를 LocalMachine\Root에도 임포트 (체인 신뢰)…'
Import-PfxCertificate `
    -CertStoreLocation 'Cert:\LocalMachine\Root' `
    -FilePath $PfxPath `
    -Password $PfxPassword | Out-Null

Write-Host '[3/3] MSIX 패키지 설치…'
Add-AppxPackage -Path $MsixPath -ForceApplicationShutdown

Write-Host ''
Write-Host '✅ 설치 완료. Win11 메인 우클릭 메뉴에 "Everything2Everything으로 변환" 카스케이드가 보일 겁니다.'
Write-Host '   서브메뉴: JPEG · PNG · WebP · PDF · TXT/DOCX(OCR) · AVIF · GIF · TIFF · BMP · 변환…'
Write-Host '   (탐색기 재시작이 필요할 수 있음: 작업 관리자 → "Windows 탐색기" 다시 시작)'
Write-Host ''
Write-Host '⚠️ 중복 방지: Portable EXE 카스케이드를 이미 등록했다면 unregister 권장 — 메인 메뉴와 추가 옵션 메뉴에 모두 노출되는 것을 막음:'
Write-Host '       <publish 폴더>\Everything2Everything.exe unregister'
