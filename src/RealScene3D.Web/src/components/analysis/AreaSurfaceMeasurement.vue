<template>
  <div class="area-surface-measurement">
    <div class="section-title">贴地面积测量</div>
    <div class="btn-group">
      <button class="btn-primary" @click="handleMeasure" :disabled="isMeasuring">
        {{ isMeasuring ? '请在地图上绘制...' : '开始测量' }}
      </button>
    </div>
    <div v-if="result" class="result-panel">
      <div class="result-item"><span class="label">贴地面积:</span><span class="value highlight">{{ formatArea(result.area) }}</span></div>
      <div class="result-item"><span class="label">周长:</span><span class="value">{{ formatDistance(result.perimeter) }}</span></div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'

const props = defineProps<{ viewerInstance?: any }>()
const { measureAreaSurface, isReady, init } = useAnalysisTool(ref(props.viewerInstance))

const isMeasuring = ref(false)
const result = ref<{ area: number; perimeter: number } | null>(null)

onMounted(() => { if (!isReady.value) init() })

async function handleMeasure() {
  isMeasuring.value = true
  result.value = await measureAreaSurface()
  isMeasuring.value = false
}

function formatArea(a: number): string {
  return a >= 1e6 ? `${(a / 1e6).toFixed(4)} km²` : `${a.toFixed(2)} m²`
}
function formatDistance(d: number): string {
  return d >= 1000 ? `${(d / 1000).toFixed(3)} km` : `${d.toFixed(2)} m`
}
</script>

<style scoped>
.area-surface-measurement { padding: 12px; }
.section-title { font-size: 14px; font-weight: bold; margin-bottom: 12px; color: #e0e0e0; }
.btn-group { margin-bottom: 12px; }
.btn-primary { width: 100%; padding: 6px; background: #1890ff; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 12px; }
.btn-primary:disabled { opacity: 0.5; }
.result-panel { padding: 8px; background: #1a1a2e; border-radius: 4px; }
.result-item { display: flex; justify-content: space-between; font-size: 12px; margin-bottom: 4px; }
.result-item .label { color: #aaa; }
.result-item .value { color: #e0e0e0; }
.result-item .highlight { color: #52c41a; font-weight: bold; }
</style>
