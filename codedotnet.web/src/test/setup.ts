import '@testing-library/jest-dom/vitest'

// jsdom does not implement queryCommandSupported/matchMedia, which Monaco's clipboard and
// theme-detection code paths probe at module-load time.
if (!document.queryCommandSupported) {
  document.queryCommandSupported = () => false
}

if (!window.matchMedia) {
  window.matchMedia = (query: string) =>
    ({
      matches: false,
      media: query,
      onchange: null,
      addListener: () => {},
      removeListener: () => {},
      addEventListener: () => {},
      removeEventListener: () => {},
      dispatchEvent: () => false,
    }) as unknown as MediaQueryList
}

// jsdom does not implement the Worker constructor. Tests that don't explicitly mock a Worker
// (e.g. App.test.tsx's non-worker-lifecycle assertions) still rely on feature detection
// (see src/browserSupport.ts) reporting Web Workers as supported, matching real browsers.
if (typeof window !== 'undefined' && typeof window.Worker === 'undefined') {
  class StubWorker {
    onmessage: ((event: MessageEvent) => void) | null = null
    onerror: ((event: ErrorEvent) => void) | null = null
    addEventListener() {}
    removeEventListener() {}
    postMessage() {}
    terminate() {}
  }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  window.Worker = StubWorker as any
}
