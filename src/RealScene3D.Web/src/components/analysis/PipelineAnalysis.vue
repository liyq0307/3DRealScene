<template>
  <div class="pipeline-analysis">
    <div class="section-title">管线分析</div>
    <div class="form-group">
      <label>管径(m)</label>
      <input v-model.number="pipeRadius" type="number" min="0.1" step="0.1" />
    </div>
    <div class="form-group">
      <label>颜色</label>
      <input v-model="pipeColor" type="color" />
    </div>
    <div class="btn-group">
      <button class="btn-primary" @click="handleDraw" :disabled="isDrawing">绘制管线</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'

const props = defineProps<{ viewerInstance?: any }>()
const { drawPipeline, isReady, init } = useAnalysisTool(ref(props.viewerInstance))

const pipeRadius = ref(1)
const pipeColor = ref('#00ffff')
const isDrawing = ref(false)

onMounted(() => { if (!isReady.value) init() })

async function handleDraw() {
  isDrawing.value = true
  await drawPipeline({ radius: pipeRadius.value, color: pipeColor.value })
  isDrawing.value = false
}
</script>

<style scoped>
.pipeline-analysis { padding: 12px; }
.section-title { font-size: 14px; font-weight: bold; margin-bottom: 12px; color: #e0e0e0; }
.form-group { margin-bottom: 8px; display: flex; align-items: center; gap: 8px; }
.form-group label { width: 60px; font-size: 12px; color: #aaa; }
.form-group input { flex: 1; padding: 4px 8px; background: #2a2a2a; border: 1px solid #444; color: #e0e0e0; border-radius: 4px; font-size: 12px; }
.btn-group { margin-top: 12px; }
.btn-primary { width: 100%; padding: 6px; background: #1890ff; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 12px; }
.btn-primary:disabled { opacity: 0.5; }
</style>
