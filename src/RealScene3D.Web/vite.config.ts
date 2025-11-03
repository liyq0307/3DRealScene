import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'
import cesium from 'vite-plugin-cesium'

export default defineConfig({
  plugins: [
    vue(),
    cesium()
  ],
  resolve: {
    alias: {
      '@': resolve(__dirname, 'src')
    }
  },
  build: {
    rollupOptions: {
      output: {
        manualChunks: (id) => {
          // 将Vue相关代码拆分到单独的chunk
          if (id.includes('vue') || id.includes('pinia') || id.includes('@vue')) {
            return 'vue-vendor'
          }
          // 将UI库和工具库拆分
          if (id.includes('element-plus') || id.includes('@element-plus') ||
              id.includes('axios') || id.includes('@/services') ||
              id.includes('@/composables')) {
            return 'ui-vendor'
          }
        }
      }
    },
    chunkSizeWarningLimit: 1000 // 提高chunk大小警告限制
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true
      }
    }
  }
})
