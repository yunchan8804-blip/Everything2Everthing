Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms

$proc = Get-Process -Name "Everything2Everything" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) {
    Write-Host "Everything2Everything 프로세스가 없습니다."
    exit 1
}

Write-Host "Process ID: $($proc.Id), MainWindowHandle: $($proc.MainWindowHandle), Title: '$($proc.MainWindowTitle)'"

# Find UI Automation element
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
$windowEl = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)

if (-not $windowEl) {
    # Try descendants
    $windowEl = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

if ($windowEl) {
    Write-Host "UI Automation Window Name: '$($windowEl.Current.Name)'"
    Write-Host "BoundingRectangle: $($windowEl.Current.BoundingRectangle)"

    # Look for our specific controls:
    $controls = @("SmartPresetCombo", "QualitySlider", "VideoCrfSlider", "AudioBitrateQuickCombo", "PdfCompressQuickCombo", "AdvancedOptionsExpander", "OutputFormatCombo")
    foreach ($cid in $controls) {
        $cCond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $cid)
        $el = $windowEl.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cCond)
        if ($el) {
            Write-Host "  FOUND Control [$cid]: Type=$($el.Current.ControlType.ProgrammaticName), Name='$($el.Current.Name)', IsOffscreen=$($el.Current.IsOffscreen)"
        } else {
            Write-Host "  MISSING Control [$cid]"
        }
    }

    # Take screenshot of the window
    $rect = $windowEl.Current.BoundingRectangle
    if ($rect.Width -gt 50 -and $rect.Height -gt 50) {
        $bmp = New-Object System.Drawing.Bitmap ([int]$rect.Width), ([int]$rect.Height)
        $gfx = [System.Drawing.Graphics]::FromImage($bmp)
        $gfx.CopyFromScreen([int]$rect.Left, [int]$rect.Top, 0, 0, (New-Object System.Drawing.Size([int]$rect.Width, [int]$rect.Height)))
        $shotPath = "C:\Users\encep\.gemini\antigravity\brain\a5abaf02-dd4b-45f7-8890-144e9da36bcc\window_screenshot.png"
        $bmp.Save($shotPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $gfx.Dispose()
        $bmp.Dispose()
        Write-Host "SCREENSHOT_SAVED: $shotPath"
    }
} else {
    Write-Host "AutomationElement를 찾지 못했습니다."
}
