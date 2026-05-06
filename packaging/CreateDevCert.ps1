#Requires -Version 5.1
# 자체 서명 코드 사이닝 인증서 생성 + PFX export.
# Subject가 Package.appxmanifest 의 <Identity Publisher="..."> 와 정확히 일치해야 한다.

[CmdletBinding()]
param(
    [string]$Subject = 'CN=EverythingToJpegDev',
    [string]$OutputPfx = (Join-Path $PSScriptRoot 'EverythingToJpeg-DevCert.pfx'),
    [securestring]$Password
)

$ErrorActionPreference = 'Stop'

if (-not $Password) {
    Write-Host '인증서 PFX 보호용 비밀번호를 입력하세요. (5대 PC에 설치할 때 필요합니다)'
    $Password = Read-Host -AsSecureString -Prompt '비밀번호'
}

Write-Host "Creating self-signed code-signing certificate: $Subject"
$cert = New-SelfSignedCertificate `
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
    -FriendlyName 'EverythingToJpeg Dev'

Write-Host "Thumbprint: $($cert.Thumbprint)"
Write-Host "Exporting PFX: $OutputPfx"
Export-PfxCertificate -Cert $cert -FilePath $OutputPfx -Password $Password | Out-Null

Write-Host ''
Write-Host '--- 다음 단계 ---'
Write-Host "  1. 이 PFX 파일을 5대 PC 각각에 복사"
Write-Host "  2. 각 PC에서 관리자 PowerShell로:"
Write-Host '     Import-PfxCertificate -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" -FilePath <경로>.pfx -Password (Read-Host -AsSecureString)'
Write-Host '  3. MSIX 빌드 시 BuildMsix.ps1 -CertThumbprint ' + $cert.Thumbprint
Write-Host ''
Write-Host "PFX는 비밀이므로 절대 git에 커밋하지 마세요. (.gitignore에 *.pfx 추가됨)"
