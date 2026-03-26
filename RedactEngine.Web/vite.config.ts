import { defineConfig, type Plugin } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'

function aspireRefreshPreamble(): Plugin {
  return {
    name: 'aspire-refresh-preamble',
    apply: 'serve',
    transformIndexHtml() {
      if (!process.env.ASPIRE) return
      return [{
        tag: 'script',
        attrs: { type: 'module' },
        children: [
          'import { injectIntoGlobalHook } from "/@react-refresh";',
          'injectIntoGlobalHook(window);',
          'window.$RefreshReg$ = () => {};',
          'window.$RefreshSig$ = () => (type) => type;',
        ].join('\n'),
        injectTo: 'head-prepend',
      }]
    },
  }
}

export default defineConfig({
  plugins: [react(), tailwindcss(), aspireRefreshPreamble()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 5173
  },
})
