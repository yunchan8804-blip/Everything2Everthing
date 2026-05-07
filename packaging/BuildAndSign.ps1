#Requires -Version 5.1
# 한방에 빌드+자체서명: 인증서 자동 생성 → MSIX 빌드 → 서명까지 일관 처리.
# 산출:
#   - packaging/dist/Everything2Everything-x64.msix (서명됨)
#   - packaging/Everything2Everything-DevCert.pfx (5대 PC 신뢰 등록용)

[CmdletBinding()]
param(
    [string]$Subject = 'CN=Everything2EverythingDev',
    [string]$Password = 'Everything2EverythingDev',
    [string]$Configuration = 'Release',
    [string]$Platform = 'x64'
)

$ErrorActionPreference = 'Stop'
$packagingDir = $PSScriptRoot
$pfxPath = Join-Path $packagingDir 'Everything2Everything-DevCert.pfx'
$securePassword = ConvertTo-SecureString -String $Password -AsPlainText -Force

# ---- 1) 인증서 ----
$existing = Get-ChildItem -Path 'Cert:\CurrentUser\My' -ErrorAction SilentlyContinue |
    Where-Object { $_.Subject -eq $Subject } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if (-not $existing) {
    Write-Host "[1/3] 자체 서명 인증서 생성: $Subject"
    $existing = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $Subject `
        -KeyAlgorithm RSA `
        -KeyLength 3072 `
        -Provider 'Microsoft Enhanced RSA and AES Cryptographic Provider' `
        -KeyExportPolicy Exportable `
        -KeyUsage DigitalSignature `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -HashAlgorithm SHA256 `
        -NotAfter (Get-Date).AddYears(5) `
        -FriendlyName 'Everything2Everything Dev'
}
else {
    Write-Host "[1/3] 기존 인증서 재사용 (Thumbprint $($existing.Thumbprint))"
}

if (-not (Test-Path $pfxPath)) {
    Export-PfxCertificate -Cert $existing -FilePath $pfxPath -Password $securePassword | Out-Null
    Write-Host "      PFX 내보냄: $pfxPath"
}

# ---- 2) MSIX 빌드 + 서명 ----
Write-Host "[2/3] MSIX 빌드 + 서명"
& (Join-Path $packagingDir 'BuildMsix.ps1') `
    -Configuration $Configuration `
    -Platform $Platform `
    -Sign `
    -CertThumbprint $existing.Thumbprint

# ---- 3) 안내 ----
Write-Host ''
Write-Host '[3/3] 완료. 다음 단계:'
Write-Host '  1. PFX 파일을 5대 PC 각각에 복사:'
Write-Host "       $pfxPath"
Write-Host '  2. 각 PC에서 관리자 PowerShell:'
Write-Host '       cd packaging'
Write-Host "       .\Install-Everything2Everything.ps1 -PfxPath .\Everything2Everything-DevCert.pfx -MsixPath .\dist\Everything2Everything-x64.msix"
Write-Host '     PFX 비밀번호:' $Password
Write-Host ''
Write-Host '  3. 우클릭 → JPEG로 빠른 변환 / JPEG로 변환… 이 메인 메뉴에 노출됨.'
