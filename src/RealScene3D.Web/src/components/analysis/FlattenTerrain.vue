<template>
  <div class="flatten-terrain">
    <div class="panel-header">
      <h4>压平功能</h4>
    </div>

    <div class="params-section">
      <div class="param-item">
        <label>图层基准高(m)</label>
        <input v-model.number="baseHeight" type="number" step="1" @blur="handleUpdateHeight" />
      </div>
      <div class="param-item">
        <label>建筑压平高(m)</label>
        <input v-model.number="flatHeight" type="number" step="1" @blur="handleUpdateHeight" />
      </div>
    </div>

    <div class="instructions">
      <p>
        <span class="step">1.</span> 设置压平高度参数<br />
        <span class="step">2.</span> 点击"建筑压平"在地图上绘制区域<br />
        <span class="step">3.</span> 可随时调整高度并更新
      </p>
    </div>

    <div v-if="store.error" class="error-banner">
      <p>{{ store.error.message }}</p>
    </div>

    <div v-if="isFlattening" class="status-section">
      <div class="status-card">
        <div class="status-row">
          <span class="status-label">压平状态</span>
          <span class="status-active">进行中</span>
        </div>
        <div class="status-row">
          <span class="status-label">基准高度</span>
          <span class="status-value">{{ baseHeight }} m</span>
        </div>
        <div class="status-row">
          <span class="status-label">压平高度</span>
          <span class="status-value">{{ flatHeight }} m</span>
        </div>
        <div class="status-row">
          <span class="status-label">已压平区域</span>
          <span class="status-value">{{ flattenCount }} 个</span>
        </div>
      </div>
    </div>

    <div class="actions">
      <button class="btn-primary" @click="handleStartFlatten" :disabled="store.isAnalyzing">
        建筑压平
      </button>
    </div>
    <div class="actions" style="margin-top: 0.3rem;">
      <button class="btn-secondary" @click="handleUpdateHeight" :disabled="!isFlattening">更新高度</button>
      <button class="btn-danger" @click="handleClear" :disabled="!isFlattening">清除压平</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import { useAnalysisStore } from '@/stores/analysis'

const props = defineProps<{ viewerInstance?: any }>()
const { startFlatten, updateFlattenHeight, clearFlatten, isReady, init } = useAnalysisTool(ref(props.viewerInstance))
const store = useAnalysisStore()

const baseHeight = ref(0)
const flatHeight = ref(0)
const isFlattening = ref(false)
const flattenCount = ref(0)

onMounted(() => { if (!isReady.value) init() })

async function handleStartFlatten() {
  const result = await startFlatten({ height: flatHeight.value })
  if (result) {
    isFlattening.value = true
    flattenCount.value++
    store.addResult({
      type: 'flatten',
      name: `压平-${flattenCount.value}`,
      data: { flatHeight: flatHeight.value, baseHeight: baseHeight.value, region: result },
      visible: true
    })
  }
}

function handleUpdateHeight() {
  if (isFlattening.value) {
    updateFlattenHeight(flatHeight.value)
  }
}

function handleClear() {
  clearFlatten()
  isFlattening.value = false
  flattenCount.value = 0
  store.clearByType('flatten')
}
</script>

<style scoped>
.flatten-terrain { padding: 0.6rem; }
.panel-header { margin-bottom: 0.8rem; }
.panel-header h4 { margin: 0; font-size: 0.85rem; color: rgba(255, 255, 255, 0.9); }

.params-section { margin-bottom: 0.8rem; }
.param-item { display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.5rem; }
.param-item label { font-size: 0.75rem; color: rgba(255, 255, 255, 0.7); }
.param-item input {
  width: 100px; padding: 0.25rem 0.4rem; background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.2); border-radius: 4px;
  color: white; font-size: 0.75rem;
}

.instructions {
  background: rgba(99, 102, 241, 0.1); border: 1px solid rgba(99, 102, 241, 0.2);
  border-radius: 6px; padding: 0.6rem; margin-bottom: 0.8rem;
}
.instructions p { margin: 0; font-size: 0.7rem; color: rgba(255, 255, 255, 0.8); line-height: 1.5; }
.step { font-weight: 600; color: #a5b4fc; }

.error-banner {
  background: rgba(239, 68, 68, 0.15); border: 1px solid rgba(239, 68, 68, 0.3);
  border-radius: 4px; padding: 0.4rem 0.6rem; margin-bottom: 0.8rem;
}
.error-banner p { margin: 0; font-size: 0.7rem; color: rgba(239, 68, 68, 0.9); }

.status-section { margin-bottom: 0.8rem; }
.status-card { background: rgba(0, 0, 0, 0.2); border-radius: 6px; padding: 0.5rem; }
.status-row { display: flex; justify-content: space-between; padding: 0.2rem 0; font-size: 0.72rem; }
.status-label { color: rgba(255, 255, 255, 0.6); }
.status-active { color: #4ade80; font-weight: 500; }
.status-value { color: #a5b4fc; font-weight: 500; }

.actions { display: flex; gap: 0.4rem; }
.btn-primary, .btn-secondary, .btn-danger {
  flex: 1; padding: 0.4rem; border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px; font-size: 0.75rem; cursor: pointer; transition: all 0.2s ease;
}
.btn-primary { background: rgba(99, 102, 241, 0.2); color: #a5b4fc; }
.btn-primary:hover:not(:disabled) { background: rgba(99, 102, 241, 0.3); }
.btn-secondary { background: rgba(255, 255, 255, 0.1); color: rgba(255, 255, 255, 0.8); }
.btn-secondary:hover:not(:disabled) { background: rgba(255, 255, 255, 0.15); }
.btn-danger { background: rgba(239, 68, 68, 0.15); color: rgba(239, 68, 68, 0.9); }
.btn-danger:hover:not(:disabled) { background: rgba(239, 68, 68, 0.25); }
.btn-primary:disabled, .btn-secondary:disabled, .btn-danger:disabled { opacity: 0.4; cursor: not-allowed; }
</style>
