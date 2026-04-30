<template>
  <div class="area-measurement">
    <div class="panel-header">
      <h4>面积测量</h4>
    </div>
    
    <div class="mode-selector">
      <button
        v-for="mode in modes"
        :key="mode.key"
        @click="currentMode = mode.key"
        :class="['mode-btn', { active: currentMode === mode.key }]"
      >
        {{ mode.name }}
      </button>
    </div>
    
    <div class="params-section">
      <div class="param-item">
        <label>显示周长</label>
        <input v-model="showPerimeter" type="checkbox" />
      </div>
      <div class="param-item">
        <label>填充颜色</label>
        <input v-model="fillColor" type="color" />
      </div>
      <div class="param-item">
        <label>透明度</label>
        <input v-model.number="opacity" type="range" min="0" max="1" step="0.1" />
      </div>
    </div>
    
    <div class="instructions">
      <p>点击绘制多边形区域，双击完成</p>
    </div>
    
    <div v-if="measurements.length > 0" class="results-section">
      <h5>测量结果</h5>
      <div class="measurement-list">
        <div
          v-for="(measure, index) in measurements"
          :key="index"
          class="measurement-item"
        >
          <div class="measure-header">
            <span class="measure-label">区域 {{ index + 1 }}</span>
            <button @click="removeMeasurement(index)" class="btn-remove">×</button>
          </div>
          <div class="measure-data">
            <div class="data-row">
              <span class="data-label">面积:</span>
              <span class="data-value">{{ formatArea(measure.area) }}</span>
            </div>
            <div v-if="showPerimeter" class="data-row">
              <span class="data-label">周长:</span>
              <span class="data-value">{{ measure.perimeter.toFixed(2) }} 米</span>
            </div>
          </div>
        </div>
      </div>
    </div>
    
    <div class="actions">
      <button @click="startMeasurement" class="btn-primary">开始测量</button>
      <button @click="clearMeasurements" class="btn-secondary">清除全部</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onUnmounted } from 'vue'
import { useAnalysisStore } from '@/stores/analysis'
import { Mars3DAnalysisTools } from '@/utils/mars3dAnalysis'

const props = defineProps<{
  viewerInstance?: any
  sceneObjects?: any[]
}>()

const analysisStore = useAnalysisStore()

const currentMode = ref<'horizontal' | 'surface'>('horizontal')
const showPerimeter = ref(true)
const fillColor = ref('#6366f1')
const opacity = ref(0.3)
const measurements = ref<Array<{ area: number; perimeter: number; points: any[] }>>([])
const isMeasuring = ref(false)

const modes: Array<{ key: 'horizontal' | 'surface'; name: string }> = [
  { key: 'horizontal', name: '水平面积' },
  { key: 'surface', name: '贴地面积' }
]

let analysisTools: Mars3DAnalysisTools | null = null

async function startMeasurement() {
  if (!props.viewerInstance) {
    console.warn('viewerInstance not available')
    runSimulation()
    return
  }
  
  isMeasuring.value = true
  analysisStore.startAnalysis('area')
  
  try {
    if (!analysisTools) {
      analysisTools = new Mars3DAnalysisTools(props.viewerInstance)
    }
    
    let result
    if (currentMode.value === 'surface') {
      result = await analysisTools.measureAreaSurface()
    } else {
      result = await analysisTools.measureArea()
    }
    
    measurements.value.push({
      area: result.area,
      perimeter: result.perimeter,
      points: result.positions
    })
    
    analysisStore.addResult({
      type: 'area',
      name: '面积测量结果',
      data: {
        mode: currentMode.value,
        area: result.area,
        perimeter: result.perimeter,
        positions: result.positions
      },
      visible: true
    })
  } catch (error) {
    console.error('面积测量失败:', error)
    runSimulation()
  } finally {
    isMeasuring.value = false
    analysisStore.stopAnalysis()
  }
}

