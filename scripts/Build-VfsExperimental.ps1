param(
    [switch]$CleanPublishRoot
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot "src\StalkerModLauncher\StalkerModLauncher.csproj"
$publishRoot = Join-Path $repositoryRoot "publish"
$outputDirectory = Join-Path $publishRoot "vfs-experimental"
$usvfsRoot = Join-Path $repositoryRoot ".external\usvfs"
$x86Host = Join-Path $repositoryRoot "native\StalkerModLauncher.UsvfsX86Host\build32\StalkerModLauncher.UsvfsX86Host.exe"

$runtimeFiles = @{
    "usvfs_x64.dll" = Join-Path $usvfsRoot "lib\usvfs_x64.dll"
    "usvfs_proxy_x64.exe" = Join-Path $usvfsRoot "bin\usvfs_proxy_x64.exe"
    "usvfs_x86.dll" = Join-Path $usvfsRoot "lib\usvfs_x86.dll"
    "StalkerModLauncher.UsvfsX86Host.exe" = $x86Host
}

foreach ($entry in $runtimeFiles.GetEnumerator()) {
    if (-not (Test-Path -LiteralPath $entry.Value -PathType Leaf)) {
        throw "Missing USVFS dependency '$($entry.Key)': $($entry.Value)"
    }
}

$resolvedRepositoryRoot = [System.IO.Path]::GetFullPath($repositoryRoot)
$resolvedPublishRoot = [System.IO.Path]::GetFullPath($publishRoot)
if (-not $resolvedPublishRoot.StartsWith($resolvedRepositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean publish directory outside the repository: $resolvedPublishRoot"
}

$runningLauncher = Get-Process -Name "StalkerModLauncher" -ErrorAction SilentlyContinue | Where-Object {
    try {
        $_.Path -and [System.IO.Path]::GetFullPath($_.Path).StartsWith($resolvedPublishRoot, [System.StringComparison]::OrdinalIgnoreCase)
    } catch {
        $false
    }
} | Select-Object -First 1

if ($runningLauncher) {
    throw "Close the launcher running from publish before rebuilding it (PID $($runningLauncher.Id))."
}

if ($CleanPublishRoot -and (Test-Path -LiteralPath $publishRoot)) {
    Remove-Item -LiteralPath $publishRoot -Recurse -Force
} elseif (Test-Path -LiteralPath $outputDirectory) {
    Remove-Item -LiteralPath $outputDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=false `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $outputDirectory

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

foreach ($entry in $runtimeFiles.GetEnumerator()) {
    Copy-Item -LiteralPath $entry.Value -Destination (Join-Path $outputDirectory $entry.Key)
}

$unexpected = Get-ChildItem -LiteralPath $outputDirectory -File | Where-Object {
    $_.Extension -in @('.pdb', '.json', '.md')
}
if ($unexpected) {
    throw "Unexpected files in VFS build: $($unexpected.Name -join ', ')"
}

Write-Host "Experimental USVFS build created in $outputDirectory"
Get-ChildItem -LiteralPath $outputDirectory -File | Sort-Object Name | Format-Table Name, Length
