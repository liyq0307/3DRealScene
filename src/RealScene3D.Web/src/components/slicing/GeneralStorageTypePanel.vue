<template>
  <div class="storage-type-panel">
    <!-- 输出路径 -->
    <div class="form-item">
      <label class="form-label">输出路径 <span class="required">*</span></label>
      <input
        :value="props.outputPath"
        @input="emit('update:outputPath', ($event.target as HTMLInputElement).value)"
        type="text"
        class="form-input"
        placeholder="例如: F:/Data/3D/Output"
      />
      <small class="form-hint">名称或绝对路径或空，名称或者为空则切片保存到minio</small>
    </div>

    <!-- 生成配置 -->
    <div class="form-item">
      <SwitchControl
        v-model="localGenerateTileset"
        label="生成 tileset.json"
      />
    </div>

    <!-- 提交按钮 -->
    <div class="form-item">
      <button @click="emit('submit')" class="btn btn-success btn-block" type="button">
        创建任务
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import SwitchControl from './SwitchControl.vue'

interface Props {
  outputPath: string
  generateTileset: boolean
}

interface Emits {
  (e: 'update:outputPath', value: string): void
  (e: 'update:generateTileset', value: boolean): void
  (e: 'submit'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const localGenerateTileset = computed({
  get: () => props.generateTileset,
  set: (val) => emit('update:generateTileset', val)
})
</script>

<style scoped>
.storage-type-panel {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.form-item {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.form-label {
  font-size: 0.9rem;
  font-weight: 500;
  color: #333;
}

.required {
  color: #333;
  margin-left: 0.25rem;
}

.form-input {
  padding: 0.5rem 0.75rem;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  font-size: 0.9rem;
  transition: border-color 0.2s ease;
}

.form-input:focus {
  outline: none;
  border-color: #007acc;
}

.form-hint {
  font-size: 0.85rem;
  color: #666;
  margin-top: 0.25rem;
}

.input-with-button {
  display: flex;
  gap: 0.5rem;
}

.input-with-button .form-input {
  flex: 1;
}

.btn {
  padding: 0.5rem 1rem;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  background: white;
  cursor: pointer;
  font-size: 0.9rem;
  transition: all 0.2s ease;
}

.btn-primary {
  background: #007acc;
  color: white;
  border-color: #007acc;
}

.btn-primary:hover {
  background: #005999;
}

.btn-success {
  background: #28a745;
  color: white;
  border-color: #28a745;
}

.btn-success:hover {
  background: #218838;
}

.btn-block {
  width: 100%;
  margin-top: 0.5rem;
}
</style>
