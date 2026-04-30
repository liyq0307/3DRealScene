<template>
  <div class="site-selection">
    <div class="panel-header">
      <h4>在线选址</h4>
    </div>

    <div class="params-section">
      <div class="param-item">
        <label>模型URL</label>
        <input v-model="modelUrl" type="text" placeholder="3DTiles/GLTF URL" />
      </div>
      <div class="constraint-section">
        <h5>约束条件</h5>
        <div class="param-item">
          <label>最小面积(m²)</label>
          <input v-model.number="minArea" type="number" min="0" />
        </div>
        <div class="param-item">
          <label>最大坡度(°)</label>
          <input v-model.number="maxSlope" type="number" min="0" max="90" />
        </div>
        <div class="param-item">
          <label>避开保护区</label>
          <label class="toggle">
            <input v-model="avoidProtected" type="checkbox" />
            <span class="toggle-slider"></span>
          </label>
        </div>
      </div>
    </div>

    <div class="actions">
      <button class="btn-primary" @click="handleDraw" :disabled="isDrawing || !modelUrl">
        {{ isDrawing ? '放置中...' : '放置模型' }}
      </button>
    </div>

    <div v-if="errorMsg" class="error-banner">
      <p>{{ errorMsg }}</p>
    </div>

    <div v-if="candidateAreas.length > 0" class="results-section">
      <h5>候选区域 ({{ candidateAreas.length }})</h5>
      <div v-for="(area, i) in candidateAreas" :key="i" class="area-item" @click="selectArea(i)">
        <span class="area-name">{{ area.name }}</span>
        <span class="area-score">评分: {{ area.score.toFixed(1) }}</span>
      </div>
    </div>

    <div v-if="selectedArea" class="detail-section">
      <h5>区域详情</h5>
      <div class="result-card">
        <div class="result-row">
          <span class="result-label">名称:</span>
          <span class="result-value">{{ selectedArea.name }}</span>
        </div>
        <div class="result-row">
          <span class="result-label">面积:</span>
          <span class="result-value">{{ selectedArea.area.toFixed(0) }} m²</span>
        </div>
        <div class="result-row">
          <span class="result-label">坡度:</span>
          <span class="result-value">{{ selectedArea.slope.toFixed(1) }}°</span>
        </div>
        <div class="result-row">
          <span class="result-label">评分:</span>
          <span class="result-value">{{ selectedArea.score.toFixed(1) }}</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import { useAnalysisStore } from '@/stores/analysis'

interface CandidateArea {
  name: string
  area: number
  slope: number
  score: number
  position: any
}

const props = defineProps<{ viewerInstance?: any }>()
const { drawPoint, isReady, init } = useAnalysisTool(ref(props.viewerInstance))
const store = useAnalysisStore()

const modelUrl = ref('')
const minArea = ref(1000)
const maxSlope = ref(15)
const avoidProtected = ref(true)
const isDrawing = ref(false)
const errorMsg = ref('')
const candidateAreas = ref<CandidateArea[]>([])
const selectedArea = ref<CandidateArea | null>(null)

onMounted(() => { if (!isReady.value) init() })

function simulateCandidateAreas(): CandidateArea[] {
  return Array.from({ length: 3 + Math.floor(Math.random() * 3) }, (_, i) => ({
    name: `候选区${String.fromCharCode(65 + i)}`,
    area: minArea.value * (1 + Math.random() * 5),
    slope: Math.random() * maxSlope.value,
    score: 60 + Math.random() * 40,
    position: null
  })).sort((a, b) => b.score - a.score)
}

function selectArea(index: number) {
  selectedArea.value = candidateAreas.value[index]
}

