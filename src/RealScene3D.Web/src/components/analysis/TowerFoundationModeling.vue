<template>
  <div class="tower-foundation">
    <div class="panel-header">
      <h4>塔基建模</h4>
    </div>

    <div class="params-section">
      <div class="param-item">
        <label>杆高(m)</label>
        <input v-model.number="poleHeight" type="number" min="1" />
      </div>
      <div class="param-item">
        <label>半径(m)</label>
        <input v-model.number="poleRadius" type="number" min="0.1" step="0.1" />
      </div>
      <div class="param-item">
        <label>颜色</label>
        <input v-model="poleColor" type="color" />
      </div>
      <div class="param-item">
        <label>分段数</label>
        <input v-model.number="segments" type="number" min="3" max="64" />
      </div>
    </div>

    <div class="preview-info">
      <h5>3D预览参数</h5>
      <div class="result-card">
        <div class="result-row">
          <span class="result-label">高度:</span>
          <span class="result-value">{{ poleHeight }} m</span>
        </div>
        <div class="result-row">
          <span class="result-label">底面周长:</span>
          <span class="result-value">{{ (2 * Math.PI * poleRadius).toFixed(2) }} m</span>
        </div>
        <div class="result-row">
          <span class="result-label">底面面积:</span>
          <span class="result-value">{{ (Math.PI * poleRadius * poleRadius).toFixed(2) }} m²</span>
        </div>
        <div class="result-row">
          <span class="result-label">分段数:</span>
          <span class="result-value">{{ segments }}</span>
        </div>
      </div>
    </div>

    <div v-if="errorMsg" class="error-banner">
      <p>{{ errorMsg }}</p>
    </div>

    <div class="actions">
      <button class="btn-primary" @click="handleDraw" :disabled="isDrawing">
        {{ isDrawing ? '绘制中...' : '绘制线杆' }}
      </button>
      <button class="btn-secondary" @click="handleClear">清除</button>
    </div>

    <div v-if="lastResult" class="results-section">
      <h5>建模结果</h5>
      <div class="result-card">
        <div class="result-row">
          <span class="result-label">位置:</span>
          <span class="result-value">{{ lastResult.position }}</span>
        </div>
        <div class="result-row">
          <span class="result-label">状态:</span>
          <span class="result-value text-success">已完成</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import { useAnalysisStore } from '@/stores/analysis'

const props = defineProps<{ viewerInstance?: any }>()
const { drawTowerPole, isReady, init } = useAnalysisTool(ref(props.viewerInstance))
const store = useAnalysisStore()

const poleHeight = ref(30)
const poleRadius = ref(0.5)
const poleColor = ref('#ff0000')
const segments = ref(16)
const isDrawing = ref(false)
const errorMsg = ref('')
const lastResult = ref<{ position: string } | null>(null)

onMounted(() => { if (!isReady.value) init() })

async function handleDraw() {
  if (poleHeight.value < 1) { errorMsg.value = '杆高不能小于1m'; return }
  if (poleRadius.value < 0.1) { errorMsg.value = '半径不能小于0.1m'; return }
  errorMsg.value = ''
  isDrawing.value = true
  try {
    const result = await drawTowerPole({ height: poleHeight.value, radius: poleRadius.value, color: poleColor.value })
    const posStr = result?.positionShow ? `${result.positionShow[0]?.toFixed(4)}, ${result.positionShow[1]?.toFixed(4)}` : '已放置'
    lastResult.value = { position: posStr }
    store.addResult({
      type: 'tower-foundation',
      name: '塔基建模',
      data: { position: result?.positionShow, poleHeight: poleHeight.value, poleRadius: poleRadius.value, poleColor: poleColor.value, segments: segments.value },
      visible: true
    })
  } catch (e: any) {
    errorMsg.value = e?.message || '绘制线杆失败'
    store.addErrorResult('tower-foundation', '塔基建模', errorMsg.value)
  } finally {
    isDrawing.value = false
  }
}

function handleClear() {
  lastResult.value = null
  store.clearByType('tower-foundation')
}
</script>

<style scoped>
.tower-foundation { padding: 0.6rem; }
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
.preview-info { margin-bottom: 0.8rem; }
.preview-info h5 { margin: 0 0 0.5rem 0; font-size: 0.7rem; color: rgba(255, 255, 255, 0.6); }
.result-card { background: rgba(0, 0, 0, 0.2); border-radius: 6px; padding: 0.5rem; }
.result-row { display: flex; justify-content: space-between; padding: 0.3rem 0; font-size: 0.75rem; }
.result-label { color: rgba(255, 255, 255, 0.6); }
.result-value { color: #a5b4fc; font-weight: 500; }
.text-success { color: #4ade80; }
.error-banner {
  background: rgba(239, 68, 68, 0.15); border: 1px solid rgba(239, 68, 68, 0.3);
  border-radius: 4px; padding: 0.4rem 0.6rem; margin-bottom: 0.8rem;
}
.error-banner p { margin: 0; font-size: 0.7rem; color: rgba(239, 68, 68, 0.9); }
.results-section { margin-top: 0.8rem; }
.results-section h5 { margin: 0 0 0.5rem 0; font-size: 0.75rem; color: rgba(255, 255, 255, 0.8); }
.actions { display: flex; gap: 0.4rem; }
.btn-primary,
.btn-secondary {
  flex: 1; padding: 0.4rem; border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px; font-size: 0.75rem; cursor: pointer; transition: all 0.2s ease;
}
.btn-primary { background: rgba(99, 102, 241, 0.2); color: #a5b4fc; }
.btn-primary:hover:not(:disabled) { background: rgba(99, 102, 241, 0.3); }
.btn-primary:disabled { opacity: 0.5; cursor: not-allowed; }
.btn-secondary { background: rgba(255, 255, 255, 0.1); color: rgba(255, 255, 255, 0.8); }
.btn-secondary:hover { background: rgba(255, 255, 255, 0.15); }
</style>
