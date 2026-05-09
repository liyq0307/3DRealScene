<template>
  <div class="visibility-analysis">
    <div class="panel-header">
      <h4>通视分析</h4>
    </div>

    <!-- 模式选择 -->
    <div class="mode-selector">
      <button
        v-for="mode in modes"
        :key="mode.key"
        @click="currentMode = mode.key"
        :class="['mode-btn', { active: currentMode === mode.key }]"
      >
        <span class="mode-icon">{{ mode.icon }}</span>
        <span>{{ mode.name }}</span>
      </button>
    </div>

    <!-- 参数设置 -->
    <div class="params-section">
      <div v-if="currentMode !== 'viewshed'" class="param-item">
        <label>观察点高度 (米)</label>
        <input v-model.number="observerHeight" type="number" min="0" max="100" step="0.5" />
      </div>
      <div v-if="currentMode === 'circular'" class="param-item">
        <label>分析方向数</label>
        <input v-model.number="sampleCount" type="number" min="10" max="90" step="5" />
      </div>
      <div v-if="currentMode !== 'viewshed'" class="param-item">
        <label>可视区域颜色</label>
        <input v-model="visibleColor" type="color" />
      </div>
      <div v-if="currentMode !== 'viewshed'" class="param-item">
        <label>不可视区域颜色</label>
        <input v-model="hiddenColor" type="color" />
      </div>
    </div>

    <!-- 可视域参数 -->
    <div v-if="currentMode === 'viewshed'" class="viewshed-params">
      <div class="param-item">
        <label>水平张角 (°)</label>
        <div class="slider-group">
          <input v-model.number="viewshedProps.horizontalAngle" type="range" min="1" max="60" step="0.1" @input="onViewshedPropChange" />
          <span class="slider-value">{{ viewshedProps.horizontalAngle }}°</span>
        </div>
      </div>
      <div class="param-item">
        <label>垂直张角 (°)</label>
        <div class="slider-group">
          <input v-model.number="viewshedProps.verticalAngle" type="range" min="10" max="30" step="0.1" @input="onViewshedPropChange" />
          <span class="slider-value">{{ viewshedProps.verticalAngle }}°</span>
        </div>
      </div>
      <div class="param-item">
        <label>投射距离 (m)</label>
        <div class="slider-group">
          <input v-model.number="viewshedProps.distance" type="range" min="1" max="5000" step="1" @input="onViewshedPropChange" />
          <span class="slider-value">{{ viewshedProps.distance }}m</span>
        </div>
      </div>
      <div class="param-item">
        <label>四周方向 (°)</label>
        <div class="slider-group">
          <input v-model.number="viewshedProps.heading" type="range" min="0" max="360" step="0.1" @input="onViewshedPropChange" />
          <span class="slider-value">{{ viewshedProps.heading }}°</span>
        </div>
      </div>
      <div class="param-item">
        <label>俯仰角度 (°)</label>
        <div class="slider-group">
          <input v-model.number="viewshedProps.pitch" type="range" min="-180" max="180" step="0.1" @input="onViewshedPropChange" />
          <span class="slider-value">{{ viewshedProps.pitch }}°</span>
        </div>
      </div>
      <div class="param-item">
        <label>视椎框线</label>
        <label class="checkbox-label">
          <input v-model="viewshedProps.showFrustum" type="checkbox" @change="onViewshedPropChange" />
          <span>是否显示</span>
        </label>
      </div>
      <div class="param-item">
        <label>透明度</label>
        <div class="slider-group">
          <input v-model.number="viewshedProps.opacity" type="range" min="0" max="1" step="0.1" @input="onViewshedPropChange" />
          <span class="slider-value">{{ viewshedProps.opacity.toFixed(1) }}</span>
        </div>
      </div>
    </div>

    <!-- 操作提示 -->
    <div class="instructions">
      <p v-if="currentMode === 'linear'">
        <span class="step">1.</span> 点击地图设置观察点<br />
        <span class="step">2.</span> 再次点击设置目标点
      </p>
      <p v-else-if="currentMode === 'circular'">
        <span class="step">1.</span> 点击地图设置观察点<br />
        <span class="step">2.</span> 拖动确定分析范围
      </p>
      <p v-else>
        <span class="step">1.</span> 设置可视域参数<br />
        <span class="step">2.</span> 点击地图放置观察点
      </p>
    </div>

    <!-- 错误提示 -->
    <div v-if="store.error" class="error-banner">
      <p>{{ store.error.message }}</p>
    </div>

    <!-- 分析结果 -->
    <div v-if="displayResults.length > 0" class="results-section">
      <h5>分析结果</h5>
      <div class="result-list">
        <div v-for="(item, index) in displayResults" :key="index" class="result-item">
          <span class="result-label">{{ item.label }}</span>
          <span class="result-value">{{ item.value }}</span>
        </div>
      </div>
    </div>

    <!-- 通视线列表 -->
    <div v-if="sightlineList.length > 0" class="sightline-list-section">
      <h5>通视线列表</h5>
      <div class="sightline-list">
        <div v-for="item in sightlineList" :key="item.id" class="sightline-item">
          <button @click="toggleItemVisibility(item.id)" class="btn-icon" :title="item.visible ? '隐藏' : '显示'">
            {{ item.visible ? '👁' : '👁‍🗨' }}
          </button>
          <span class="sightline-type">{{ getSightlineTypeLabel(item.type) }}</span>
          <span class="sightline-time">{{ formatSightlineTime(item.timestamp) }}</span>
          <button @click="removeItem(item.id)" class="btn-icon delete" title="删除">✕</button>
        </div>
      </div>
    </div>

    <!-- 操作按钮 -->
    <div class="actions">
      <button
        v-if="!isDrawing"
        @click="startAnalysis"
        class="btn-primary"
        :disabled="store.isAnalyzing"
      >
        {{ store.isAnalyzing ? '分析中...' : '开始分析' }}
      </button>
      <button
        v-else
        @click="cancelDraw"
        class="btn-warning"
      >
        取消绘制
      </button>
      <button @click="clearResults" class="btn-secondary">
        清除结果
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, toRef, onUnmounted } from 'vue'
import { useAnalysisStore } from '@/stores/analysis'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import type { VisibilityMode, ViewshedPropertyData, SightlineItemData } from '@/types/analysis'

