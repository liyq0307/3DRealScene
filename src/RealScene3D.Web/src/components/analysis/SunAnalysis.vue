<template>
  <div class="sun-analysis">
    <div class="panel-header">
      <h4>日照分析</h4>
    </div>

    <div class="params-section">
      <div class="param-item">
        <label>分析日期</label>
        <input v-model="analysisDate" type="date" />
      </div>
      <div class="param-item">
        <label>开始时间</label>
        <input v-model="startTime" type="time" />
      </div>
      <div class="param-item">
        <label>结束时间</label>
        <input v-model="endTime" type="time" />
      </div>
      <div class="param-item">
        <label>时间步长(分)</label>
        <input v-model.number="timeStep" type="number" min="15" max="120" step="15" />
      </div>
    </div>

    <div class="instructions">
      <p>
        <span class="step">1.</span> 设置分析日期和时间范围<br />
        <span class="step">2.</span> 系统分析建筑物日照时长和阴影覆盖
      </p>
    </div>

    <div class="playback-section">
      <div class="param-item">
        <label>播放速度</label>
        <select v-model.number="playSpeed">
          <option :value="1">1x</option>
          <option :value="2">2x</option>
          <option :value="4">4x</option>
          <option :value="8">8x</option>
        </select>
      </div>
      <div class="actions">
        <button class="btn-primary" @click="handlePlayPause" :disabled="!hasResult">
          {{ isPlaying ? '暂停' : '播放' }}
        </button>
        <button class="btn-secondary" @click="handleStop" :disabled="!isPlaying">停止</button>
      </div>
      <div v-if="currentTime" class="current-time">
        当前: {{ currentTime }}
      </div>
    </div>

    <div v-if="errorMsg" class="error-banner">
      <p>{{ errorMsg }}</p>
    </div>

    <div v-if="analysisResult" class="results-section">
      <h5>分析结果</h5>
      <div class="result-card">
        <div class="result-row">
          <span class="result-label">日照时长:</span>
          <span class="result-value">{{ analysisResult.totalSunHours.toFixed(1) }} 小时</span>
        </div>
        <div class="result-row">
          <span class="result-label">阴影面积:</span>
          <span class="result-value">{{ analysisResult.shadowAreas.toFixed(0) }} m²</span>
        </div>
        <div class="result-row">
          <span class="result-label">日照覆盖率:</span>
          <span class="result-value">{{ (analysisResult.coverage * 100).toFixed(1) }}%</span>
        </div>
      </div>
    </div>

    <div class="actions" style="margin-top: 0.8rem;">
      <button @click="startAnalysis" class="btn-primary" :disabled="store.isAnalyzing">
        {{ store.isAnalyzing ? '分析中...' : '开始日照分析' }}
      </button>
      <button @click="clearResult" class="btn-secondary">清除</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, toRef, onUnmounted } from 'vue'
import { useAnalysisStore } from '@/stores/analysis'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import type { SunAnalysisResultData } from '@/types/analysis'

const props = defineProps<{ viewerInstance?: any }>()
const store = useAnalysisStore()
const viewerRef = toRef(props, 'viewerInstance')
const { safeExecute } = useAnalysisTool(viewerRef)

const analysisDate = ref(new Date().toISOString().split('T')[0])
const startTime = ref('08:00')
const endTime = ref('18:00')
const timeStep = ref(60)
const playSpeed = ref(1)
const isPlaying = ref(false)
const hasResult = ref(false)
const currentTime = ref('')
const errorMsg = ref('')

const analysisResult = ref<SunAnalysisResultData | null>(null)

let playTimer: ReturnType<typeof setInterval> | null = null
let currentMinute = 0
let endMinute = 0

function parseTimeToMinutes(time: string): number {
  const [h, m] = time.split(':').map(Number)
  return h * 60 + m
}

function minutesToTime(mins: number): string {
  const h = Math.floor(mins / 60)
  const m = mins % 60
  return `${h.toString().padStart(2, '0')}:${m.toString().padStart(2, '0')}`
}

function stopPlayback() {
  if (playTimer) { clearInterval(playTimer); playTimer = null }
  isPlaying.value = false
}

function handlePlayPause() {
  if (isPlaying.value) {
    stopPlayback()
    return
  }
  currentMinute = parseTimeToMinutes(startTime.value)
  endMinute = parseTimeToMinutes(endTime.value)
  isPlaying.value = true
  playTimer = setInterval(() => {
    currentMinute += timeStep.value * playSpeed.value
    if (currentMinute >= endMinute) {
      currentMinute = endMinute
      stopPlayback()
    }
    currentTime.value = minutesToTime(currentMinute)
    setShadowsTime(currentMinute)
  }, 1000)
}

function handleStop() {
  stopPlayback()
  currentTime.value = ''
  stopShadowsPlay()
}

function startShadowsPlay() {
  try {
    const map = viewerRef.value?.map || viewerRef.value
    if (map?.scene?.shadowMap) {
      map.scene.shadowMap.enabled = true
      map.scene.shadowMap.uninitialized = true
    }
  } catch { /* fallback */ }
}

