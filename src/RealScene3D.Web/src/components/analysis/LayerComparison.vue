<template>
  <div class="layer-comparison">
    <div class="panel-header">
      <h4>卷帘对比</h4>
    </div>
    
    <div class="comparison-config">
      <div class="layer-select">
        <label>左侧图层</label>
        <select v-model="leftLayerId" @change="updateComparison">
          <option value="">请选择</option>
          <option v-for="layer in availableLayers" :key="layer.id" :value="layer.id">
            {{ layer.name }}
          </option>
        </select>
      </div>
      
      <div class="layer-select">
        <label>右侧图层</label>
        <select v-model="rightLayerId" @change="updateComparison">
          <option value="">请选择</option>
          <option v-for="layer in availableLayers" :key="layer.id" :value="layer.id">
            {{ layer.name }}
          </option>
        </select>
      </div>
      
      <div class="slider-position">
        <label>分割位置</label>
        <input v-model.number="splitPosition" type="range" min="0" max="100" step="1" @input="updateSplitPosition" />
        <span class="position-value">{{ splitPosition }}%</span>
      </div>
      
      <div class="param-item">
        <label>对比模式</label>
        <select v-model="comparisonMode">
          <option value="vertical">垂直分割</option>
          <option value="horizontal">水平分割</option>
          <option value="wipe">擦除对比</option>
        </select>
      </div>
    </div>
    
    <div v-if="isComparing" class="comparison-info">
      <div class="info-header">
        <span class="info-title">对比信息</span>
        <button @click="toggleInfo" class="btn-toggle">{{ showInfo ? '收起' : '展开' }}</button>
      </div>
      
      <div v-show="showInfo" class="info-content">
        <div class="info-item">
          <span class="info-label">左侧:</span>
          <span class="info-value">{{ getLayerName(leftLayerId) }}</span>
        </div>
        <div class="info-item">
          <span class="info-label">右侧:</span>
          <span class="info-value">{{ getLayerName(rightLayerId) }}</span>
        </div>
        <div class="info-item">
          <span class="info-label">模式:</span>
          <span class="info-value">{{ getModeLabel(comparisonMode) }}</span>
        </div>
      </div>
    </div>
    
    <div class="actions">
      <button @click="startComparison" class="btn-primary" :disabled="!canCompare">
        {{ isComparing ? '更新对比' : '开始对比' }}
      </button>
      <button @click="stopComparison" class="btn-secondary" :disabled="!isComparing">停止对比</button>
      <button @click="captureScreenshot" class="btn-secondary" :disabled="!isComparing">截图</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useAnalysisStore } from '@/stores/analysis'

const props = defineProps<{
  sceneObjects?: any[]
}>()

const analysisStore = useAnalysisStore()

const leftLayerId = ref('')
const rightLayerId = ref('')
const splitPosition = ref(50)
const comparisonMode = ref<'vertical' | 'horizontal' | 'wipe'>('vertical')
const isComparing = ref(false)
const showInfo = ref(true)

const availableLayers = computed(() => {
  return props.sceneObjects?.map(obj => ({
    id: obj.id,
    name: obj.name,
    url: obj.displayPath || obj.modelPath
  })) || []
})

const canCompare = computed(() => {
  return leftLayerId.value && rightLayerId.value && leftLayerId.value !== rightLayerId.value
})

function getLayerName(layerId: string): string {
  const layer = availableLayers.value.find(l => l.id === layerId)
  return layer?.name || ''
}

function getModeLabel(mode: string): string {
  const labels: Record<string, string> = {
    vertical: '垂直分割',
    horizontal: '水平分割',
    wipe: '擦除对比'
  }
  return labels[mode] || mode
}

function updateComparison() {
  if (isComparing.value && canCompare.value) {
    createComparison()
  }
}

function updateSplitPosition() {
  if (isComparing.value) {
    // 更新分割位置
  }
}

function startComparison() {
  if (!canCompare.value) return
  
  analysisStore.startAnalysis('layer-comparison')
  createComparison()
}

function createComparison() {
  isComparing.value = true
  
  analysisStore.addResult({
    type: 'layer-comparison',
    name: '卷帘对比结果',
    data: {
      leftLayerId: leftLayerId.value,
      rightLayerId: rightLayerId.value,
      splitPosition: splitPosition.value,
      comparisonMode: comparisonMode.value
    },
    visible: true
  })
  
  analysisStore.stopAnalysis()
}

function stopComparison() {
  isComparing.value = false
  analysisStore.clearByType('layer-comparison')
}

function captureScreenshot() {
  // 截图功能
  const canvas = document.querySelector('canvas')
  if (!canvas) return
  
  const dataUrl = canvas.toDataURL('image/png')
  const a = document.createElement('a')
  a.href = dataUrl
  a.download = `comparison_${Date.now()}.png`
  a.click()
}

function toggleInfo() {
  showInfo.value = !showInfo.value
}
</script>

<style scoped>
.layer-comparison {
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

.comparison-config {
  margin-bottom: 0.8rem;
}

.layer-select {
  margin-bottom: 0.6rem;
}

.layer-select label {
  display: block;
  font-size: 0.75rem;
  color: rgba(255, 255, 255, 0.7);
  margin-bottom: 0.3rem;
}

.layer-select select {
  width: 100%;
  padding: 0.3rem;
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px;
  color: white;
  font-size: 0.75rem;
}

.layer-select select option {
  background: #1a1a1a;
}

.slider-position {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  margin-bottom: 0.6rem;
}

.slider-position label {
  flex: 0 0 80px;
  font-size: 0.75rem;
  color: rgba(255, 255, 255, 0.7);
}

.slider-position input[type="range"] {
  flex: 1;
}

.position-value {
  width: 40px;
  font-size: 0.7rem;
  color: #a5b4fc;
}

.param-item {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  margin-bottom: 0.6rem;
}

.param-item label {
  flex: 0 0 80px;
  font-size: 0.75rem;
  color: rgba(255, 255, 255, 0.7);
}

.param-item select {
  flex: 1;
  padding: 0.3rem;
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px;
  color: white;
  font-size: 0.75rem;
}

.comparison-info {
  background: rgba(0, 0, 0, 0.2);
  border-radius: 6px;
  margin-bottom: 0.8rem;
}

.info-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.4rem 0.6rem;
  background: rgba(99, 102, 241, 0.15);
  border-radius: 6px 6px 0 0;
}

.info-title {
  font-size: 0.75rem;
  color: rgba(255, 255, 255, 0.9);
}

.btn-toggle {
  padding: 0.2rem 0.5rem;
  background: rgba(255, 255, 255, 0.1);
  border: none;
  border-radius: 4px;
  color: white;
  font-size: 0.65rem;
  cursor: pointer;
}

.info-content {
  padding: 0.4rem 0.6rem;
}

.info-item {
  display: flex;
  justify-content: space-between;
  font-size: 0.7rem;
  padding: 0.2rem 0;
}

.info-label {
  color: rgba(255, 255, 255, 0.6);
}

.info-value {
  color: #a5b4fc;
}

.actions {
  display: flex;
  gap: 0.4rem;
  flex-wrap: wrap;
}

.btn-primary,
.btn-secondary {
  flex: 1;
  min-width: calc(50% - 0.2rem);
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

.btn-secondary:hover:not(:disabled) {
  background: rgba(255, 255, 255, 0.15);
}

.btn-secondary:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>