const props = defineProps<{
  viewerInstance?: any
  sceneObjects?: any[]
}>()

defineEmits<{
  close: []
}>()

const store = useAnalysisStore()

const viewerRef = toRef(props, 'viewerInstance')
const {
  analyzeVisibility,
  clearSightline,
  getSightlineList,
  toggleSightlineVisibilityById,
  removeSightlineById,
  clearAllSightlineAnalysis,
  cancelDrawing,
  isDrawingState,
  // 可视域属性编辑
  setSelectedViewshed,
  setViewshedAngle,
  setViewshedAngle2,
  setViewshedDistance,
  setViewshedHeading,
  setViewshedPitch,
  setViewshedFrustum,
  setViewshedOpacity,
} = useAnalysisTool(viewerRef)

const currentMode = ref<VisibilityMode>('linear')
const observerHeight = ref(1.5)
const sampleCount = ref(45)
const visibleColor = ref('#00ff00')
const hiddenColor = ref('#ff0000')
const lastResult = ref<any>(null)
const localSightlineList = ref<SightlineItemData[]>([])
const isDrawing = ref(false)
const sightlineRefreshKey = ref(0)  // 用于手动刷新通视线列表

const viewshedProps = ref<ViewshedPropertyData>({
  horizontalAngle: 60,
  verticalAngle: 45,
  distance: 80,
  heading: 44,
  pitch: -12,
  showFrustum: false,
  opacity: 1.0
})

const modes = [
  { key: 'linear' as const, name: '线通视', icon: '📏' },
  { key: 'circular' as const, name: '圆通视', icon: '⭕' },
  { key: 'viewshed' as const, name: '可视域', icon: '🔭' }
]

// 同步通视线列表
const sightlineList = computed(() => {
  // eslint-disable-next-line @typescript-eslint/no-unused-expressions
  sightlineRefreshKey.value  // 依赖刷新key触发重算
  return getSightlineList()
})

// 轮询检测绘制状态
let drawingCheckTimer: ReturnType<typeof setInterval> | null = null
function startDrawingCheck() {
  stopDrawingCheck()
  drawingCheckTimer = setInterval(() => {
    isDrawing.value = isDrawingState()
  }, 300)
}
function stopDrawingCheck() {
  if (drawingCheckTimer) {
    clearInterval(drawingCheckTimer)
    drawingCheckTimer = null
  }
  isDrawing.value = false
}

const displayResults = computed(() => {
  if (!lastResult.value) {
    return []
  }
  const items: Array<{ label: string; value: string }> = [
    { label: '分析模式', value: currentMode.value === 'linear' ? '线通视' : currentMode.value === 'circular' ? '圆通视' : '可视域' },
    { label: '观察点高度', value: `${observerHeight.value} 米` }
  ]

  if (currentMode.value === 'linear' && lastResult.value.distance) {
    items.push({ label: '观察距离', value: `${lastResult.value.distance.toFixed(1)} 米` })
  }
  if (currentMode.value === 'circular') {
    items.push({ label: '分析方向数', value: `${lastResult.value.targetCount ?? sampleCount.value}` })
  }
  if (currentMode.value === 'viewshed') {
    items.push({ label: '水平张角', value: `${viewshedProps.value.horizontalAngle}°` })
    items.push({ label: '垂直张角', value: `${viewshedProps.value.verticalAngle}°` })
    items.push({ label: '投射距离', value: `${viewshedProps.value.distance} m` })
  }

  return items
})

