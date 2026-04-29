<template>
  <div class="coordinate-measure">
    <div class="section-title">坐标测量</div>
    <div class="btn-group">
      <button class="btn-primary" @click="handleMeasure" :disabled="isMeasuring">
        {{ isMeasuring ? '请点击地图...' : '开始测量' }}
      </button>
    </div>
    <div v-if="result" class="result-panel">
      <div class="result-item"><span class="label">经度:</span><span class="value">{{ result.longitude.toFixed(6) }}</span></div>
      <div class="result-item"><span class="label">纬度:</span><span class="value">{{ result.latitude.toFixed(6) }}</span></div>
      <div class="result-item"><span class="label">海拔:</span><span class="value">{{ result.height.toFixed(2) }} m</span></div>
      <button class="btn-small" @click="copyResult">复制坐标</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'

const props = defineProps<{ viewerInstance?: any }>()
const { measurePoint, isReady, init } = useAnalysisTool(ref(props.viewerInstance))

const isMeasuring = ref(false)
const result = ref<{ longitude: number; latitude: number; height: number } | null>(null)

onMounted(() => { if (!isReady.value) init() })

async function handleMeasure() {
  isMeasuring.value = true
  result.value = await measurePoint()
  isMeasuring.value = false
}

function copyResult() {
  if (result.value) {
    navigator.clipboard.writeText(`${result.value.longitude.toFixed(6)}, ${result.value.latitude.toFixed(6)}, ${result.value.height.toFixed(2)}`)
  }
}
</script>

<style scoped>
.coordinate-measure { padding: 12px; }
.section-title { font-size: 14px; font-weight: bold; margin-bottom: 12px; color: #e0e0e0; }
.btn-group { margin-bottom: 12px; }
.btn-primary { width: 100%; padding: 6px; background: #1890ff; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 12px; }
.btn-primary:disabled { opacity: 0.5; }
.result-panel { padding: 8px; background: #1a1a2e; border-radius: 4px; }
.result-item { display: flex; justify-content: space-between; font-size: 12px; margin-bottom: 4px; }
.result-item .label { color: #aaa; }
.result-item .value { color: #e0e0e0; }
.btn-small { margin-top: 4px; padding: 2px 8px; background: #333; color: #aaa; border: 1px solid #555; border-radius: 3px; cursor: pointer; font-size: 11px; }
</style>