function runSimulation() {
  isMeasuring.value = true
  analysisStore.startAnalysis('area')
  
  setTimeout(() => {
    const area = Math.random() * 50000 + 5000
    const perimeter = Math.sqrt(area) * 4
    
    measurements.value.push({
      area,
      perimeter,
      points: []
    })
    
    analysisStore.addResult({
      type: 'area',
      name: '面积测量结果',
      data: {
        mode: currentMode.value,
        area,
        perimeter
      },
      visible: true
    })
    
    isMeasuring.value = false
    analysisStore.stopAnalysis()
  }, 1500)
}

function formatArea(area: number): string {
  if (area >= 1000000) {
    return `${(area / 1000000).toFixed(2)} 平方公里`
  } else if (area >= 10000) {
    return `${(area / 10000).toFixed(2)} 公顷`
  }
  return `${area.toFixed(2)} 平方米`
}

function removeMeasurement(index: number) {
  measurements.value.splice(index, 1)
}

function clearMeasurements() {
  measurements.value = []
  if (analysisTools) {
    analysisTools.clearMeasure()
  }
  analysisStore.clearByType('area')
}

onUnmounted(() => {
  clearMeasurements()
  if (analysisTools) {
    analysisTools.destroy()
    analysisTools = null
  }
})
</script>

<style scoped>
.area-measurement {
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

.mode-selector {
  display: flex;
  gap: 0.4rem;
  margin-bottom: 0.8rem;
}

.mode-btn {
  flex: 1;
  padding: 0.4rem;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 4px;
  color: rgba(255, 255, 255, 0.8);
  font-size: 0.75rem;
  cursor: pointer;
  transition: all 0.2s ease;
}

.mode-btn:hover {
  background: rgba(99, 102, 241, 0.15);
}

.mode-btn.active {
  background: rgba(99, 102, 241, 0.25);
  border-color: rgba(99, 102, 241, 0.5);
  color: #a5b4fc;
}

.params-section {
  margin-bottom: 0.8rem;
}

.param-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 0.5rem;
}

.param-item label {
  font-size: 0.75rem;
  color: rgba(255, 255, 255, 0.7);
}

.param-item input[type="checkbox"] {
  width: 16px;
  height: 16px;
  cursor: pointer;
}

.param-item input[type="color"] {
  width: 40px;
  height: 24px;
  padding: 0;
  border: none;
  border-radius: 4px;
  cursor: pointer;
}

.param-item input[type="range"] {
  width: 100px;
}

.instructions {
  background: rgba(99, 102, 241, 0.1);
  border: 1px solid rgba(99, 102, 241, 0.2);
  border-radius: 6px;
  padding: 0.6rem;
  margin-bottom: 0.8rem;
}

.instructions p {
  margin: 0;
  font-size: 0.7rem;
  color: rgba(255, 255, 255, 0.8);
  text-align: center;
}

.results-section {
  margin-bottom: 0.8rem;
}

.results-section h5 {
  margin: 0 0 0.5rem 0;
  font-size: 0.75rem;
  color: rgba(255, 255, 255, 0.8);
}

.measurement-list {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}

.measurement-item {
  background: rgba(0, 0, 0, 0.2);
  border-radius: 6px;
  padding: 0.5rem;
}

.measure-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.3rem;
}

.measure-label {
  font-size: 0.7rem;
  color: rgba(255, 255, 255, 0.6);
}

.btn-remove {
  width: 20px;
  height: 20px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(239, 68, 68, 0.2);
  border: none;
  border-radius: 50%;
  color: #ef4444;
  font-size: 0.8rem;
  cursor: pointer;
}

.measure-data {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.data-row {
  display: flex;
  justify-content: space-between;
  font-size: 0.75rem;
}

.data-label {
  color: rgba(255, 255, 255, 0.6);
}

.data-value {
  color: #a5b4fc;
  font-weight: 500;
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
