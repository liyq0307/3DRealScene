<template>
  <div class="profile-analysis">
    <div class="panel-header">
      <h4>剖面分析</h4>
    </div>

    <div class="params-section">
      <div class="param-item">
        <label>采样间隔 (米)</label>
        <input v-model.number="sampleInterval" type="number" min="1" max="100" step="1" />
      </div>
      <div class="param-item">
        <label>显示高度基准</label>
        <select v-model="heightBaseline">
          <option value="sea">海平面</option>
          <option value="ground">地面</option>
          <option value="relative">相对高度</option>
        </select>
      </div>
      <div class="param-item">
        <label>剖面线颜色</label>
        <input v-model="lineColor" type="color" />
      </div>
    </div>

    <div class="instructions">
      <p>
        <span class="step">1.</span> 点击地图绘制剖面线<br />
        <span class="step">2.</span> 双击完成绘制并生成剖面图
      </p>
    </div>

    <div v-if="store.error" class="error-banner">
      <p>{{ store.error.message }}</p>
    </div>

    <div v-if="profileData.length > 0" class="chart-section">
      <h5>剖面曲线</h5>
      <div class="chart-container">
        <LineChart
          v-if="chartData.length > 0"
          :data="chartData"
          :width="280"
          :height="150"
          :show-legend="false"
        />
      </div>
      <div class="profile-stats">
        <div class="stat-item">
          <span class="stat-label">总长度:</span>
          <span class="stat-value">{{ totalLength.toFixed(1) }} 米</span>
        </div>
        <div class="stat-item">
          <span class="stat-label">最大高程:</span>
          <span class="stat-value">{{ maxElevation.toFixed(1) }} 米</span>
        </div>
        <div class="stat-item">
          <span class="stat-label">最小高程:</span>
          <span class="stat-value">{{ minElevation.toFixed(1) }} 米</span>
        </div>
        <div class="stat-item">
          <span class="stat-label">高程差:</span>
          <span class="stat-value">{{ elevationDiff.toFixed(1) }} 米</span>
        </div>
      </div>
    </div>

    <div class="actions">
      <button
        @click="startDrawing"
        class="btn-primary"
        :disabled="store.isAnalyzing"
      >
        {{ store.isAnalyzing ? '绘制中...' : '开始绘制' }}
      </button>
      <button
        @click="exportProfile"
        class="btn-secondary"
        :disabled="profileData.length === 0"
      >
        导出数据
      </button>
      <button @click="clearProfile" class="btn-secondary">
        清除
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, toRef } from 'vue'
import { storeToRefs } from 'pinia'
import { useAnalysisStore } from '@/stores/analysis'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import LineChart from '@/components/LineChart.vue'
import type { HeightBaseline } from '@/types/analysis'

const props = defineProps<{
  viewerInstance?: any
  sceneObjects?: any[]
}>()

const store = useAnalysisStore()
const viewerRef = toRef(props, 'viewerInstance')
const { analyzeProfile, clearMeasure } = useAnalysisTool(viewerRef)

const sampleInterval = ref(10)
const heightBaseline = ref<HeightBaseline>('sea')
const lineColor = ref('#ff6b6b')
const profileData = ref<Array<{ distance: number; elevation: number }>>([])

const chartData = computed(() =>
  profileData.value.map((point) => ({
    time: `${point.distance.toFixed(0)}m`,
    value: point.elevation
  }))
)

const totalLength = computed(() =>
  profileData.value.length > 0 ? profileData.value[profileData.value.length - 1].distance : 0
)

const maxElevation = computed(() =>
  profileData.value.length > 0 ? Math.max(...profileData.value.map((p) => p.elevation)) : 0
)

const minElevation = computed(() =>
  profileData.value.length > 0 ? Math.min(...profileData.value.map((p) => p.elevation)) : 0
)

const elevationDiff = computed(() => maxElevation.value - minElevation.value)

async function startDrawing() {
  const result = await analyzeProfile()
  if (result) {
    profileData.value = result.profileData
  }
}

function exportProfile() {
  const data = {
    timestamp: new Date().toISOString(),
    params: { sampleInterval: sampleInterval.value, heightBaseline: heightBaseline.value },
    profileData: profileData.value,
    stats: { totalLength: totalLength.value, maxElevation: maxElevation.value, minElevation: minElevation.value }
  }
  const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `profile_${Date.now()}.json`
  a.click()
  URL.revokeObjectURL(url)
}

function clearProfile() {
  profileData.value = []
  clearMeasure()
  store.clearByType('profile')
}
</script>

<style scoped>
.profile-analysis { padding: 0.6rem; }
.panel-header { margin-bottom: 0.8rem; }
.panel-header h4 { margin: 0; font-size: 0.85rem; color: rgba(255, 255, 255, 0.9); }

.params-section { margin-bottom: 0.8rem; }
.param-item { display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.5rem; }
.param-item label { font-size: 0.75rem; color: rgba(255, 255, 255, 0.7); }
.param-item input[type="number"],
.param-item select {
  width: 100px; padding: 0.25rem 0.4rem; background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.2); border-radius: 4px;
  color: white; font-size: 0.75rem;
}
.param-item select option { background: #1a1a1a; }
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

.chart-section { margin-bottom: 0.8rem; }
.chart-section h5 { margin: 0 0 0.5rem 0; font-size: 0.75rem; color: rgba(255, 255, 255, 0.8); }
.chart-container {
  background: rgba(0, 0, 0, 0.3); border-radius: 6px; padding: 0.4rem; margin-bottom: 0.6rem;
}
.profile-stats { display: grid; grid-template-columns: repeat(2, 1fr); gap: 0.4rem; }
.stat-item {
  display: flex; justify-content: space-between; font-size: 0.7rem;
  padding: 0.3rem; background: rgba(255, 255, 255, 0.05); border-radius: 4px;
}
.stat-label { color: rgba(255, 255, 255, 0.6); }
.stat-value { color: #a5b4fc; font-weight: 500; }

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
.btn-secondary:hover:not(:disabled) { background: rgba(255, 255, 255, 0.15); }
.btn-secondary:disabled { opacity: 0.5; cursor: not-allowed; }
</style>
