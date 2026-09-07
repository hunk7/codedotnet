import { describe, it, expect } from 'vitest'
import {
  detectFeatureSupport,
  getMissingRequiredFeatures,
  isBrowserSupported,
} from '../browserSupport'

describe('browserSupport', () => {
  it('reports all required features supported in the jsdom test environment', () => {
    const support = detectFeatureSupport()
    // jsdom provides Worker/Blob/localStorage stand-ins in this project's test setup; WebAssembly
    // is provided by Node.js itself.
    expect(support.webAssembly).toBe(true)
    expect(support.storage).toBe(true)
    expect(isBrowserSupported(support)).toBe(true)
    expect(getMissingRequiredFeatures(support)).toEqual([])
  })

  it('flags missing features as unsupported', () => {
    const support = {
      webAssembly: false,
      webWorkers: true,
      blobUrls: true,
      storage: true,
      download: true,
      fullscreen: true,
    }

    expect(isBrowserSupported(support)).toBe(false)
    expect(getMissingRequiredFeatures(support)).toEqual(['WebAssembly'])
  })
})
