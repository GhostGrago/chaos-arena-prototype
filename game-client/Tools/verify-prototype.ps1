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
    throw "Unity 6000.5.10f1 was not found. Finish the Unity Hub editor installation first."
}

$logDirectory = Join-Path $projectPath "Logs"
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$logPath = Join-Path $logDirectory "prototype-verification.log"

$unityArguments = @(
    "-batchmode",
    "-nographics",
    "-quit",
    "-projectPath", $projectPath,
    "-executeMethod", "ChaosArena.Editor.PrototypeSceneBuilder.Build",
    "-logFile", $logPath
)

$process = Start-Process `
    -FilePath $UnityPath `
    -ArgumentList $unityArguments `
    -WindowStyle Hidden `
    -Wait `
    -PassThru

if ($process.ExitCode -ne 0) {
    Get-Content -Tail 120 $logPath -ErrorAction SilentlyContinue
    throw "Unity prototype verification failed with exit code $($process.ExitCode). See $logPath."
}

Get-Content -Tail 40 $logPath -ErrorAction SilentlyContinue
Write-Output "Prototype import, compile, and scene generation completed successfully."
