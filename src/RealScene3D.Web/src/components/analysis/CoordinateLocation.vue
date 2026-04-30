<template>
  <div class="coordinate-location">
    <div class="panel-header">
      <h4>坐标定位</h4>
    </div>

    <div class="params-section">
      <div class="param-item">
        <label>坐标格式</label>
        <div class="radio-group">
          <button v-for="f in formats" :key="f.value" class="radio-btn"
            :class="{ active: coordFormat === f.value }" @click="coordFormat = f.value">
            {{ f.label }}
          </button>
        </div>
      </div>

      <template v-if="coordFormat === 'decimal'">
        <div class="param-item">
          <label>经度</label>
          <input v-model.number="longitude" type="number" step="0.000001"
            :class="{ error: longitudeError }" @input="validateLng" />
        </div>
        <div class="param-item">
          <label>纬度</label>
          <input v-model.number="latitude" type="number" step="0.000001"
            :class="{ error: latitudeError }" @input="validateLat" />
        </div>
      </template>

      <template v-else>
        <div class="param-item">
          <label>经度</label>
          <div class="dms-input">
            <input v-model.number="lngDeg" type="number" />°
            <input v-model.number="lngMin" type="number" />′
            <input v-model.number="lngSec" type="number" />″
          </div>
        </div>
        <div class="param-item">
          <label>纬度</label>
          <div class="dms-input">
            <input v-model.number="latDeg" type="number" />°
            <input v-model.number="latMin" type="number" />′
            <input v-model.number="latSec" type="number" />″
          </div>
        </div>
      </template>

      <div class="param-item">
        <label>高度(m)</label>
        <input v-model.number="altitude" type="number" step="0.1" />
      </div>
    </div>

    <div v-if="longitudeError || latitudeError" class="error-banner">
      <p>{{ longitudeError || latitudeError }}</p>
    </div>

    <div v-if="currentCoord" class="result-section">
      <div class="result-card">
        <div class="result-row">
          <span class="result-label">当前坐标</span>
          <span class="result-value">{{ formatCoordinate(currentCoord) }}</span>
        </div>
        <div class="result-row">
          <span class="result-label">高度</span>
          <span class="result-value">{{ currentCoord.alt.toFixed(1) }} m</span>
        </div>
      </div>
      <button class="btn-copy" @click="copyCoordinate">复制坐标</button>
    </div>

    <div class="actions">
      <button class="btn-primary" @click="handlePickFromMap" :disabled="isPicking">
        {{ isPicking ? '请在地图上点击...' : '地图选点' }}
      </button>
      <button class="btn-secondary" @click="handleLocate" :disabled="!!longitudeError || !!latitudeError">
        坐标定位
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import { useAnalysisStore } from '@/stores/analysis'

const props = defineProps<{ viewerInstance?: any }>()
const { bindMouseClickForCoordinate, addCoordinateMarker, locateCoordinate, isReady, init } = useAnalysisTool(ref(props.viewerInstance))
const store = useAnalysisStore()

const formats = [
  { value: 'decimal' as const, label: '十进制' },
  { value: 'dms' as const, label: '度分秒' }
]

const longitude = ref(0)
const latitude = ref(0)
const altitude = ref(0)
const coordFormat = ref<'decimal' | 'dms'>('decimal')
const currentCoord = ref<{ lng: number; lat: number; alt: number } | null>(null)
const isPicking = ref(false)
const longitudeError = ref('')
const latitudeError = ref('')

const lngDeg = ref(0)
const lngMin = ref(0)
const lngSec = ref(0)
const latDeg = ref(0)
const latMin = ref(0)
const latSec = ref(0)

onMounted(() => { if (!isReady.value) init() })

function validateLng() {
  if (longitude.value < -180 || longitude.value > 180) {
    longitudeError.value = '经度范围: -180~180'
  } else {
    longitudeError.value = ''
  }
}

function validateLat() {
  if (latitude.value < -90 || latitude.value > 90) {
    latitudeError.value = '纬度范围: -90~90'
  } else {
    latitudeError.value = ''
  }
}

function dmsToDecimal(deg: number, min: number, sec: number): number {
  const sign = deg >= 0 ? 1 : -1
  return sign * (Math.abs(deg) + min / 60 + sec / 3600)
}

function decimalToDms(decimal: number) {
  const sign = decimal >= 0 ? 1 : -1
  const abs = Math.abs(decimal)
  const deg = Math.floor(abs)
  const minFull = (abs - deg) * 60
  const min = Math.floor(minFull)
  const sec = (minFull - min) * 60
  return { deg: sign * deg, min, sec }
}

function getCurrentLngLat(): { lng: number; lat: number } {
  if (coordFormat.value === 'dms') {
    return {
      lng: dmsToDecimal(lngDeg.value, lngMin.value, lngSec.value),
      lat: dmsToDecimal(latDeg.value, latMin.value, latSec.value)
    }
  }
  return { lng: longitude.value, lat: latitude.value }
}

