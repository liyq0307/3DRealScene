<template>
  <div class="coordinate-location">
    <div class="section-title">坐标定位</div>
    <div class="form-group">
      <label>经度</label>
      <input v-model.number="longitude" type="number" step="0.000001" placeholder="经度" />
    </div>
    <div class="form-group">
      <label>纬度</label>
      <input v-model.number="latitude" type="number" step="0.000001" placeholder="纬度" />
    </div>
    <div class="form-group">
      <label>高度</label>
      <input v-model.number="altitude" type="number" step="0.1" placeholder="高度(米)" />
    </div>
    <div class="form-group">
      <label>坐标格式</label>
      <select v-model="coordFormat">
        <option value="decimal">十进制度</option>
        <option value="dms">度分秒</option>
      </select>
    </div>
    <div class="btn-group">
      <button class="btn-primary" @click="handleLocate">定位</button>
      <button class="btn-secondary" @click="handlePickFromMap">地图选点</button>
    </div>
    <div v-if="currentCoord" class="result-panel">
      <div class="result-item">
        <span class="label">当前坐标:</span>
        <span class="value">{{ formatCoordinate(currentCoord) }}</span>
      </div>
      <button class="btn-small" @click="copyCoordinate">复制</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'

const props = defineProps<{ viewerInstance?: any }>()
const emit = defineEmits<{ (e: 'close'): void }>()

const { bindMouseClickForCoordinate, addCoordinateMarker, locateCoordinate, isReady, init } = useAnalysisTool(
  ref(props.viewerInstance)
)

const longitude = ref(0)
const latitude = ref(0)
const altitude = ref(0)
const coordFormat = ref<'decimal' | 'dms'>('decimal')
const currentCoord = ref<{ lng: number; lat: number; alt: number } | null>(null)
const isPicking = ref(false)

onMounted(() => { if (!isReady.value) init() })

function handleLocate() {
  if (longitude.value && latitude.value) {
    locateCoordinate(longitude.value, latitude.value, altitude.value)
    addCoordinateMarker(longitude.value, latitude.value, altitude.value)
    currentCoord.value = { lng: longitude.value, lat: latitude.value, alt: altitude.value }
  }
}

function handlePickFromMap() {
  isPicking.value = true
  bindMouseClickForCoordinate((lng, lat, alt) => {
    longitude.value = lng
    latitude.value = lat
    altitude.value = alt
    currentCoord.value = { lng, lat, alt }
    addCoordinateMarker(lng, lat, alt)
    isPicking.value = false
  })
}

function formatCoordinate(coord: { lng: number; lat: number; alt: number }): string {
  if (coordFormat.value === 'dms') {
    return `${toDMS(coord.lng, 'E')} ${toDMS(coord.lat, 'N')} ${coord.alt.toFixed(1)}m`
  }
  return `${coord.lng.toFixed(6)}, ${coord.lat.toFixed(6)}, ${coord.alt.toFixed(1)}m`
}

function toDMS(decimal: number, dir: string): string {
  const d = Math.floor(Math.abs(decimal))
  const m = Math.floor((Math.abs(decimal) - d) * 60)
  const s = ((Math.abs(decimal) - d) * 60 - m) * 60
  const prefix = decimal >= 0 ? dir : dir === 'E' ? 'W' : 'S'
  return `${prefix}${d}°${m}'${s.toFixed(2)}"`
}

function copyCoordinate() {
  if (currentCoord.value) {
    navigator.clipboard.writeText(formatCoordinate(currentCoord.value))
  }
}
</script>

<style scoped>
.coordinate-location { padding: 12px; }
.section-title { font-size: 14px; font-weight: bold; margin-bottom: 12px; color: #e0e0e0; }
.form-group { margin-bottom: 8px; display: flex; align-items: center; gap: 8px; }
.form-group label { width: 60px; font-size: 12px; color: #aaa; flex-shrink: 0; }
.form-group input, .form-group select { flex: 1; padding: 4px 8px; background: #2a2a2a; border: 1px solid #444; color: #e0e0e0; border-radius: 4px; font-size: 12px; }
.btn-group { display: flex; gap: 8px; margin-top: 12px; }
.btn-primary { flex: 1; padding: 6px; background: #1890ff; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 12px; }
.btn-secondary { flex: 1; padding: 6px; background: #444; color: #e0e0e0; border: none; border-radius: 4px; cursor: pointer; font-size: 12px; }
.result-panel { margin-top: 12px; padding: 8px; background: #1a1a2e; border-radius: 4px; }
.result-item { display: flex; justify-content: space-between; font-size: 12px; }
.result-item .label { color: #aaa; }
.result-item .value { color: #e0e0e0; }
.btn-small { margin-top: 4px; padding: 2px 8px; background: #333; color: #aaa; border: 1px solid #555; border-radius: 3px; cursor: pointer; font-size: 11px; }
</style>
