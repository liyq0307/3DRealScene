<template>
  <div class="processing-params-panel">
    <!-- 重建顶层 -->
    <div class="param-section">
      <div class="section-header">
        <h4 class="section-title">重建顶层</h4>
      </div>
      <div class="section-content">
        <SwitchControl
          v-model="localHighQualityReconstruction"
          label="高质量重建"
        />
      </div>
    </div>

    <!-- 效果参数 -->
    <div class="param-section">
      <div class="section-header">
        <h4 class="section-title">效果参数</h4>
      </div>
      <div class="section-content">
        <SwitchControl
          v-model="localForceDoubleSided"
          label="强制双面"
        />
        <SwitchControl
          v-model="localNoLighting"
          label="无光照"
        />
      </div>
    </div>

    <!-- 压缩参数 -->
    <div class="param-section">
      <div class="section-header">
        <h4 class="section-title">压缩参数</h4>
      </div>
      <div class="section-content">
        <SwitchControl
          v-model="localTextureCompression"
          label="纹理压缩"
        />
        <SwitchControl
          v-model="localVertexCompression"
          label="顶点压缩"
        />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import SwitchControl from './SwitchControl.vue'

interface Props {
  highQualityReconstruction: boolean
  forceDoubleSided: boolean
  noLighting: boolean
  textureCompression: boolean
  vertexCompression: boolean
}

interface Emits {
  (e: 'update:highQualityReconstruction', value: boolean): void
  (e: 'update:forceDoubleSided', value: boolean): void
  (e: 'update:noLighting', value: boolean): void
  (e: 'update:textureCompression', value: boolean): void
  (e: 'update:vertexCompression', value: boolean): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

// 使用computed实现双向绑定
const localHighQualityReconstruction = computed({
  get: () => props.highQualityReconstruction,
  set: (val) => emit('update:highQualityReconstruction', val)
})

const localForceDoubleSided = computed({
  get: () => props.forceDoubleSided,
  set: (val) => emit('update:forceDoubleSided', val)
})

const localNoLighting = computed({
  get: () => props.noLighting,
  set: (val) => emit('update:noLighting', val)
})

const localTextureCompression = computed({
  get: () => props.textureCompression,
  set: (val) => emit('update:textureCompression', val)
})

const localVertexCompression = computed({
  get: () => props.vertexCompression,
  set: (val) => emit('update:vertexCompression', val)
})
</script>

<style scoped>
.processing-params-panel {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.param-section {
  background: #f8f9fa;
  border-radius: 6px;
  padding: 0.75rem;
}

.section-header {
  margin-bottom: 0.75rem;
  padding-bottom: 0.5rem;
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
  gap: 0.6rem;
}
</style>
