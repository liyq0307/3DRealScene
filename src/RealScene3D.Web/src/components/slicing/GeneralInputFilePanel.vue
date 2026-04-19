<template>
  <div class="input-file-panel">
    <!-- 模型路径 -->
    <div class="form-item">
      <label class="form-label">模型路径 <span class="required">*</span></label>
      <input
        :value="props.modelPath"
        @input="emit('update:modelPath', ($event.target as HTMLInputElement).value)"
        type="text"
        class="form-input"
        placeholder="例如: F:/Data/3D/model.obj"
      />
      <small class="form-hint">支持OBJ、GLTF、FBX、IFC等格式，请输入完整路径</small>
    </div>
  </div>
</template>

<script setup lang="ts">
interface Props {
  modelPath: string
}

interface Emits {
  (e: 'update:modelPath', value: string): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()
</script>

<style scoped>
.input-file-panel {
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
</style>
