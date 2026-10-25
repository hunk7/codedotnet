<#
.SYNOPSIS
  Publishes the local AI service (CodeDotNet.Ai) as a framework-dependent build
  into artifacts/ai and writes a SHA256 manifest. No network access is needed beyond NuGet restore.
.PARAMETER Output
  Output directory. Default: artifacts/ai under the repository root.
.PARAMETER Runtime
  Optional runtime identifier, e.g. win-x64. Omit for a portable build.
#>
[CmdletBinding()]
param(
    [string]$Output,
    [string]$Runtime
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
if (-not $Output) { $Output = Join-Path $repoRoot 'artifacts\ai' }

if (Test-Path $Output) { Remove-Item $Output -Recurse -Force }

$publishArgs = @('publish', (Join-Path $repoRoot 'CodeDotNet.Ai\CodeDotNet.Ai.csproj'), '-c', 'Release', '-o', $Output, '--nologo')
if ($Runtime) { $publishArgs += @('-r', $Runtime, '--self-contained', 'false') }
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

$manifest = Get-ChildItem $Output -Recurse -File |
    Where-Object { $_.Name -ne 'SHA256SUMS.txt' } |
    Sort-Object FullName |
    ForEach-Object {
        $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        '{0}  {1}' -f $hash, $_.FullName.Substring($Output.ToString().Length).TrimStart('\')
    }
Set-Content -Path (Join-Path $Output 'SHA256SUMS.txt') -Value $manifest -Encoding ascii

Write-Host "Published to $Output ($($manifest.Count) files)."
Write-Host "Run: dotnet `"$Output\CodeDotNet.Ai.dll`"  (listens on 127.0.0.1:7901 only)"


