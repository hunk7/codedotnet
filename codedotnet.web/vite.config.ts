import react, { reactCompilerPreset } from '@vitejs/plugin-react'
import babel from '@rolldown/plugin-babel'
import { defineConfig } from 'vitest/config'
import { execSync } from 'node:child_process'

function safeGitShortHash(): string {
  try {
    return execSync('git rev-parse --short HEAD').toString().trim()
  } catch {
    return 'unknown'
  }
}

// https://vite.dev/config/
export default defineConfig({
  // Repository-subpath support for GitHub Pages project pages (docs §17.2). Set
  // VITE_BASE_PATH=/codedotnet/ when building for https://<user>.github.io/codedotnet/;
  // defaults to '/' for local dev and custom-domain deployments.
  base: process.env.VITE_BASE_PATH ?? '/',
  define: {
    __APP_VERSION__: JSON.stringify(process.env.npm_package_version ?? '0.0.0'),
    __APP_COMMIT__: JSON.stringify(process.env.VITE_COMMIT_SHA ?? safeGitShortHash()),
    __APP_BUILD_DATE__: JSON.stringify(new Date().toISOString()),
  },
  plugins: [react(), babel({ presets: [reactCompilerPreset()] })],
  build: {
    // monaco-editor is inherently large; it is split into its own chunk (see manualChunks below)
    // so it can be cached independently of app code, but it still exceeds the default 500 kB
    // warning threshold. Raise the limit instead of silencing real regressions in app chunks.
    chunkSizeWarningLimit: 2800,
    rollupOptions: {
      output: {
        manualChunks(id: string) {
          if (id.includes('node_modules/monaco-editor')) {
            return 'monaco-editor'
          }
          if (id.includes('node_modules/react') || id.includes('node_modules/react-dom')) {
            return 'react'
          }
        },
      },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
  },
})
