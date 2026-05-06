#Requires -Version 5.1
#Requires -RunAsAdministrator
# 5대 PC에서 MSIX 사이드로드 설치 — 1회 셋업 스크립트.
# 사용법:
#   PowerShell (관리자) > .\Install-EverythingToJpeg.ps1 -PfxPath .\EverythingToJpeg-DevCert.pfx -MsixPath .\EverythingToJpeg.msix

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$PfxPath,
    [Parameter(Mandatory)] [string]$MsixPath,
    [securestring]$PfxPassword,
    [string]$Password = 'EverythingToJpegDev'
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
Write-Host '✅ 설치 완료. Win11 메인 우클릭 메뉴에 "JPEG로 빠른 변환" / "JPEG로 변환…" 항목이 보일 겁니다.'
Write-Host '   (탐색기 재시작이 필요할 수 있음: 작업 관리자 → "Windows 탐색기" 다시 시작)'
