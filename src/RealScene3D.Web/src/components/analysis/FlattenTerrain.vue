<template>
  <div class="flatten-terrain">
    <div class="section-title">压平功能</div>
    <div class="form-group">
      <label>压平高度(m)</label>
      <input v-model.number="flatHeight" type="number" step="1" />
    </div>
    <div class="btn-group">
      <button class="btn-primary" @click="handleStartFlatten">开始压平</button>
      <button class="btn-secondary" @click="handleUpdateHeight">更新高度</button>
    </div>
    <div class="btn-group" style="margin-top: 8px;">
      <button class="btn-danger" @click="handleClear">清除压平</button>
    </div>
    <div v-if="isFlattening" class="status-info">
      <span class="status-active">压平中 - 高度: {{ flatHeight }}m</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'

const props = defineProps<{ viewerInstance?: any }>()
const { startFlatten, updateFlattenHeight, clearFlatten, isReady, init } = useAnalysisTool(ref(props.viewerInstance))

const flatHeight = ref(0)
const isFlattening = ref(false)

onMounted(() => { if (!isReady.value) init() })

async function handleStartFlatten() {
  const result = await startFlatten({ height: flatHeight.value })
  if (result) isFlattening.value = true
}

function handleUpdateHeight() {
  updateFlattenHeight(flatHeight.value)
}

function handleClear() {
  clearFlatten()
  isFlattening.value = false
}
</script>

<style scoped>
.flatten-terrain { padding: 12px; }
.section-title { font-size: 14px; font-weight: bold; margin-bottom: 12px; color: #e0e0e0; }
.form-group { margin-bottom: 8px; display: flex; align-items: center; gap: 8px; }
.form-group label { width: 90px; font-size: 12px; color: #aaa; flex-shrink: 0; }
.form-group input { flex: 1; padding: 4px 8px; background: #2a2a2a; border: 1px solid #444; color: #e0e0e0; border-radius: 4px; font-size: 12px; }
.btn-group { display: flex; gap: 8px; }
.btn-primary { flex: 1; padding: 6px; background: #1890ff; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 12px; }
.btn-secondary { flex: 1; padding: 6px; background: #444; color: #e0e0e0; border: none; border-radius: 4px; cursor: pointer; font-size: 12px; }
.btn-danger { flex: 1; padding: 6px; background: #ff4d4f; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 12px; }
.status-info { margin-top: 12px; padding: 8px; background: #1a1a2e; border-radius: 4px; }
.status-active { font-size: 12px; color: #52c41a; }
</style>
