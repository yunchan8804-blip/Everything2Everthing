#Requires -Version 5.1
# MSIX 빌드 파이프라인.
# 1) .NET App publish (framework-dependent)
# 2) C++ Shell DLL 빌드
# 3) 패키지 Layout 디렉토리 구성
# 4) makeappx pack
# 5) (선택) signtool sign

[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Platform = 'x64',
    [switch]$Sign,
    [string]$PfxPath,
    [securestring]$PfxPassword,
    [string]$CertThumbprint,
    [switch]$SelfContained
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$packagingDir = $PSScriptRoot
$layoutDir = Join-Path $packagingDir 'Layout'
$distDir = Join-Path $packagingDir 'dist'
$manifestPath = Join-Path $packagingDir 'Package.appxmanifest'
$assetsSrc = Join-Path $packagingDir 'Assets'

$appProj = Join-Path $repoRoot 'src\Everything2Everything.App\Everything2Everything.App.csproj'
$shellProj = Join-Path $repoRoot 'src\Everything2Everything.Shell\Everything2Everything.Shell.vcxproj'

function Find-WindowsSdkTool {
    param([string]$ToolName)
    $sdkRoots = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin",
        "$env:ProgramFiles\Windows Kits\10\bin"
    ) | Where-Object { Test-Path $_ }
    foreach ($root in $sdkRoots) {
        $versions = Get-ChildItem -Path $root -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
            Sort-Object Name -Descending
        foreach ($v in $versions) {
            $candidate = Join-Path $v.FullName "x64\$ToolName"
            if (Test-Path $candidate) { return [string]$candidate }
        }
    }
    return $null
}

$makeappx = Find-WindowsSdkTool 'makeappx.exe'
if (-not $makeappx) { throw 'makeappx.exe를 찾지 못했습니다. Windows 10 SDK가 필요합니다.' }
Write-Host "makeappx: $makeappx"

if ($Sign) {
    $signtool = Find-WindowsSdkTool 'signtool.exe'
    if (-not $signtool) { throw 'signtool.exe를 찾지 못했습니다.' }
    Write-Host "signtool: $signtool"
}

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) { throw 'vswhere.exe를 찾지 못했습니다.' }
$msbuild = (& $vswhere -latest -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1)
if (-not $msbuild) { throw 'MSBuild를 찾지 못했습니다.' }
Write-Host "msbuild: $msbuild"

# ---- 1) .NET App publish ----
Write-Host ''
Write-Host '[1/5] .NET App publish'
$publishOut = Join-Path $repoRoot ('artifacts\publish\app-' + $Platform.ToLower())
if (Test-Path $publishOut) { Remove-Item $publishOut -Recurse -Force }
$rid = if ($Platform -eq 'ARM64') { 'win-arm64' } else { 'win-x64' }
$selfFlag = if ($SelfContained) { 'true' } else { 'false' }
& dotnet publish $appProj -c $Configuration -r $rid --self-contained $selfFlag -o $publishOut | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish 실패' }

# ---- 2) C++ Shell DLL ----
Write-Host ''
Write-Host '[2/5] C++ Shell DLL 빌드'

# nuget.exe 자동 다운로드 (없으면)
$nuget = Get-Command nuget.exe -ErrorAction SilentlyContinue
if (-not $nuget) {
    $nugetExe = Join-Path $repoRoot 'tools\nuget.exe'
    if (-not (Test-Path $nugetExe)) {
        New-Item -ItemType Directory -Path (Split-Path $nugetExe) -Force | Out-Null
        Write-Host '  nuget.exe 다운로드 중…'
        Invoke-WebRequest -Uri 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe' -OutFile $nugetExe
    }
    $nugetCmd = $nugetExe
} else {
    $nugetCmd = $nuget.Source
}

$packagesDir = Join-Path $repoRoot 'packages'
& $nugetCmd restore (Join-Path $repoRoot 'src\Everything2Everything.Shell\packages.config') -PackagesDirectory $packagesDir | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'NuGet restore 실패' }

& $msbuild $shellProj /p:Configuration=$Configuration /p:Platform=$Platform /m /v:minimal | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Shell build 실패' }
$shellDll = Join-Path $repoRoot ("src\Everything2Everything.Shell\$Platform\$Configuration\Everything2Everything.Shell.dll")
if (-not (Test-Path $shellDll)) { throw "Shell DLL 산출물 없음: $shellDll" }

# ---- 3) Layout 디렉토리 ----
Write-Host ''
Write-Host '[3/5] Layout 디렉토리 구성'
if (Test-Path $layoutDir) { Remove-Item $layoutDir -Recurse -Force }
New-Item -ItemType Directory -Path $layoutDir | Out-Null

Copy-Item -Path (Join-Path $publishOut '*') -Destination $layoutDir -Recurse -Force
Copy-Item -Path $shellDll -Destination $layoutDir -Force

$layoutAssets = Join-Path $layoutDir 'Assets'
New-Item -ItemType Directory -Path $layoutAssets -Force | Out-Null
Copy-Item -Path (Join-Path $assetsSrc '*') -Destination $layoutAssets -Force

Copy-Item -Path $manifestPath -Destination (Join-Path $layoutDir 'AppxManifest.xml') -Force

# ---- 4) makeappx pack ----
Write-Host ''
Write-Host '[4/5] makeappx pack'
if (-not (Test-Path $distDir)) { New-Item -ItemType Directory -Path $distDir | Out-Null }
$msixPath = Join-Path $distDir ("Everything2Everything-$($Platform.ToLower()).msix")
if (Test-Path $msixPath) { Remove-Item $msixPath -Force }
& $makeappx pack /d $layoutDir /p $msixPath /o | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'makeappx pack 실패' }
Write-Host "✅ MSIX 산출: $msixPath"

# ---- 5) (선택) sign ----
if ($Sign) {
    Write-Host ''
    Write-Host '[5/5] signtool sign'
    if ($CertThumbprint) {
        & $signtool sign /fd SHA256 /sha1 $CertThumbprint /tr 'http://timestamp.digicert.com' /td SHA256 $msixPath | Out-Host
    } elseif ($PfxPath) {
        if (-not $PfxPassword) {
            $PfxPassword = Read-Host -AsSecureString -Prompt 'PFX 비밀번호'
        }
        $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($PfxPassword)
        try {
            $plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
            & $signtool sign /fd SHA256 /a /f $PfxPath /p $plain /tr 'http://timestamp.digicert.com' /td SHA256 $msixPath | Out-Host
        } finally {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        }
    } else {
        throw '서명을 하려면 -CertThumbprint 또는 -PfxPath 가 필요합니다.'
    }
    if ($LASTEXITCODE -ne 0) { throw 'signtool sign 실패' }
    Write-Host '✅ 서명 완료'
} else {
    Write-Host ''
    Write-Host '[5/5] 서명 건너뜀 (-Sign 미지정). 사이드로드 시 인증서 필요.'
}

Write-Host ''
Write-Host "최종 산출물: $msixPath"
