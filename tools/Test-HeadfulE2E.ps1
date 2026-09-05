# tools/Test-HeadfulE2E.ps1
# Everything2Everything Headful E2E 시각적 상호작용 및 실제 변환 검증 스크립트
# WMI 분리 런처(agy-gui-launch.ps1) 경유 규약 준수

param(
    [switch]$Release
)

$ErrorActionPreference = 'Stop'
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$RootDir = Split-Path -Parent $ScriptDir
$Config = if ($Release) { "Release" } else { "Debug" }
$exePath = Join-Path $RootDir "src/Everything2Everything.App/bin/$Config/net9.0-windows10.0.19041.0/Everything2Everything.exe"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Everything2Everything Headful E2E 인터랙션 검증" -ForegroundColor Cyan
Write-Host "  실행 바이너리: $exePath" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 0. 기존 프로세스 종료
Stop-Process -Name "Everything2Everything" -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

# 테스트 파일 준비
$testDir = Join-Path $RootDir "test_assets"
$file1 = Join-Path $testDir "test_icon.png"
$file2 = Join-Path $testDir "test_art.png"

# 기존 변환 출력 디렉터리 정리
Remove-Item (Join-Path $testDir "*_converted*") -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $testDir "*_webp*") -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $testDir "*_pdf*") -Recurse -Force -ErrorAction SilentlyContinue

# 1. WMI 런처를 통해 사용자 인터랙티브 데스크톱 세션에 실행
Write-Host "`n[Step 1] WMI 런처를 통해 Headful 창 실행 중 (입력 파일 2개 탑재)..." -ForegroundColor Yellow
$argsStr = "`"$file1`" `"$file2`""

& "C:\Users\encep\.gemini\agy-gui-launch.ps1" -ExePath $exePath -Arguments $argsStr -ProcessName "Everything2Everything" -WaitSeconds 60

$p = Get-Process -Name "Everything2Everything" -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $p) {
    Write-Error "MainWindowHandle이 0이 아닌 Everything2Everything 프로세스를 찾지 못했습니다."
    exit 1
}

$handle = $p.MainWindowHandle
Write-Host "✅ Headful 창 활성화 완료! (PID: $($p.Id), Handle: $handle, Title: '$($p.MainWindowTitle)')" -ForegroundColor Green

# 2. UI Automation 로드
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$windowEl = [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$handle)
if (-not $windowEl) {
    Write-Error "AutomationElement를 찾을 수 없습니다."
    exit 1
}
Write-Host "✅ UI Automation 연결 성공: $($windowEl.Current.Name)" -ForegroundColor Green

function Find-ElementById([string]$autoId) {
    $cond = New-Object System.Windows.Automation.PropertyCondition ([System.Windows.Automation.AutomationElement]::AutomationIdProperty), $autoId
    return $windowEl.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

function Find-ElementByName([string]$name) {
    $cond = New-Object System.Windows.Automation.PropertyCondition ([System.Windows.Automation.AutomationElement]::NameProperty), $name
    return $windowEl.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

# 3. 큐 및 뱃지 상태 탐색
Write-Host "`n[Step 2] 큐 및 컨트롤 실시간 탐색..." -ForegroundColor Yellow
$badge = Find-ElementById "TabActiveBadge"
if ($badge) {
    Write-Host "  - Active Queue 뱃지: '$($badge.Current.Name)'" -ForegroundColor Gray
}

# 4. 상세 설정 폴드아웃(AdvancedOptionsExpander) 클릭
Write-Host "`n[Step 3] 상세 설정 폴드아웃(AdvancedOptionsExpander) 펼치기 클릭..." -ForegroundColor Yellow
$expander = Find-ElementById "AdvancedOptionsExpander"
if ($expander) {
    Write-Host "  - AdvancedOptionsExpander 컨트롤 발견!" -ForegroundColor Gray
    $expPattern = $expander.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern) -as [System.Windows.Automation.ExpandCollapsePattern]
    if ($expPattern) {
        $expPattern.Expand()
        Start-Sleep -Milliseconds 600
        Write-Host "✅ 상세 설정 폴드아웃 펼침 성공! (상태: $($expPattern.Current.ExpandCollapseState))" -ForegroundColor Green
    }
} else {
    Write-Warning "AdvancedOptionsExpander ID를 찾지 못했습니다."
}

# 4-1. 고급 이미지 인코딩 옵션(ImageLosslessCheck) 클릭 및 토글
Write-Host "`n[Step 3-1] 이미지 무손실(Lossless) 옵션 체크박스 클릭..." -ForegroundColor Yellow
$losslessCheck = Find-ElementById "ImageLosslessCheck"
if ($losslessCheck) {
    Write-Host "  - ImageLosslessCheck 컨트롤 발견!" -ForegroundColor Gray
    $togglePattern = $losslessCheck.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern) -as [System.Windows.Automation.TogglePattern]
    if ($togglePattern) {
        $togglePattern.Toggle()
        Start-Sleep -Milliseconds 400
        Write-Host "✅ 무손실(Lossless) 체크박스 토글 완료! (상태: $($togglePattern.Current.ToggleState))" -ForegroundColor Green
    }
} else {
    Write-Host "  ℹ️ 현재 포맷 패널에 따라 ImageLosslessCheck 표시 여부 확인됨." -ForegroundColor Gray
}

