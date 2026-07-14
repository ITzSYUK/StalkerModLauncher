param(
    [string]$Version = "1.2.2"
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot "src\StalkerModLauncher\StalkerModLauncher.csproj"
$releaseRoot = Join-Path $repositoryRoot "publish\release\v$Version"
$stagingRoot = Join-Path $repositoryRoot "publish\release\.staging-v$Version"
$usvfsRoot = Join-Path $repositoryRoot ".external\usvfs"
$x86Host = Join-Path $repositoryRoot "native\StalkerModLauncher.UsvfsX86Host\build32\StalkerModLauncher.UsvfsX86Host.exe"

$runtimeFiles = @{
    "usvfs_x64.dll" = Join-Path $usvfsRoot "lib\usvfs_x64.dll"
    "usvfs_proxy_x64.exe" = Join-Path $usvfsRoot "bin\usvfs_proxy_x64.exe"
    "usvfs_x86.dll" = Join-Path $usvfsRoot "lib\usvfs_x86.dll"
    "usvfs_proxy_x86.exe" = Join-Path $usvfsRoot "bin\usvfs_proxy_x86.exe"
    "StalkerModLauncher.UsvfsX86Host.exe" = $x86Host
}

foreach ($entry in $runtimeFiles.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath $entry.Value -PathType Leaf)) {
        throw "Missing release dependency '$($entry.Key)': $($entry.Value)"
    }
}

foreach ($path in @($releaseRoot, $stagingRoot)) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

New-Item -ItemType Directory -Path $releaseRoot, $stagingRoot | Out-Null

function Publish-Package {
    param(
        [string]$PackageName,
        [bool]$SelfContained,
        [string]$ExecutableName
    )

    $publishDirectory = Join-Path $stagingRoot "$PackageName-publish"
    $packageDirectory = Join-Path $releaseRoot $PackageName
    New-Item -ItemType Directory -Path $publishDirectory, $packageDirectory | Out-Null

    dotnet publish $project `
        -c Release `
        -r win-x64 `
        --self-contained $SelfContained `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=$SelfContained `
        -p:PublishTrimmed=false `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $publishDirectory

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $PackageName."
    }

    Copy-Item -LiteralPath (Join-Path $publishDirectory "StalkerModLauncher.exe") -Destination (Join-Path $packageDirectory $ExecutableName)
    foreach ($entry in $runtimeFiles.GetEnumerator()) {
        Copy-Item -LiteralPath $entry.Value -Destination (Join-Path $packageDirectory $entry.Key)
    }

    Copy-Item -LiteralPath (Join-Path $repositoryRoot "LICENSE.md") -Destination (Join-Path $packageDirectory "LICENSE.txt")
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "THIRD_PARTY_NOTICES.md") -Destination (Join-Path $packageDirectory "THIRD-PARTY-NOTICES.txt")

    $unexpected = Get-ChildItem -LiteralPath $packageDirectory -File | Where-Object { $_.Extension -in @('.pdb', '.json', '.md') }
    if ($unexpected) {
        throw "Unexpected debug/runtime files in ${PackageName}: $($unexpected.Name -join ', ')"
    }

    $archive = Join-Path $releaseRoot "$PackageName.zip"
    Compress-Archive -Path (Join-Path $packageDirectory '*') -DestinationPath $archive -CompressionLevel Optimal
}

Publish-Package `
    -PackageName "StalkerModLauncher-v$Version-win-x64" `
    -SelfContained $false `
    -ExecutableName "StalkerModLauncher.exe"

Publish-Package `
    -PackageName "StalkerModLauncher-v$Version-win-x64-standalone" `
    -SelfContained $true `
    -ExecutableName "StalkerModLauncher-Standalone.exe"

Remove-Item -LiteralPath $stagingRoot -Recurse -Force
Write-Host "Release packages created in $releaseRoot"