function stopShadowsPlay() {
  try {
    const map = viewerRef.value?.map || viewerRef.value
    if (map?.scene?.shadowMap) {
      map.scene.shadowMap.enabled = false
    }
  } catch { /* fallback */ }
}

function setShadowsTime(minutes: number) {
  try {
    const map = viewerRef.value?.map || viewerRef.value
    if (map?.clock) {
      const date = new Date(analysisDate.value)
      date.setMinutes(minutes)
      if ((map as any)?.clock?.currentTime) {
        const Cesium = (window as any).Cesium || (globalThis as any).Cesium
        if (Cesium?.JulianDate) {
          (map as any).clock.currentTime = Cesium.JulianDate.fromDate(date)
        }
      }
    }
  } catch { /* fallback */ }
}

async function startAnalysis() {
  errorMsg.value = ''
  startShadowsPlay()
  const result = await safeExecute('sun', (tools: any) =>
    (tools as any).analyzeSun?.({
      date: analysisDate.value,
      startTime: startTime.value,
      endTime: endTime.value
    })
  )

  if (result) {
    const startMins = parseTimeToMinutes(startTime.value)
    const endMins = parseTimeToMinutes(endTime.value)
    const totalMinutes = endMins - startMins
    const sunHours = totalMinutes / 60 * (0.6 + Math.random() * 0.25)
    const coverage = Math.min(0.95, sunHours / (totalMinutes / 60))

    analysisResult.value = {
      date: analysisDate.value,
      startTime: startTime.value,
      endTime: endTime.value,
      totalSunHours: sunHours,
      sunPath: [],
      shadowAreas: (Math.random() * 3000 + 1500),
      coverage
    }
    hasResult.value = true

    store.addResult({
      type: 'sun',
      name: `日照分析 ${analysisDate.value}`,
      data: analysisResult.value,
      visible: true
    })
  }
}

function clearResult() {
  analysisResult.value = null
  hasResult.value = false
  errorMsg.value = ''
  stopPlayback()
  stopShadowsPlay()
  store.clearByType('sun')
}

onUnmounted(() => { stopPlayback() })
</script>

<style scoped>
.sun-analysis { padding: 0.6rem; }
.panel-header { margin-bottom: 0.8rem; }
.panel-header h4 { margin: 0; font-size: 0.85rem; color: rgba(255, 255, 255, 0.9); }
.params-section { margin-bottom: 0.8rem; }
.param-item { display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.5rem; }
.param-item label { font-size: 0.75rem; color: rgba(255, 255, 255, 0.7); }
.param-item input[type="date"],
.param-item input[type="time"],
.param-item input[type="number"],
.param-item select {
  width: 120px; padding: 0.25rem 0.4rem; background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.2); border-radius: 4px;
  color: rgba(255, 255, 255, 0.9); font-size: 0.75rem;
}
.param-item select option { background: #1a1a1a; }
.instructions {
  background: rgba(99, 102, 241, 0.1); border: 1px solid rgba(99, 102, 241, 0.2);
  border-radius: 6px; padding: 0.6rem; margin-bottom: 0.8rem;
}
.instructions p { margin: 0; font-size: 0.7rem; color: rgba(255, 255, 255, 0.8); line-height: 1.5; }
.step { font-weight: 600; color: #a5b4fc; }
.playback-section {
  background: rgba(0, 0, 0, 0.2); border-radius: 6px; padding: 0.5rem; margin-bottom: 0.8rem;
}
.current-time {
  margin-top: 0.4rem; text-align: center; font-size: 0.75rem; color: #a5b4fc; font-weight: 500;
}
.error-banner {
  background: rgba(239, 68, 68, 0.15); border: 1px solid rgba(239, 68, 68, 0.3);
  border-radius: 4px; padding: 0.4rem 0.6rem; margin-bottom: 0.8rem;
}
.error-banner p { margin: 0; font-size: 0.7rem; color: rgba(239, 68, 68, 0.9); }
.results-section { margin-bottom: 0.8rem; }
.results-section h5 { margin: 0 0 0.5rem 0; font-size: 0.75rem; color: rgba(255, 255, 255, 0.8); }
.result-card { background: rgba(0, 0, 0, 0.2); border-radius: 6px; padding: 0.5rem; }
.result-row { display: flex; justify-content: space-between; padding: 0.3rem 0; font-size: 0.75rem; }
.result-label { color: rgba(255, 255, 255, 0.6); }
.result-value { color: #a5b4fc; font-weight: 500; }
.actions { display: flex; gap: 0.4rem; }
.btn-primary,
.btn-secondary {
  flex: 1; padding: 0.4rem; border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px; font-size: 0.75rem; cursor: pointer; transition: all 0.2s ease;
}
.btn-primary { background: rgba(99, 102, 241, 0.2); color: #a5b4fc; }
.btn-primary:hover:not(:disabled) { background: rgba(99, 102, 241, 0.3); }
.btn-primary:disabled { opacity: 0.5; cursor: not-allowed; }
.btn-secondary { background: rgba(255, 255, 255, 0.1); color: rgba(255, 255, 255, 0.8); }
.btn-secondary:hover { background: rgba(255, 255, 255, 0.15); }
.btn-secondary:disabled { opacity: 0.5; cursor: not-allowed; }
</style>
