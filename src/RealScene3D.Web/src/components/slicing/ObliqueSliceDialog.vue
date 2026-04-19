<template>
  <div v-if="visible" class="modal-overlay" @click="handleCancel">
    <div class="modal-content large" @click.stop>
      <div class="modal-header">
        <h3>创建切片任务</h3>
        <button @click="handleCancel" class="btn-close" type="button" aria-label="关闭">×</button>
      </div>

      <div class="modal-body">
        <!-- 通用字段 -->
        <div class="common-fields">
          <div class="form-group">
            <label>任务名称 *</label>
            <input
              v-model="currentFormData.name"
              type="text"
              class="form-input"
              placeholder="输入任务名称"
            />
          </div>
          <div class="form-group">
            <label>描述</label>
            <textarea
              v-model="currentFormData.description"
              class="form-textarea"
              placeholder="输入任务描述"
            ></textarea>
          </div>
        </div>

        <!-- 数据类型选择 -->
        <div class="form-group data-type-group">
          <label>数据类型</label>
          <div class="data-type-selector">
            <button
              :class="['type-btn', { active: dataType === DataType.General }]"
              @click="selectDataType(DataType.General)"
              type="button"
            >
              <span class="type-icon">📦</span>
              <div class="type-content">
                <span class="type-label">通用3D模型</span>
                <span class="type-desc">OBJ、GLTF、FBX等</span>
              </div>
            </button>
            <button
              :class="['type-btn', { active: dataType === DataType.Oblique }]"
              @click="selectDataType(DataType.Oblique)"
              type="button"
            >
              <span class="type-icon">🗺️</span>
              <div class="type-content">
                <span class="type-label">倾斜摄影</span>
                <span class="type-desc">OSGB数据文件夹</span>
              </div>
            </button>
          </div>
        </div>

        <!-- 界面切换 -->
        <transition name="fade" mode="out-in">
          <!-- 倾斜摄影界面 -->
          <ObliqueWorkflowLayout
            v-if="dataType === DataType.Oblique"
            :key="'oblique'"
            :formData="obliqueFormData"
            @update:formData="updateObliqueFormData"
            @submit="handleSubmit"
            @publish="handlePublish"
          />

          <!-- 通用模型界面 -->
          <GeneralWorkflowLayout
            v-else
            :key="'general'"
            :formData="generalFormData"
            @update:formData="updateGeneralFormData"
            @submit="handleSubmit"
          />
        </transition>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import ObliqueWorkflowLayout from './ObliqueWorkflowLayout.vue'
import GeneralWorkflowLayout from './GeneralWorkflowLayout.vue'
import { 
  DataType, 
  type ObliqueSliceFormData, 
  type GeneralSliceFormData 
} from '@/types/obliqueSlice'
import { 
  getDefaultObliqueFormData, 
  getDefaultGeneralFormData,
  validateObliqueForm
} from '@/composables/useObliqueSlice'

interface Props {
  visible: boolean
  initialModelPath?: string
}

