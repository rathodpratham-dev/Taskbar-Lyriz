param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $projectRoot "src\TaskbarLyriz.App\TaskbarLyriz.App.csproj"
$projectBinRoot = Join-Path $projectRoot "src\TaskbarLyriz.App\bin"

$existingApp = Get-CimInstance Win32_Process -Filter "Name = 'TaskbarLyriz.App.exe'" |
    Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($projectBinRoot, [StringComparison]::OrdinalIgnoreCase) } |
    Select-Object -First 1
if ($existingApp)
{
    Write-Host "TaskbarLyriz is already running from this project (PID $($existingApp.ProcessId))."
    exit 0
}

$runLock = [Threading.Mutex]::new($false, "Local\TaskbarLyriz.RunDev")
if (-not $runLock.WaitOne(0))
{
    $runLock.Dispose()
    throw "Another TaskbarLyriz development build is already starting. Wait for it to finish, then try again."
}

try
{
    dotnet run `
        --project $appProject `
        --configuration $Configuration `
        -p:Platform=x64 `
        -p:WindowsPackageType=None `
        -p:WindowsAppSDKSelfContained=true `
        -p:EnableWinAppRunSupport=false

    $exitCode = $LASTEXITCODE
}
finally
{
    $runLock.ReleaseMutex()
    $runLock.Dispose()
}

exit $exitCode
