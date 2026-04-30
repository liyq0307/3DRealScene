<template>
  <div class="bearing-measurement">
    <div class="panel-header"><h4>方位角测量</h4></div>

    <div class="instructions">
      <p>选择2个点，计算从起点到终点相对于正北方向的顺时针方位角 (0°-360°)</p>
    </div>

    <div v-if="result" class="results-section">
      <h5>测量结果</h5>
      <div class="result-card">
        <div class="result-row">
          <span class="result-label">方位角</span>
          <span class="result-value highlight">{{ (result.bearing ?? result.angle ?? 0).toFixed(2) }}°</span>
        </div>
        <div v-if="result.distance" class="result-row">
          <span class="result-label">两点距离</span>
          <span class="result-value">{{ formatDistance(result.distance) }}</span>
        </div>
      </div>
    </div>

    <div class="actions">
      <button class="btn-primary" @click="handleMeasure" :disabled="isMeasuring">
        {{ isMeasuring ? '请选择2个点...' : '开始测量' }}
      </button>
      <button class="btn-secondary" @click="result = null" :disabled="!result">清除</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import { useAnalysisStore } from '@/stores/analysis'

const props = defineProps<{ viewerInstance?: any }>()
const { measureAngle, isReady, init } = useAnalysisTool(ref(props.viewerInstance))
const store = useAnalysisStore()

const isMeasuring = ref(false)
const result = ref<Record<string, any> | null>(null)

onMounted(() => { if (!isReady.value) init() })

async function handleMeasure() {
  isMeasuring.value = true
  try {
    const r = await measureAngle()
    if (r) {
      result.value = r
      store.addResult({ type: 'bearing', name: '方位角测量', data: r, visible: true })
    }
  } catch (e) {
    console.error('方位角测量失败:', e)
  } finally {
    isMeasuring.value = false
  }
}

function formatDistance(d: number): string {
  return d >= 1000 ? `${(d / 1000).toFixed(3)} km` : `${d.toFixed(2)} m`
}
</script>

<style scoped>
.bearing-measurement { padding: 0.6rem; }
.panel-header { margin-bottom: 0.8rem; }
.panel-header h4 { margin: 0; font-size: 0.85rem; color: rgba(255, 255, 255, 0.9); }
.instructions {
  background: rgba(99, 102, 241, 0.1); border: 1px solid rgba(99, 102, 241, 0.2);
  border-radius: 6px; padding: 0.5rem; margin-bottom: 0.8rem;
}
.instructions p { margin: 0; font-size: 0.7rem; color: rgba(255, 255, 255, 0.8); }
.results-section { margin-bottom: 0.8rem; }
.results-section h5 { margin: 0 0 0.5rem 0; font-size: 0.75rem; color: rgba(255, 255, 255, 0.8); }
.result-card { background: rgba(0, 0, 0, 0.2); border-radius: 6px; padding: 0.5rem; }
.result-row { display: flex; justify-content: space-between; padding: 0.2rem 0; font-size: 0.72rem; }
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
