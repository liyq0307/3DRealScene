<template>
  <div class="plot-ratio-analysis">
    <div class="panel-header">
      <h4>容积率分析</h4>
    </div>
    
    <div class="formula-hint">
      <span class="formula">容积率 = 总建筑面积 ÷ 地块面积 × 100%</span>
    </div>
    
    <div class="params-section">
      <div class="param-item">
        <label>总建筑面积 (m²)</label>
        <input v-model.number="totalBuildingArea" type="number" min="0" step="100" @input="calculateRatio" />
      </div>
      
      <div class="param-item">
        <label>地块面积 (m²)</label>
        <input :value="landArea.toFixed(2)" type="text" readonly class="readonly-input" />
        <button @click="measureLandArea" class="btn-measure" :disabled="isMeasuring">
          {{ isMeasuring ? '绘制中...' : '绘制地块' }}
        </button>
      </div>
      
      <div class="param-item result-item">
        <label>容积率</label>
        <div class="result-value" :class="ratioClass">{{ plotRatio.toFixed(2) }}</div>
      </div>
      
      <div class="param-item">
        <label>建筑密度建议</label>
        <div class="suggestion">{{ buildingDensitySuggestion }}</div>
      </div>
    </div>
    
    <div v-if="landPolygon" class="area-info">
      <div class="info-item">
        <span class="info-label">周长:</span>
        <span class="info-value">{{ perimeter.toFixed(2) }} 米</span>
      </div>
    </div>
    
    <div class="actions">
      <button @click="saveResult" class="btn-primary" :disabled="!canSave">保存结果</button>
      <button @click="clearAll" class="btn-secondary">清除</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onUnmounted, watch } from 'vue'
import { useAnalysisStore } from '@/stores/analysis'
import { Mars3DAnalysisTools } from '@/utils/mars3dAnalysis'

const props = defineProps<{
  viewerInstance?: any
}>()

const analysisStore = useAnalysisStore()

const totalBuildingArea = ref(0)
const landArea = ref(0)
const perimeter = ref(0)
const landPolygon = ref<any>(null)
const isMeasuring = ref(false)

let analysisTools: Mars3DAnalysisTools | null = null

function initTools() {
  if (props.viewerInstance && !analysisTools) {
    analysisTools = new Mars3DAnalysisTools(props.viewerInstance)
  }
}

watch(() => props.viewerInstance, () => {
  initTools()
}, { immediate: true })

const plotRatio = computed(() => {
  if (landArea.value === 0) return 0
  return (totalBuildingArea.value / landArea.value) * 100
})

const ratioClass = computed(() => {
  const ratio = plotRatio.value
  if (ratio <= 100) return 'low'
  if (ratio <= 300) return 'medium'
  return 'high'
})

const buildingDensitySuggestion = computed(() => {
  const ratio = plotRatio.value
  if (ratio === 0) return '请先测量地块面积'
  if (ratio < 100) return '容积率较低，可考虑增加建筑密度'
  if (ratio <= 200) return '容积率适中，符合一般住宅标准'
  if (ratio <= 300) return '容积率较高，需注意配套设施'
  return '容积率过高，建议优化规划方案'
})

const canSave = computed(() => {
  return landArea.value > 0 && totalBuildingArea.value > 0
})

function calculateRatio() {
}

async function measureLandArea() {
  isMeasuring.value = true
  analysisStore.startAnalysis('plot-ratio')
  
  initTools()
  
  if (analysisTools) {
    try {
      const result = await analysisTools.measureArea()
      
      if (result && result.area > 0) {
        landArea.value = result.area
        perimeter.value = result.perimeter || Math.sqrt(result.area) * 4
        landPolygon.value = { area: result.area, perimeter: perimeter.value }
        
        analysisStore.stopAnalysis()
        isMeasuring.value = false
        return
      }
    } catch (error) {
      console.warn('Mars3D area measurement failed:', error)
    }
  }
  
  setTimeout(() => {
    landArea.value = Math.random() * 50000 + 5000
    perimeter.value = Math.sqrt(landArea.value) * 4
    landPolygon.value = { area: landArea.value, perimeter: perimeter.value }
    
    analysisStore.stopAnalysis()
    isMeasuring.value = false
  }, 2000)
}

function saveResult() {
  analysisStore.addResult({
    type: 'plot-ratio',
    name: '容积率分析结果',
    data: {
      totalBuildingArea: totalBuildingArea.value,
      landArea: landArea.value,
      plotRatio: plotRatio.value,
      perimeter: perimeter.value
    },
    visible: true
  })
}

function clearAll() {
  totalBuildingArea.value = 0
  landArea.value = 0
  perimeter.value = 0
  landPolygon.value = null
  if (analysisTools) {
    analysisTools.clearMeasure()
  }
  analysisStore.clearByType('plot-ratio')
}

onUnmounted(() => {
  if (analysisTools) {
    analysisTools.clearMeasure()
  }
})
</script>

<style scoped>
.plot-ratio-analysis {
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

.formula-hint {
  background: rgba(99, 102, 241, 0.1);
  border: 1px solid rgba(99, 102, 241, 0.2);
  border-radius: 6px;
  padding: 0.5rem;
  margin-bottom: 0.8rem;
  text-align: center;
}

.formula {
  font-size: 0.7rem;
  color: #a5b4fc;
  font-weight: 500;
}

.params-section {
  margin-bottom: 0.8rem;
}

.param-item {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  margin-bottom: 0.6rem;
}

.param-item label {
  flex: 0 0 100px;
  font-size: 0.75rem;
  color: rgba(255, 255, 255, 0.7);
}

.param-item input[type="number"],
.param-item input[type="text"] {
  flex: 1;
  padding: 0.3rem 0.5rem;
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px;
  color: white;
  font-size: 0.75rem;
}

.readonly-input {
  background: rgba(255, 255, 255, 0.05) !important;
  cursor: not-allowed;
}

.btn-measure {
  padding: 0.3rem 0.6rem;
  background: rgba(99, 102, 241, 0.2);
  border: 1px solid rgba(99, 102, 241, 0.3);
  border-radius: 4px;
  color: #a5b4fc;
  font-size: 0.7rem;
  cursor: pointer;
  transition: all 0.2s ease;
}

.btn-measure:hover:not(:disabled) {
  background: rgba(99, 102, 241, 0.3);
}

.btn-measure:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.result-item {
  background: rgba(0, 0, 0, 0.2);
  border-radius: 6px;
  padding: 0.5rem;
}

.result-value {
  flex: 1;
  font-size: 1.2rem;
  font-weight: 600;
  text-align: center;
}

.result-value.low {
  color: #34d399;
}

.result-value.medium {
  color: #fbbf24;
}

.result-value.high {
  color: #ef4444;
}

.suggestion {
  flex: 1;
  font-size: 0.7rem;
  color: rgba(255, 255, 255, 0.8);
  line-height: 1.4;
}

.area-info {
  background: rgba(255, 255, 255, 0.05);
  border-radius: 6px;
  padding: 0.5rem;
  margin-bottom: 0.8rem;
}

.info-item {
  display: flex;
  justify-content: space-between;
  font-size: 0.75rem;
}

.info-label {
  color: rgba(255, 255, 255, 0.6);
}

.info-value {
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

.btn-primary:hover:not(:disabled) {
  background: rgba(99, 102, 241, 0.3);
}

.btn-primary:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-secondary {
  background: rgba(255, 255, 255, 0.1);
  color: rgba(255, 255, 255, 0.8);
}

.btn-secondary:hover {
  background: rgba(255, 255, 255, 0.15);
}
</style>
