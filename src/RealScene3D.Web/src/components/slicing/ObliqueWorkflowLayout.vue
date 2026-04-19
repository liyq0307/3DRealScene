<template>
  <div class="oblique-workflow-layout">
    <!-- 左侧：输入文件 -->
    <WorkflowStage title="输入文件" icon="📁" position="left">
      <InputFilePanel
        v-model:dataPath="localFormData.dataPath"
        v-model:spatialReference="localFormData.spatialReference"
        v-model:zeroPoint="localFormData.zeroPoint"
        @publish="$emit('publish')"
      />
    </WorkflowStage>

    <!-- 箭头1 -->
    <div class="workflow-arrow-container">
      <WorkflowArrow />
    </div>

    <!-- 中间：处理参数 -->
    <WorkflowStage title="处理参数" icon="⚙️" position="center">
      <ProcessingParamsPanel
        v-model:highQualityReconstruction="localFormData.highQualityReconstruction"
        v-model:forceDoubleSided="localFormData.forceDoubleSided"
        v-model:noLighting="localFormData.noLighting"
        v-model:textureCompression="localFormData.textureCompression"
        v-model:vertexCompression="localFormData.vertexCompression"
      />
    </WorkflowStage>

    <!-- 箭头2 -->
    <div class="workflow-arrow-container">
      <WorkflowArrow />
    </div>

    <!-- 右侧：存储类型 -->
    <WorkflowStage title="存储类型" icon="💾" position="right">
      <StorageTypePanel
        v-model:store3DTiles11="localFormData.store3DTiles11"
        v-model:storageType="localFormData.storageType"
        v-model:outputPath="localFormData.outputPath"
        @submit="handleSubmit"
      />
    </WorkflowStage>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import WorkflowStage from './WorkflowStage.vue'
import WorkflowArrow from './WorkflowArrow.vue'
import InputFilePanel from './InputFilePanel.vue'
import ProcessingParamsPanel from './ProcessingParamsPanel.vue'
import StorageTypePanel from './StorageTypePanel.vue'
import type { ObliqueSliceFormData } from '@/types/obliqueSlice'

interface Props {
  formData: ObliqueSliceFormData
}

interface Emits {
  (e: 'update:formData', value: ObliqueSliceFormData): void
  (e: 'submit'): void
  (e: 'publish'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

// 使用computed实现双向绑定
const localFormData = computed({
  get: () => props.formData,
  set: (val) => emit('update:formData', val)
})

const handleSubmit = () => {
  emit('submit')
}
</script>

<style scoped>
.oblique-workflow-layout {
  display: flex;
  align-items: center;
  gap: 0;
  padding: 1.5rem;
  background: #f5f5f5;
  border-radius: 8px;
}

.workflow-arrow-container {
  display: flex;
  align-items: center;
  padding: 0 0.5rem;
}

/* 响应式布局 */
@media (max-width: 1200px) {
  .oblique-workflow-layout {
    flex-direction: column;
    gap: 1rem;
    padding: 1rem;
  }
  
  .workflow-arrow-container {
    padding: 0.5rem 0;
  }
}

@media (max-width: 768px) {
  .oblique-workflow-layout {
    padding: 0.75rem;
  }
}
</style>
