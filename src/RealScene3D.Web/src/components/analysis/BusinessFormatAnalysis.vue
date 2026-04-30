<template>
  <div class="business-format-analysis">
    <div class="panel-header">
      <h4>业态分析</h4>
    </div>

    <div class="params-section">
      <div class="param-item">
        <label>区域类型</label>
        <select v-model="areaType">
          <option value="rectangle">矩形</option>
          <option value="circle">圆形</option>
          <option value="polygon">多边形</option>
        </select>
      </div>
    </div>

    <div class="actions">
      <button class="btn-primary" @click="handleDraw" :disabled="isDrawing">
        {{ isDrawing ? '绘制中...' : '绘制分析区域' }}
      </button>
    </div>

    <div v-if="errorMsg" class="error-banner">
      <p>{{ errorMsg }}</p>
    </div>

    <div v-if="analysisData" class="results-section">
      <h5>业态分布</h5>
      <div class="chart-container">
        <div class="pie-chart">
          <div v-for="(item, i) in analysisData.statistics" :key="i" class="pie-legend-item">
            <span class="legend-dot" :style="{ background: chartColors[i % chartColors.length] }"></span>
            <span class="legend-label">{{ item.category }}</span>
            <span class="legend-value">{{ item.percentage.toFixed(1) }}%</span>
          </div>
        </div>
        <div class="bar-chart">
          <div v-for="(item, i) in analysisData.statistics" :key="i" class="bar-item">
            <span class="bar-label">{{ item.category }}</span>
            <div class="bar-track">
              <div class="bar-fill" :style="{ width: item.percentage + '%', background: chartColors[i % chartColors.length] }"></div>
            </div>
            <span class="bar-count">{{ item.count }}</span>
          </div>
        </div>
      </div>
      <div class="result-card">
        <div class="result-row">
          <span class="result-label">业态类型数:</span>
          <span class="result-value">{{ analysisData.statistics.length }}</span>
        </div>
        <div class="result-row">
          <span class="result-label">总数量:</span>
          <span class="result-value">{{ totalBusinessCount }}</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import { useAnalysisStore } from '@/stores/analysis'
import type { DrawAreaType, BusinessFormatResultData } from '@/types/analysis'

const props = defineProps<{ viewerInstance?: any }>()
const { drawRectangle, drawCircleArea, drawPolygonArea, isReady, init } = useAnalysisTool(ref(props.viewerInstance))
const store = useAnalysisStore()

const areaType = ref<DrawAreaType>('rectangle')
const isDrawing = ref(false)
const errorMsg = ref('')
const analysisData = ref<BusinessFormatResultData | null>(null)

const chartColors = ['#a5b4fc', '#4ade80', '#fbbf24', '#f87171', '#818cf8', '#34d399', '#fb923c', '#f472b6']

const totalBusinessCount = computed(() =>
  analysisData.value ? analysisData.value.statistics.reduce((s, i) => s + i.count, 0) : 0
)

onMounted(() => { if (!isReady.value) init() })

function simulateBusinessData(): BusinessFormatResultData['statistics'] {
  const categories = ['零售', '餐饮', '办公', '居住', '教育', '医疗', '娱乐', '其他']
  const total = 40 + Math.floor(Math.random() * 60)
  return categories.map((category, i) => {
    const count = Math.max(1, Math.floor(total * (0.25 - i * 0.02) + Math.random() * 5))
    return { category, count, area: count * (50 + Math.random() * 100), percentage: 0 }
  }).map(item => ({ ...item, percentage: (item.count / total) * 100 }))
}

async function handleDraw() {
  errorMsg.value = ''
  isDrawing.value = true
  try {
    let result = null
    switch (areaType.value) {
      case 'rectangle': result = await drawRectangle(); break
      case 'circle': result = await drawCircleArea(); break
      case 'polygon': result = await drawPolygonArea(); break
    }
    if (result) {
      const statistics = simulateBusinessData()
      analysisData.value = {
        areaType: areaType.value,
        regionPositions: result.positionsShow || result.positions,
        statistics
      }
      store.addResult({
        type: 'business-format',
        name: '业态分析',
        data: analysisData.value,
        visible: true
      })
    }
  } catch (e: any) {
    errorMsg.value = e?.message || '绘制区域失败'
    store.addErrorResult('business-format', '业态分析', errorMsg.value)
  } finally {
    isDrawing.value = false
  }
}
</script>

<style scoped>
.business-format-analysis { padding: 0.6rem; }
.panel-header { margin-bottom: 0.8rem; }
.panel-header h4 { margin: 0; font-size: 0.85rem; color: rgba(255, 255, 255, 0.9); }
.params-section { margin-bottom: 0.8rem; }
.param-item { display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.5rem; }
.param-item label { font-size: 0.75rem; color: rgba(255, 255, 255, 0.7); }
.param-item select {
  width: 120px; padding: 0.25rem 0.4rem; background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.2); border-radius: 4px;
  color: rgba(255, 255, 255, 0.9); font-size: 0.75rem;
}
.param-item select option { background: #1a1a1a; }
.error-banner {
  background: rgba(239, 68, 68, 0.15); border: 1px solid rgba(239, 68, 68, 0.3);
  border-radius: 4px; padding: 0.4rem 0.6rem; margin-bottom: 0.8rem;
}
.error-banner p { margin: 0; font-size: 0.7rem; color: rgba(239, 68, 68, 0.9); }
.results-section { margin-bottom: 0.8rem; }
.results-section h5 { margin: 0 0 0.5rem 0; font-size: 0.75rem; color: rgba(255, 255, 255, 0.8); }
.chart-container { margin-bottom: 0.6rem; }
.pie-chart { margin-bottom: 0.5rem; }
.pie-legend-item { display: flex; align-items: center; gap: 0.4rem; padding: 0.2rem 0; font-size: 0.7rem; }
.legend-dot { width: 8px; height: 8px; border-radius: 50%; flex-shrink: 0; }
.legend-label { color: rgba(255, 255, 255, 0.7); flex: 1; }
.legend-value { color: #a5b4fc; font-weight: 500; }
.bar-chart { margin-top: 0.5rem; }
.bar-item { display: flex; align-items: center; gap: 0.3rem; margin-bottom: 0.3rem; font-size: 0.7rem; }
.bar-label { width: 40px; color: rgba(255, 255, 255, 0.6); text-align: right; }
.bar-track { flex: 1; height: 6px; background: rgba(255, 255, 255, 0.1); border-radius: 3px; overflow: hidden; }
.bar-fill { height: 100%; border-radius: 3px; transition: width 0.3s ease; }
.bar-count { width: 20px; color: rgba(255, 255, 255, 0.6); }
.result-card { background: rgba(0, 0, 0, 0.2); border-radius: 6px; padding: 0.5rem; }
.result-row { display: flex; justify-content: space-between; padding: 0.3rem 0; font-size: 0.75rem; }
.result-label { color: rgba(255, 255, 255, 0.6); }
.result-value { color: #a5b4fc; font-weight: 500; }
.actions { display: flex; gap: 0.4rem; margin-top: 0.4rem; }
.btn-primary {
  flex: 1; padding: 0.4rem; border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px; font-size: 0.75rem; cursor: pointer; transition: all 0.2s ease;
  background: rgba(99, 102, 241, 0.2); color: #a5b4fc;
}
.btn-primary:hover:not(:disabled) { background: rgba(99, 102, 241, 0.3); }
.btn-primary:disabled { opacity: 0.5; cursor: not-allowed; }
</style>
