[CmdletBinding()]
param(
    [ValidateSet("x64", "x86", "arm64")]
    [string[]] $Architecture = @("x64"),

    [ValidatePattern("^\d+\.\d+\.\d+$")]
    [string] $Version = "1.0.0",

    [switch] $SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectPath = Join-Path $repositoryRoot "src\TaskbarLyriz.App\TaskbarLyriz.App.csproj"
$solutionPath = Join-Path $repositoryRoot "TaskbarLyriz.sln"
$installerScript = Join-Path $repositoryRoot "installer\TaskbarLyriz.iss"
$artifactsRoot = Join-Path $repositoryRoot "artifacts"
$publishRoot = Join-Path $artifactsRoot "publish"
$installerRoot = Join-Path $artifactsRoot "installers"

function Resolve-InnoCompiler {
    $command = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($null -ne $command)
    {
        return $command.Source
    }

    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
    )
    foreach ($candidate in $candidates)
    {
        if (Test-Path -LiteralPath $candidate -PathType Leaf)
        {
            return $candidate
        }
    }

    throw "Inno Setup 6 was not found. Install JRSoftware.InnoSetup with WinGet."
}

function Reset-ArtifactDirectory([string] $Path)
{
    $resolvedArtifactsRoot = [System.IO.Path]::GetFullPath($artifactsRoot)
    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    $requiredPrefix = $resolvedArtifactsRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolvedPath.StartsWith($requiredPrefix, [System.StringComparison]::OrdinalIgnoreCase))
    {
        throw "Refusing to reset an output directory outside the artifacts folder: $resolvedPath"
    }

    if (Test-Path -LiteralPath $resolvedPath)
    {
        Remove-Item -LiteralPath $resolvedPath -Recurse -Force
    }

    [void](New-Item -ItemType Directory -Path $resolvedPath -Force)
}

if (-not $SkipTests)
{
    & dotnet test $solutionPath -c Release -p:Platform=x64 -m:1 -nr:false
    if ($LASTEXITCODE -ne 0)
    {
        throw "The release test suite failed."
    }
}

$innoCompiler = Resolve-InnoCompiler
[void](New-Item -ItemType Directory -Path $publishRoot -Force)
[void](New-Item -ItemType Directory -Path $installerRoot -Force)

$platforms = @{
    "x64" = "x64"
    "x86" = "x86"
    "arm64" = "ARM64"
}

foreach ($targetArchitecture in $Architecture)
{
    $runtimeIdentifier = "win-$targetArchitecture"
    $publishDirectory = Join-Path $publishRoot $runtimeIdentifier
    Reset-ArtifactDirectory $publishDirectory

    & dotnet restore $projectPath `
        -r $runtimeIdentifier `
        -p:Platform=$($platforms[$targetArchitecture]) `
        -p:WindowsPackageType=None `
        -p:WindowsAppSDKSelfContained=true `
        -p:EnableWinAppRunSupport=false `
        -m:1 `
        -nr:false
    if ($LASTEXITCODE -ne 0)
    {
        throw "Restore failed for $targetArchitecture."
    }

    & dotnet publish $projectPath `
        -c Release `
        -r $runtimeIdentifier `
        --self-contained true `
        -p:Platform=$($platforms[$targetArchitecture]) `
        -p:WindowsPackageType=None `
        -p:WindowsAppSDKSelfContained=true `
        -p:EnableWinAppRunSupport=false `
        -p:PublishSingleFile=false `
        -p:PublishTrimmed=false `
        -p:PublishReadyToRun=false `
        -p:DebugSymbols=false `
        -p:DebugType=None `
        -p:PublishDir=$publishDirectory `
        -m:1 `
        -nr:false
    if ($LASTEXITCODE -ne 0)
    {
        throw "Publishing failed for $targetArchitecture."
    }

    $publishedExecutable = Join-Path $publishDirectory "TaskbarLyriz.App.exe"
    if (-not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf))
    {
        throw "The $targetArchitecture publish did not produce TaskbarLyriz.App.exe."
    }

    & $innoCompiler `
        "/Q" `
        "/DArchitecture=$targetArchitecture" `
        "/DSourceDir=$publishDirectory" `
        "/DAppVersion=$Version" `
        "/O$installerRoot" `
        $installerScript
    if ($LASTEXITCODE -ne 0)
    {
        throw "Installer compilation failed for $targetArchitecture."
    }
}

$installers = @(Get-ChildItem -LiteralPath $installerRoot -Filter "TaskbarLyriz-Setup-*-$Version.exe" -File |
    Sort-Object Name
)
if ($installers.Count -ne $Architecture.Count)
{
    throw "Expected $($Architecture.Count) installer(s), but found $($installers.Count)."
}

$checksumPath = Join-Path $installerRoot "SHA256SUMS.txt"
$checksums = foreach ($installer in $installers)
{
    $hash = (Get-FileHash -LiteralPath $installer.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($installer.Name)"
}
Set-Content -LiteralPath $checksumPath -Value $checksums -Encoding UTF8

$installers | Select-Object Name, Length, FullName
Write-Host "SHA-256 checksums: $checksumPath"
