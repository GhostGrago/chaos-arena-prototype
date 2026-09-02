param(
    [string]$UnityPath = ""
)

$ErrorActionPreference = "Stop"
$projectPath = Split-Path -Parent $PSScriptRoot

if (-not $UnityPath) {
    $candidates = @(
        "C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe",
        "$env:LOCALAPPDATA\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe"
    )
    $UnityPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if (-not $UnityPath -or -not (Test-Path -LiteralPath $UnityPath)) {
    throw "Unity 6000.5.10f1 was not found."
}

$logDirectory = Join-Path $projectPath "Logs"
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$logPath = Join-Path $logDirectory "prototype-build.log"

$unityArguments = @(
    "-batchmode",
    "-nographics",
    "-quit",
    "-projectPath", $projectPath,
    "-executeMethod", "ChaosArena.Editor.PrototypeSceneBuilder.BuildWindows",
    "-logFile", $logPath
)

$process = Start-Process `
    -FilePath $UnityPath `
    -ArgumentList $unityArguments `
    -WindowStyle Hidden `
    -Wait `
    -PassThru

if ($process.ExitCode -ne 0) {
    Get-Content -Tail 160 $logPath -ErrorAction SilentlyContinue
    throw "Unity Windows build failed with exit code $($process.ExitCode). See $logPath."
}

Get-Content -Tail 50 $logPath -ErrorAction SilentlyContinue
Write-Output "Windows prototype build completed successfully."
