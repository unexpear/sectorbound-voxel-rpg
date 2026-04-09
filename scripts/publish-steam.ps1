param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$NoSelfContained,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repoRoot "Sectorbound.sln"
$projectPath = Join-Path $repoRoot "src\Sectorbound.Client\Sectorbound.Client.csproj"
$outputPath = Join-Path $repoRoot "artifacts\steam\$Runtime"
$selfContained = if ($NoSelfContained) { "false" } else { "true" }

Write-Host "Publishing Sectorbound for Steam..."
Write-Host "Configuration: $Configuration"
Write-Host "Runtime: $Runtime"
Write-Host "Self-contained: $selfContained"
Write-Host "Output: $outputPath"

if (-not $SkipTests) {
    dotnet test $solutionPath -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Tests failed. Steam publish aborted."
    }
}

if (Test-Path -LiteralPath $outputPath) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

dotnet publish `
    $projectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained $selfContained `
    -p:PublishTrimmed=false `
    -p:PublishSingleFile=false `
    -o $outputPath `
    --nologo

if ($LASTEXITCODE -ne 0) {
    throw "Publish failed."
}

Write-Host ""
Write-Host "Steam build ready in $outputPath"
Write-Host "Only upload operating systems and features that the build actually supports today."
