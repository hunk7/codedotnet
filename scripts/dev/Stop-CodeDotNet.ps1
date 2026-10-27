<#
.SYNOPSIS
  Stops the processes started by Start-CodeDotNet.ps1 (listed in .codedotnet/pids.txt).
  Only stops those recorded process IDs and their children.
#>
[CmdletBinding()]
param()

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$pidFile = Join-Path $repoRoot '.codedotnet\pids.txt'
if (-not (Test-Path $pidFile)) {
    Write-Host 'No recorded processes. Nothing to stop.'
    exit 0
}

foreach ($line in Get-Content $pidFile) {
    $parts = $line.Split(':')
    if ($parts.Count -ne 2) { continue }
    $name = $parts[0]; $procId = [int]$parts[1]
    $proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
    if ($null -eq $proc) { Write-Host "$name ($procId) already stopped."; continue }
    & taskkill.exe /PID $procId /T /F | Out-Null
    Write-Host "Stopped $name ($procId)."
}
Remove-Item $pidFile -Force
exit 0
