<template>
  <div class="map-marking">
    <div class="section-title">图上标记</div>
    <div class="marking-types">
      <button v-for="t in markingTypes" :key="t.type" class="type-btn"
        :class="{ active: currentType === t.type }" @click="currentType = t.type">
        {{ t.icon }} {{ t.name }}
      </button>
    </div>
    <div class="style-group">
      <div class="form-group">
        <label>颜色</label>
        <input v-model="markColor" type="color" />
      </div>
      <div class="form-group" v-if="currentType !== 'point' && currentType !== 'text'">
        <label>线宽</label>
        <input v-model.number="lineWidth" type="number" min="1" max="20" />
      </div>
    </div>
    <div class="btn-group">
      <button class="btn-primary" @click="handleDraw">绘制</button>
      <button class="btn-secondary" @click="handleExport">导出GeoJSON</button>
    </div>
    <div class="btn-group" style="margin-top: 8px;">
      <button class="btn-danger" @click="handleClear">清除</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import type { MarkingType } from '@/types/analysis'

const props = defineProps<{ viewerInstance?: any }>()
const { drawPoint, drawPolyline, drawPolygon, drawCircle, exportGeoJSON, clearGraphics, isReady, init } = useAnalysisTool(ref(props.viewerInstance))

const markingTypes: Array<{ type: MarkingType; name: string; icon: string }> = [
  { type: 'point', name: '点', icon: '📍' },
  { type: 'polyline', name: '线', icon: '📏' },
  { type: 'polygon', name: '面', icon: '📐' },
  { type: 'circle', name: '圆', icon: '⭕' }
]

const currentType = ref<MarkingType>('point')
const markColor = ref('#ffff00')
const lineWidth = ref(3)

onMounted(() => { if (!isReady.value) init() })

async function handleDraw() {
  const style = { color: markColor.value, width: lineWidth.value }
  switch (currentType.value) {
    case 'point': await drawPoint(style); break
    case 'polyline': await drawPolyline(style); break
    case 'polygon': await drawPolygon(style); break
    case 'circle': await drawCircle(style); break
  }
}

function handleExport() {
  const geojson = exportGeoJSON()
  if (geojson) {
    const blob = new Blob([geojson], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url; a.download = 'marking.geojson'; a.click()
    URL.revokeObjectURL(url)
  }
}

function handleClear() { clearGraphics() }
</script>

<style scoped>
.map-marking { padding: 12px; }
.section-title { font-size: 14px; font-weight: bold; margin-bottom: 12px; color: #e0e0e0; }
.marking-types { display: flex; gap: 4px; margin-bottom: 12px; flex-wrap: wrap; }
.type-btn { padding: 4px 8px; background: #333; color: #aaa; border: 1px solid #555; border-radius: 4px; cursor: pointer; font-size: 12px; }
.type-btn.active { background: #1890ff; color: white; border-color: #1890ff; }
.style-group { margin-bottom: 12px; }
.form-group { margin-bottom: 6px; display: flex; align-items: center; gap: 8px; }
.form-group label { width: 40px; font-size: 12px; color: #aaa; }
.form-group input { flex: 1; padding: 2px 6px; background: #2a2a2a; border: 1px solid #444; color: #e0e0e0; border-radius: 4px; font-size: 12px; }
.btn-group { display: flex; gap: 8px; }
.btn-primary { flex: 1; padding: 6px; background: #1890ff; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 12px; }
.btn-secondary { flex: 1; padding: 6px; background: #444; color: #e0e0e0; border: none; border-radius: 4px; cursor: pointer; font-size: 12px; }
.btn-danger { flex: 1; padding: 6px; background: #ff4d4f; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 12px; }
</style>
