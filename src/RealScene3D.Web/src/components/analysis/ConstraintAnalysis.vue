<template>
  <div class="constraint-analysis">
    <div class="section-title">限制性分析</div>
    <div class="form-group">
      <label>区域类型</label>
      <select v-model="areaType">
        <option value="rectangle">矩形</option>
        <option value="circle">圆形</option>
        <option value="polygon">多边形</option>
      </select>
    </div>
    <div class="btn-group">
      <button class="btn-primary" @click="handleDraw" :disabled="isDrawing">绘制分析区域</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import { useAnalysisStore } from '@/stores/analysis'
import type { DrawAreaType } from '@/types/analysis'

const props = defineProps<{ viewerInstance?: any }>()
const { drawRectangle, drawCircleArea, drawPolygonArea, isReady, init } = useAnalysisTool(ref(props.viewerInstance))
const store = useAnalysisStore()

const areaType = ref<DrawAreaType>('rectangle')
const isDrawing = ref(false)

onMounted(() => { if (!isReady.value) init() })

async function handleDraw() {
  isDrawing.value = true
  let result = null
  switch (areaType.value) {
    case 'rectangle': result = await drawRectangle(); break
    case 'circle': result = await drawCircleArea(); break
    case 'polygon': result = await drawPolygonArea(); break
  }
  if (result) {
    store.addResult({ type: 'constraint', name: '限制性分析', data: { areaType: areaType.value, regionPositions: result.positionShow || result.positions }, visible: true })
  }
  isDrawing.value = false
}
</script>

<style scoped>
.constraint-analysis { padding: 12px; }
.section-title { font-size: 14px; font-weight: bold; margin-bottom: 12px; color: #e0e0e0; }
.form-group { margin-bottom: 8px; display: flex; align-items: center; gap: 8px; }
.form-group label { width: 70px; font-size: 12px; color: #aaa; }
.form-group select { flex: 1; padding: 4px 8px; background: #2a2a2a; border: 1px solid #444; color: #e0e0e0; border-radius: 4px; font-size: 12px; }
.btn-group { margin-top: 12px; }
.btn-primary { width: 100%; padding: 6px; background: #1890ff; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 12px; }
.btn-primary:disabled { opacity: 0.5; }
</style>
