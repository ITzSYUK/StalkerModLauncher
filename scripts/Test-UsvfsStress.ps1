param(
    [string]$RuntimeRoot,
    [ValidateRange(1, 100)]
    [int]$Iterations = 50
)

$ErrorActionPreference = "Stop"
$runtimeTest = Join-Path $PSScriptRoot "Test-UsvfsRuntime.ps1"
& $runtimeTest -RuntimeRoot $RuntimeRoot -Iterations $Iterations
