<template>
  <div class="height-measurement">
    <div class="panel-header"><h4>高度差测量</h4></div>

    <div class="instructions">
      <p><span class="step">1.</span> 点击"开始测量"<br /><span class="step">2.</span> 在地图上选择两个点<br /><span class="step">3.</span> 查看高度和高度差结果</p>
    </div>

    <div v-if="results.length > 0" class="results-section">
      <h5>测量结果</h5>
      <div v-for="(r, i) in results" :key="i" class="result-card">
        <div class="result-row">
          <span class="result-label">起点高度</span>
          <span class="result-value">{{ r.startHeight.toFixed(2) }} m</span>
        </div>
        <div class="result-row">
          <span class="result-label">终点高度</span>
          <span class="result-value">{{ r.endHeight.toFixed(2) }} m</span>
        </div>
        <div class="result-row">
          <span class="result-label">高度差</span>
          <span class="result-value highlight" :class="{ negative: r.heightDiff < 0 }">
            {{ r.heightDiff > 0 ? '+' : '' }}{{ r.heightDiff.toFixed(2) }} m
          </span>
        </div>
        <button class="btn-delete" @click="results.splice(i, 1)">删除</button>
      </div>
    </div>

    <div class="actions">
      <button class="btn-primary" @click="handleMeasure" :disabled="isMeasuring">
        {{ isMeasuring ? '请在地图上选择两点...' : '开始测量' }}
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
const { measureHeight, isReady, init } = useAnalysisTool(ref(props.viewerInstance))
const store = useAnalysisStore()

const isMeasuring = ref(false)
const results = ref<Array<{ startHeight: number; endHeight: number; heightDiff: number }>>([])

onMounted(() => { if (!isReady.value) init() })

async function handleMeasure() {
  isMeasuring.value = true
  try {
    const result = await measureHeight()
    if (result) {
      results.value.push(result)
      store.addResult({ type: 'height', name: `高度差-${results.value.length}`, data: result, visible: true })
    }
  } catch (e) {
    console.error('高度差测量失败:', e)
  } finally {
    isMeasuring.value = false
  }
}
</script>

<style scoped>
.height-measurement { padding: 0.6rem; }
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
.result-card {
  background: rgba(0, 0, 0, 0.2); border-radius: 6px; padding: 0.4rem 0.5rem; margin-bottom: 0.3rem;
}
.result-row { display: flex; justify-content: space-between; padding: 0.2rem 0; font-size: 0.72rem; }
.result-label { color: rgba(255, 255, 255, 0.6); }
.result-value { color: #a5b4fc; font-weight: 500; }
.result-value.highlight { color: #4ade80; }
.result-value.negative { color: rgba(239, 68, 68, 0.9); }
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