async function handleDraw() {
  if (!modelUrl.value.trim()) { errorMsg.value = '请输入模型URL'; return }
  errorMsg.value = ''
  isDrawing.value = true
  try {
    const result = await drawPoint()
    if (result) {
      const areas = simulateCandidateAreas()
      candidateAreas.value = areas
      selectedArea.value = areas[0] || null
      store.addResult({
        type: 'site-selection',
        name: `在线选址 ${modelUrl.value.split('/').pop()}`,
        data: { position: result.positionShow, modelUrl: modelUrl.value, minArea: minArea.value, maxSlope: maxSlope.value, avoidProtected: avoidProtected.value, candidateAreas: areas },
        visible: true
      })
    }
  } catch (e: any) {
    errorMsg.value = e?.message || '放置模型失败'
    store.addErrorResult('site-selection', '在线选址', errorMsg.value)
  } finally {
    isDrawing.value = false
  }
}
</script>

<style scoped>
.site-selection { padding: 0.6rem; }
.panel-header { margin-bottom: 0.8rem; }
.panel-header h4 { margin: 0; font-size: 0.85rem; color: rgba(255, 255, 255, 0.9); }
.params-section { margin-bottom: 0.8rem; }
.param-item { display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.5rem; }
.param-item label { font-size: 0.75rem; color: rgba(255, 255, 255, 0.7); }
.param-item input[type="text"],
.param-item input[type="number"] {
  width: 120px; padding: 0.25rem 0.4rem; background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.2); border-radius: 4px;
  color: rgba(255, 255, 255, 0.9); font-size: 0.75rem;
}
.constraint-section { margin-top: 0.5rem; padding-top: 0.5rem; border-top: 1px solid rgba(255, 255, 255, 0.1); }
.constraint-section h5 { margin: 0 0 0.5rem 0; font-size: 0.7rem; color: rgba(255, 255, 255, 0.6); }
.toggle { position: relative; display: inline-block; width: 36px; height: 20px; }
.toggle input { opacity: 0; width: 0; height: 0; }
.toggle-slider {
  position: absolute; cursor: pointer; top: 0; left: 0; right: 0; bottom: 0;
  background: rgba(255, 255, 255, 0.15); border-radius: 20px; transition: 0.2s;
}
.toggle-slider::before {
  content: ''; position: absolute; height: 16px; width: 16px;
  left: 2px; bottom: 2px; background: white; border-radius: 50%; transition: 0.2s;
}
.toggle input:checked + .toggle-slider { background: rgba(99, 102, 241, 0.5); }
.toggle input:checked + .toggle-slider::before { transform: translateX(16px); }
.error-banner {
  background: rgba(239, 68, 68, 0.15); border: 1px solid rgba(239, 68, 68, 0.3);
  border-radius: 4px; padding: 0.4rem 0.6rem; margin-bottom: 0.8rem;
}
.error-banner p { margin: 0; font-size: 0.7rem; color: rgba(239, 68, 68, 0.9); }
.results-section, .detail-section { margin-bottom: 0.8rem; }
.results-section h5, .detail-section h5 { margin: 0 0 0.5rem 0; font-size: 0.75rem; color: rgba(255, 255, 255, 0.8); }
.area-item {
  display: flex; justify-content: space-between; padding: 0.3rem 0.4rem;
  margin-bottom: 0.3rem; background: rgba(0, 0, 0, 0.2); border-radius: 4px;
  cursor: pointer; transition: background 0.2s; font-size: 0.7rem;
}
.area-item:hover { background: rgba(99, 102, 241, 0.15); }
.area-name { color: rgba(255, 255, 255, 0.7); }
.area-score { color: #a5b4fc; font-weight: 500; }
.result-card { background: rgba(0, 0, 0, 0.2); border-radius: 6px; padding: 0.5rem; }
.result-row { display: flex; justify-content: space-between; padding: 0.3rem 0; font-size: 0.75rem; }
.result-label { color: rgba(255, 255, 255, 0.6); }
.result-value { color: #a5b4fc; font-weight: 500; }
.actions { display: flex; gap: 0.4rem; margin-top: 0.4rem; }
.btn-primary {
  flex: 1; padding: 0.4rem; border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px; font-size: 0.75rem; cursor: pointer; transition: all 0.2s ease;
  background: rgba(99, 102, 241, 0.2); color: #a5b4fc;
}
.btn-primary:hover:not(:disabled) { background: rgba(99, 102, 241, 0.3); }
.btn-primary:disabled { opacity: 0.5; cursor: not-allowed; }
</style>
