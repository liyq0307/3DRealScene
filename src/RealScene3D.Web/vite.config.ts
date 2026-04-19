import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'
import mars3d from 'vite-plugin-mars3d'

export default defineConfig({
  plugins: [
    vue(),
    mars3d()
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
          // 将Mars3D/Cesium相关代码拆分到单独的chunk
          if (id.includes('mars3d') || id.includes('cesium') || id.includes('Cesium')) {
            return 'mars3d-vendor'
          }
          // 将Three.js相关代码拆分
          if (id.includes('three') || id.includes('3d-tiles-renderer')) {
            return 'three-vendor'
          }
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
    chunkSizeWarningLimit: 5000 // Mars3D+Cesium体积较大，提高警告限制
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
