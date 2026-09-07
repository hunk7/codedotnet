#!/usr/bin/env node
/**
 * Cross-platform launcher for scripts/copy-worker-assets.ps1.
 *
 * GitHub Actions' ubuntu-latest runners ship PowerShell 7+ as `pwsh` (no `powershell`
 * binary exists there), while some Windows dev machines only have the built-in
 * `powershell.exe` (Windows PowerShell 5.1) and not `pwsh`. This wrapper tries `pwsh`
 * first and falls back to `powershell` so `npm run build:worker[:release]` works in both
 * environments without requiring every contributor to install PowerShell 7.
 */
const { spawnSync } = require('node:child_process')

const arg = process.argv[2]
const configuration = arg === '--release' ? 'Release' : arg === '--debug' ? 'Debug' : 'Debug'
const scriptPath = require('node:path').join(__dirname, 'copy-worker-assets.ps1')

function tryRun(command) {
  const result = spawnSync(command, ['-File', scriptPath, '-Configuration', configuration], {
    stdio: 'inherit',
    shell: false,
  })
  return result
}

let result = tryRun('pwsh')

if (result.error && result.error.code === 'ENOENT') {
  console.warn('"pwsh" was not found; falling back to "powershell".')
  result = tryRun('powershell')
}

if (result.error) {
  console.error('Failed to locate a PowerShell executable (tried "pwsh" and "powershell").')
  console.error(result.error.message)
  process.exit(1)
}

process.exit(result.status ?? 1)