interface Emits {
  (e: 'update:visible', value: boolean): void
  (e: 'submit', data: any): void
  (e: 'cancel'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

// 状态
const dataType = ref<DataType>(DataType.General)
const obliqueFormData = ref<ObliqueSliceFormData>(getDefaultObliqueFormData())
const generalFormData = ref<GeneralSliceFormData>(getDefaultGeneralFormData())

// 当前表单数据
const currentFormData = computed(() => {
  if (dataType.value === DataType.Oblique) {
    return obliqueFormData.value
  }
  return generalFormData.value
})

// 选择数据类型
const selectDataType = (type: DataType) => {
  // 保存通用字段
  const preservedName = currentFormData.value.name
  const preservedDescription = currentFormData.value.description
  
  // 切换数据类型
  dataType.value = type
  
  // 根据新类型初始化表单数据
  if (type === DataType.Oblique) {
    obliqueFormData.value = getDefaultObliqueFormData()
  } else {
    generalFormData.value = getDefaultGeneralFormData()
  }
  
  // 恢复通用字段
  currentFormData.value.name = preservedName
  currentFormData.value.description = preservedDescription
}

// 更新倾斜摄影表单数据
const updateObliqueFormData = (data: ObliqueSliceFormData) => {
  obliqueFormData.value = { ...data }
}

// 更新通用模型表单数据
const updateGeneralFormData = (data: GeneralSliceFormData) => {
  generalFormData.value = { ...data }
}

// 处理提交
const handleSubmit = () => {
  // 验证任务名称
  if (!currentFormData.value.name?.trim()) {
    alert('请输入任务名称')
    return
  }
  
  // 根据数据类型验证路径
  if (dataType.value === DataType.Oblique) {
    // 倾斜摄影验证
    if (!obliqueFormData.value.dataPath?.trim()) {
      alert('请选择数据路径')
      return
    }
    
    const errors = validateObliqueForm(obliqueFormData.value)
    if (errors.size > 0) {
      const errorMessages = Array.from(errors.values()).join('\n')
      alert(errorMessages)
      return
    }
    
    emit('submit', {
      type: 'oblique',
      data: obliqueFormData.value
    })
  } else {
    // 通用模型验证
    if (!generalFormData.value.modelPath?.trim()) {
      alert('请输入模型路径')
      return
    }
    
    emit('submit', {
      type: 'general',
      data: generalFormData.value
    })
  }
}

// 处理发布
const handlePublish = () => {
  // TODO: 实现发布服务逻辑
  alert('发布服务功能开发中...')
}

// 处理取消
const handleCancel = () => {
  emit('update:visible', false)
  emit('cancel')
}

// 监听visible变化，重置表单
watch(() => props.visible, (val) => {
  if (val) {
    dataType.value = DataType.General
    obliqueFormData.value = getDefaultObliqueFormData()
    generalFormData.value = getDefaultGeneralFormData()
  }
})
</script>

<style scoped>
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-content {
  background: white;
  border-radius: 8px;
  width: 600px;
  max-width: 90vw;
  max-height: 90vh;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  position: relative;
  z-index: 1001;
}

.modal-content.large {
  width: 1200px;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem 1.5rem;
  border-bottom: 1px solid #e1e5e9;
  background: white;
  position: sticky;
  top: 0;
  z-index: 10;
}

.modal-header h3 {
  margin: 0;
  font-size: 1.1rem;
  font-weight: 600;
}

.btn-close {
  padding: 0.5rem;
  border: none;
  background: transparent;
  font-size: 1.5rem;
  cursor: pointer;
  color: #666;
  transition: color 0.2s ease;
}

.btn-close:hover {
  color: #dc3545;
}

.modal-body {
  flex: 1;
  padding: 1rem 1.5rem;
  overflow-y: auto;
}

.common-fields {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.form-group {
  margin-bottom: 1rem;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 500;
  color: #333;
}

.form-input,
.form-select,
.form-textarea {
  padding: 0.5rem;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  font-size: 0.9rem;
  width: 100%;
}

.form-input:focus,
.form-select:focus,
.form-textarea:focus {
  outline: none;
  border-color: #007acc;
}

/* 数据类型选择器样式 */
.data-type-group {
  margin-bottom: 0.5rem;
}

.data-type-selector {
  display: flex;
  gap: 0.5rem;
  margin-top: 0.35rem;
}

.type-btn {
  flex: 1;
  display: flex;
  align-items: center;
  padding: 0.5rem 0.75rem;
  border: 2px solid #e1e5e9;
  border-radius: 4px;
  background: white;
  cursor: pointer;
  transition: all 0.2s ease;
  gap: 0.5rem;
}

.type-btn:hover {
  border-color: #007acc;
  background: #f8f9fa;
}

.type-btn.active {
  border-color: #007acc;
  background: #e3f2fd;
}

.type-icon {
  font-size: 1.25rem;
  flex-shrink: 0;
}

.type-content {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  text-align: left;
}

.type-label {
  font-size: 0.85rem;
  font-weight: 600;
  color: #333;
}

.type-desc {
  font-size: 0.7rem;
  color: #666;
}

.type-btn.active .type-label {
  color: #007acc;
}

.form-textarea {
  min-height: 60px;
  max-height: 120px;
  resize: vertical;
  line-height: 1.4;
}

.form-hint {
  display: block;
  font-size: 0.85rem;
  color: #666;
  margin-top: 0.25rem;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 1rem;
}

.form-group.full-width {
  grid-column: 1 / -1;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  cursor: pointer;
}

.checkbox-label input[type="checkbox"] {
  width: auto;
}

.general-form {
  background: #f8f9fa;
  padding: 1.5rem;
  border-radius: 8px;
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 1rem;
  margin-top: 2rem;
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

.btn-secondary {
  background: #6c757d;
  color: white;
  border-color: #6c757d;
}

.btn-secondary:hover {
  background: #5a6268;
}

/* 过渡动画 */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
