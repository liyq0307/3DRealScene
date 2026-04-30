<template>
  <div class="building-spacing-analysis">
    <div class="panel-header">
      <h4>多建筑间距分析</h4>
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
      <div class="param-item">
        <label>间距阈值(m)</label>
        <input v-model.number="minDistance" type="number" step="1" min="1" />
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

    <div v-if="spacingResult" class="results-section">
      <h5>分析结果</h5>
      <div class="result-card">
        <div class="result-row">
          <span class="result-label">建筑对数:</span>
          <span class="result-value">{{ spacingResult.totalPairs }}</span>
        </div>
        <div class="result-row">
          <span class="result-label">合规率:</span>
          <span class="result-value" :class="{ 'text-danger': spacingResult.complianceRate < 1 }">
            {{ (spacingResult.complianceRate * 100).toFixed(1) }}%
          </span>
        </div>
        <div class="result-row">
          <span class="result-label">违规数:</span>
          <span class="result-value" :class="{ 'text-danger': spacingResult.totalViolations > 0 }">
            {{ spacingResult.totalViolations }}
          </span>
        </div>
      </div>

      <div class="spacing-table">
        <div class="table-header">
          <span>建筑A</span>
          <span>建筑B</span>
          <span>间距</span>
          <span>状态</span>
        </div>
        <div v-for="(pair, i) in spacingResult.pairs" :key="i" class="table-row" :class="{ 'row-violation': !pair.compliant }">
          <span>{{ pair.buildingA }}</span>
          <span>{{ pair.buildingB }}</span>
          <span>{{ pair.distance.toFixed(1) }}m</span>
          <span class="status-badge" :class="pair.compliant ? 'compliant' : 'violation'">
            {{ pair.compliant ? '合规' : '违规' }}
          </span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import { useAnalysisStore } from '@/stores/analysis'
import type { DrawAreaType, BuildingSpacingResultData } from '@/types/analysis'

const props = defineProps<{ viewerInstance?: any }>()
const { drawRectangle, drawCircleArea, drawPolygonArea, isReady, init } = useAnalysisTool(ref(props.viewerInstance))
const store = useAnalysisStore()

const areaType = ref<DrawAreaType>('rectangle')
const minDistance = ref(6)
const isDrawing = ref(false)
const errorMsg = ref('')
const spacingResult = ref<BuildingSpacingResultData | null>(null)

onMounted(() => { if (!isReady.value) init() })

function simulateSpacingData(): BuildingSpacingResultData {
  const buildingNames = ['A栋', 'B栋', 'C栋', 'D栋', 'E栋']
  const pairs: BuildingSpacingResultData['pairs'] = []
  for (let i = 0; i < buildingNames.length; i++) {
    for (let j = i + 1; j < buildingNames.length; j++) {
      const distance = minDistance.value * (0.5 + Math.random() * 1.5)
      pairs.push({
        buildingA: buildingNames[i],
        buildingB: buildingNames[j],
        distance,
        minRequiredDistance: minDistance.value,
        compliant: distance >= minDistance.value
      })
    }
  }
  const totalViolations = pairs.filter(p => !p.compliant).length
  return {
    pairs,
    totalViolations,
    totalPairs: pairs.length,
    complianceRate: (pairs.length - totalViolations) / pairs.length
  }
}

async function handleDraw() {
  if (minDistance.value < 1) { errorMsg.value = '间距阈值不能小于1m'; return }
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
      const simResult = simulateSpacingData()
      spacingResult.value = simResult
      store.addResult({
        type: 'building-spacing',
        name: '建筑间距分析',
        data: { ...simResult, areaType: areaType.value, minDistance: minDistance.value, regionPositions: result.positionShow || result.positions },
        visible: true
      })
    }
  } catch (e: any) {
    errorMsg.value = e?.message || '绘制区域失败'
    store.addErrorResult('building-spacing', '建筑间距分析', errorMsg.value)
  } finally {
    isDrawing.value = false
  }
}
</script>

<style scoped>
.building-spacing-analysis { padding: 0.6rem; }
.panel-header { margin-bottom: 0.8rem; }
.panel-header h4 { margin: 0; font-size: 0.85rem; color: rgba(255, 255, 255, 0.9); }
.params-section { margin-bottom: 0.8rem; }
.param-item { display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.5rem; }
.param-item label { font-size: 0.75rem; color: rgba(255, 255, 255, 0.7); }
.param-item select,
.param-item input[type="number"] {
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
.result-card { background: rgba(0, 0, 0, 0.2); border-radius: 6px; padding: 0.5rem; margin-bottom: 0.6rem; }
.result-row { display: flex; justify-content: space-between; padding: 0.3rem 0; font-size: 0.75rem; }
.result-label { color: rgba(255, 255, 255, 0.6); }
.result-value { color: #a5b4fc; font-weight: 500; }
.text-danger { color: rgba(239, 68, 68, 0.9) !important; }
.spacing-table { font-size: 0.7rem; }
.table-header, .table-row {
  display: grid; grid-template-columns: 1fr 1fr 1fr 1fr; gap: 0.2rem;
  padding: 0.3rem 0.4rem; align-items: center;
}
.table-header {
  color: rgba(255, 255, 255, 0.5); border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}
.table-row { border-bottom: 1px solid rgba(255, 255, 255, 0.05); color: rgba(255, 255, 255, 0.7); }
.row-violation { background: rgba(239, 68, 68, 0.08); }
.status-badge {
  display: inline-block; padding: 0.1rem 0.3rem; border-radius: 3px; font-size: 0.6rem;
}
.status-badge.compliant { background: rgba(74, 222, 128, 0.2); color: #4ade80; }
.status-badge.violation { background: rgba(239, 68, 68, 0.2); color: rgba(239, 68, 68, 0.9); }
.actions { display: flex; gap: 0.4rem; margin-top: 0.4rem; }
.btn-primary {
  flex: 1; padding: 0.4rem; border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px; font-size: 0.75rem; cursor: pointer; transition: all 0.2s ease;
  background: rgba(99, 102, 241, 0.2); color: #a5b4fc;
}
.btn-primary:hover:not(:disabled) { background: rgba(99, 102, 241, 0.3); }
.btn-primary:disabled { opacity: 0.5; cursor: not-allowed; }
</style>
