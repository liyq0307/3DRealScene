<template>
  <div class="constraint-analysis">
    <div class="panel-header">
      <h4>限制性分析</h4>
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

    <div v-if="constraints.length > 0" class="results-section">
      <h5>限制条件 ({{ constraints.length }})</h5>
      <div class="constraint-list">
        <div v-for="(c, i) in constraints" :key="i" class="constraint-item">
          <span class="constraint-type" :class="getConstraintClass(c.type)">{{ c.type }}</span>
          <div class="constraint-info">
            <span class="constraint-desc">{{ c.description }}</span>
            <span class="constraint-value">{{ c.value }}</span>
          </div>
        </div>
      </div>
      <div class="result-card">
        <div class="result-row">
          <span class="result-label">高度限制:</span>
          <span class="result-value">{{ heightLimit }}</span>
        </div>
        <div class="result-row">
          <span class="result-label">用地性质:</span>
          <span class="result-value">{{ landUse }}</span>
        </div>
        <div class="result-row">
          <span class="result-label">限制项数:</span>
          <span class="result-value">{{ constraints.length }}</span>
        </div>
      </div>
      <button class="btn-secondary" @click="handleExport" style="margin-top: 0.4rem; width: 100%;">
        导出结果
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import { useAnalysisStore } from '@/stores/analysis'
import type { DrawAreaType, ConstraintResultData } from '@/types/analysis'

const props = defineProps<{ viewerInstance?: any }>()
const { drawRectangle, drawCircleArea, drawPolygonArea, isReady, init } = useAnalysisTool(ref(props.viewerInstance))
const store = useAnalysisStore()

const areaType = ref<DrawAreaType>('rectangle')
const isDrawing = ref(false)
const errorMsg = ref('')
const constraints = ref<ConstraintResultData['constraints']>([])

const heightLimit = computed(() => {
  const h = constraints.value.find(c => c.type === '高度限制')
  return h ? String(h.value) : '--'
})
const landUse = computed(() => {
  const l = constraints.value.find(c => c.type === '用地性质')
  return l ? String(l.value) : '--'
})

onMounted(() => { if (!isReady.value) init() })

function getConstraintClass(type: string): string {
  if (['高度限制', '退让距离'].includes(type)) return 'warning'
  if (['用地性质', '保护区域'].includes(type)) return 'info'
  return ''
}

function simulateConstraints(): ConstraintResultData['constraints'] {
  const types = ['高度限制', '用地性质', '退让距离', '容积率上限', '绿化率下限', '保护区域', '建筑密度']
  const values = ['80m', 'R2居住用地', '6m', '2.5', '30%', '无', '35%']
  const descs = ['建筑高度不超过80米', '二类居住用地', '建筑退让道路红线6米', '容积率不超过2.5', '绿化率不低于30%', '无文物保护区域', '建筑密度不超过35%']
  return types.map((type, i) => ({ type, value: values[i], description: descs[i] }))
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
      const simConstraints = simulateConstraints()
      constraints.value = simConstraints
      const data: ConstraintResultData = {
        areaType: areaType.value,
        regionPositions: result.positionShow || result.positions,
        constraints: simConstraints
      }
      store.addResult({ type: 'constraint', name: '限制性分析', data, visible: true })
    }
  } catch (e: any) {
    errorMsg.value = e?.message || '绘制区域失败'
    store.addErrorResult('constraint', '限制性分析', errorMsg.value)
  } finally {
    isDrawing.value = false
  }
}

function handleExport() {
  const results = store.getResultsByType('constraint')
  if (results.length === 0) return
  const json = store.exportResults(results.map(r => r.id))
  const blob = new Blob([json], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `constraint_analysis_${Date.now()}.json`
  a.click()
  URL.revokeObjectURL(url)
}
</script>

<style scoped>
.constraint-analysis { padding: 0.6rem; }
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
.constraint-list { margin-bottom: 0.6rem; }
.constraint-item {
  display: flex; align-items: center; gap: 0.4rem;
  padding: 0.3rem 0.4rem; margin-bottom: 0.3rem;
  background: rgba(0, 0, 0, 0.2); border-radius: 4px;
}
.constraint-type {
  font-size: 0.65rem; padding: 0.15rem 0.3rem; border-radius: 3px;
  background: rgba(255, 255, 255, 0.1); color: rgba(255, 255, 255, 0.7); white-space: nowrap;
}
.constraint-type.warning { background: rgba(251, 191, 36, 0.2); color: #fbbf24; }
.constraint-type.info { background: rgba(99, 102, 241, 0.2); color: #a5b4fc; }
.constraint-info { flex: 1; display: flex; justify-content: space-between; font-size: 0.7rem; }
.constraint-desc { color: rgba(255, 255, 255, 0.6); }
.constraint-value { color: #a5b4fc; font-weight: 500; }
.result-card { background: rgba(0, 0, 0, 0.2); border-radius: 6px; padding: 0.5rem; }
.result-row { display: flex; justify-content: space-between; padding: 0.3rem 0; font-size: 0.75rem; }
.result-label { color: rgba(255, 255, 255, 0.6); }
.result-value { color: #a5b4fc; font-weight: 500; }
.actions { display: flex; gap: 0.4rem; margin-top: 0.4rem; }
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
</style>
