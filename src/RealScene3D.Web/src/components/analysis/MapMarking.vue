<template>
  <div class="map-marking">
    <div class="panel-header">
      <h4>图上标记</h4>
    </div>

    <div class="type-section">
      <div class="type-grid">
        <button v-for="t in markingTypes" :key="t.type" class="type-btn"
          :class="{ active: currentType === t.type }" @click="currentType = t.type">
          <span class="type-icon">{{ t.icon }}</span>
          <span class="type-name">{{ t.name }}</span>
        </button>
      </div>
    </div>

    <div class="params-section">
      <div class="param-item">
        <label>颜色</label>
        <input v-model="markColor" type="color" />
      </div>
      <div class="param-item" v-if="currentType !== 'point'">
        <label>线宽</label>
        <input v-model.number="lineWidth" type="range" min="1" max="20" step="1" />
        <span class="param-value">{{ lineWidth }}px</span>
      </div>
      <div class="param-item" v-if="currentType === 'polygon' || currentType === 'circle'">
        <label>透明度</label>
        <input v-model.number="opacity" type="range" min="0.1" max="1" step="0.1" />
        <span class="param-value">{{ opacity }}</span>
      </div>
    </div>

    <div class="marking-info">
      <span class="count">已绘制: {{ drawCount }} 个图形</span>
    </div>

    <div v-if="store.error" class="error-banner">
      <p>{{ store.error.message }}</p>
    </div>

    <div class="actions">
      <button class="btn-primary" @click="handleDraw" :disabled="isDrawing">
        {{ isDrawing ? '请在地图上绘制...' : '绘制' }}
      </button>
      <button class="btn-secondary" @click="handleExport" :disabled="drawCount === 0">
        导出GeoJSON
      </button>
    </div>
    <div class="actions" style="margin-top: 0.3rem;">
      <button class="btn-danger" @click="handleClear" :disabled="drawCount === 0">清除全部</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import { useAnalysisStore } from '@/stores/analysis'
import type { MarkingType } from '@/types/analysis'

const props = defineProps<{ viewerInstance?: any }>()
const { drawPoint, drawPolyline, drawPolygon, drawCircle, exportGeoJSON, clearGraphics, isReady, init } = useAnalysisTool(ref(props.viewerInstance))
const store = useAnalysisStore()

const markingTypes: Array<{ type: MarkingType; name: string; icon: string }> = [
  { type: 'point', name: '标记点', icon: '📍' },
  { type: 'polyline', name: '标记线', icon: '📏' },
  { type: 'polygon', name: '标记面', icon: '📐' },
  { type: 'circle', name: '标记圆', icon: '⭕' }
]

const currentType = ref<MarkingType>('point')
const markColor = ref('#3388ff')
const lineWidth = ref(3)
const opacity = ref(0.5)
const isDrawing = ref(false)
const drawCount = ref(0)

onMounted(() => { if (!isReady.value) init() })

async function handleDraw() {
  isDrawing.value = true
  const style = { color: markColor.value, width: lineWidth.value, opacity: opacity.value }
  let result = null
  try {
    switch (currentType.value) {
      case 'point': result = await drawPoint(style); break
      case 'polyline': result = await drawPolyline(style); break
      case 'polygon': result = await drawPolygon(style); break
      case 'circle': result = await drawCircle(style); break
    }
    if (result) {
      drawCount.value++
      store.addResult({
        type: 'map-marking',
        name: `图上标记-${currentType.value}`,
        data: { markingType: currentType.value, style, geometry: result },
        visible: true
      })
    }
  } catch (e) {
    console.error('绘制失败:', e)
  } finally {
    isDrawing.value = false
  }
}

function handleExport() {
  const geojson = exportGeoJSON()
  if (geojson) {
    const blob = new Blob([geojson], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url; a.download = `marking_${Date.now()}.geojson`; a.click()
    URL.revokeObjectURL(url)
  }
}

function handleClear() {
  clearGraphics()
  drawCount.value = 0
  store.clearByType('map-marking')
}
</script>

<style scoped>
.map-marking { padding: 0.6rem; }
.panel-header { margin-bottom: 0.8rem; }
.panel-header h4 { margin: 0; font-size: 0.85rem; color: rgba(255, 255, 255, 0.9); }

.type-section { margin-bottom: 0.8rem; }
.type-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 4px; }
.type-btn {
  display: flex; flex-direction: column; align-items: center; padding: 0.35rem;
  background: rgba(255, 255, 255, 0.06); border: 1px solid rgba(255, 255, 255, 0.12);
  border-radius: 6px; cursor: pointer; transition: all 0.2s;
}
.type-btn:hover { background: rgba(255, 255, 255, 0.1); }
.type-btn.active {
  background: rgba(99, 102, 241, 0.2); border-color: rgba(99, 102, 241, 0.4);
}
.type-icon { font-size: 1rem; }
.type-name { font-size: 0.65rem; color: rgba(255, 255, 255, 0.7); margin-top: 2px; }
.type-btn.active .type-name { color: #a5b4fc; }

.params-section { margin-bottom: 0.6rem; }
.param-item { display: flex; align-items: center; gap: 0.4rem; margin-bottom: 0.4rem; }
.param-item label { flex: 0 0 50px; font-size: 0.75rem; color: rgba(255, 255, 255, 0.7); }
.param-item input[type="color"] { width: 36px; height: 22px; padding: 0; border: none; border-radius: 4px; cursor: pointer; }
.param-item input[type="range"] { flex: 1; }
.param-value { width: 36px; font-size: 0.7rem; color: #a5b4fc; text-align: right; }

.marking-info { margin-bottom: 0.5rem; }
.count { font-size: 0.7rem; color: rgba(255, 255, 255, 0.5); }

.error-banner {
  background: rgba(239, 68, 68, 0.15); border: 1px solid rgba(239, 68, 68, 0.3);
  border-radius: 4px; padding: 0.4rem 0.6rem; margin-bottom: 0.8rem;
}
.error-banner p { margin: 0; font-size: 0.7rem; color: rgba(239, 68, 68, 0.9); }

.actions { display: flex; gap: 0.4rem; }
.btn-primary, .btn-secondary, .btn-danger {
  flex: 1; padding: 0.4rem; border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px; font-size: 0.75rem; cursor: pointer; transition: all 0.2s ease;
}
.btn-primary { background: rgba(99, 102, 241, 0.2); color: #a5b4fc; }
.btn-primary:hover:not(:disabled) { background: rgba(99, 102, 241, 0.3); }
.btn-secondary { background: rgba(255, 255, 255, 0.1); color: rgba(255, 255, 255, 0.8); }
.btn-secondary:hover:not(:disabled) { background: rgba(255, 255, 255, 0.15); }
.btn-danger { background: rgba(239, 68, 68, 0.15); color: rgba(239, 68, 68, 0.9); }
.btn-danger:hover:not(:disabled) { background: rgba(239, 68, 68, 0.25); }
.btn-primary:disabled, .btn-secondary:disabled, .btn-danger:disabled { opacity: 0.4; cursor: not-allowed; }
</style>
