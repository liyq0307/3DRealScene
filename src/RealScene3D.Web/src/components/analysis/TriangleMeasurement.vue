<template>
  <div class="triangle-measurement">
    <div class="panel-header"><h4>三角测量</h4></div>

    <div class="instructions">
      <p><span class="step">1.</span> 选择3个点<br /><span class="step">2.</span> 查看三边长度、角度和面积</p>
    </div>

    <div v-if="result" class="results-section">
      <h5>测量结果</h5>
      <div class="result-card">
        <div class="result-row"><span class="result-label">边AB</span><span class="result-value">{{ result.sideAB?.toFixed(2) ?? '-' }} m</span></div>
        <div class="result-row"><span class="result-label">边BC</span><span class="result-value">{{ result.sideBC?.toFixed(2) ?? '-' }} m</span></div>
        <div class="result-row"><span class="result-label">边CA</span><span class="result-value">{{ result.sideCA?.toFixed(2) ?? '-' }} m</span></div>
        <div class="result-divider"></div>
        <div class="result-row"><span class="result-label">角A</span><span class="result-value">{{ result.angleA?.toFixed(2) ?? '-' }}°</span></div>
        <div class="result-row"><span class="result-label">角B</span><span class="result-value">{{ result.angleB?.toFixed(2) ?? '-' }}°</span></div>
        <div class="result-row"><span class="result-label">角C</span><span class="result-value">{{ result.angleC?.toFixed(2) ?? '-' }}°</span></div>
        <div class="result-divider"></div>
        <div class="result-row"><span class="result-label">面积</span><span class="result-value highlight">{{ result.area?.toFixed(2) ?? '-' }} m²</span></div>
      </div>
    </div>

    <div class="actions">
      <button class="btn-primary" @click="handleMeasure" :disabled="isMeasuring">
        {{ isMeasuring ? '请选择3个点...' : '开始测量' }}
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
const { measureHeightTriangle, isReady, init } = useAnalysisTool(ref(props.viewerInstance))
const store = useAnalysisStore()

const isMeasuring = ref(false)
const result = ref<Record<string, any> | null>(null)

onMounted(() => { if (!isReady.value) init() })

async function handleMeasure() {
  isMeasuring.value = true
  try {
    const r = await measureHeightTriangle()
    if (r) {
      result.value = r
      store.addResult({ type: 'height' as any, name: '三角测量', data: r, visible: true })
    }
  } catch (e) {
    console.error('三角测量失败:', e)
  } finally {
    isMeasuring.value = false
  }
}
</script>

<style scoped>
.triangle-measurement { padding: 0.6rem; }
.panel-header { margin-bottom: 0.8rem; }
.panel-header h4 { margin: 0; font-size: 0.85rem; color: rgba(255, 255, 255, 0.9); }
.instructions {
  background: rgba(99, 102, 241, 0.1); border: 1px solid rgba(99, 102, 241, 0.2);
  border-radius: 6px; padding: 0.5rem; margin-bottom: 0.8rem;
}
.instructions p { margin: 0; font-size: 0.7rem; color: rgba(255, 255, 255, 0.8); line-height: 1.5; }
.step { font-weight: 600; color: #a5b4fc; }
.results-section { margin-bottom: 0.8rem; }
.results-section h5 { margin: 0 0 0.5rem 0; font-size: 0.75rem; color: rgba(255, 255, 255, 0.8); }
.result-card { background: rgba(0, 0, 0, 0.2); border-radius: 6px; padding: 0.5rem; }
.result-row { display: flex; justify-content: space-between; padding: 0.2rem 0; font-size: 0.72rem; }
.result-label { color: rgba(255, 255, 255, 0.6); }
.result-value { color: #a5b4fc; font-weight: 500; }
.result-value.highlight { color: #4ade80; }
.result-divider { height: 1px; background: rgba(255, 255, 255, 0.1); margin: 0.3rem 0; }
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
