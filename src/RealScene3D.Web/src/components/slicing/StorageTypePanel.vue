<template>
  <div class="storage-type-panel">
    <!-- 存储3DTiles1.1 -->
    <div class="form-item">
      <SwitchControl
        v-model="localStore3DTiles11"
        label="存储3DTiles1.1"
      />
    </div>

    <!-- 存储类型 -->
    <div class="form-item">
      <label class="form-label">存储类型</label>
      <select
        :value="props.storageType"
        @change="emit('update:storageType', ($event.target as HTMLSelectElement).value as 'hash' | 'hierarchy')"
        class="form-select"
      >
        <option value="hash">散列</option>
        <option value="hierarchy">分层</option>
      </select>
    </div>

    <!-- 输出路径 -->
    <div class="form-item">
      <label class="form-label">输出路径 <span class="required">*</span></label>
      <input
        :value="props.outputPath"
        @input="emit('update:outputPath', ($event.target as HTMLInputElement).value)"
        type="text"
        class="form-input"
        placeholder="请输入输出路径"
      />
      <small class="form-hint">名称或绝对路径或空，名称或者为空则切片保存到minio</small>
    </div>

    <!-- 提交处理按钮 -->
    <div class="form-item">
      <button @click="emit('submit')" class="btn btn-success btn-block" type="button">
        提交处理
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import SwitchControl from './SwitchControl.vue'

interface Props {
  store3DTiles11: boolean
  storageType: 'hash' | 'hierarchy'
  outputPath: string
}

interface Emits {
  (e: 'update:store3DTiles11', value: boolean): void
  (e: 'update:storageType', value: 'hash' | 'hierarchy'): void
  (e: 'update:outputPath', value: string): void
  (e: 'submit'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

// 使用computed实现双向绑定
const localStore3DTiles11 = computed({
  get: () => props.store3DTiles11,
  set: (val) => emit('update:store3DTiles11', val)
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

.form-input,
.form-select {
  padding: 0.5rem 0.75rem;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  font-size: 0.9rem;
  transition: border-color 0.2s ease;
}

.form-input:focus,
.form-select:focus {
  outline: none;
  border-color: #007acc;
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
