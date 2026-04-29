<template>
  <div class="site-selection">
    <div class="section-title">在线选址</div>
    <div class="form-group">
      <label>模型URL</label>
      <input v-model="modelUrl" type="text" placeholder="3DTiles/GLTF URL" />
    </div>
    <div class="btn-group">
      <button class="btn-primary" @click="handleDraw" :disabled="isDrawing">放置模型</button>
    </div>
    <div v-if="hasResult" class="result-panel">
      <div class="result-item"><span class="label">状态:</span><span class="value">已放置</span></div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import { useAnalysisStore } from '@/stores/analysis'

const props = defineProps<{ viewerInstance?: any }>()
const { drawPoint, isReady, init } = useAnalysisTool(ref(props.viewerInstance))
const store = useAnalysisStore()

const modelUrl = ref('')
const isDrawing = ref(false)
const hasResult = ref(false)

onMounted(() => { if (!isReady.value) init() })

async function handleDraw() {
  isDrawing.value = true
  const result = await drawPoint()
  if (result) {
    store.addResult({ type: 'site-selection', name: '在线选址', data: { position: result.positionShow, modelUrl: modelUrl.value }, visible: true })
    hasResult.value = true
  }
  isDrawing.value = false
}
</script>

<style scoped>
.site-selection { padding: 12px; }
.section-title { font-size: 14px; font-weight: bold; margin-bottom: 12px; color: #e0e0e0; }
.form-group { margin-bottom: 8px; display: flex; align-items: center; gap: 8px; }
.form-group label { width: 70px; font-size: 12px; color: #aaa; }
.form-group input { flex: 1; padding: 4px 8px; background: #2a2a2a; border: 1px solid #444; color: #e0e0e0; border-radius: 4px; font-size: 12px; }
.btn-group { margin-top: 12px; }
.btn-primary { width: 100%; padding: 6px; background: #1890ff; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 12px; }
.btn-primary:disabled { opacity: 0.5; }
.result-panel { margin-top: 12px; padding: 8px; background: #1a1a2e; border-radius: 4px; }
.result-item { display: flex; justify-content: space-between; font-size: 12px; }
.result-item .label { color: #aaa; }
.result-item .value { color: #e0e0e0; }
</style>
