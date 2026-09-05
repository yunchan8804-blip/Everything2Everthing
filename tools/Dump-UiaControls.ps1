Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

$proc = Get-Process -Name "Everything2Everything" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) {
    Write-Host "프로세스가 없습니다."
    exit 0
}

Write-Host "발견된 프로세스 ID: $($proc.Id)"

$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
$windowEl = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)

if (-not $windowEl) {
    Write-Host "루트 엘리먼트 아래에서 윈도우를 찾을 수 없습니다."
    exit 0
}

Write-Host "윈도우 제목: '$($windowEl.Current.Name)', NativeHandle: $($windowEl.Current.NativeWindowHandle)"

$all = $windowEl.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
Write-Host "총 발견된 UI 컨트롤 수: $($all.Count)"

foreach ($el in $all) {
    $autoId = $el.Current.AutomationId
    $name = $el.Current.Name
    $type = $el.Current.ControlType.ProgrammaticName
    if ($autoId -or $name) {
        Write-Host "Type: $type | Id: '$autoId' | Name: '$name'"
    }
}
