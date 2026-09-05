$exe = "D:\workspace\Everything2Everthing\src\Everything2Everything.App\bin\Debug\net9.0-windows10.0.19041.0\Everything2Everything.exe"
$testFile = "D:\workspace\Everything2Everthing\test_assets\test_icon.png"
Write-Host "Starting: $exe"
$proc = Start-Process -FilePath $exe -ArgumentList "`"$testFile`"" -PassThru
Start-Sleep -Seconds 3
Get-Process -Id $proc.Id | Format-List Id, ProcessName, MainWindowHandle, MainWindowTitle, Responding
Start-Sleep -Seconds 1
Stop-Process -Id $proc.Id -Force
Write-Host "Done"
