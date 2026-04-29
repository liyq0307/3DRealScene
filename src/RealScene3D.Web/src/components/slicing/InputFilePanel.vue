<template>
  <div class="input-file-panel">
    <!-- 数据路径 -->
    <div class="form-item">
      <label class="form-label">数据路径 <span class="required">*</span></label>
      <input
        :value="props.dataPath"
        @input="emit('update:dataPath', ($event.target as HTMLInputElement).value)"
        type="text"
        class="form-input"
        placeholder="请输入模型所在文件夹路径"
      />
      <small class="form-hint">例如: F:/Data/Oblique/Tile/Data</small>
    </div>

    <!-- 空间参考 -->
    <div class="form-item">
      <label class="form-label">空间参考</label>
      <input
        :value="props.spatialReference"
        @input="emit('update:spatialReference', ($event.target as HTMLInputElement).value)"
        type="text"
        class="form-input"
        placeholder="请输入空间参考（如：EPSG:4326）"
      />
    </div>

    <!-- 零点坐标 -->
    <div class="form-item">
      <label class="form-label">零点坐标</label>
      <input
        :value="props.zeroPoint"
        @input="emit('update:zeroPoint', ($event.target as HTMLInputElement).value)"
        type="text"
        class="form-input"
        placeholder="请输入零点坐标（如：0,0,0）"
      />
    </div>

    <!-- 发布服务按钮 -->
    <div class="form-item">
      <button @click="emit('publish')" class="btn btn-primary btn-block" type="button">
        发布服务
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
interface Props {
  dataPath: string
  spatialReference: string
  zeroPoint: string
}

interface Emits {
  (e: 'update:dataPath', value: string): void
  (e: 'update:spatialReference', value: string): void
  (e: 'update:zeroPoint', value: string): void
  (e: 'publish'): void
}

const props = withDefaults(defineProps<Props>(), {
  dataPath: '',
  spatialReference: '',
  zeroPoint: ''
})

const emit = defineEmits<Emits>()
</script>

<style scoped>
.input-file-panel {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.form-item {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}

.form-label {
  font-size: 0.85rem;
  font-weight: 500;
  color: #333;
}

.required {
  color: #333;
  margin-left: 0.25rem;
}

.form-input {
  padding: 0.4rem 0.6rem;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  font-size: 0.85rem;
  transition: border-color 0.2s ease;
}

.form-input:focus {
  outline: none;
  border-color: #007acc;
}

.form-hint {
  font-size: 0.75rem;
  color: #666;
}

.input-with-button {
  display: flex;
  gap: 0.5rem;
}

.input-with-button .form-input {
  flex: 1;
}

.btn {
  padding: 0.4rem 0.8rem;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  background: white;
  cursor: pointer;
  font-size: 0.85rem;
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

.btn-block {
  width: 100%;
}
</style>
