Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

$proc = Get-Process -Name "Everything2Everything" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) {
    Write-Host "프로세스를 먼저 실행합니다..."
    $file1 = "d:\workspace\Everything2Everthing\test_assets\test_icon.png"
    $proc = Start-Process "d:\workspace\Everything2Everthing\src\Everything2Everything.App\bin\Debug\net9.0-windows10.0.19041.0\Everything2Everything.exe" -ArgumentList "`"$file1`"" -PassThru
    Start-Sleep -Seconds 3
}

$p = Get-Process -Id $proc.Id
Write-Host "Process Id: $($p.Id), Handle: $($p.MainWindowHandle)"

$windowEl = [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]$p.MainWindowHandle)
$all = $windowEl.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)

Write-Host "총 UIA 컨트롤 수: $($all.Count)"
foreach ($el in $all) {
    $id = $el.Current.AutomationId
    $name = $el.Current.Name
    $type = $el.Current.ControlType.ProgrammaticName
    if ($id -or $name) {
        Write-Host "[$type] Id='$id' Name='$name'"
    }
}
