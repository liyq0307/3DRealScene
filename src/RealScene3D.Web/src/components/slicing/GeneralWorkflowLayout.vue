<template>
  <div class="general-workflow-layout">
    <!-- 左侧：输入文件 -->
    <WorkflowStage title="输入文件" icon="📁" position="left">
      <GeneralInputFilePanel
        v-model:modelPath="localFormData.modelPath"
      />
    </WorkflowStage>

    <!-- 箭头1 -->
    <div class="workflow-arrow-container">
      <WorkflowArrow />
    </div>

    <!-- 中间：处理参数 -->
    <WorkflowStage title="处理参数" icon="⚙️" position="center">
      <GeneralProcessingParamsPanel
        v-model:lodLevels="localFormData.lodLevels"
        v-model:divisions="localFormData.divisions"
        v-model:outputFormat="localFormData.outputFormat"
        v-model:textureStrategy="localFormData.textureStrategy"
        v-model:enableMeshDecimation="localFormData.enableMeshDecimation"
        v-model:enableCompression="localFormData.enableCompression"
        v-model:enableIncrementalUpdate="localFormData.enableIncrementalUpdate"
      />
    </WorkflowStage>

    <!-- 箭头2 -->
    <div class="workflow-arrow-container">
      <WorkflowArrow />
    </div>

    <!-- 右侧：存储类型 -->
    <WorkflowStage title="存储类型" icon="💾" position="right">
      <GeneralStorageTypePanel
        v-model:outputPath="localFormData.outputPath"
        v-model:generateTileset="localFormData.generateTileset"
      />
    </WorkflowStage>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import WorkflowStage from './WorkflowStage.vue'
import WorkflowArrow from './WorkflowArrow.vue'
import GeneralInputFilePanel from './GeneralInputFilePanel.vue'
import GeneralProcessingParamsPanel from './GeneralProcessingParamsPanel.vue'
import GeneralStorageTypePanel from './GeneralStorageTypePanel.vue'
import type { GeneralSliceFormData } from '@/types/obliqueSlice'

interface Props {
  formData: GeneralSliceFormData
}

interface Emits {
  (e: 'update:formData', value: GeneralSliceFormData): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const localFormData = computed({
  get: () => props.formData,
  set: (val) => emit('update:formData', val)
})
</script>

<style scoped>
.general-workflow-layout {
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
  .general-workflow-layout {
    flex-direction: column;
    gap: 1rem;
    padding: 1rem;
  }
  
  .workflow-arrow-container {
    padding: 0.5rem 0;
  }
}

@media (max-width: 768px) {
  .general-workflow-layout {
    padding: 0.75rem;
  }
}
</style>
