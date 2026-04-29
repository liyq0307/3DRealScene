<template>
  <div class="height-measurement">
    <div class="section-title">高度差测量</div>
    <div class="btn-group">
      <button class="btn-primary" @click="handleMeasure" :disabled="isMeasuring">
        {{ isMeasuring ? '测量中...' : '开始测量' }}
      </button>
    </div>
    <div v-if="result" class="result-panel">
      <div class="result-item"><span class="label">起点高度:</span><span class="value">{{ result.startHeight.toFixed(2) }} m</span></div>
      <div class="result-item"><span class="label">终点高度:</span><span class="value">{{ result.endHeight.toFixed(2) }} m</span></div>
      <div class="result-item"><span class="label">高度差:</span><span class="value highlight">{{ result.heightDiff.toFixed(2) }} m</span></div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'

const props = defineProps<{ viewerInstance?: any }>()
const { measureHeight, isReady, init } = useAnalysisTool(ref(props.viewerInstance))

const isMeasuring = ref(false)
const result = ref<{ startHeight: number; endHeight: number; heightDiff: number } | null>(null)

onMounted(() => { if (!isReady.value) init() })

async function handleMeasure() {
  isMeasuring.value = true
  result.value = await measureHeight()
  isMeasuring.value = false
}
</script>

<style scoped>
.height-measurement { padding: 12px; }
.section-title { font-size: 14px; font-weight: bold; margin-bottom: 12px; color: #e0e0e0; }
.btn-group { margin-bottom: 12px; }
.btn-primary { width: 100%; padding: 6px; background: #1890ff; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 12px; }
.btn-primary:disabled { opacity: 0.5; cursor: not-allowed; }
.result-panel { padding: 8px; background: #1a1a2e; border-radius: 4px; }
.result-item { display: flex; justify-content: space-between; font-size: 12px; margin-bottom: 4px; }
.result-item .label { color: #aaa; }
.result-item .value { color: #e0e0e0; }
.result-item .highlight { color: #52c41a; font-weight: bold; }
</style>