async function startAnalysis() {
  isDrawing.value = true
  startDrawingCheck()
  try {
    const result = await analyzeVisibility(currentMode.value, {
      observerHeight: observerHeight.value,
      sampleCount: sampleCount.value,
      visibleColor: visibleColor.value,
      hiddenColor: hiddenColor.value,
      viewshedOptions: currentMode.value === 'viewshed' ? viewshedProps.value : undefined
    })
    if (result) {
      lastResult.value = result
      // 可视域分析完成后，设置选中图形并同步属性
      if (currentMode.value === 'viewshed' && result.graphic) {
        setSelectedViewshed(result.graphic)
        viewshedProps.value = {
          horizontalAngle: result.angle || 60,
          verticalAngle: result.angle2 || 45,
          distance: result.distance || 80,
          heading: result.heading || 44,
          pitch: result.pitch || -12,
          showFrustum: false,
          opacity: result.opacity ?? 1.0
        }
      }
    }
  } finally {
    stopDrawingCheck()
    sightlineRefreshKey.value++
  }
}

function cancelDraw() {
  cancelDrawing()
  stopDrawingCheck()
  isDrawing.value = false
}

function clearResults() {
  clearSightline()
  store.clearByType('visibility')
  lastResult.value = null
  sightlineRefreshKey.value++
}

function toggleItemVisibility(id: string) {
  toggleSightlineVisibilityById(id)
}

function removeItem(id: string) {
  removeSightlineById(id)
  sightlineRefreshKey.value++
  // 更新结果
  const list = getSightlineList()
  if (list.length === 0) {
    lastResult.value = null
  }
}

function getSightlineTypeLabel(type: string): string {
  const labels: Record<string, string> = { line: '线', circle: '圆', viewshed: '域' }
  return labels[type] || type
}

function formatSightlineTime(timestamp: Date): string {
  if (!timestamp) return ''
  const d = new Date(timestamp)
  return `${d.getHours().toString().padStart(2, '0')}:${d.getMinutes().toString().padStart(2, '0')}:${d.getSeconds().toString().padStart(2, '0')}`
}

/** 可视域属性实时更新 */
function onViewshedPropChange() {
  setViewshedAngle(viewshedProps.value.horizontalAngle)
  setViewshedAngle2(viewshedProps.value.verticalAngle)
  setViewshedDistance(viewshedProps.value.distance)
  setViewshedHeading(viewshedProps.value.heading)
  setViewshedPitch(viewshedProps.value.pitch)
  setViewshedFrustum(viewshedProps.value.showFrustum)
  setViewshedOpacity(viewshedProps.value.opacity)
}

// 组件卸载时清理定时器
onUnmounted(() => {
  stopDrawingCheck()
})
</script>

<style scoped>
.visibility-analysis {
  padding: 0.6rem;
}
.panel-header { margin-bottom: 0.8rem; }
.panel-header h4 { margin: 0; font-size: 0.85rem; color: rgba(255, 255, 255, 0.9); }

.mode-selector { display: flex; gap: 0.4rem; margin-bottom: 0.8rem; }
.mode-btn {
  flex: 1; display: flex; align-items: center; justify-content: center; gap: 0.3rem;
  padding: 0.4rem; background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1); border-radius: 4px;
  color: rgba(255, 255, 255, 0.8); font-size: 0.75rem; cursor: pointer;
  transition: all 0.2s ease;
}
.mode-btn:hover { background: rgba(99, 102, 241, 0.15); }
.mode-btn.active {
  background: rgba(99, 102, 241, 0.25);
  border-color: rgba(99, 102, 241, 0.5); color: #a5b4fc;
}
.mode-icon { font-size: 1rem; }

.params-section { margin-bottom: 0.8rem; }
.param-item { display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.5rem; }
.param-item label { font-size: 0.75rem; color: rgba(255, 255, 255, 0.7); white-space: nowrap; }
.param-item input[type="number"] {
  width: 80px; padding: 0.25rem 0.4rem; background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.2); border-radius: 4px;
  color: white; font-size: 0.75rem;
}
.param-item input[type="color"] {
  width: 40px; height: 24px; padding: 0; border: none; border-radius: 4px; cursor: pointer;
}

