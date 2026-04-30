<template>
  <div class="contour-line-analysis">
    <div class="panel-header">
      <h4>等高线分析</h4>
    </div>

    <div class="params-section">
      <div class="param-item">
        <label>等高距(m)</label>
        <input v-model.number="spacing" type="number" min="1" step="5" />
      </div>
      <div class="param-item">
        <label>线宽</label>
        <input v-model.number="lineWidth" type="number" min="1" max="10" />
      </div>
      <div class="param-item">
        <label>颜色</label>
        <input v-model="lineColor" type="color" />
      </div>
      <div class="param-item">
        <label>显示标注</label>
        <label class="toggle">
          <input v-model="showLabel" type="checkbox" />
          <span class="toggle-slider"></span>
        </label>
      </div>
    </div>

    <div v-if="errorMsg" class="error-banner">
      <p>{{ errorMsg }}</p>
    </div>

    <div class="actions">
      <button class="btn-primary" @click="handleGenerate" :disabled="isGenerating">
        {{ isGenerating ? '生成中...' : '生成等高线' }}
      </button>
    </div>
    <div class="actions" style="margin-top: 0.4rem;">
      <button class="btn-secondary" @click="handleToggleVisible">
        {{ visible ? '隐藏' : '显示' }}
      </button>
      <button class="btn-danger" @click="handleClear">清除</button>
    </div>

    <div v-if="contourResult" class="results-section">
      <h5>分析结果</h5>
      <div class="result-card">
        <div class="result-row">
          <span class="result-label">等高距:</span>
          <span class="result-value">{{ contourResult.contourSpacing }} m</span>
        </div>
        <div class="result-row">
          <span class="result-label">线宽:</span>
          <span class="result-value">{{ contourResult.lineWidth }} px</span>
        </div>
        <div class="result-row">
          <span class="result-label">颜色:</span>
          <span class="result-value">{{ contourResult.lineColor }}</span>
        </div>
        <div class="result-row">
          <span class="result-label">显示标注:</span>
          <span class="result-value">{{ contourResult.showLabel ? '是' : '否' }}</span>
        </div>
      </div>
    </div>

    <div v-if="storeResults.length > 0" class="history-section">
      <h5>历史记录 ({{ storeResults.length }})</h5>
      <div v-for="r in storeResults.slice(-3).reverse()" :key="r.id" class="history-item">
        <span class="history-label">{{ r.name }}</span>
        <span class="history-time">{{ formatTime(r.timestamp) }}</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import { useAnalysisStore } from '@/stores/analysis'
import type { ContourLineResultData } from '@/types/analysis'

const props = defineProps<{ viewerInstance?: any }>()
const { generateContourLine, toggleContourVisible, clearContour, isReady, init } = useAnalysisTool(ref(props.viewerInstance))
const store = useAnalysisStore()

const spacing = ref(10)
const lineWidth = ref(2)
const lineColor = ref('#ff0000')
const showLabel = ref(true)
const isGenerating = ref(false)
const visible = ref(true)
const errorMsg = ref('')
const contourResult = ref<ContourLineResultData | null>(null)

const storeResults = computed(() => store.getResultsByType('contour'))

onMounted(() => { if (!isReady.value) init() })

function formatTime(d: Date): string {
  return `${(d as Date).getHours().toString().padStart(2, '0')}:${(d as Date).getMinutes().toString().padStart(2, '0')}`
}

function validate(): boolean {
  if (spacing.value < 1) { errorMsg.value = '等高距不能小于1'; return false }
  if (lineWidth.value < 1 || lineWidth.value > 10) { errorMsg.value = '线宽范围1-10'; return false }
  errorMsg.value = ''
  return true
}

async function handleGenerate() {
  if (!validate()) return
  isGenerating.value = true
  try {
    const result = await generateContourLine({ spacing: spacing.value, lineWidth: lineWidth.value, lineColor: lineColor.value, showLabel: showLabel.value })
    contourResult.value = { contourSpacing: spacing.value, lineWidth: lineWidth.value, lineColor: lineColor.value, regionPositions: result, showLabel: showLabel.value }
    store.addResult({ type: 'contour', name: '等高线分析', data: contourResult.value, visible: true })
  } catch (e: any) {
    errorMsg.value = e?.message || '等高线生成失败'
    store.addErrorResult('contour', '等高线分析', errorMsg.value)
  } finally {
    isGenerating.value = false
  }
}

function handleToggleVisible() {
  visible.value = !visible.value
  toggleContourVisible(visible.value)
}

function handleClear() {
  clearContour()
  contourResult.value = null
  store.clearByType('contour')
}
</script>

<style scoped>
.contour-line-analysis { padding: 0.6rem; }
.panel-header { margin-bottom: 0.8rem; }
.panel-header h4 { margin: 0; font-size: 0.85rem; color: rgba(255, 255, 255, 0.9); }
.params-section { margin-bottom: 0.8rem; }
.param-item { display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.5rem; }
.param-item label { font-size: 0.75rem; color: rgba(255, 255, 255, 0.7); }
.param-item input[type="number"],
.param-item input[type="color"] {
  width: 100px; padding: 0.25rem 0.4rem; background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.2); border-radius: 4px;
  color: rgba(255, 255, 255, 0.9); font-size: 0.75rem;
}
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
.results-section { margin-bottom: 0.8rem; }
.results-section h5 { margin: 0 0 0.5rem 0; font-size: 0.75rem; color: rgba(255, 255, 255, 0.8); }
.result-card { background: rgba(0, 0, 0, 0.2); border-radius: 6px; padding: 0.5rem; }
.result-row { display: flex; justify-content: space-between; padding: 0.3rem 0; font-size: 0.75rem; }
.result-label { color: rgba(255, 255, 255, 0.6); }
.result-value { color: #a5b4fc; font-weight: 500; }
.history-section { margin-top: 0.8rem; }
.history-section h5 { margin: 0 0 0.4rem 0; font-size: 0.7rem; color: rgba(255, 255, 255, 0.6); }
.history-item { display: flex; justify-content: space-between; padding: 0.2rem 0; font-size: 0.7rem; }
.history-label { color: rgba(255, 255, 255, 0.7); }
.history-time { color: rgba(255, 255, 255, 0.4); }
.actions { display: flex; gap: 0.4rem; }
.btn-primary,
.btn-secondary,
.btn-danger {
  flex: 1; padding: 0.4rem; border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px; font-size: 0.75rem; cursor: pointer; transition: all 0.2s ease;
}
.btn-primary { background: rgba(99, 102, 241, 0.2); color: #a5b4fc; }
.btn-primary:hover:not(:disabled) { background: rgba(99, 102, 241, 0.3); }
.btn-primary:disabled { opacity: 0.5; cursor: not-allowed; }
.btn-secondary { background: rgba(255, 255, 255, 0.1); color: rgba(255, 255, 255, 0.8); }
.btn-secondary:hover { background: rgba(255, 255, 255, 0.15); }
.btn-danger { background: rgba(239, 68, 68, 0.15); color: rgba(239, 68, 68, 0.9); }
.btn-danger:hover { background: rgba(239, 68, 68, 0.25); }
</style>
