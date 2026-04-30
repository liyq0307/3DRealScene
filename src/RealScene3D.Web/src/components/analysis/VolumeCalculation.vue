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
        <label>围墙底高</label>
        <input v-model.number="wallBottomHeight" type="number" step="1" />
      </div>
      <div class="param-item">
        <label>围墙顶高</label>
        <input v-model.number="wallTopHeight" type="number" step="1" />
      </div>
      <div class="param-item">
        <label>计算方式</label>
        <select v-model="calcMethod">
          <option value="above">基准面以上</option>
          <option value="below">基准面以下</option>
          <option value="both">整体体积</option>
        </select>
      </div>
      <div class="param-item">
        <label>显示三角网</label>
        <label class="toggle">
          <input v-model="showTriGrid" type="checkbox" />
          <span class="toggle-slider"></span>
        </label>
      </div>
    </div>

    <div class="instructions">
      <p>选择区域计算填挖方量</p>
    </div>

    <div v-if="isCalculating" class="progress-banner">
      <p>计算中，请稍候...</p>
    </div>

    <div v-if="errorMsg" class="error-banner">
      <p>{{ errorMsg }}</p>
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
          <span class="result-value">{{ calculationResult.area.toFixed(2) }} m²</span>
        </div>
        <div class="result-row">
          <span class="result-label">平均高度:</span>
          <span class="result-value">{{ calculationResult.avgHeight.toFixed(2) }} m</span>
        </div>
      </div>
    </div>

    <div class="actions">
      <button @click="startCalculation" class="btn-primary" :disabled="isCalculating">
        {{ isCalculating ? '计算中...' : '开始计算' }}
      </button>
      <button @click="clearResult" class="btn-secondary">清除</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, toRef } from 'vue'
import { useAnalysisStore } from '@/stores/analysis'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import type { VolumeMethod, VolumeResultData } from '@/types/analysis'

const props = defineProps<{ viewerInstance?: any }>()
const store = useAnalysisStore()
const viewerRef = toRef(props, 'viewerInstance')
const { calculateVolume, clearMeasure } = useAnalysisTool(viewerRef)

const baseHeight = ref(0)
const wallBottomHeight = ref(0)
const wallTopHeight = ref(50)
const calcMethod = ref<VolumeMethod>('above')
const showTriGrid = ref(false)
const isCalculating = ref(false)
const errorMsg = ref('')
const calculationResult = ref<VolumeResultData | null>(null)

function formatVolume(volume: number): string {
  if (volume >= 1000000) return `${(volume / 1000000).toFixed(2)} 百万m³`
  if (volume >= 1000) return `${(volume / 1000).toFixed(2)} 千m³`
  return `${volume.toFixed(2)} m³`
}

async function startCalculation() {
  if (wallBottomHeight.value >= wallTopHeight.value) {
    errorMsg.value = '围墙底高必须小于顶高'
    return
  }
  errorMsg.value = ''
  isCalculating.value = true
  try {
    const result = await calculateVolume(baseHeight.value, calcMethod.value)
    if (result) {
      calculationResult.value = result
      store.addResult({
        type: 'volume',
        name: '体积计算',
        data: { ...result, wallBottomHeight: wallBottomHeight.value, wallTopHeight: wallTopHeight.value, showTriGrid: showTriGrid.value },
        visible: true
      })
    }
  } catch (e: any) {
    errorMsg.value = e?.message || '体积计算失败'
    store.addErrorResult('volume', '体积计算', errorMsg.value)
  } finally {
    isCalculating.value = false
  }
}

function clearResult() {
  calculationResult.value = null
  errorMsg.value = ''
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
  color: rgba(255, 255, 255, 0.9); font-size: 0.75rem;
}
.param-item select option { background: #1a1a1a; }
.toggle { position: relative; display: inline-block; width: 36px; height: 20px; }
.toggle input { opacity: 0; width: 0; height: 0; }
.toggle-slider {
  position: absolute; cursor: pointer; top: 0; left: 0; right: 0; bottom: 0;
  background: rgba(255, 255, 255, 0.15); border-radius: 20px; transition: 0.2s;
}
.toggle-slider::before {
  content: ''; position: absolute; height: 16px; width: 16px;
  left: 2px; bottom: 2px; background: white; border-radius: 50%; transition: 0.2s;
}
.toggle input:checked + .toggle-slider { background: rgba(99, 102, 241, 0.5); }
.toggle input:checked + .toggle-slider::before { transform: translateX(16px); }
.instructions {
  background: rgba(99, 102, 241, 0.1); border: 1px solid rgba(99, 102, 241, 0.2);
  border-radius: 6px; padding: 0.6rem; margin-bottom: 0.8rem;
}
.instructions p { margin: 0; font-size: 0.7rem; color: rgba(255, 255, 255, 0.8); text-align: center; }
.progress-banner {
  background: rgba(99, 102, 241, 0.1); border: 1px solid rgba(99, 102, 241, 0.2);
  border-radius: 4px; padding: 0.4rem 0.6rem; margin-bottom: 0.8rem;
}
.progress-banner p { margin: 0; font-size: 0.7rem; color: #a5b4fc; text-align: center; }
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
</style>