/* 可视域参数区域 */
.viewshed-params { margin-bottom: 0.8rem; }
.slider-group { display: flex; align-items: center; gap: 0.4rem; flex: 1; margin-left: 0.5rem; }
.slider-group input[type="range"] {
  flex: 1; height: 4px; -webkit-appearance: none; appearance: none;
  background: rgba(255, 255, 255, 0.15); border-radius: 2px; outline: none;
}
.slider-group input[type="range"]::-webkit-slider-thumb {
  -webkit-appearance: none; appearance: none;
  width: 12px; height: 12px; border-radius: 50%;
  background: #a5b4fc; cursor: pointer;
}
.slider-value { font-size: 0.7rem; color: #a5b4fc; min-width: 45px; text-align: right; }
.checkbox-label {
  display: flex; align-items: center; gap: 0.3rem; cursor: pointer;
}
.checkbox-label input[type="checkbox"] {
  width: 14px; height: 14px; accent-color: #6366f1;
}
.checkbox-label span { font-size: 0.75rem; color: rgba(255, 255, 255, 0.7); }

.instructions {
  background: rgba(99, 102, 241, 0.1); border: 1px solid rgba(99, 102, 241, 0.2);
  border-radius: 6px; padding: 0.6rem; margin-bottom: 0.8rem;
}
.instructions p { margin: 0; font-size: 0.7rem; color: rgba(255, 255, 255, 0.8); line-height: 1.5; }
.step { font-weight: 600; color: #a5b4fc; }

.error-banner {
  background: rgba(239, 68, 68, 0.15); border: 1px solid rgba(239, 68, 68, 0.3);
  border-radius: 4px; padding: 0.4rem 0.6rem; margin-bottom: 0.8rem;
}
.error-banner p { margin: 0; font-size: 0.7rem; color: rgba(239, 68, 68, 0.9); }

.results-section { margin-bottom: 0.8rem; }
.results-section h5 { margin: 0 0 0.5rem 0; font-size: 0.75rem; color: rgba(255, 255, 255, 0.8); }
.result-list { background: rgba(0, 0, 0, 0.2); border-radius: 6px; padding: 0.4rem; }
.result-item { display: flex; justify-content: space-between; padding: 0.3rem 0; font-size: 0.7rem; }
.result-label { color: rgba(255, 255, 255, 0.6); }
.result-value { color: #a5b4fc; font-weight: 500; }

/* 通视线列表 */
.sightline-list-section { margin-bottom: 0.8rem; }
.sightline-list-section h5 { margin: 0 0 0.5rem 0; font-size: 0.75rem; color: rgba(255, 255, 255, 0.8); }
.sightline-list { max-height: 120px; overflow-y: auto; }
.sightline-item {
  display: flex; align-items: center; gap: 0.3rem;
  padding: 0.3rem 0.4rem; border-radius: 4px; margin-bottom: 0.2rem;
  background: rgba(0, 0, 0, 0.15); font-size: 0.7rem;
}
.sightline-type {
  display: inline-flex; align-items: center; justify-content: center;
  width: 18px; height: 18px; border-radius: 50%;
  background: rgba(99, 102, 241, 0.3); color: #a5b4fc;
  font-size: 0.6rem; font-weight: 600;
}
.sightline-time { flex: 1; color: rgba(255, 255, 255, 0.4); font-size: 0.65rem; }
.btn-icon {
  width: 22px; height: 22px; display: flex; align-items: center; justify-content: center;
  border: none; border-radius: 3px; background: rgba(255, 255, 255, 0.06);
  cursor: pointer; font-size: 0.7rem; transition: all 0.2s ease;
  color: rgba(255, 255, 255, 0.6);
}
.btn-icon:hover { background: rgba(255, 255, 255, 0.12); }
.btn-icon.delete { color: rgba(239, 68, 68, 0.7); }
.btn-icon.delete:hover { background: rgba(239, 68, 68, 0.2); }

.actions { display: flex; gap: 0.4rem; }
.btn-primary,
.btn-secondary,
.btn-warning {
  flex: 1; padding: 0.4rem; border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px; font-size: 0.75rem; cursor: pointer; transition: all 0.2s ease;
}
.btn-primary { background: rgba(99, 102, 241, 0.2); color: #a5b4fc; }
.btn-primary:hover:not(:disabled) { background: rgba(99, 102, 241, 0.3); }
.btn-primary:disabled { opacity: 0.5; cursor: not-allowed; }
.btn-secondary { background: rgba(255, 255, 255, 0.1); color: rgba(255, 255, 255, 0.8); }
.btn-secondary:hover { background: rgba(255, 255, 255, 0.15); }
.btn-warning { background: rgba(245, 158, 11, 0.2); color: #fbbf24; border-color: rgba(245, 158, 11, 0.3); }
.btn-warning:hover { background: rgba(245, 158, 11, 0.3); }
</style>
