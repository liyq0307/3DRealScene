<template>
  <div class="building-layout-analysis">
    <div class="panel-header">
      <h4>建筑布局分析</h4>
    </div>
    
    <div class="params-section">
      <div class="param-item">
        <label>容积率</label>
        <input v-model.number="plotRatio" type="number" min="0.1" max="10" step="0.1" />
      </div>
      
      <div class="param-item">
        <label>建筑限高 (米)</label>
        <input v-model.number="maxHeight" type="number" min="10" max="500" step="5" />
      </div>
      
      <div class="param-item">
        <label>建筑密度 (%)</label>
        <input v-model.number="buildingDensity" type="number" min="5" max="80" step="5" />
      </div>
      
      <div class="param-item">
        <label>地块面积 (m²)</label>
        <input :value="landArea.toFixed(0)" type="text" readonly class="readonly-input" />
        <button @click="drawLandPlot" class="btn-measure">绘制地块</button>
      </div>
    </div>
    
    <div v-if="buildings.length > 0" class="result-section">
      <h5>生成结果</h5>
      
      <div class="stats-grid">
        <div class="stat-card">
          <div class="stat-icon">🏢</div>
          <div class="stat-info">
            <div class="stat-label">建筑数量</div>
            <div class="stat-value">{{ buildings.length }} 栋</div>
          </div>
        </div>
        
        <div class="stat-card">
          <div class="stat-icon">📐</div>
          <div class="stat-info">
            <div class="stat-label">总面积</div>
            <div class="stat-value">{{ totalBuildingArea.toFixed(0) }} m²</div>
          </div>
        </div>
        
        <div class="stat-card">
          <div class="stat-icon">📊</div>
          <div class="stat-info">
            <div class="stat-label">实际容积率</div>
            <div class="stat-value">{{ actualPlotRatio.toFixed(2) }}</div>
          </div>
        </div>
        
        <div class="stat-card">
          <div class="stat-icon">📏</div>
          <div class="stat-info">
            <div class="stat-label">平均层高</div>
            <div class="stat-value">{{ avgHeight.toFixed(1) }} 米</div>
          </div>
        </div>
      </div>
      
      <div class="building-list">
        <div class="list-header">
          <span>建筑列表</span>
          <span class="list-count">{{ buildings.length }}</span>
        </div>
        <div class="list-content">
          <div v-for="(building, index) in buildings" :key="index" class="building-item">
            <span class="building-name">建筑 {{ index + 1 }}</span>
            <span class="building-height">{{ building.height.toFixed(1) }}m</span>
            <span class="building-area">{{ building.area.toFixed(0) }}m²</span>
          </div>
        </div>
      </div>
    </div>
    
    <div class="actions">
      <button @click="generateLayout" class="btn-primary" :disabled="!canGenerate">
        {{ buildings.length > 0 ? '重新生成' : '生成布局' }}
      </button>
      <button @click="clearAll" class="btn-secondary">清除</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useAnalysisStore } from '@/stores/analysis'

const analysisStore = useAnalysisStore()

const plotRatio = ref(2.0)
const maxHeight = ref(50)
const buildingDensity = ref(30)
const landArea = ref(0)
const buildings = ref<Array<{ height: number; area: number; position: { x: number; y: number } }>>([])

const canGenerate = computed(() => landArea.value > 0)

const totalBuildingArea = computed(() => {
  return buildings.value.reduce((sum, b) => sum + b.area * (b.height / 3), 0)
})

const actualPlotRatio = computed(() => {
  if (landArea.value === 0) return 0
  return totalBuildingArea.value / landArea.value
})

const avgHeight = computed(() => {
  if (buildings.value.length === 0) return 0
  return buildings.value.reduce((sum, b) => sum + b.height, 0) / buildings.value.length
})

function drawLandPlot() {
  analysisStore.startAnalysis('building-layout')
  
  setTimeout(() => {
    landArea.value = Math.random() * 50000 + 10000
    analysisStore.stopAnalysis()
  }, 1500)
}

function generateLayout() {
  if (!canGenerate.value) return
  
  analysisStore.startAnalysis('building-layout')
  
  setTimeout(() => {
    const totalBaseArea = landArea.value * (buildingDensity.value / 100)
    const totalFloorArea = landArea.value * plotRatio.value
    
    const avgBuildingHeight = totalFloorArea / totalBaseArea
    const limitedHeight = Math.min(avgBuildingHeight * 3, maxHeight.value)
    
    const buildingCount = Math.max(1, Math.floor(totalBaseArea / 200))
    const singleBaseArea = totalBaseArea / buildingCount
    
    buildings.value = []
    
    for (let i = 0; i < buildingCount; i++) {
      const heightVariation = (Math.random() - 0.5) * 0.3
      const buildingHeight = limitedHeight * (1 + heightVariation)
      
      buildings.value.push({
        height: buildingHeight,
        area: singleBaseArea,
        position: {
          x: Math.random() * 100,
          y: Math.random() * 100
        }
      })
    }
    
    analysisStore.addResult({
      type: 'building-layout',
      name: '建筑布局分析结果',
      data: {
        plotRatio: plotRatio.value,
        maxHeight: maxHeight.value,
        buildingDensity: buildingDensity.value,
        landArea: landArea.value,
        buildings: buildings.value,
        totalBuildingArea: totalBuildingArea.value,
        actualPlotRatio: actualPlotRatio.value
      },
      visible: true
    })
    
    analysisStore.stopAnalysis()
  }, 2000)
}

function clearAll() {
  landArea.value = 0
  buildings.value = []
  analysisStore.clearByType('building-layout')
}
</script>

<style scoped>
.building-layout-analysis {
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

.btn-measure:hover {
  background: rgba(99, 102, 241, 0.3);
}

.result-section {
  margin-bottom: 0.8rem;
}

.result-section h5 {
  margin: 0 0 0.6rem 0;
  font-size: 0.75rem;
  color: rgba(255, 255, 255, 0.8);
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 0.5rem;
  margin-bottom: 0.6rem;
}

.stat-card {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem;
  background: rgba(255, 255, 255, 0.05);
  border-radius: 6px;
}

.stat-icon {
  font-size: 1.2rem;
}

.stat-label {
  font-size: 0.65rem;
  color: rgba(255, 255, 255, 0.6);
}

.stat-value {
  font-size: 0.85rem;
  font-weight: 600;
  color: #a5b4fc;
}

.building-list {
  background: rgba(0, 0, 0, 0.2);
  border-radius: 6px;
  max-height: 150px;
  overflow: hidden;
}

.list-header {
  display: flex;
  justify-content: space-between;
  padding: 0.4rem 0.6rem;
  background: rgba(99, 102, 241, 0.15);
  font-size: 0.7rem;
  color: rgba(255, 255, 255, 0.9);
}

.list-count {
  background: rgba(99, 102, 241, 0.3);
  padding: 0.1rem 0.4rem;
  border-radius: 10px;
}

.list-content {
  max-height: 110px;
  overflow-y: auto;
  padding: 0.3rem;
}

.building-item {
  display: flex;
  justify-content: space-between;
  padding: 0.3rem;
  font-size: 0.7rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
}

.building-item:last-child {
  border-bottom: none;
}

.building-name {
  color: rgba(255, 255, 255, 0.8);
}

.building-height,
.building-area {
  color: #a5b4fc;
  font-size: 0.65rem;
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
