// Temporary Level 0 technical spike: validate that the existing browser-wasm
// compiler artifact (published to codedotnet.web/public/dotnet-worker) can boot
// under plain Node.js and that its JSExport methods are invocable.
// This script is NOT part of the shipped product; it exists only to produce
// evidence for docs/research/SPIKE_FINDINGS.md and will be removed afterward.

import { dotnet } from '../codedotnet.web/public/dotnet-worker/wwwroot/_framework/dotnet.js'

function nowMs() {
  return Number(process.hrtime.bigint()) / 1e6
}

const results = { steps: [], ok: true }

function record(step, ok, extra) {
  results.steps.push({ step, ok, ...extra })
  if (!ok) results.ok = false
  console.log(`[spike] ${step}: ${ok ? 'OK' : 'FAIL'}`, extra ?? '')
}

try {
  const t0 = nowMs()
  const { getAssemblyExports, getConfig } = await dotnet.create()
  const t1 = nowMs()
  record('dotnet.create()', true, { ms: t1 - t0 })

  const config = getConfig()
  record('getConfig()', true, { mainAssemblyName: config.mainAssemblyName })

  const t2 = nowMs()
  const assemblyExports = await getAssemblyExports(config.mainAssemblyName)
  const t3 = nowMs()
  record('getAssemblyExports()', true, { ms: t3 - t2 })

  const t4 = nowMs()
  const infoJson = assemblyExports.CodeDotNet.Compiler.RuntimeInfo.GetRuntimeInformation()
  const t5 = nowMs()
  record('RuntimeInfo.GetRuntimeInformation()', true, { ms: t5 - t4, json: infoJson })

  const source = `
using System;
class Program {
  static void Main() {
    Console.WriteLine("Hello from Node-hosted wasm spike!");
  }
}`

  const t6 = nowMs()
  const runJson = assemblyExports.CodeDotNet.Compiler.CompileAndRunHost.CompileAndRun(source, '')
  const t7 = nowMs()
  record('CompileAndRunHost.CompileAndRun() (cold)', true, { ms: t7 - t6, json: runJson })

  const t8 = nowMs()
  const runJson2 = assemblyExports.CodeDotNet.Compiler.CompileAndRunHost.CompileAndRun(source, '')
  const t9 = nowMs()
  record('CompileAndRunHost.CompileAndRun() (warm)', true, { ms: t9 - t8, json: runJson2 })
} catch (err) {
  record('spike', false, { error: err && err.stack ? err.stack : String(err) })
}

console.log('\n=== SPIKE RESULT JSON ===')
console.log(JSON.stringify(results, null, 2))

process.exit(results.ok ? 0 : 1)