function updateCoordFields(lng: number, lat: number, alt: number) {
  longitude.value = Number(lng.toFixed(6))
  latitude.value = Number(lat.toFixed(6))
  altitude.value = Number(alt.toFixed(2))

  const lngDms = decimalToDms(lng)
  lngDeg.value = lngDms.deg
  lngMin.value = lngDms.min
  lngSec.value = Number(lngDms.sec.toFixed(2))

  const latDms = decimalToDms(lat)
  latDeg.value = latDms.deg
  latMin.value = latDms.min
  latSec.value = Number(latDms.sec.toFixed(2))
}

function handleLocate() {
  const { lng, lat } = getCurrentLngLat()
  if (lng < -180 || lng > 180 || lat < -90 || lat > 90) return

  locateCoordinate(lng, lat, altitude.value)
  addCoordinateMarker(lng, lat, altitude.value)
  currentCoord.value = { lng, lat, alt: altitude.value }

  store.addResult({
    type: 'coordinate',
    name: '坐标定位',
    data: { lng, lat, alt: altitude.value },
    visible: true
  })
}

function handlePickFromMap() {
  isPicking.value = true
  bindMouseClickForCoordinate((lng: number, lat: number, alt: number) => {
    updateCoordFields(lng, lat, alt)
    currentCoord.value = { lng, lat, alt }
    addCoordinateMarker(lng, lat, alt)
    isPicking.value = false

    store.addResult({
      type: 'coordinate',
      name: '坐标定位',
      data: { lng, lat, alt },
      visible: true
    })
  })
}

function formatCoordinate(coord: { lng: number; lat: number; alt: number }): string {
  if (coordFormat.value === 'dms') {
    return `${toDMS(coord.lng, 'E')} ${toDMS(coord.lat, 'N')}`
  }
  return `${coord.lng.toFixed(6)}, ${coord.lat.toFixed(6)}`
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

onUnmounted(() => {
  isPicking.value = false
})
</script>

<style scoped>
.coordinate-location { padding: 0.6rem; }
.panel-header { margin-bottom: 0.8rem; }
.panel-header h4 { margin: 0; font-size: 0.85rem; color: rgba(255, 255, 255, 0.9); }

.params-section { margin-bottom: 0.8rem; }
.param-item { display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.5rem; }
.param-item label { font-size: 0.75rem; color: rgba(255, 255, 255, 0.7); flex-shrink: 0; width: 60px; }
.param-item input {
  width: 120px; padding: 0.25rem 0.4rem; background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.2); border-radius: 4px;
  color: white; font-size: 0.75rem;
}
.param-item input.error { border-color: rgba(239, 68, 68, 0.6); }

.radio-group { display: flex; gap: 4px; }
.radio-btn {
  padding: 0.2rem 0.5rem; background: rgba(255, 255, 255, 0.08);
  border: 1px solid rgba(255, 255, 255, 0.15); border-radius: 4px;
  color: rgba(255, 255, 255, 0.7); font-size: 0.7rem; cursor: pointer;
}
.radio-btn.active {
  background: rgba(99, 102, 241, 0.2); color: #a5b4fc;
  border-color: rgba(99, 102, 241, 0.4);
}

.dms-input {
  display: flex; align-items: center; gap: 2px;
  font-size: 0.7rem; color: rgba(255, 255, 255, 0.5);
}
.dms-input input {
  width: 45px; padding: 0.2rem 0.3rem; background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.2); border-radius: 3px;
  color: white; font-size: 0.7rem; text-align: center;
}

.error-banner {
  background: rgba(239, 68, 68, 0.15); border: 1px solid rgba(239, 68, 68, 0.3);
  border-radius: 4px; padding: 0.4rem 0.6rem; margin-bottom: 0.8rem;
}
.error-banner p { margin: 0; font-size: 0.7rem; color: rgba(239, 68, 68, 0.9); }

.result-section { margin-bottom: 0.8rem; }
.result-card {
  background: rgba(0, 0, 0, 0.2); border-radius: 6px; padding: 0.5rem; margin-bottom: 0.4rem;
}
.result-row { display: flex; justify-content: space-between; padding: 0.2rem 0; font-size: 0.72rem; }
.result-label { color: rgba(255, 255, 255, 0.6); }
.result-value { color: #a5b4fc; font-weight: 500; word-break: break-all; }

.btn-copy {
  width: 100%; padding: 0.25rem; background: rgba(255, 255, 255, 0.08);
  border: 1px solid rgba(255, 255, 255, 0.15); border-radius: 4px;
  color: rgba(255, 255, 255, 0.7); font-size: 0.7rem; cursor: pointer;
}
.btn-copy:hover { background: rgba(255, 255, 255, 0.12); }

.actions { display: flex; gap: 0.4rem; }
.btn-primary, .btn-secondary {
  flex: 1; padding: 0.4rem; border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px; font-size: 0.75rem; cursor: pointer; transition: all 0.2s ease;
}
.btn-primary { background: rgba(99, 102, 241, 0.2); color: #a5b4fc; }
.btn-primary:hover:not(:disabled) { background: rgba(99, 102, 241, 0.3); }
.btn-primary:disabled { opacity: 0.5; cursor: not-allowed; }
.btn-secondary { background: rgba(255, 255, 255, 0.1); color: rgba(255, 255, 255, 0.8); }
.btn-secondary:hover:not(:disabled) { background: rgba(255, 255, 255, 0.15); }
.btn-secondary:disabled { opacity: 0.5; cursor: not-allowed; }
</style>
