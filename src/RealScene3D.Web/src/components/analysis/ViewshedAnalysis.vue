<template>
  <div class="viewshed-analysis">
    <div class="section-title">可视域分析</div>
    <div class="form-group">
      <label>方向角(°)</label>
      <input v-model.number="direction" type="number" min="0" max="360" />
    </div>
    <div class="form-group">
      <label>俯仰角(°)</label>
      <input v-model.number="pitch" type="number" min="-90" max="90" />
    </div>
    <div class="form-group">
      <label>水平视场角(°)</label>
      <input v-model.number="horizontalFov" type="number" min="1" max="180" />
    </div>
    <div class="form-group">
      <label>垂直视场角(°)</label>
      <input v-model.number="verticalFov" type="number" min="1" max="180" />
    </div>
    <div class="form-group">
      <label>距离(m)</label>
      <input v-model.number="distance" type="number" min="1" />
    </div>
    <div class="btn-group">
      <button class="btn-primary" @click="handleAnalyze">开始分析</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import { useAnalysisStore } from '@/stores/analysis'

const props = defineProps<{ viewerInstance?: any }>()
const { tools, isReady, init } = useAnalysisTool(ref(props.viewerInstance))
const store = useAnalysisStore()

const direction = ref(0)
const pitch = ref(0)
const horizontalFov = ref(90)
const verticalFov = ref(60)
const distance = ref(100)

onMounted(() => { if (!isReady.value) init() })

function handleAnalyze() {
  if (tools.value) {
    const viewshed = (tools.value as any).analyzeViewshed({
      direction: direction.value,
      pitch: pitch.value,
      horizontalFov: horizontalFov.value,
      verticalFov: verticalFov.value,
      distance: distance.value
    })
    store.addResult({
      type: 'viewshed',
      name: '可视域分析',
      data: { direction: direction.value, pitch: pitch.value, horizontalFov: horizontalFov.value, verticalFov: verticalFov.value, distance: distance.value },
      visible: true
    })
  }
}
</script>

<style scoped>
.viewshed-analysis { padding: 12px; }
.section-title { font-size: 14px; font-weight: bold; margin-bottom: 12px; color: #e0e0e0; }
.form-group { margin-bottom: 8px; display: flex; align-items: center; gap: 8px; }
.form-group label { width: 90px; font-size: 12px; color: #aaa; }
.form-group input { flex: 1; padding: 4px 8px; background: #2a2a2a; border: 1px solid #444; color: #e0e0e0; border-radius: 4px; font-size: 12px; }
.btn-group { margin-top: 12px; }
.btn-primary { width: 100%; padding: 6px; background: #1890ff; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 12px; }
</style>
