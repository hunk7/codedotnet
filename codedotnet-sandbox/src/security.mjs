// Pure helpers for the sandbox's security-relevant configuration. Kept free of I/O so
// they can be unit tested without the published wasm artifact.

export const DEFAULT_MAX_TIMEOUT_MS = 30_000
export const DEFAULT_MAX_PAYLOAD_BYTES = 1024 * 1024
export const MAX_POOL_SIZE = 8

const LOOPBACK_HOSTS = new Set(['localhost', '127.0.0.1', '[::1]'])

/**
 * Parses MCP_ALLOWED_ORIGIN: unset/empty -> null (default policy), '*' -> '*' (explicit
 * opt-out), otherwise a comma-separated list of exact origins.
 */
export function parseAllowedOrigins(value) {
  if (value === undefined || value === null || value.trim() === '') {
    return null
  }
  const trimmed = value.trim()
  if (trimmed === '*') {
    return '*'
  }
  return trimmed
    .split(',')
    .map((item) => item.trim())
    .filter((item) => item.length > 0)
}

function isLoopbackOrigin(origin) {
  try {
    const url = new URL(origin)
    return (url.protocol === 'http:' || url.protocol === 'https:') && LOOPBACK_HOSTS.has(url.hostname)
  } catch {
    return false
  }
}

/**
 * Default-deny Origin check.
 *  - No Origin header (non-browser client such as a script or MCP host): allowed.
 *  - allowed === '*': allowed (explicit opt-out).
 *  - allowed is a list: only exact matches.
 *  - allowed === null (default): only loopback origins (local dev servers).
 */
export function isOriginAllowed(origin, allowed) {
  if (origin === undefined || origin === null || origin === '') {
    return true
  }
  if (allowed === '*') {
    return true
  }
  if (Array.isArray(allowed)) {
    return allowed.includes(origin)
  }
  return isLoopbackOrigin(origin)
}

/** Returns a positive integer timeout capped at maxMs, or undefined to use the pool default. */
export function clampTimeout(requested, maxMs = DEFAULT_MAX_TIMEOUT_MS) {
  if (typeof requested !== 'number' || !Number.isFinite(requested) || requested <= 0) {
    return undefined
  }
  return Math.min(Math.floor(requested), maxMs)
}

/** Parses a positive integer environment value, falling back when missing or invalid. */
export function parsePositiveInt(value, fallback, max = Number.MAX_SAFE_INTEGER) {
  const parsed = Number(value)
  if (!Number.isInteger(parsed) || parsed <= 0) {
    return fallback
  }
  return Math.min(parsed, max)
}
