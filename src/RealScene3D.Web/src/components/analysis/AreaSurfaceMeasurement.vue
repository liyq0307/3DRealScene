<template>
  <div class="area-surface-measurement">
    <div class="panel-header"><h4>贴地面积测量</h4></div>

    <div class="params-section">
      <div class="param-item">
        <label>采样精度</label>
        <select v-model="splitNum">
          <option :value="5">低(快速)</option>
          <option :value="10">中(默认)</option>
          <option :value="20">高(慢)</option>
        </select>
      </div>
    </div>

    <div class="instructions">
      <p>沿地形表面起伏计算实际表面积，贴地面积 >= 水平面积</p>
    </div>

    <div v-if="results.length > 0" class="results-section">
      <h5>测量结果</h5>
      <div v-for="(r, i) in results" :key="i" class="result-card">
        <div class="result-row">
          <span class="result-label">贴地面积</span>
          <span class="result-value highlight">{{ formatArea(r.area) }}</span>
        </div>
        <div class="result-row">
          <span class="result-label">周长</span>
          <span class="result-value">{{ formatDistance(r.perimeter) }}</span>
        </div>
        <button class="btn-delete" @click="results.splice(i, 1)">删除</button>
      </div>
    </div>

    <div class="actions">
      <button class="btn-primary" @click="handleMeasure" :disabled="isMeasuring">
        {{ isMeasuring ? '请在地图上绘制...' : '开始测量' }}
      </button>
      <button class="btn-secondary" @click="results = []" :disabled="results.length === 0">清除</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import { useAnalysisStore } from '@/stores/analysis'

const props = defineProps<{ viewerInstance?: any }>()
const { measureAreaSurface, isReady, init } = useAnalysisTool(ref(props.viewerInstance))
const store = useAnalysisStore()

const splitNum = ref(10)
const isMeasuring = ref(false)
const results = ref<Array<{ area: number; perimeter: number }>>([])

onMounted(() => { if (!isReady.value) init() })

async function handleMeasure() {
  isMeasuring.value = true
  try {
    const result = await measureAreaSurface()
    if (result) {
      results.value.push(result)
      store.addResult({ type: 'area-surface', name: `贴地面积-${results.value.length}`, data: result, visible: true })
    }
  } catch (e) {
    console.error('贴地面积测量失败:', e)
  } finally {
    isMeasuring.value = false
  }
}

function formatArea(a: number): string {
  if (a >= 1e6) return `${(a / 1e6).toFixed(4)} km²`
  if (a >= 1e4) return `${(a / 1e4).toFixed(4)} 公顷`
  return `${a.toFixed(2)} m²`
}
function formatDistance(d: number): string {
  return d >= 1000 ? `${(d / 1000).toFixed(3)} km` : `${d.toFixed(2)} m`
}
</script>

<style scoped>
.area-surface-measurement { padding: 0.6rem; }
.panel-header { margin-bottom: 0.8rem; }
.panel-header h4 { margin: 0; font-size: 0.85rem; color: rgba(255, 255, 255, 0.9); }
.params-section { margin-bottom: 0.6rem; }
.param-item { display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.5rem; }
.param-item label { font-size: 0.75rem; color: rgba(255, 255, 255, 0.7); }
.param-item select {
  width: 120px; padding: 0.25rem; background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.2); border-radius: 4px; color: white; font-size: 0.75rem;
}
.param-item select option { background: #1a1a1a; }
.instructions {
  background: rgba(99, 102, 241, 0.1); border: 1px solid rgba(99, 102, 241, 0.2);
  border-radius: 6px; padding: 0.5rem; margin-bottom: 0.8rem;
}
.instructions p { margin: 0; font-size: 0.7rem; color: rgba(255, 255, 255, 0.8); }
.results-section { margin-bottom: 0.8rem; }
.results-section h5 { margin: 0 0 0.5rem 0; font-size: 0.75rem; color: rgba(255, 255, 255, 0.8); }
.result-card {
  background: rgba(0, 0, 0, 0.2); border-radius: 6px; padding: 0.4rem 0.5rem; margin-bottom: 0.3rem;
}
.result-row { display: flex; justify-content: space-between; font-size: 0.72rem; padding: 0.15rem 0; }
.result-label { color: rgba(255, 255, 255, 0.6); }
.result-value { color: #a5b4fc; font-weight: 500; }
.result-value.highlight { color: #4ade80; }
.btn-delete {
  margin-top: 0.2rem; padding: 0.15rem 0.4rem; background: rgba(239, 68, 68, 0.1);
  color: rgba(239, 68, 68, 0.8); border: 1px solid rgba(239, 68, 68, 0.2);
  border-radius: 3px; font-size: 0.65rem; cursor: pointer;
}
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
