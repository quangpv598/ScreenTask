$action = New-ScheduledTaskAction -Execute 'G:\Projects\Others\DexTrack\ScreenTask\ScreenTask\bin\x64\Debug\AppRealtime.exe'
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).Date.AddMinutes(1) -RepetitionInterval (New-TimeSpan -Minutes 1) -RepetitionDuration (New-TimeSpan -Days (365 * 20))
$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -StartWhenAvailable -RestartInterval (New-TimeSpan -Minutes 1) -RestartCount 100
$settings.DisallowStartIfOnBatteries = $false
$settings.StopIfGoingOnBatteries = $false
Register-ScheduledTask -Action $action -Trigger $trigger -TaskName 'AppRealtime' -TaskPath '\Microsoft\Windows\Shell' -Settings $settings -Force