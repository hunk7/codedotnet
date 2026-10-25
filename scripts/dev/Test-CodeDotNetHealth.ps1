<#
.SYNOPSIS
  Checks the local stack. Exit code 0 = all required checks pass, non-zero = failure.
  Required: sandbox (compile_and_run works) and AI service /v1/health reachable.
  Ollama/model gaps are reported as warnings (degraded), not failures.
#>
[CmdletBinding()]
param([switch]$SkipSandboxRun)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$failures = 0

# AI service
try {
    $health = Invoke-RestMethod -Uri 'http://127.0.0.1:7901/v1/health' -TimeoutSec 10
    Write-Host ("[ok] AI service reachable, status: {0}" -f $health.status)
    foreach ($c in $health.checks) {
        if ($c.status -ne 'ok') { Write-Warning ("  {0}: {1} {2}" -f $c.name, $c.status, $c.detail) }
    }
} catch {
    Write-Host "[FAIL] AI service not reachable on 127.0.0.1:7901: $($_.Exception.Message)"
    $failures++
}

# Sandbox
if ($SkipSandboxRun) {
    Write-Host '[skip] Sandbox compile_and_run probe.'
} else {
    & node (Join-Path $PSScriptRoot 'probe-mcp.mjs')
    if ($LASTEXITCODE -ne 0) { Write-Host '[FAIL] Sandbox probe failed.'; $failures++ } else { Write-Host '[ok] Sandbox probe passed.' }
}

if ($failures -gt 0) { exit 1 }
exit 0
