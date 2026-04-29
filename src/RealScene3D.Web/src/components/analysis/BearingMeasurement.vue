<template>
  <div class="bearing-measurement">
    <div class="section-title">方位角测量</div>
    <div class="btn-group">
      <button class="btn-primary" @click="handleMeasure" :disabled="isMeasuring">
        {{ isMeasuring ? '测量中...' : '开始测量' }}
      </button>
    </div>
    <div v-if="result" class="result-panel">
      <div class="result-item" v-for="(val, key) in result" :key="key">
        <span class="label">{{ key }}:</span>
        <span class="value">{{ typeof val === 'number' ? val.toFixed(2) : val }}</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'

const props = defineProps<{ viewerInstance?: any }>()
const { measureAngle, isReady, init } = useAnalysisTool(ref(props.viewerInstance))

const isMeasuring = ref(false)
const result = ref<Record<string, any> | null>(null)

onMounted(() => { if (!isReady.value) init() })

async function handleMeasure() {
  isMeasuring.value = true
  result.value = await measureAngle()
  isMeasuring.value = false
}
</script>

<style scoped>
.bearing-measurement { padding: 12px; }
.section-title { font-size: 14px; font-weight: bold; margin-bottom: 12px; color: #e0e0e0; }
.btn-group { margin-bottom: 12px; }
.btn-primary { width: 100%; padding: 6px; background: #1890ff; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 12px; }
.btn-primary:disabled { opacity: 0.5; }
.result-panel { padding: 8px; background: #1a1a2e; border-radius: 4px; }
.result-item { display: flex; justify-content: space-between; font-size: 12px; margin-bottom: 4px; }
.result-item .label { color: #aaa; }
.result-item .value { color: #e0e0e0; }
</style>
