<template>
  <div class="distance-surface-measurement">
    <div class="panel-header"><h4>贴地距离测量</h4></div>

    <div class="params-section">
      <div class="param-item">
        <label>采样精度</label>
        <select v-model="samplingPrecision">
          <option value="low">低精度(快速)</option>
          <option value="medium">中精度</option>
          <option value="high">高精度(慢)</option>
        </select>
      </div>
    </div>

    <div class="instructions">
      <p>沿地形表面计算实际贴地距离，适用于起伏地形</p>
    </div>

    <div v-if="results.length > 0" class="results-section">
      <h5>测量结果</h5>
      <div v-for="(r, i) in results" :key="i" class="result-card">
        <div class="result-row">
          <span class="result-label">贴地距离</span>
          <span class="result-value highlight">{{ formatDistance(r.distance) }}</span>
        </div>
        <button class="btn-delete" @click="results.splice(i, 1)">删除</button>
      </div>
      <div class="result-summary">
        <span>共 {{ results.length }} 条测量</span>
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
const { measureDistanceSurface, isReady, init } = useAnalysisTool(ref(props.viewerInstance))
const store = useAnalysisStore()

const samplingPrecision = ref('medium')
const isMeasuring = ref(false)
const results = ref<Array<{ distance: number }>>([])

onMounted(() => { if (!isReady.value) init() })

async function handleMeasure() {
  isMeasuring.value = true
  try {
    const result = await measureDistanceSurface()
    if (result) {
      results.value.push(result)
      store.addResult({ type: 'distance-surface', name: `贴地距离-${results.value.length}`, data: result, visible: true })
    }
  } catch (e) {
    console.error('贴地距离测量失败:', e)
  } finally {
    isMeasuring.value = false
  }
}

function formatDistance(d: number): string {
  return d >= 1000 ? `${(d / 1000).toFixed(3)} km` : `${d.toFixed(2)} m`
}
</script>

<style scoped>
.distance-surface-measurement { padding: 0.6rem; }
.panel-header { margin-bottom: 0.8rem; }
.panel-header h4 { margin: 0; font-size: 0.85rem; color: rgba(255, 255, 255, 0.9); }
.params-section { margin-bottom: 0.6rem; }
.param-item { display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.5rem; }
.param-item label { font-size: 0.75rem; color: rgba(255, 255, 255, 0.7); }
.param-item select {
  width: 120px; padding: 0.25rem 0.4rem; background: rgba(0, 0, 0, 0.3);
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
  background: rgba(0, 0, 0, 0.2); border-radius: 6px; padding: 0.4rem 0.5rem;
  margin-bottom: 0.3rem; display: flex; justify-content: space-between; align-items: center;
}
.result-row { display: flex; justify-content: space-between; flex: 1; font-size: 0.72rem; }
.result-label { color: rgba(255, 255, 255, 0.6); }
.result-value { color: #a5b4fc; font-weight: 500; }
.result-value.highlight { color: #4ade80; }
.btn-delete {
  padding: 0.15rem 0.4rem; background: rgba(239, 68, 68, 0.1); color: rgba(239, 68, 68, 0.8);
  border: 1px solid rgba(239, 68, 68, 0.2); border-radius: 3px; font-size: 0.65rem; cursor: pointer;
}
.result-summary { font-size: 0.65rem; color: rgba(255, 255, 255, 0.4); text-align: center; padding-top: 0.3rem; }
.actions { display: flex; gap: 0.4rem; }
.btn-primary, .btn-secondary {
  flex: 1; padding: 0.4rem; border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px; font-size: 0.75rem; cursor: pointer; transition: all 0.2s ease;
}
.btn-primary { background: rgba(99, 102, 241, 0.2); color: #a5b4fc; }
.btn-primary:hover:not(:disabled) { background: rgba(99, 102, 241, 0.3); }
.btn-secondary { background: rgba(255, 255, 255, 0.1); color: rgba(255, 255, 255, 0.8); }
.btn-secondary:hover:not(:disabled) { background: rgba(255, 255, 255, 0.15); }
.btn-primary:disabled, .btn-secondary:disabled { opacity: 0.4; cursor: not-allowed; }
</style>
