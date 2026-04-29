<template>
  <div class="performance-analysis">
    <div class="panel-header">
      <h4>实时性能监控</h4>
    </div>
    
    <div class="metrics-grid">
      <div class="metric-card">
        <div class="metric-icon">🎮</div>
        <div class="metric-info">
          <div class="metric-label">帧率</div>
          <div class="metric-value" :class="fpsClass">{{ performanceMetrics.fps.toFixed(1) }}</div>
          <div class="metric-unit">FPS</div>
        </div>
      </div>
      
      <div class="metric-card">
        <div class="metric-icon">⏱️</div>
        <div class="metric-info">
          <div class="metric-label">帧时间</div>
          <div class="metric-value">{{ performanceMetrics.frameTime.toFixed(2) }}</div>
          <div class="metric-unit">ms</div>
        </div>
      </div>
      
      <div class="metric-card">
        <div class="metric-icon">💾</div>
        <div class="metric-info">
          <div class="metric-label">内存</div>
          <div class="metric-value">{{ formatMemory(performanceMetrics.memory) }}</div>
          <div class="metric-unit">MB</div>
        </div>
      </div>
      
      <div class="metric-card">
        <div class="metric-icon">🔺</div>
        <div class="metric-info">
          <div class="metric-label">三角面</div>
          <div class="metric-value">{{ formatNumber(performanceMetrics.triangles) }}</div>
          <div class="metric-unit">个</div>
        </div>
      </div>
      
      <div class="metric-card">
        <div class="metric-icon">🎨</div>
        <div class="metric-info">
          <div class="metric-label">绘制调用</div>
          <div class="metric-value">{{ performanceMetrics.drawCalls }}</div>
          <div class="metric-unit">次</div>
        </div>
      </div>
      
      <div class="metric-card">
        <div class="metric-icon">📦</div>
        <div class="metric-info">
          <div class="metric-label">对象数量</div>
          <div class="metric-value">{{ performanceMetrics.objects }}</div>
          <div class="metric-unit">个</div>
        </div>
      </div>
    </div>
    
    <div class="chart-section">
      <h5>FPS 历史曲线</h5>
      <div class="chart-container" ref="chartContainer">
        <LineChart
          v-if="chartData.length > 0"
          :data="chartData"
          :width="280"
          :height="120"
          :show-legend="false"
        />
        <div v-else class="no-data">收集数据中...</div>
      </div>
    </div>
    
    <div class="actions">
      <button @click="exportReport" class="btn-primary">导出报告</button>
      <button @click="clearHistory" class="btn-secondary">清除历史</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { useAnalysisStore } from '@/stores/analysis'
import LineChart from '@/components/LineChart.vue'

const props = defineProps<{
  viewerInstance?: any
  sceneObjects?: any[]
}>()

const analysisStore = useAnalysisStore()
const chartContainer = ref<HTMLElement>()

const performanceMetrics = computed(() => analysisStore.performanceMetrics)
const performanceHistory = computed(() => analysisStore.performanceHistory)

const fpsClass = computed(() => {
  const fps = performanceMetrics.value.fps
  if (fps >= 55) return 'good'
  if (fps >= 30) return 'medium'
  return 'bad'
})

const chartData = computed(() => {
  return performanceHistory.value.map(item => ({
    time: new Date(item.time).toLocaleTimeString(),
    value: item.metrics.fps
  }))
})

let monitoringInterval: number | null = null
let lastTime = performance.now()
let frameCount = 0

