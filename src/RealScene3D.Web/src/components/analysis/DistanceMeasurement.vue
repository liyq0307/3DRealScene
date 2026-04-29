<template>
  <div class="distance-measurement">
    <div class="panel-header">
      <h4>距离测量</h4>
    </div>

    <div class="mode-selector">
      <button
        v-for="mode in modes"
        :key="mode.key"
        @click="currentMode = mode.key"
        :class="['mode-btn', { active: currentMode === mode.key }]"
      >
        {{ mode.name }}
      </button>
    </div>

    <div v-if="currentMode === 'space'" class="params-section">
      <div class="param-item">
        <label>显示标注</label>
        <input v-model="showLabels" type="checkbox" />
      </div>
      <div class="param-item">
        <label>测量线颜色</label>
        <input v-model="lineColor" type="color" />
      </div>
    </div>

    <div class="instructions">
      <p v-if="currentMode === 'direct'">点击两点测量直线距离</p>
      <p v-else-if="currentMode === 'surface'">沿地表测量距离（考虑地形起伏）</p>
      <p v-else>测量三维空间中的直线距离</p>
    </div>

    <div v-if="store.error" class="error-banner">
      <p>{{ store.error.message }}</p>
    </div>

    <div v-if="measurements.length > 0" class="results-section">
      <h5>测量结果</h5>
      <div class="measurement-list">
        <div
          v-for="(measure, index) in measurements"
          :key="index"
          class="measurement-item"
        >
          <div class="measure-header">
            <span class="measure-label">测量 {{ index + 1 }}</span>
            <button @click="removeMeasurement(index)" class="btn-remove">×</button>
          </div>
          <div class="measure-data">
            <span class="data-value">{{ measure.distance.toFixed(2) }}</span>
            <span class="data-unit">米</span>
          </div>
        </div>
      </div>
      <div v-if="measurements.length > 1" class="total-section">
        <span class="total-label">总距离:</span>
        <span class="total-value">{{ totalDistance.toFixed(2) }} 米</span>
      </div>
    </div>

    <div class="actions">
      <button @click="startMeasurement" class="btn-primary">
        开始测量
      </button>
      <button @click="clearMeasurements" class="btn-secondary">
        清除全部
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, toRef } from 'vue'
import { useAnalysisStore } from '@/stores/analysis'
import { useAnalysisTool } from '@/composables/useAnalysisTool'

const props = defineProps<{
  viewerInstance?: any
  sceneObjects?: any[]
}>()

const store = useAnalysisStore()
const viewerRef = toRef(props, 'viewerInstance')
const { measureDistance, measureDistanceSurface, clearMeasure } = useAnalysisTool(viewerRef)

const currentMode = ref<'direct' | 'surface' | 'space'>('direct')
const showLabels = ref(true)
const lineColor = ref('#00ffff')
const measurements = ref<Array<{ distance: number; points: any[] }>>([])

const modes = [
  { key: 'direct' as const, name: '直线' },
  { key: 'surface' as const, name: '贴地' },
  { key: 'space' as const, name: '空间' }
]

const totalDistance = computed(() =>
  measurements.value.reduce((sum, m) => sum + m.distance, 0)
)

async function startMeasurement() {
  const measureFn = currentMode.value === 'surface' ? measureDistanceSurface : measureDistance
  const result = await measureFn()
  if (result) {
    measurements.value.push({
      distance: result.distance,
      points: result.positions
    })
  }
}

function removeMeasurement(index: number) {
  measurements.value.splice(index, 1)
}

function clearMeasurements() {
  measurements.value = []
  clearMeasure()
  store.clearByType('distance')
}
</script>

<style scoped>
.distance-measurement { padding: 0.6rem; }
.panel-header { margin-bottom: 0.8rem; }
.panel-header h4 { margin: 0; font-size: 0.85rem; color: rgba(255, 255, 255, 0.9); }
.mode-selector { display: flex; gap: 0.4rem; margin-bottom: 0.8rem; }
.mode-btn {
  flex: 1; padding: 0.4rem; background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1); border-radius: 4px;
  color: rgba(255, 255, 255, 0.8); font-size: 0.75rem; cursor: pointer;
  transition: all 0.2s ease;
}
.mode-btn:hover { background: rgba(99, 102, 241, 0.15); }
.mode-btn.active {
  background: rgba(99, 102, 241, 0.25);
  border-color: rgba(99, 102, 241, 0.5); color: #a5b4fc;
}
.params-section { margin-bottom: 0.8rem; }
.param-item { display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.5rem; }
.param-item label { font-size: 0.75rem; color: rgba(255, 255, 255, 0.7); }
.param-item input[type="checkbox"] { width: 16px; height: 16px; cursor: pointer; }
.param-item input[type="color"] {
  width: 40px; height: 24px; padding: 0; border: none; border-radius: 4px; cursor: pointer;
}

.instructions {
  background: rgba(99, 102, 241, 0.1); border: 1px solid rgba(99, 102, 241, 0.2);
  border-radius: 6px; padding: 0.6rem; margin-bottom: 0.8rem;
}
.instructions p { margin: 0; font-size: 0.7rem; color: rgba(255, 255, 255, 0.8); text-align: center; }

.error-banner {
  background: rgba(239, 68, 68, 0.15); border: 1px solid rgba(239, 68, 68, 0.3);
  border-radius: 4px; padding: 0.4rem 0.6rem; margin-bottom: 0.8rem;
}
.error-banner p { margin: 0; font-size: 0.7rem; color: rgba(239, 68, 68, 0.9); }

.results-section { margin-bottom: 0.8rem; }
.results-section h5 { margin: 0 0 0.5rem 0; font-size: 0.75rem; color: rgba(255, 255, 255, 0.8); }
.measurement-list { display: flex; flex-direction: column; gap: 0.4rem; margin-bottom: 0.6rem; }
.measurement-item { background: rgba(0, 0, 0, 0.2); border-radius: 6px; padding: 0.5rem; }
.measure-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.3rem; }
.measure-label { font-size: 0.7rem; color: rgba(255, 255, 255, 0.6); }
.btn-remove {
  width: 20px; height: 20px; display: flex; align-items: center; justify-content: center;
  background: rgba(239, 68, 68, 0.2); border: none; border-radius: 50%;
  color: #ef4444; font-size: 0.8rem; cursor: pointer;
}
.measure-data { display: flex; align-items: baseline; gap: 0.3rem; }
.data-value { font-size: 1.2rem; font-weight: 600; color: #a5b4fc; }
.data-unit { font-size: 0.7rem; color: rgba(255, 255, 255, 0.5); }
.total-section {
  display: flex; justify-content: space-between; padding: 0.4rem;
  background: rgba(99, 102, 241, 0.15); border-radius: 6px; font-size: 0.75rem;
}
.total-label { color: rgba(255, 255, 255, 0.7); }
.total-value { color: #a5b4fc; font-weight: 600; }

.actions { display: flex; gap: 0.4rem; }
.btn-primary,
.btn-secondary {
  flex: 1; padding: 0.4rem; border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px; font-size: 0.75rem; cursor: pointer; transition: all 0.2s ease;
}
.btn-primary { background: rgba(99, 102, 241, 0.2); color: #a5b4fc; }
.btn-primary:hover { background: rgba(99, 102, 241, 0.3); }
.btn-secondary { background: rgba(255, 255, 255, 0.1); color: rgba(255, 255, 255, 0.8); }
.btn-secondary:hover { background: rgba(255, 255, 255, 0.15); }
</style>
