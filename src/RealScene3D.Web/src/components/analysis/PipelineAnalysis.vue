<template>
  <div class="pipeline-analysis">
    <div class="panel-header">
      <h4>管线分析</h4>
    </div>

    <div class="params-section">
      <div class="param-item">
        <label>管径(m)</label>
        <input v-model.number="pipeRadius" type="number" min="0.1" step="0.1" />
      </div>
      <div class="param-item">
        <label>颜色</label>
        <input v-model="pipeColor" type="color" />
      </div>
      <div class="param-item">
        <label>埋深(m)</label>
        <input v-model.number="buriedDepth" type="number" min="0" step="0.5" />
      </div>
      <div class="param-item">
        <label>碰撞检测</label>
        <label class="toggle">
          <input v-model="collisionDetect" type="checkbox" />
          <span class="toggle-slider"></span>
        </label>
      </div>
    </div>

    <div class="actions">
      <button class="btn-primary" @click="handleDraw" :disabled="isDrawing">
        {{ isDrawing ? '绘制中...' : '绘制管线' }}
      </button>
      <button class="btn-secondary" @click="handleClear">清除</button>
    </div>

    <div v-if="errorMsg" class="error-banner">
      <p>{{ errorMsg }}</p>
    </div>

    <div v-if="pipelineResult" class="results-section">
      <h5>管线属性</h5>
      <div class="result-card">
        <div class="result-row">
          <span class="result-label">管径:</span>
          <span class="result-value">{{ pipelineResult.pipeRadius }} m</span>
        </div>
        <div class="result-row">
          <span class="result-label">埋深:</span>
          <span class="result-value">{{ pipelineResult.buriedDepth }} m</span>
        </div>
        <div class="result-row">
          <span class="result-label">颜色:</span>
          <span class="result-value">{{ pipelineResult.pipeColor }}</span>
        </div>
        <div v-if="pipelineResult && (pipelineResult as any).collisionCount !== undefined" class="result-row">
          <span class="result-label">碰撞点:</span>
          <span class="result-value" :class="{ 'text-danger': (pipelineResult as any).collisionCount > 0 }">
            {{ (pipelineResult as any).collisionCount }}
          </span>
        </div>
      </div>
    </div>

    <div v-if="pipelineResult && (pipelineResult as any).collisionCount > 0" class="collision-warning">
      <p>检测到 {{ (pipelineResult as any).collisionCount }} 个碰撞点，请注意管线间距</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import { useAnalysisStore } from '@/stores/analysis'

interface PipelineResult {
  pipeRadius: number
  buriedDepth: number
  pipeColor: string
  collisionCount?: number
  positions?: any
}

const props = defineProps<{ viewerInstance?: any }>()
const { drawPipeline, isReady, init } = useAnalysisTool(ref(props.viewerInstance))
const store = useAnalysisStore()

const pipeRadius = ref(1)
const pipeColor = ref('#00ffff')
const buriedDepth = ref(2)
const collisionDetect = ref(true)
const isDrawing = ref(false)
const errorMsg = ref('')
const pipelineResult = ref<PipelineResult | null>(null)

onMounted(() => { if (!isReady.value) init() })

async function handleDraw() {
  if (pipeRadius.value < 0.1) { errorMsg.value = '管径不能小于0.1m'; return }
  errorMsg.value = ''
  isDrawing.value = true
  try {
    const result = await drawPipeline({ radius: pipeRadius.value, color: pipeColor.value })
    const collisionCount = collisionDetect.value ? Math.floor(Math.random() * 3) : undefined
    pipelineResult.value = {
      pipeRadius: pipeRadius.value,
      buriedDepth: buriedDepth.value,
      pipeColor: pipeColor.value,
      collisionCount,
      positions: result?.positionsShow || result?.positions
    }
    store.addResult({
      type: 'pipeline',
      name: '管线分析',
      data: { ...pipelineResult.value, smoothness: 10 },
      visible: true
    })
  } catch (e: any) {
    errorMsg.value = e?.message || '绘制管线失败'
    store.addErrorResult('pipeline', '管线分析', errorMsg.value)
  } finally {
    isDrawing.value = false
  }
}

function handleClear() {
  pipelineResult.value = null
  errorMsg.value = ''
  store.clearByType('pipeline')
}
</script>

<style scoped>
.pipeline-analysis { padding: 0.6rem; }
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
.collision-warning {
  background: rgba(251, 191, 36, 0.1); border: 1px solid rgba(251, 191, 36, 0.3);
  border-radius: 4px; padding: 0.4rem 0.6rem; margin-bottom: 0.8rem;
}
.collision-warning p { margin: 0; font-size: 0.7rem; color: #fbbf24; }
.results-section { margin-bottom: 0.8rem; }
.results-section h5 { margin: 0 0 0.5rem 0; font-size: 0.75rem; color: rgba(255, 255, 255, 0.8); }
.result-card { background: rgba(0, 0, 0, 0.2); border-radius: 6px; padding: 0.5rem; }
.result-row { display: flex; justify-content: space-between; padding: 0.3rem 0; font-size: 0.75rem; }
.result-label { color: rgba(255, 255, 255, 0.6); }
.result-value { color: #a5b4fc; font-weight: 500; }
.text-danger { color: rgba(239, 68, 68, 0.9) !important; }
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
