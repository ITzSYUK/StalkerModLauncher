param(
    [string]$SourceRoot
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($SourceRoot)) {
    $SourceRoot = Join-Path $repositoryRoot ".external\usvfs"
}

$manifestPath = Join-Path $PSScriptRoot "UsvfsRuntimeManifest.psd1"
$integrityScript = Join-Path $PSScriptRoot "ReleaseIntegrity.ps1"
$patchPath = Join-Path $repositoryRoot "scripts\patches\usvfs-msvc-pch.patch"

. $integrityScript
$manifest = Import-UsvfsRuntimeManifest -Path $manifestPath

$resolvedSourceRoot = [System.IO.Path]::GetFullPath($SourceRoot)
$revisionResult = Invoke-RepositoryGit -SourceRoot $resolvedSourceRoot -CommandArguments @("rev-parse", "HEAD")
$revision = ($revisionResult.Output -join "").Trim()
if ($revisionResult.ExitCode -ne 0 -or $revision -ne $manifest.SourceRevision) {
    throw "USVFS source revision must be $($manifest.SourceRevision), found '$revision'."
}

$reverseCheck = Invoke-RepositoryGit `
    -SourceRoot $resolvedSourceRoot `
    -CommandArguments @("apply", "--reverse", "--check", $patchPath)
if ($reverseCheck.ExitCode -ne 0) {
    $forwardCheck = Invoke-RepositoryGit `
        -SourceRoot $resolvedSourceRoot `
        -CommandArguments @("apply", "--check", $patchPath)
    if ($forwardCheck.ExitCode -ne 0) {
        throw "USVFS source has unrelated changes or the tracked patch cannot be applied."
    }

    $applyResult = Invoke-RepositoryGit `
        -SourceRoot $resolvedSourceRoot `
        -CommandArguments @("apply", $patchPath)
    if ($applyResult.ExitCode -ne 0) {
        throw "Failed to apply the tracked USVFS source patch."
    }
}

Test-UsvfsSourceProvenance `
    -SourceRoot $resolvedSourceRoot `
    -Manifest $manifest `
    -PatchPath $patchPath

Write-Host "USVFS source is prepared reproducibly."
