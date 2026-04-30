<template>
  <div class="coordinate-measure">
    <div class="panel-header"><h4>坐标测量</h4></div>

    <div class="params-section">
      <div class="param-item">
        <label>坐标格式</label>
        <div class="radio-group">
          <button class="radio-btn" :class="{ active: format === 'decimal' }" @click="format = 'decimal'">十进制</button>
          <button class="radio-btn" :class="{ active: format === 'dms' }" @click="format = 'dms'">度分秒</button>
        </div>
      </div>
    </div>

    <div class="instructions">
      <p>鼠标移动实时显示坐标，点击固定标记</p>
    </div>

    <div v-if="results.length > 0" class="results-section">
      <h5>测量点 ({{ results.length }})</h5>
      <div v-for="(r, i) in results" :key="i" class="result-card">
        <div class="result-row">
          <span class="result-label">经度</span>
          <span class="result-value">{{ format === 'dms' ? toDMS(r.longitude, 'E') : r.longitude.toFixed(6) }}</span>
        </div>
        <div class="result-row">
          <span class="result-label">纬度</span>
          <span class="result-value">{{ format === 'dms' ? toDMS(r.latitude, 'N') : r.latitude.toFixed(6) }}</span>
        </div>
        <div class="result-row">
          <span class="result-label">海拔</span>
          <span class="result-value">{{ r.height.toFixed(2) }} m</span>
        </div>
      </div>
    </div>

    <div class="actions">
      <button class="btn-primary" @click="handleMeasure" :disabled="isMeasuring">
        {{ isMeasuring ? '请点击地图...' : '开始测量' }}
      </button>
      <button class="btn-secondary" @click="handleCopyAll" :disabled="results.length === 0">复制全部</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import { useAnalysisStore } from '@/stores/analysis'

const props = defineProps<{ viewerInstance?: any }>()
const { measurePoint, isReady, init } = useAnalysisTool(ref(props.viewerInstance))
const store = useAnalysisStore()

const format = ref<'decimal' | 'dms'>('decimal')
const isMeasuring = ref(false)
const results = ref<Array<{ longitude: number; latitude: number; height: number }>>([])

onMounted(() => { if (!isReady.value) init() })

async function handleMeasure() {
  isMeasuring.value = true
  try {
    const result = await measurePoint()
    if (result) {
      results.value.push(result)
      store.addResult({ type: 'coordinate', name: `坐标点-${results.value.length}`, data: result, visible: true })
    }
  } catch (e) {
    console.error('坐标测量失败:', e)
  } finally {
    isMeasuring.value = false
  }
}

function toDMS(decimal: number, dir: string): string {
  const d = Math.floor(Math.abs(decimal))
  const m = Math.floor((Math.abs(decimal) - d) * 60)
  const s = ((Math.abs(decimal) - d) * 60 - m) * 60
  const prefix = decimal >= 0 ? dir : dir === 'E' ? 'W' : 'S'
  return `${prefix}${d}°${m}'${s.toFixed(2)}"`
}

function handleCopyAll() {
  const text = results.value.map(r => `${r.longitude.toFixed(6)}, ${r.latitude.toFixed(6)}, ${r.height.toFixed(2)}`).join('\n')
  navigator.clipboard.writeText(text)
}
</script>

<style scoped>
.coordinate-measure { padding: 0.6rem; }
.panel-header { margin-bottom: 0.8rem; }
.panel-header h4 { margin: 0; font-size: 0.85rem; color: rgba(255, 255, 255, 0.9); }
.params-section { margin-bottom: 0.6rem; }
.param-item { display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.5rem; }
.param-item label { font-size: 0.75rem; color: rgba(255, 255, 255, 0.7); }
.radio-group { display: flex; gap: 4px; }
.radio-btn {
  padding: 0.2rem 0.5rem; background: rgba(255, 255, 255, 0.08);
  border: 1px solid rgba(255, 255, 255, 0.15); border-radius: 4px;
  color: rgba(255, 255, 255, 0.7); font-size: 0.7rem; cursor: pointer;
}
.radio-btn.active { background: rgba(99, 102, 241, 0.2); color: #a5b4fc; border-color: rgba(99, 102, 241, 0.4); }
.instructions {
  background: rgba(99, 102, 241, 0.1); border: 1px solid rgba(99, 102, 241, 0.2);
  border-radius: 6px; padding: 0.5rem; margin-bottom: 0.8rem;
}
.instructions p { margin: 0; font-size: 0.7rem; color: rgba(255, 255, 255, 0.8); }
.results-section { margin-bottom: 0.8rem; max-height: 200px; overflow-y: auto; }
.results-section h5 { margin: 0 0 0.5rem 0; font-size: 0.75rem; color: rgba(255, 255, 255, 0.8); }
.result-card { background: rgba(0, 0, 0, 0.2); border-radius: 6px; padding: 0.35rem 0.5rem; margin-bottom: 0.25rem; }
.result-row { display: flex; justify-content: space-between; font-size: 0.7rem; padding: 0.1rem 0; }
.result-label { color: rgba(255, 255, 255, 0.6); }
.result-value { color: #a5b4fc; font-weight: 500; }
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
