<#
.SYNOPSIS
  Records actual hardware facts (OS, CPU, RAM, GPU, driver, free disk) and the profile that
  would be chosen. Output is a JSON file under .codedotnet/ (git-ignored). Read-only.
.PARAMETER Profile
  Optional forced profile name: low-resource or recommended.
#>
[CmdletBinding()]
param([string]$Profile)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$outDir = Join-Path $repoRoot '.codedotnet'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$os = Get-CimInstance Win32_OperatingSystem
$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
$gpus = @(Get-CimInstance Win32_VideoController | ForEach-Object {
    [ordered]@{ name = $_.Name; driverVersion = $_.DriverVersion; adapterRamBytes = $_.AdapterRAM }
})
$drive = Get-PSDrive -Name ((Get-Location).Drive.Name) -ErrorAction SilentlyContinue
$freeDisk = $null
if ($null -ne $drive) { $freeDisk = [int64]$drive.Free }

$totalRam = [int64]$os.TotalVisibleMemorySize * 1024
$cores = [int]$env:NUMBER_OF_PROCESSORS

# Mirrors CodeDotNet.Ai ProfileResolver: >= 24 GB RAM and >= 8 logical cores => recommended.
$chosen = 'low-resource'
if ($totalRam -ge 24GB -and $cores -ge 8) { $chosen = 'recommended' }
$forced = $false
if ($Profile -eq 'low-resource' -or $Profile -eq 'recommended') { $chosen = $Profile; $forced = $true }

$result = [ordered]@{
    capturedAt = (Get-Date).ToUniversalTime().ToString('o')
    os = "$($os.Caption) $($os.Version) build $($os.BuildNumber)"
    cpu = $cpu.Name
    logicalCores = $cores
    totalRamBytes = $totalRam
    freeDiskBytes = $freeDisk
    gpus = $gpus
    profile = $chosen
    profileForcedByUser = $forced
    note = 'Facts only. No performance claims are derived from this file.'
}

$path = Join-Path $outDir 'hardware-probe.json'
$result | ConvertTo-Json -Depth 5 | Set-Content -Path $path -Encoding UTF8
Write-Host "Wrote $path"
Write-Host ("Profile: {0}{1}" -f $chosen, $(if ($forced) { ' (forced)' } else { '' }))
$result | ConvertTo-Json -Depth 5