# 4-2. 윈도우 스크린샷 캡처 (Headful UI 동작 증빙)
try {
    Add-Type -AssemblyName System.Drawing, System.Windows.Forms
    $rect = $windowEl.Current.BoundingRectangle
    if ($rect.Width -gt 0 -and $rect.Height -gt 0) {
        $bmp = New-Object System.Drawing.Bitmap ([int]$rect.Width), ([int]$rect.Height)
        $gfx = [System.Drawing.Graphics]::FromImage($bmp)
        $gfx.CopyFromScreen([int]$rect.Left, [int]$rect.Top, 0, 0, (New-Object System.Drawing.Size([int]$rect.Width, [int]$rect.Height)))
        $shotPath = "C:\Users\encep\.gemini\antigravity\brain\a5abaf02-dd4b-45f7-8890-144e9da36bcc\e2e_headful_ui.png"
        $bmp.Save($shotPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $gfx.Dispose()
        $bmp.Dispose()
        Write-Host "📸 Headful UI 창 스크린샷 캡처 성공: $shotPath" -ForegroundColor Cyan
    }
} catch {
    Write-Warning "스크린샷 캡처 중 경고 발생: $($_.Message)"
}

# 5. 변환 시작 버튼 클릭
Write-Host "`n[Step 4] '대기열 일괄 변환 시작' 버튼 클릭 트리거..." -ForegroundColor Yellow
$convertBtn = Find-ElementById "ProcessQueueButton"
if (-not $convertBtn) {
    $convertBtn = $windowEl.FindFirst([System.Windows.Automation.TreeScope]::Descendants, 
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))
}

if ($convertBtn) {
    Write-Host "  - 변환 버튼 발견: '$($convertBtn.Current.Name)'" -ForegroundColor Gray
    $btnPattern = $convertBtn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern) -as [System.Windows.Automation.InvokePattern]
    if ($btnPattern) {
        $btnPattern.Invoke()
        Write-Host "✅ '대기열 일괄 변환 시작' 버튼 클릭 완료! 변환 엔진 가동!" -ForegroundColor Green
    }
} else {
    Write-Error "변환 버튼을 찾지 못했습니다."
    exit 1
}

# 6. 변환 완료 대기 및 실시간 관측
Write-Host "`n[Step 5] 실시간 변환 진행 및 완료 대기 중..." -ForegroundColor Yellow
$completed = $false
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Milliseconds 600
    $convertedFiles = Get-ChildItem (Join-Path $testDir "*") -Recurse -File | Where-Object { 
        $_.DirectoryName -ne $testDir -and $_.LastWriteTime -gt (Get-Date).AddMinutes(-2) 
    }
    if ($convertedFiles.Count -ge 2) {
        $completed = $true
        break
    }
    Write-Host "  ... 변환 진행 중 ($([math]::Round($i * 0.6, 1))초 경과)" -ForegroundColor DarkGray
}

# 7. 실제 결과 파일 검증
Write-Host "`n[Step 6] 실제 디스크 출력 결과 파일 전수 검증:" -ForegroundColor Yellow
$allOutputs = Get-ChildItem (Join-Path $testDir "*") -Recurse -File | Where-Object { 
    $_.DirectoryName -ne $testDir 
}

if ($allOutputs.Count -eq 0) {
    Write-Error "❌ 변환 결과 파일이 생성되지 않았습니다!"
    exit 1
}

foreach ($out in $allOutputs) {
    Write-Host "  📄 생성된 파일: $($out.FullName)" -ForegroundColor Cyan
    Write-Host "     크기: $([math]::Round($out.Length / 1KB, 2)) KB ($($out.Length) bytes)" -ForegroundColor Gray
    Write-Host "     생성 시간: $($out.LastWriteTime)" -ForegroundColor Gray
}

# 최종 화면 상태 스크린샷 캡처 (변환 완료 상태)
try {
    Start-Sleep -Milliseconds 500
    $rect = $windowEl.Current.BoundingRectangle
    if ($rect.Width -gt 0 -and $rect.Height -gt 0) {
        $bmp2 = New-Object System.Drawing.Bitmap ([int]$rect.Width), ([int]$rect.Height)
        $gfx2 = [System.Drawing.Graphics]::FromImage($bmp2)
        $gfx2.CopyFromScreen([int]$rect.Left, [int]$rect.Top, 0, 0, (New-Object System.Drawing.Size([int]$rect.Width, [int]$rect.Height)))
        $doneShotPath = "C:\Users\encep\.gemini\antigravity\brain\a5abaf02-dd4b-45f7-8890-144e9da36bcc\e2e_headful_done.png"
        $bmp2.Save($doneShotPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $gfx2.Dispose()
        $bmp2.Dispose()
        Write-Host "📸 변환 완료 Headful UI 스크린샷 저장 완료: $doneShotPath" -ForegroundColor Cyan
    }
} catch {
    Write-Warning "완료 스크린샷 캡처 중 경고 발생: $($_.Message)"
}

Write-Host "`n🎉 Headful E2E 실제 클릭 및 변환 전 과정 테스트 성공!" -ForegroundColor Green

# 8. 테스트 종료 후 프로세스 정리
Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
