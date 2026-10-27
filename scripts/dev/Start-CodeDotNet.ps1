<#
.SYNOPSIS
  Starts the local CodeDotNet stack: Ollama (if installed and not running), the sandbox
  WebSocket server (127.0.0.1:7900), the AI service (127.0.0.1:7901) and optionally the
  frontend dev server. Child process output goes to .codedotnet/logs/. Prints the pairing code.
.PARAMETER NoFrontend
  Do not start the Vite dev server.
.PARAMETER Profile
  Force a resource profile (low-resource or recommended).
.PARAMETER GpuMode
  off, auto (default) or on. CPU is used unless 'on'.
.PARAMETER AllowedOrigin
  Extra browser origin(s) allowed to call the local services, for example the hosted GitHub Pages
  site: -AllowedOrigin https://hunk7.github.io (local dev origins are always kept).
#>
[CmdletBinding()]
param(
    [switch]$NoFrontend,
    [string]$Profile,
    [ValidateSet('off', 'auto', 'on')][string]$GpuMode = 'auto',
    [string[]]$AllowedOrigin = @()
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$logDir = Join-Path $repoRoot '.codedotnet\logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

$pidFile = Join-Path $repoRoot '.codedotnet\pids.txt'
if (Test-Path $pidFile) { Remove-Item $pidFile -Force }

function Test-Port {
    param([int]$Port)
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $iar = $client.BeginConnect('127.0.0.1', $Port, $null, $null)
        $ok = $iar.AsyncWaitHandle.WaitOne(500)
        if ($ok) { $client.EndConnect($iar) }
        return $ok
    } catch { return $false } finally { $client.Close() }
}

function Wait-Port {
    param([int]$Port, [int]$TimeoutSeconds = 60)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-Port -Port $Port) { return $true }
        Start-Sleep -Milliseconds 500
    }
    return $false
}

function Start-Logged {
    param([string]$Name, [string]$File, [string[]]$Arguments, [string]$WorkingDirectory)
    $out = Join-Path $logDir "$Name.out.log"
    $err = Join-Path $logDir "$Name.err.log"
    $p = Start-Process -FilePath $File -ArgumentList $Arguments -WorkingDirectory $WorkingDirectory `
        -RedirectStandardOutput $out -RedirectStandardError $err -WindowStyle Hidden -PassThru
    Add-Content -Path $pidFile -Value ("{0}:{1}" -f $Name, $p.Id)
    return $p
}

# 1. Ollama (optional)
if (Get-Command ollama -ErrorAction SilentlyContinue) {
    if (-not (Test-Port -Port 11434)) {
        Write-Host 'Starting Ollama...'
        $env:OLLAMA_MAX_LOADED_MODELS = '2'
        $env:OLLAMA_NUM_PARALLEL = '1'
        Start-Logged -Name 'ollama' -File 'ollama' -Arguments @('serve') -WorkingDirectory $repoRoot | Out-Null
        if (-not (Wait-Port -Port 11434 -TimeoutSeconds 30)) { Write-Warning 'Ollama did not start; AI answers will be unavailable.' }
    } else {
        Write-Host 'Ollama already running.'
    }
} else {
    Write-Warning 'Ollama not found. The AI service will start but report the provider as unavailable.'
}

# 2. Sandbox
$sandboxDir = Join-Path $repoRoot 'codedotnet-sandbox'
if ($AllowedOrigin.Count -gt 0) {
    $localOrigins = @('http://localhost:5173', 'http://127.0.0.1:5173')
    $env:MCP_ALLOWED_ORIGIN = (($localOrigins + $AllowedOrigin) -join ',')
    for ($i = 0; $i -lt $AllowedOrigin.Count; $i++) { Set-Item -Path "Env:Ai__AllowedOrigins__$i" -Value $AllowedOrigin[$i] }
}
if (-not (Test-Port -Port 7900)) {
    Write-Host 'Starting sandbox WebSocket server on 127.0.0.1:7900...'
    Start-Logged -Name 'sandbox' -File 'node' -Arguments @('src/wsServer.mjs') -WorkingDirectory $sandboxDir | Out-Null
    if (-not (Wait-Port -Port 7900 -TimeoutSeconds 30)) { throw 'Sandbox did not start. See .codedotnet/logs/sandbox.err.log' }
} else {
    Write-Host 'Sandbox already running on 7900.'
}

# 3. AI service
$aiProject = Join-Path $repoRoot 'CodeDotNet.Ai\CodeDotNet.Ai.csproj'
$env:Ai__GpuMode = $GpuMode
if ($Profile) { $env:Ai__Profile = $Profile }
if (-not (Test-Port -Port 7901)) {
    Write-Host 'Starting AI service on 127.0.0.1:7901...'
    Start-Logged -Name 'ai' -File 'dotnet' -Arguments @('run', '--project', $aiProject, '-c', 'Release') -WorkingDirectory $repoRoot | Out-Null
    if (-not (Wait-Port -Port 7901 -TimeoutSeconds 90)) { throw 'AI service did not start. See .codedotnet/logs/ai.err.log' }
} else {
    Write-Host 'AI service already running on 7901 (pairing code is in the console that started it).'
}

# 4. Frontend
if (-not $NoFrontend) {
    $webDir = Join-Path $repoRoot 'codedotnet.web'
    if (-not (Test-Port -Port 5173)) {
        Write-Host 'Starting frontend dev server...'
        Start-Logged -Name 'web' -File 'npm.cmd' -Arguments @('run', 'dev') -WorkingDirectory $webDir | Out-Null
    }
}

$aiLog = Join-Path $logDir 'ai.out.log'
$code = $null
if (Test-Path $aiLog) {
    $match = Select-String -Path $aiLog -Pattern 'Pairing code .*: (\S+)' | Select-Object -Last 1
    if ($match) { $code = $match.Matches[0].Groups[1].Value }
}
Write-Host ''
Write-Host 'CodeDotNet is up.'
Write-Host '  Sandbox : ws://127.0.0.1:7900'
Write-Host '  AI      : http://127.0.0.1:7901/v1'
if (-not $NoFrontend) { Write-Host '  Frontend: http://localhost:5173' }
if ($code) { Write-Host "  Pairing code: $code  (valid 30 minutes; the same code can be reused until then)" }
Write-Host 'Stop with scripts/dev/Stop-CodeDotNet.ps1. Check with scripts/dev/Test-CodeDotNetHealth.ps1.'
