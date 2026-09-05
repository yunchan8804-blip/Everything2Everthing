Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase

$binDir = "D:\workspace\Everything2Everthing\src\Everything2Everything.App\bin\Debug\net9.0-windows10.0.19041.0"
[System.Reflection.Assembly]::LoadFrom((Join-Path $binDir "Everything2Everything.Core.dll")) | Out-Null
[System.Reflection.Assembly]::LoadFrom((Join-Path $binDir "Everything2Everything.dll")) | Out-Null

$testAssets = "D:\workspace\Everything2Everthing\test_assets"
$testFiles = @(Join-Path $testAssets "test_icon.png", Join-Path $testAssets "test_art.png")

$thread = New-Object System.Threading.Thread([System.Threading.ThreadStart]{
    try {
        if ($null -eq [System.Windows.Application]::Current) {
            $app = New-Object System.Windows.Application
        }

        $engine = [Everything2Everything.Core.Everything2EverythingBootstrap]::CreateDefault()
        
        # Simple settings store via dynamic object or reflection
        $settingsType = [System.Type]::GetType("Everything2Everything.Core.ISettingsStore, Everything2Everything.Core")
        # Use existing memory settings store if available
        $servicesField = $engine.GetType().GetProperty("Providers")
        $store = New-Object Everything2Everything.Core.Settings.MemorySettingsStore

        $win = New-Object Everything2Everything.App.Views.MainWindow($engine, $store, [string[]]$testFiles)
        
        $size = New-Object System.Windows.Size(1280, 960)
        $win.Measure($size)
        $win.Arrange((New-Object System.Windows.Rect(0, 0, 1280, 960)))
        $win.UpdateLayout()

        $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap(1280, 960, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
        $rtb.Render($win)

        $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
        $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($rtb))

        $outPath = "C:\Users\encep\.gemini\antigravity\brain\a5abaf02-dd4b-45f7-8890-144e9da36bcc\app_rendered_preview.png"
        $fs = [System.IO.File]::OpenWrite($outPath)
        $encoder.Save($fs)
        $fs.Close()
        Write-Host "RENDER_SUCCESS: $outPath"
    } catch {
        Write-Error $_.Exception.ToString()
    }
})

$thread.SetApartmentState([System.Threading.ApartmentState]::STA)
$thread.Start()
$thread.Join()
