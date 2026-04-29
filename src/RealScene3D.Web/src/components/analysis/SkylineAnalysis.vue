<template>
  <div class="skyline-analysis">
    <div class="panel-header">
      <h4>天际线分析</h4>
    </div>

    <div class="params-section">
      <div class="param-item">
        <label>天际线宽度</label>
        <input v-model.number="lineWidth" type="range" min="1" max="10" step="1" />
        <span class="param-value">{{ lineWidth }} px</span>
      </div>
      <div class="param-item">
        <label>天际线颜色</label>
        <input v-model="lineColor" type="color" />
      </div>
      <div class="param-item">
        <label>显示模式</label>
        <select v-model="displayMode">
          <option value="2d">2D 投影</option>
          <option value="3d">3D 空间</option>
        </select>
      </div>
    </div>

    <div v-if="store.error" class="error-banner">
      <p>{{ store.error.message }}</p>
    </div>

    <div v-if="skylineData" class="preview-section">
      <h5>天际线预览</h5>
      <div class="skyline-chart">
        <LineChart
          v-if="chartData.length > 0"
          :data="chartData"
          :width="280"
          :height="120"
          :color="lineColor"
          :show-legend="false"
        />
      </div>
      <div class="skyline-stats">
        <div class="stat-item">
          <span class="stat-label">最高点:</span>
          <span class="stat-value">{{ skylineData.maxHeight.toFixed(1) }} 米</span>
        </div>
        <div class="stat-item">
          <span class="stat-label">最低点:</span>
          <span class="stat-value">{{ skylineData.minHeight.toFixed(1) }} 米</span>
        </div>
        <div class="stat-item">
          <span class="stat-label">起伏度:</span>
          <span class="stat-value">{{ skylineData.variance.toFixed(2) }}</span>
        </div>
      </div>
    </div>

    <div class="actions">
      <button @click="generateSkyline" class="btn-primary">
        {{ skylineData ? '更新天际线' : '生成天际线' }}
      </button>
      <button @click="changeColor" class="btn-secondary">随机换色</button>
      <button @click="exportSkyline" class="btn-secondary" :disabled="!skylineData">
        导出
      </button>
      <button @click="clearSkyline" class="btn-secondary">清除</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, toRef } from 'vue'
import { useAnalysisStore } from '@/stores/analysis'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import LineChart from '@/components/LineChart.vue'
import type { SkylineResultData, SkylineDisplayMode } from '@/types/analysis'

const props = defineProps<{
  viewerInstance?: any
}>()

const store = useAnalysisStore()
const viewerRef = toRef(props, 'viewerInstance')
const { analyzeSkyline, setSkylineWidth, setSkylineColor, clearSkyline: clearSkylineTool } = useAnalysisTool(viewerRef)

const lineWidth = ref(5)
const lineColor = ref('#ff6b6b')
const displayMode = ref<SkylineDisplayMode>('2d')
const skylineData = ref<SkylineResultData | null>(null)

const chartData = computed(() => {
  if (!skylineData.value) return []
  return skylineData.value.points.map((p) => ({
    time: `${p.angle.toFixed(0)}°`,
    value: p.height
  }))
})

function generateSkyline() {
  setSkylineWidth(lineWidth.value)
  setSkylineColor(lineColor.value)

  const result = analyzeSkyline()
  if (result) {
    skylineData.value = result
  }
}

function changeColor() {
  const colors = ['#ff6b6b', '#4ecdc4', '#45b7d1', '#f9ca24', '#6c5ce7', '#a29bfe']
  lineColor.value = colors[Math.floor(Math.random() * colors.length)]
  setSkylineColor(lineColor.value)
}

function exportSkyline() {
  if (!skylineData.value) return
  const data = {
    timestamp: new Date().toISOString(),
    params: { lineWidth: lineWidth.value, lineColor: lineColor.value, displayMode: displayMode.value },
    ...skylineData.value
  }
  const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `skyline_${Date.now()}.json`
  a.click()
  URL.revokeObjectURL(url)
}

function clearSkyline() {
  skylineData.value = null
  clearSkylineTool()
  store.clearByType('skyline')
}
</script>

<style scoped>
.skyline-analysis { padding: 0.6rem; }
.panel-header { margin-bottom: 0.8rem; }
.panel-header h4 { margin: 0; font-size: 0.85rem; color: rgba(255, 255, 255, 0.9); }

.params-section { margin-bottom: 0.8rem; }
.param-item { display: flex; align-items: center; gap: 0.4rem; margin-bottom: 0.6rem; }
.param-item label { flex: 0 0 80px; font-size: 0.75rem; color: rgba(255, 255, 255, 0.7); }
.param-item input[type="range"] { flex: 1; }
.param-value { width: 40px; font-size: 0.7rem; color: #a5b4fc; }
.param-item input[type="color"] {
  width: 40px; height: 24px; padding: 0; border: none; border-radius: 4px; cursor: pointer;
}
.param-item select {
  flex: 1; padding: 0.3rem; background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.2); border-radius: 4px;
  color: white; font-size: 0.75rem;
}
.param-item select option { background: #1a1a1a; }

.error-banner {
  background: rgba(239, 68, 68, 0.15); border: 1px solid rgba(239, 68, 68, 0.3);
  border-radius: 4px; padding: 0.4rem 0.6rem; margin-bottom: 0.8rem;
}
.error-banner p { margin: 0; font-size: 0.7rem; color: rgba(239, 68, 68, 0.9); }

.preview-section { margin-bottom: 0.8rem; }
.preview-section h5 { margin: 0 0 0.5rem 0; font-size: 0.75rem; color: rgba(255, 255, 255, 0.8); }
.skyline-chart {
  background: rgba(0, 0, 0, 0.3); border-radius: 6px; padding: 0.4rem; margin-bottom: 0.6rem;
}
.skyline-stats { display: grid; grid-template-columns: repeat(3, 1fr); gap: 0.4rem; }
.stat-item {
  display: flex; flex-direction: column; font-size: 0.7rem;
  padding: 0.3rem; background: rgba(255, 255, 255, 0.05); border-radius: 4px;
}
.stat-label { color: rgba(255, 255, 255, 0.6); }
.stat-value { color: #a5b4fc; font-weight: 500; font-size: 0.8rem; }

.actions { display: flex; gap: 0.4rem; flex-wrap: wrap; }
.btn-primary,
.btn-secondary {
  flex: 1; min-width: calc(50% - 0.2rem); padding: 0.4rem;
  border: 1px solid rgba(255, 255, 255, 0.2); border-radius: 4px;
  font-size: 0.75rem; cursor: pointer; transition: all 0.2s ease;
}
.btn-primary { background: rgba(99, 102, 241, 0.2); color: #a5b4fc; }
.btn-primary:hover { background: rgba(99, 102, 241, 0.3); }
.btn-secondary { background: rgba(255, 255, 255, 0.1); color: rgba(255, 255, 255, 0.8); }
.btn-secondary:hover:not(:disabled) { background: rgba(255, 255, 255, 0.15); }
.btn-secondary:disabled { opacity: 0.5; cursor: not-allowed; }
</style>
