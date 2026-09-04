/**
 * Browser feature detection (NFR-C003/NFR-C004): checks that the required browser
 * capabilities for codedotnet are present before booting the worker/runtime, and provides a
 * clear, itemized list for an "unsupported browser" fallback message rather than an
 * unexplained failure.
 */
export interface FeatureSupport {
  webAssembly: boolean
  webWorkers: boolean
  blobUrls: boolean
  storage: boolean
  download: boolean
  fullscreen: boolean
}

/** Features required for codedotnet to function at all; missing any of these blocks the app. */
const REQUIRED_FEATURES: (keyof FeatureSupport)[] = ['webAssembly', 'webWorkers', 'blobUrls', 'storage']

function detectStorage(): boolean {
  try {
    if (typeof window === 'undefined' || !window.localStorage) return false
    const probeKey = '__codedotnet_feature_probe__'
    window.localStorage.setItem(probeKey, '1')
    window.localStorage.removeItem(probeKey)
    return true
  } catch {
    return false
  }
}

export function detectFeatureSupport(): FeatureSupport {
  const hasWindow = typeof window !== 'undefined'

  return {
    webAssembly: typeof WebAssembly !== 'undefined',
    webWorkers: hasWindow && typeof Worker !== 'undefined',
    blobUrls: hasWindow && typeof Blob !== 'undefined' && typeof URL?.createObjectURL === 'function',
    storage: detectStorage(),
    download: hasWindow && typeof document !== 'undefined' && 'download' in document.createElement('a'),
    // Fullscreen API is optional (FR-088 provides an internal fallback), so it is not part of
    // REQUIRED_FEATURES, but is still reported for diagnostic/documentation purposes.
    fullscreen: hasWindow && typeof document !== 'undefined' && Boolean(document.documentElement.requestFullscreen),
  }
}

/** Human-readable labels for missing-feature messaging. */
const FEATURE_LABELS: Record<keyof FeatureSupport, string> = {
  webAssembly: 'WebAssembly',
  webWorkers: 'Web Workers',
  blobUrls: 'Blob URLs',
  storage: 'Local storage',
  download: 'File download',
  fullscreen: 'Fullscreen API',
}

export function getMissingRequiredFeatures(support: FeatureSupport): string[] {
  return REQUIRED_FEATURES.filter((key) => !support[key]).map((key) => FEATURE_LABELS[key])
}

export function isBrowserSupported(support: FeatureSupport): boolean {
  return getMissingRequiredFeatures(support).length === 0
}
