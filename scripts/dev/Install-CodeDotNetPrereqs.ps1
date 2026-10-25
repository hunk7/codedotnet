<#
.SYNOPSIS
  Verifies the tools CodeDotNet needs locally and prints what is missing. Installs nothing.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$missing = 0

function Test-Tool {
    param([string]$Name, [string]$Command, [string[]]$VersionArgs, [string]$Hint, [bool]$Required = $true)
    $cmd = Get-Command $Command -ErrorAction SilentlyContinue
    if ($null -eq $cmd) {
        $level = 'MISSING'
        if (-not $Required) { $level = 'optional, not found' }
        Write-Host ("[{0}] {1} - {2}" -f $level, $Name, $Hint)
        if ($Required) { $script:missing++ }
        return
    }
    $version = (& $Command @VersionArgs 2>&1 | Select-Object -First 1)
    Write-Host ("[ok] {0}: {1}" -f $Name, $version)
}

Test-Tool -Name '.NET SDK'  -Command 'dotnet' -VersionArgs @('--version') -Hint 'Install .NET SDK 10.'
Test-Tool -Name 'Node.js'   -Command 'node'   -VersionArgs @('--version') -Hint 'Install Node.js 22 or later.'
Test-Tool -Name 'npm'       -Command 'npm'    -VersionArgs @('--version') -Hint 'Comes with Node.js.'
Test-Tool -Name 'Git'       -Command 'git'    -VersionArgs @('--version') -Hint 'Install Git.'
Test-Tool -Name 'Ollama'    -Command 'ollama' -VersionArgs @('--version') -Hint 'Install Ollama for Windows (needed for local models).' -Required $false

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$workerDir = Join-Path $repoRoot 'codedotnet.web\public\dotnet-worker'
if (Test-Path $workerDir) {
    Write-Host '[ok] Worker assets present.'
} else {
    Write-Host '[MISSING] Worker assets. Run: npm run build:worker (in codedotnet.web). The sandbox needs them.'
    $missing++
}

if (-not (Test-Path (Join-Path $repoRoot 'codedotnet-sandbox\node_modules'))) {
    Write-Host '[MISSING] Sandbox dependencies. Run: npm ci (in codedotnet-sandbox).'
    $missing++
}

if ($missing -gt 0) {
    Write-Host ("{0} required item(s) missing. Nothing was installed." -f $missing)
    exit 1
}
Write-Host 'All required prerequisites found.'
exit 0
