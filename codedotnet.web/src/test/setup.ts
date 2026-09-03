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
