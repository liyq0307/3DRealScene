<template>
  <div class="processing-params-panel">
    <!-- LOD配置 -->
    <div class="param-section">
      <div class="section-header">
        <h4 class="section-title">LOD配置</h4>
      </div>
      <div class="section-content">
        <SwitchControl
          v-model="localEnableMeshDecimation"
          label="启用网格简化（LOD生成）"
        />
        <div class="form-row">
          <label class="form-label">LOD层级数</label>
          <input
            :value="props.lodLevels"
            @input="emit('update:lodLevels', Number(($event.target as HTMLInputElement).value))"
            type="number"
            min="1"
            max="5"
            class="form-input-small"
          />
        </div>
        <div class="form-row" v-if="props.enableMeshDecimation">
          <label class="form-label">空间分割深度</label>
          <input
            :value="props.divisions"
            @input="emit('update:divisions', Number(($event.target as HTMLInputElement).value))"
            type="number"
            min="1"
            max="4"
            class="form-input-small"
          />
        </div>
      </div>
    </div>

    <!-- 输出配置 -->
    <div class="param-section">
      <div class="section-header">
        <h4 class="section-title">输出配置</h4>
      </div>
      <div class="section-content">
        <div class="form-row">
          <label class="form-label">输出格式</label>
          <select
            :value="props.outputFormat"
            @change="emit('update:outputFormat', ($event.target as HTMLSelectElement).value)"
            class="form-select"
          >
            <option value="b3dm">B3DM（推荐）</option>
            <option value="gltf">GLTF</option>
            <option value="i3dm">I3DM</option>
            <option value="pnts">PNTS</option>
            <option value="cmpt">CMPT</option>
          </select>
        </div>
        <div class="form-row">
          <label class="form-label">纹理策略</label>
          <select
            :value="props.textureStrategy"
            @change="emit('update:textureStrategy', Number(($event.target as HTMLSelectElement).value))"
            class="form-select"
          >
            <option :value="2">Repack（推荐）</option>
            <option :value="3">RepackCompressed</option>
            <option :value="1">Compress</option>
            <option :value="0">KeepOriginal</option>
          </select>
        </div>
      </div>
    </div>

    <!-- 优化选项 -->
    <div class="param-section">
      <div class="section-header">
        <h4 class="section-title">优化选项</h4>
      </div>
      <div class="section-content">
        <SwitchControl
          v-model="localEnableCompression"
          label="启用几何压缩"
        />
        <SwitchControl
          v-model="localEnableIncrementalUpdate"
          label="启用增量更新"
        />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import SwitchControl from './SwitchControl.vue'

interface Props {
  lodLevels: number
  divisions: number
  outputFormat: string
  textureStrategy: number
  enableMeshDecimation: boolean
  enableCompression: boolean
  enableIncrementalUpdate: boolean
}

interface Emits {
  (e: 'update:lodLevels', value: number): void
  (e: 'update:divisions', value: number): void
  (e: 'update:outputFormat', value: string): void
  (e: 'update:textureStrategy', value: number): void
  (e: 'update:enableMeshDecimation', value: boolean): void
  (e: 'update:enableCompression', value: boolean): void
  (e: 'update:enableIncrementalUpdate', value: boolean): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const localEnableMeshDecimation = computed({
  get: () => props.enableMeshDecimation,
  set: (val) => emit('update:enableMeshDecimation', val)
})

const localEnableCompression = computed({
  get: () => props.enableCompression,
  set: (val) => emit('update:enableCompression', val)
})

const localEnableIncrementalUpdate = computed({
  get: () => props.enableIncrementalUpdate,
  set: (val) => emit('update:enableIncrementalUpdate', val)
})
</script>

<style scoped>
.processing-params-panel {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.param-section {
  background: #f8f9fa;
  border-radius: 4px;
  padding: 0.75rem;
}

.section-header {
  margin-bottom: 0.5rem;
  padding-bottom: 0.35rem;
  border-bottom: 1px solid #e1e5e9;
}

.section-title {
  margin: 0;
  font-size: 0.85rem;
  font-weight: 600;
  color: #007acc;
}

.section-content {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.form-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
}

.form-label {
  font-size: 0.85rem;
  font-weight: 500;
  color: #333;
  flex-shrink: 0;
}

.form-input-small {
  width: 80px;
  padding: 0.3rem 0.5rem;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  font-size: 0.85rem;
  text-align: center;
}

.form-input-small:focus {
  outline: none;
  border-color: #007acc;
}

.form-select {
  width: 140px;
  padding: 0.3rem 0.5rem;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  font-size: 0.85rem;
}

.form-select:focus {
  outline: none;
  border-color: #007acc;
}
</style>
