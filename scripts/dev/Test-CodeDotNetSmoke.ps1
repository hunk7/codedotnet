<#
.SYNOPSIS
  End-to-end smoke test of a RUNNING CodeDotNet stack (sandbox 7900, AI 7901). Pairs using the code
  from .codedotnet/logs/ai.out.log. Exit 0 = all checks passed. Ollama-dependent checks are
  reported as SKIP when Ollama is not running; they are not counted as passes.
#>
[CmdletBinding()]
param([string]$LogFile)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
if (-not $LogFile) { $LogFile = Join-Path $repoRoot '.codedotnet\logs\ai.out.log' }
$b = 'http://127.0.0.1:7901/v1'
$script:fail = 0

function Status([scriptblock]$Call) {
    try { & $Call | Out-Null; return 200 } catch {
        if ($_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }
        return -1
    }
}
function Check([string]$Name, [bool]$Ok, [string]$Detail = '') {
    if ($Ok) { Write-Host "[PASS] $Name $Detail" } else { Write-Host "[FAIL] $Name $Detail"; $script:fail++ }
}

# Negative checks first, with no pairing attempt so the real code is not rotated.
Check 'health is open' ((Status { Invoke-RestMethod "$b/health" }) -eq 200)
Check 'protected route without token -> 401' ((Status { Invoke-RestMethod "$b/kb/documents" }) -eq 401)
Check 'foreign Origin -> 403' ((Status { Invoke-RestMethod "$b/health" -Headers @{ Origin = 'https://evil.example' } }) -eq 401)

$m = Select-String -Path $LogFile -Pattern 'Pairing code .*: (\S+)' | Select-Object -Last 1
if (-not $m) { Write-Host "[FAIL] no pairing code found in $LogFile"; exit 1 }
$code = $m.Matches[0].Groups[1].Value

$pair = Invoke-RestMethod -Method Post "$b/pair" -ContentType application/json -Body (@{ code = $code } | ConvertTo-Json)
Check 'pairing with printed code' ([bool]$pair.token)
$h = @{ Authorization = "Bearer $($pair.token)" }
Check 'code is reusable for its lifetime (re-pair -> 200)' ((Status { Invoke-RestMethod -Method Post "$b/pair" -ContentType application/json -Body (@{ code = $code } | ConvertTo-Json) }) -eq 200)

Check 'status with token' ((Status { Invoke-RestMethod "$b/status" -Headers $h }) -eq 200)
Check 'loopback Origin allowed' ((Status { Invoke-RestMethod "$b/status" -Headers ($h + @{ Origin = 'http://localhost:5173' }) }) -eq 200)
Check 'profile' ((Invoke-RestMethod "$b/profile" -Headers $h).name -in 'recommended', 'low-resource')
Check 'capacity' ([bool](Invoke-RestMethod "$b/capacity" -Headers $h))

$doc = @{ title = 'Span guide'; source = 'smoke'; text = "# Span`nSpan<T> is a stack-only ref struct giving a typed view over contiguous memory." } | ConvertTo-Json
$add = Invoke-RestMethod -Method Post "$b/kb/documents" -Headers $h -ContentType application/json -Body $doc
Check 'kb ingest (lexical fallback allowed)' ([bool]$add.documentId) "chunks=$($add.chunks) embedded=$($add.embedded)"
$listed = @((Invoke-RestMethod "$b/kb/documents" -Headers $h).documents | ForEach-Object { $_.id })
Check 'kb list contains document' ($listed -contains $add.documentId) "listed=$($listed.Count)"

$ollamaUp = (Invoke-RestMethod "$b/health").checks | Where-Object { $_.name -eq 'ollama' -and $_.status -eq 'ok' }
if ($ollamaUp) {
    $r = Invoke-WebRequest -Method Post "$b/ask" -Headers $h -ContentType application/json -UseBasicParsing -Body '{"question":"What is Span<T>?","useKnowledge":true}'
    Check '/ask streams events' ($r.Content -match 'event: (token|done)')
} else {
    Write-Host '[SKIP] /ask, /review, /tests, /agent: Ollama is not running (not counted as passes).'
}

Check 'kb delete' ((Status { Invoke-RestMethod -Method Delete "$b/kb/documents/$($add.documentId)" -Headers $h }) -in 200, 204)

if ($script:fail -gt 0) { Write-Host "$script:fail check(s) failed."; exit 1 }
Write-Host 'All smoke checks passed.'
exit 0
