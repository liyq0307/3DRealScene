<template>
  <div class="volume-calculation">
    <div class="panel-header">
      <h4>体积计算</h4>
    </div>

    <div class="params-section">
      <div class="param-item">
        <label>基准面高度</label>
        <input v-model.number="baseHeight" type="number" step="1" />
      </div>
      <div class="param-item">
        <label>计算方式</label>
        <select v-model="calcMethod">
          <option value="above">基准面以上</option>
          <option value="below">基准面以下</option>
          <option value="both">整体体积</option>
        </select>
      </div>
    </div>

    <div class="instructions">
      <p>选择区域计算填挖方量</p>
    </div>

    <div v-if="store.error" class="error-banner">
      <p>{{ store.error.message }}</p>
    </div>

    <div v-if="calculationResult" class="results-section">
      <h5>计算结果</h5>
      <div class="result-card">
        <div class="result-row">
          <span class="result-label">总体积:</span>
          <span class="result-value">{{ formatVolume(calculationResult.totalVolume) }}</span>
        </div>
        <div v-if="calcMethod !== 'both'" class="result-row">
          <span class="result-label">{{ calcMethod === 'above' ? '填方量' : '挖方量' }}:</span>
          <span class="result-value">{{ formatVolume(calculationResult.fillVolume) }}</span>
        </div>
        <div class="result-row">
          <span class="result-label">计算面积:</span>
          <span class="result-value">{{ calculationResult.area.toFixed(2) }} 平方米</span>
        </div>
        <div class="result-row">
          <span class="result-label">平均高度:</span>
          <span class="result-value">{{ calculationResult.avgHeight.toFixed(2) }} 米</span>
        </div>
      </div>
    </div>

    <div class="actions">
      <button @click="startCalculation" class="btn-primary">
        开始计算
      </button>
      <button @click="clearResult" class="btn-secondary">
        清除
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, toRef } from 'vue'
import { useAnalysisStore } from '@/stores/analysis'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import type { VolumeMethod, VolumeResultData } from '@/types/analysis'

const props = defineProps<{
  viewerInstance?: any
}>()

const store = useAnalysisStore()
const viewerRef = toRef(props, 'viewerInstance')
const { calculateVolume, clearMeasure } = useAnalysisTool(viewerRef)

const baseHeight = ref(0)
const calcMethod = ref<VolumeMethod>('above')
const calculationResult = ref<VolumeResultData | null>(null)

function formatVolume(volume: number): string {
  if (volume >= 1000000) {
    return `${(volume / 1000000).toFixed(2)} 百万立方米`
  }
  if (volume >= 1000) {
    return `${(volume / 1000).toFixed(2)} 千立方米`
  }
  return `${volume.toFixed(2)} 立方米`
}

async function startCalculation() {
  const result = await calculateVolume(baseHeight.value, calcMethod.value)
  if (result) {
    calculationResult.value = result
  }
}

function clearResult() {
  calculationResult.value = null
  clearMeasure()
  store.clearByType('volume')
}
</script>

<style scoped>
.volume-calculation { padding: 0.6rem; }
.panel-header { margin-bottom: 0.8rem; }
.panel-header h4 { margin: 0; font-size: 0.85rem; color: rgba(255, 255, 255, 0.9); }

.params-section { margin-bottom: 0.8rem; }
.param-item { display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.5rem; }
.param-item label { font-size: 0.75rem; color: rgba(255, 255, 255, 0.7); }
.param-item input[type="number"],
.param-item select {
  width: 120px; padding: 0.25rem 0.4rem; background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.2); border-radius: 4px;
  color: white; font-size: 0.75rem;
}
.param-item select option { background: #1a1a1a; }

.instructions {
  background: rgba(99, 102, 241, 0.1); border: 1px solid rgba(99, 102, 241, 0.2);
  border-radius: 6px; padding: 0.6rem; margin-bottom: 0.8rem;
}
.instructions p { margin: 0; font-size: 0.7rem; color: rgba(255, 255, 255, 0.8); text-align: center; }

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
.btn-primary:hover { background: rgba(99, 102, 241, 0.3); }
.btn-secondary { background: rgba(255, 255, 255, 0.1); color: rgba(255, 255, 255, 0.8); }
.btn-secondary:hover { background: rgba(255, 255, 255, 0.15); }
</style>
