// Pure unit tests for security.mjs. These do not need the published wasm artifact.

import { test } from 'node:test'
import assert from 'node:assert/strict'
import {
  DEFAULT_MAX_TIMEOUT_MS,
  clampTimeout,
  isOriginAllowed,
  parseAllowedOrigins,
  parsePositiveInt,
} from '../src/security.mjs'

test('parseAllowedOrigins: unset or blank means default policy', () => {
  assert.equal(parseAllowedOrigins(undefined), null)
  assert.equal(parseAllowedOrigins(''), null)
  assert.equal(parseAllowedOrigins('   '), null)
})

test('parseAllowedOrigins: star and lists', () => {
  assert.equal(parseAllowedOrigins('*'), '*')
  assert.deepEqual(parseAllowedOrigins('https://a.example, https://b.example'), [
    'https://a.example',
    'https://b.example',
  ])
})

test('isOriginAllowed: no Origin header (non-browser client) is allowed', () => {
  assert.equal(isOriginAllowed(undefined, null), true)
  assert.equal(isOriginAllowed('', null), true)
})

test('isOriginAllowed: default policy allows only loopback origins', () => {
  assert.equal(isOriginAllowed('http://localhost:5173', null), true)
  assert.equal(isOriginAllowed('http://127.0.0.1:5173', null), true)
  assert.equal(isOriginAllowed('https://evil.example', null), false)
  assert.equal(isOriginAllowed('https://localhost.evil.example', null), false)
  assert.equal(isOriginAllowed('null', null), false)
  assert.equal(isOriginAllowed('not a url', null), false)
})

test('isOriginAllowed: explicit list is exact match only', () => {
  const allowed = ['https://hunk7.github.io']
  assert.equal(isOriginAllowed('https://hunk7.github.io', allowed), true)
  assert.equal(isOriginAllowed('http://localhost:5173', allowed), false)
})

test('isOriginAllowed: star disables the check', () => {
  assert.equal(isOriginAllowed('https://anything.example', '*'), true)
})

test('clampTimeout: caps, floors and rejects invalid values', () => {
  assert.equal(clampTimeout(2000), 2000)
  assert.equal(clampTimeout(10_000_000), DEFAULT_MAX_TIMEOUT_MS)
  assert.equal(clampTimeout(1500.9), 1500)
  assert.equal(clampTimeout(0), undefined)
  assert.equal(clampTimeout(-5), undefined)
  assert.equal(clampTimeout(Number.NaN), undefined)
  assert.equal(clampTimeout('1000'), undefined)
  assert.equal(clampTimeout(undefined), undefined)
  assert.equal(clampTimeout(5000, 1000), 1000)
})

test('parsePositiveInt: parses, falls back and caps', () => {
  assert.equal(parsePositiveInt('3', 2), 3)
  assert.equal(parsePositiveInt(undefined, 2), 2)
  assert.equal(parsePositiveInt('abc', 2), 2)
  assert.equal(parsePositiveInt('0', 2), 2)
  assert.equal(parsePositiveInt('99', 2, 8), 8)
})
