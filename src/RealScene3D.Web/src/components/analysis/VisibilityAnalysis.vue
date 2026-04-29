<template>
  <div class="visibility-analysis">
    <div class="panel-header">
      <h4>通视分析</h4>
    </div>

    <div class="mode-selector">
      <button
        v-for="mode in modes"
        :key="mode.key"
        @click="currentMode = mode.key"
        :class="['mode-btn', { active: currentMode === mode.key }]"
      >
        <span class="mode-icon">{{ mode.icon }}</span>
        <span>{{ mode.name }}</span>
      </button>
    </div>

    <div class="params-section">
      <div class="param-item">
        <label>观察点高度 (米)</label>
        <input v-model.number="observerHeight" type="number" min="0" max="100" step="0.5" />
      </div>
      <div v-if="currentMode === 'circular'" class="param-item">
        <label>分析范围 (米)</label>
        <input v-model.number="analysisRadius" type="number" min="10" max="1000" step="10" />
      </div>
      <div class="param-item">
        <label>可视区域颜色</label>
        <input v-model="visibleColor" type="color" />
      </div>
      <div class="param-item">
        <label>不可视区域颜色</label>
        <input v-model="hiddenColor" type="color" />
      </div>
    </div>

    <div class="instructions">
      <p v-if="currentMode === 'linear'">
        <span class="step">1.</span> 点击地图设置观察点<br />
        <span class="step">2.</span> 再次点击设置目标点
      </p>
      <p v-else>
        <span class="step">1.</span> 点击地图设置观察点<br />
        <span class="step">2.</span> 系统自动分析圆周范围内的可视区域
      </p>
    </div>

    <div v-if="store.error" class="error-banner">
      <p>{{ store.error.message }}</p>
    </div>

    <div v-if="displayResults.length > 0" class="results-section">
      <h5>分析结果</h5>
      <div class="result-list">
        <div
          v-for="(item, index) in displayResults"
          :key="index"
          class="result-item"
        >
          <span class="result-label">{{ item.label }}</span>
          <span class="result-value">{{ item.value }}</span>
        </div>
      </div>
    </div>

    <div class="actions">
      <button
        @click="startAnalysis"
        class="btn-primary"
        :disabled="store.isAnalyzing"
      >
        {{ store.isAnalyzing ? '分析中...' : '开始分析' }}
      </button>
      <button @click="clearResults" class="btn-secondary">
        清除结果
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, toRef } from 'vue'
import { storeToRefs } from 'pinia'
import { useAnalysisStore } from '@/stores/analysis'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import type { VisibilityMode } from '@/types/analysis'

const props = defineProps<{
  viewerInstance?: any
  sceneObjects?: any[]
}>()

defineEmits<{
  close: []
}>()

const store = useAnalysisStore()
const { isAnalyzing } = storeToRefs(store)

const viewerRef = toRef(props, 'viewerInstance')
const { analyzeVisibility, clearSightline } = useAnalysisTool(viewerRef)

const currentMode = ref<VisibilityMode>('linear')
const observerHeight = ref(1.5)
const analysisRadius = ref(100)
const visibleColor = ref('#00ff00')
const hiddenColor = ref('#ff0000')
const lastResult = ref<any>(null)

const modes = [
  { key: 'linear' as const, name: '线通视', icon: '📏' },
  { key: 'circular' as const, name: '圆通视', icon: '⭕' }
]

const displayResults = computed(() => {
  if (!lastResult.value) {
    return []
  }
  const items: Array<{ label: string; value: string }> = [
    { label: '分析模式', value: currentMode.value === 'linear' ? '线通视' : '圆通视' },
    { label: '观察点高度', value: `${observerHeight.value} 米` },
    { label: '分析范围', value: currentMode.value === 'circular' ? `${analysisRadius.value} 米` : '两点连线' }
  ]
  if (currentMode.value === 'circular') {
    items.push({ label: '目标点数', value: `${lastResult.value.targets?.length || 0}` })
  }
  return items
})

async function startAnalysis() {
  const result = await analyzeVisibility(currentMode.value)
  if (result) {
    lastResult.value = result
  }
}

function clearResults() {
  clearSightline()
  store.clearByType('visibility')
  lastResult.value = null
}
</script>

<style scoped>
.visibility-analysis {
  padding: 0.6rem;
}
.panel-header { margin-bottom: 0.8rem; }
.panel-header h4 { margin: 0; font-size: 0.85rem; color: rgba(255, 255, 255, 0.9); }

.mode-selector { display: flex; gap: 0.4rem; margin-bottom: 0.8rem; }
.mode-btn {
  flex: 1; display: flex; align-items: center; justify-content: center; gap: 0.3rem;
  padding: 0.4rem; background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1); border-radius: 4px;
  color: rgba(255, 255, 255, 0.8); font-size: 0.75rem; cursor: pointer;
  transition: all 0.2s ease;
}
.mode-btn:hover { background: rgba(99, 102, 241, 0.15); }
.mode-btn.active {
  background: rgba(99, 102, 241, 0.25);
  border-color: rgba(99, 102, 241, 0.5); color: #a5b4fc;
}
.mode-icon { font-size: 1rem; }

.params-section { margin-bottom: 0.8rem; }
.param-item { display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.5rem; }
.param-item label { font-size: 0.75rem; color: rgba(255, 255, 255, 0.7); }
.param-item input[type="number"] {
  width: 80px; padding: 0.25rem 0.4rem; background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.2); border-radius: 4px;
  color: white; font-size: 0.75rem;
}
.param-item input[type="color"] {
  width: 40px; height: 24px; padding: 0; border: none; border-radius: 4px; cursor: pointer;
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

.results-section { margin-bottom: 0.8rem; }
.results-section h5 { margin: 0 0 0.5rem 0; font-size: 0.75rem; color: rgba(255, 255, 255, 0.8); }
.result-list { background: rgba(0, 0, 0, 0.2); border-radius: 6px; padding: 0.4rem; }
.result-item { display: flex; justify-content: space-between; padding: 0.3rem 0; font-size: 0.7rem; }
.result-label { color: rgba(255, 255, 255, 0.6); }
.result-value { color: #a5b4fc; font-weight: 500; }

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
