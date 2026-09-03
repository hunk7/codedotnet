<#
.SYNOPSIS
    Publishes codedotnet.compiler (the .NET 10 browser-wasm worker project) and copies the
    published static assets into codedotnet.web/public/dotnet-worker so the React app can load
    them at both development and GitHub Pages production time.

.PARAMETER Configuration
    Build configuration to publish, either "Debug" or "Release". Defaults to "Debug".
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$compilerProject = Join-Path $repoRoot 'codedotnet.compiler/codedotnet.compiler.csproj'
$targetDir = Join-Path $repoRoot 'codedotnet.web/public/dotnet-worker'

Write-Host "Publishing codedotnet.compiler ($Configuration)..." -ForegroundColor Cyan

if (Test-Path $targetDir) {
    Write-Host "Clearing previous worker assets at $targetDir..." -ForegroundColor Cyan
    Remove-Item -Recurse -Force $targetDir
}

dotnet publish -c $Configuration $compilerProject

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Write-Host "Worker assets published directly to $targetDir via PublishDir." -ForegroundColor Green

if (-not (Test-Path $targetDir)) {
    throw "Expected published assets at $targetDir but the directory was not found. Check the PublishDir setting in codedotnet.compiler.csproj."
}

Write-Host "codedotnet.compiler worker assets are ready at $targetDir" -ForegroundColor Green
