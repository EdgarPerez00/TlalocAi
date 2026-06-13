import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const devProxyTarget = env.VITE_DEV_PROXY_TARGET || 'http://127.0.0.1:5100'

  return {
    plugins: [react()],
    server: {
      port: 5173,
      proxy: {
        '/api': {
          target: devProxyTarget,
          changeOrigin: true,
          secure: false,
        },
      },
    },
    preview: {
      port: 4173,
    },
  }
})
