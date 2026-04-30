<template>
  <div class="viewshed-analysis">
    <div class="panel-header"><h4>可视域分析</h4></div>

    <div class="params-section">
      <div class="param-item">
        <label>方向角(°)</label>
        <input v-model.number="direction" type="number" min="0" max="360" step="1" />
      </div>
      <div class="param-item">
        <label>俯仰角(°)</label>
        <input v-model.number="pitch" type="number" min="-90" max="90" step="1" />
      </div>
      <div class="param-item">
        <label>水平视场角(°)</label>
        <input v-model.number="horizontalFov" type="number" min="1" max="180" step="1" />
      </div>
      <div class="param-item">
        <label>垂直视场角(°)</label>
        <input v-model.number="verticalFov" type="number" min="1" max="180" step="1" />
      </div>
      <div class="param-item">
        <label>距离(m)</label>
        <input v-model.number="distance" type="number" min="1" step="10" />
      </div>
    </div>

    <div class="instructions">
      <p><span class="step">1.</span> 设置可视域参数<br /><span class="step">2.</span> 点击"开始分析"在地图上放置观察点</p>
    </div>

    <div v-if="store.error" class="error-banner">
      <p>{{ store.error.message }}</p>
    </div>

    <div v-if="results.length > 0" class="results-section">
      <h5>分析结果 ({{ results.length }})</h5>
      <div v-for="(r, i) in results" :key="i" class="result-card">
        <div class="result-row">
          <span class="result-label">方向角</span>
          <span class="result-value">{{ r.direction }}°</span>
        </div>
        <div class="result-row">
          <span class="result-label">距离</span>
          <span class="result-value">{{ r.distance }} m</span>
        </div>
        <div class="result-row">
          <span class="result-label">可见性</span>
          <span class="result-value highlight">已分析</span>
        </div>
      </div>
    </div>

    <div class="actions">
      <button class="btn-primary" @click="handleAnalyze" :disabled="store.isAnalyzing">
        {{ store.isAnalyzing ? '请放置观察点...' : '开始分析' }}
      </button>
      <button class="btn-secondary" @click="handleClear" :disabled="results.length === 0">清除</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import { useAnalysisStore } from '@/stores/analysis'

const props = defineProps<{ viewerInstance?: any }>()
const { safeExecute, isReady, init } = useAnalysisTool(ref(props.viewerInstance))
const store = useAnalysisStore()

const direction = ref(0)
const pitch = ref(-12)
const horizontalFov = ref(60)
const verticalFov = ref(45)
const distance = ref(80)

const results = ref<Array<any>>([])

onMounted(() => { if (!isReady.value) init() })

async function handleAnalyze() {
  const result = await safeExecute('viewshed', (tools) =>
    tools.analyzeViewshed({
      direction: direction.value,
      pitch: pitch.value,
      horizontalFov: horizontalFov.value,
      verticalFov: verticalFov.value,
      distance: distance.value
    })
  )
  if (result) {
    const data = { direction: direction.value, pitch: pitch.value, horizontalFov: horizontalFov.value, verticalFov: verticalFov.value, distance: distance.value, result }
    results.value.push(data)
    store.addResult({ type: 'viewshed', name: `可视域-${results.value.length}`, data, visible: true })
  }
}

function handleClear() {
  results.value = []
  store.clearByType('viewshed')
}
</script>

<style scoped>
.viewshed-analysis { padding: 0.6rem; }
.panel-header { margin-bottom: 0.8rem; }
.panel-header h4 { margin: 0; font-size: 0.85rem; color: rgba(255, 255, 255, 0.9); }
.params-section { margin-bottom: 0.8rem; }
.param-item { display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.5rem; }
.param-item label { font-size: 0.75rem; color: rgba(255, 255, 255, 0.7); }
.param-item input {
  width: 100px; padding: 0.25rem 0.4rem; background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.2); border-radius: 4px; color: white; font-size: 0.75rem;
}
.instructions {
  background: rgba(99, 102, 241, 0.1); border: 1px solid rgba(99, 102, 241, 0.2);
  border-radius: 6px; padding: 0.5rem; margin-bottom: 0.8rem;
}
.instructions p { margin: 0; font-size: 0.7rem; color: rgba(255, 255, 255, 0.8); line-height: 1.5; }
.step { font-weight: 600; color: #a5b4fc; }
.error-banner {
  background: rgba(239, 68, 68, 0.15); border: 1px solid rgba(239, 68, 68, 0.3);
  border-radius: 4px; padding: 0.4rem 0.6rem; margin-bottom: 0.8rem;
}
.error-banner p { margin: 0; font-size: 0.7rem; color: rgba(239, 68, 68, 0.9); }
.results-section { margin-bottom: 0.8rem; max-height: 150px; overflow-y: auto; }
.results-section h5 { margin: 0 0 0.5rem 0; font-size: 0.75rem; color: rgba(255, 255, 255, 0.8); }
.result-card { background: rgba(0, 0, 0, 0.2); border-radius: 6px; padding: 0.4rem 0.5rem; margin-bottom: 0.25rem; }
.result-row { display: flex; justify-content: space-between; font-size: 0.72rem; padding: 0.15rem 0; }
.result-label { color: rgba(255, 255, 255, 0.6); }
.result-value { color: #a5b4fc; font-weight: 500; }
.result-value.highlight { color: #4ade80; }
.actions { display: flex; gap: 0.4rem; }
.btn-primary, .btn-secondary {
  flex: 1; padding: 0.4rem; border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px; font-size: 0.75rem; cursor: pointer; transition: all 0.2s ease;
}
.btn-primary { background: rgba(99, 102, 241, 0.2); color: #a5b4fc; }
.btn-primary:hover:not(:disabled) { background: rgba(99, 102, 241, 0.3); }
.btn-secondary { background: rgba(255, 255, 255, 0.1); color: rgba(255, 255, 255, 0.8); }
.btn-primary:disabled, .btn-secondary:disabled { opacity: 0.4; cursor: not-allowed; }
</style>