function startMonitoring() {
  // 从viewer获取真实性能数据
  const updateFromViewer = () => {
    if (props.viewerInstance) {
      const viewer = props.viewerInstance
      
      // Three.js 性能数据
      if (viewer.renderer?.info) {
        const info = viewer.renderer.info
        analysisStore.updatePerformanceMetrics({
          triangles: info.render.triangles,
          drawCalls: info.render.calls,
          objects: viewer.scene?.children?.length || 0,
          memory: info.memory.geometries * 1024 + info.memory.textures * 4096
        })
      }
      
      // Mars3D/Cesium 性能数据
      if (viewer.scene) {
        const scene = viewer.scene
        analysisStore.updatePerformanceMetrics({
          triangles: scene.debugShowMemoryUsage ? (scene as any)._primitiveCollection?.length || 0 : 0,
          objects: scene.primitives?.length || 0
        })
      }
    }
  }
  
  monitoringInterval = window.setInterval(() => {
    const now = performance.now()
    const deltaTime = now - lastTime
    const fps = (frameCount / deltaTime) * 1000
    const frameTime = deltaTime / Math.max(frameCount, 1)
    
    analysisStore.updatePerformanceMetrics({
      fps: fps || 0,
      frameTime: frameTime || 0
    })
    
    updateFromViewer()
    
    frameCount = 0
    lastTime = now
  }, 1000)
  
  function countFrame() {
    frameCount++
    requestAnimationFrame(countFrame)
  }
  requestAnimationFrame(countFrame)
}

function stopMonitoring() {
  if (monitoringInterval) {
    clearInterval(monitoringInterval)
    monitoringInterval = null
  }
}

function formatMemory(bytes: number): string {
  if (!bytes || bytes === 0) return '0'
  return (bytes / (1024 * 1024)).toFixed(1)
}

function formatNumber(num: number): string {
  if (num >= 1000000) return (num / 1000000).toFixed(1) + 'M'
  if (num >= 1000) return (num / 1000).toFixed(1) + 'K'
  return num.toString()
}

function exportReport() {
  const report = {
    timestamp: new Date().toISOString(),
    metrics: performanceMetrics.value,
    history: performanceHistory.value.slice(-50)
  }
  
  const blob = new Blob([JSON.stringify(report, null, 2)], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `performance_report_${Date.now()}.json`
  a.click()
  URL.revokeObjectURL(url)
}

function clearHistory() {
  analysisStore.clearPerformanceHistory()
}

onMounted(() => {
  startMonitoring()
})

onUnmounted(() => {
  stopMonitoring()
})
</script>

<style scoped>
.performance-analysis {
  padding: 0.6rem;
}

.panel-header {
  margin-bottom: 0.8rem;
}

.panel-header h4 {
  margin: 0;
  font-size: 0.85rem;
  color: rgba(255, 255, 255, 0.9);
}

.metrics-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 0.5rem;
  margin-bottom: 1rem;
}

.metric-card {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
}

.metric-icon {
  font-size: 1.2rem;
}

.metric-info {
  flex: 1;
}

.metric-label {
  font-size: 0.65rem;
  color: rgba(255, 255, 255, 0.6);
}

.metric-value {
  font-size: 1rem;
  font-weight: 600;
  color: #a5b4fc;
}

.metric-value.good {
  color: #34d399;
}

.metric-value.medium {
  color: #fbbf24;
}

.metric-value.bad {
  color: #ef4444;
}

.metric-unit {
  font-size: 0.6rem;
  color: rgba(255, 255, 255, 0.5);
}

.chart-section {
  margin-bottom: 0.8rem;
}

.chart-section h5 {
  margin: 0 0 0.5rem 0;
  font-size: 0.75rem;
  color: rgba(255, 255, 255, 0.8);
}

.chart-container {
  background: rgba(0, 0, 0, 0.3);
  border-radius: 6px;
  padding: 0.4rem;
}

.no-data {
  text-align: center;
  padding: 2rem;
  color: rgba(255, 255, 255, 0.5);
  font-size: 0.75rem;
}

.actions {
  display: flex;
  gap: 0.4rem;
}

.btn-primary,
.btn-secondary {
  flex: 1;
  padding: 0.4rem;
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px;
  font-size: 0.75rem;
  cursor: pointer;
  transition: all 0.2s ease;
}

.btn-primary {
  background: rgba(99, 102, 241, 0.2);
  color: #a5b4fc;
}

.btn-primary:hover {
  background: rgba(99, 102, 241, 0.3);
}

.btn-secondary {
  background: rgba(255, 255, 255, 0.1);
  color: rgba(255, 255, 255, 0.8);
}

.btn-secondary:hover {
  background: rgba(255, 255, 255, 0.15);
}
</style>
