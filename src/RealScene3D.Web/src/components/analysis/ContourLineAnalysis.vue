<template>
  <div class="contour-line-analysis">
    <div class="section-title">等高线分析</div>
    <div class="form-group">
      <label>等高距(m)</label>
      <input v-model.number="spacing" type="number" min="1" step="5" />
    </div>
    <div class="form-group">
      <label>线宽</label>
      <input v-model.number="lineWidth" type="number" min="1" max="10" />
    </div>
    <div class="form-group">
      <label>颜色</label>
      <input v-model="lineColor" type="color" />
    </div>
    <div class="btn-group">
      <button class="btn-primary" @click="handleGenerate" :disabled="isGenerating">生成等高线</button>
    </div>
    <div class="btn-group" style="margin-top: 8px;">
      <button class="btn-secondary" @click="handleToggleVisible">{{ visible ? '隐藏' : '显示' }}</button>
      <button class="btn-danger" @click="handleClear">清除</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'

const props = defineProps<{ viewerInstance?: any }>()
const { generateContourLine, setContourSpacing, setContourWidth, setContourColor, toggleContourVisible, clearContour, isReady, init } = useAnalysisTool(ref(props.viewerInstance))

const spacing = ref(10)
const lineWidth = ref(2)
const lineColor = ref('#ff0000')
const isGenerating = ref(false)
const visible = ref(true)

onMounted(() => { if (!isReady.value) init() })

async function handleGenerate() {
  isGenerating.value = true
  await generateContourLine({ spacing: spacing.value, lineWidth: lineWidth.value, lineColor: lineColor.value, showLabel: true })
  isGenerating.value = false
}

function handleToggleVisible() {
  visible.value = !visible.value
  toggleContourVisible(visible.value)
}

function handleClear() { clearContour() }
</script>

<style scoped>
.contour-line-analysis { padding: 12px; }
.section-title { font-size: 14px; font-weight: bold; margin-bottom: 12px; color: #e0e0e0; }
.form-group { margin-bottom: 8px; display: flex; align-items: center; gap: 8px; }
.form-group label { width: 70px; font-size: 12px; color: #aaa; }
.form-group input { flex: 1; padding: 4px 8px; background: #2a2a2a; border: 1px solid #444; color: #e0e0e0; border-radius: 4px; font-size: 12px; }
.btn-group { display: flex; gap: 8px; }
.btn-primary { flex: 1; padding: 6px; background: #1890ff; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 12px; }
.btn-secondary { flex: 1; padding: 6px; background: #444; color: #e0e0e0; border: none; border-radius: 4px; cursor: pointer; font-size: 12px; }
.btn-danger { flex: 1; padding: 6px; background: #ff4d4f; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 12px; }
.btn-primary:disabled { opacity: 0.5; }
</style>
