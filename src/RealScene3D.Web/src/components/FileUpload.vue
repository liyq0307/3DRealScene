<template>
  <div
    class="file-upload"
    :class="{ 'is-dragging': isDragging }"
    @drop.prevent="handleDrop"
    @dragover.prevent="isDragging = true"
    @dragleave.prevent="isDragging = false"
  >
    <!-- 上传区域 -->
    <div v-if="!file && !modelValue" class="upload-area" @click="triggerFileInput">
      <input
        ref="fileInputRef"
        type="file"
        :accept="accept"
        :multiple="multiple"
        class="file-input"
        @change="handleFileSelect"
      />

      <div class="upload-content">
        <div class="upload-icon">
          <svg width="48" height="48" viewBox="0 0 48 48" fill="none">
            <path
              d="M24 8v24m0-24l-8 8m8-8l8 8M8 36h32"
              stroke="currentColor"
              stroke-width="3"
              stroke-linecap="round"
              stroke-linejoin="round"
            />
          </svg>
        </div>
        <p class="upload-text">
          <span class="upload-text-primary">点击上传</span>
          或拖拽文件到此处
        </p>
        <p class="upload-hint">{{ hint }}</p>
      </div>
    </div>

    <!-- 文件预览 -->
    <div v-else class="file-preview">
      <div class="file-info">
        <div class="file-icon">
          <svg width="32" height="32" viewBox="0 0 32 32" fill="none">
            <path
              d="M6 4h12l8 8v16H6V4z"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
              fill="rgba(99, 102, 241, 0.1)"
            />
          </svg>
        </div>
        <div class="file-details">
          <div class="file-name">{{ fileName }}</div>
          <div class="file-size">{{ fileSize }}</div>
        </div>
      </div>

      <!-- 上传进度 -->
      <div v-if="uploading" class="upload-progress">
        <div class="progress-bar">
          <div
            class="progress-fill"
            :style="{ width: `${uploadProgress}%` }"
          ></div>
        </div>
        <div class="progress-text">{{ uploadProgress }}%</div>
      </div>

      <!-- 操作按钮 -->
      <div v-if="!uploading" class="file-actions">
        <button v-if="!uploaded" class="btn btn-upload" @click="handleUpload">
          上传文件
        </button>
        <button class="btn btn-remove" @click="handleRemove">
          移除文件
        </button>
      </div>

      <!-- 上传成功标识 -->
      <div v-if="uploaded" class="upload-success">
        <svg width="24" height="24" viewBox="0 0 24 24" fill="none">
          <path
            d="M5 13l4 4L19 7"
            stroke="currentColor"
            stroke-width="3"
            stroke-linecap="round"
            stroke-linejoin="round"
          />
        </svg>
        上传成功
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * 文件上传组件
 *
 * 功能特性:
 * - 支持点击上传和拖拽上传
 * - 显示上传进度
 * - 文件类型和大小限制
 * - 文件预览
 * - 支持多文件上传
 *
 * @author liyq
 * @date 2025-10-15
 */

import { ref, computed } from 'vue'
import { useMessage } from '@/composables/useMessage'

interface Props {
  modelValue?: File | null
  accept?: string
  maxSize?: number // MB
  multiple?: boolean
  hint?: string
  autoUpload?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: null,
  accept: '*/*',
  maxSize: 100,
  multiple: false,
  hint: '支持所有格式,单个文件不超过100MB',
  autoUpload: false
})

const emit = defineEmits<{
  'update:modelValue': [file: File | null]
  upload: [file: File]
  remove: []
  progress: [percent: number]
  success: [response: any]
  error: [error: Error]
}>()

const { error: showError } = useMessage()

const fileInputRef = ref<HTMLInputElement | null>(null)
const file = ref<File | null>(props.modelValue)
const isDragging = ref(false)
const uploading = ref(false)
const uploaded = ref(false)
const uploadProgress = ref(0)

// 文件名
const fileName = computed(() => {
  return file.value?.name || ''
})

// 文件大小(格式化)
const fileSize = computed(() => {
  if (!file.value) return ''
  const size = file.value.size
  if (size < 1024) return `${size} B`
  if (size < 1024 * 1024) return `${(size / 1024).toFixed(2)} KB`
  return `${(size / (1024 * 1024)).toFixed(2)} MB`
})

// 触发文件选择
const triggerFileInput = () => {
  fileInputRef.value?.click()
}

// 验证文件
const validateFile = (selectedFile: File): boolean => {
  // 检查文件大小
  const maxSizeBytes = props.maxSize * 1024 * 1024
  if (selectedFile.size > maxSizeBytes) {
    showError(`文件大小不能超过${props.maxSize}MB`)
    return false
  }

  // 检查文件类型
  if (props.accept !== '*/*') {
    const acceptTypes = props.accept.split(',').map(t => t.trim())
    const fileExt = '.' + selectedFile.name.split('.').pop()?.toLowerCase()
    const mimeType = selectedFile.type

    const isValid = acceptTypes.some(type => {
      if (type.startsWith('.')) {
        return fileExt === type.toLowerCase()
      }
      if (type.includes('*')) {
        const baseType = type.split('/')[0]
        return mimeType.startsWith(baseType)
      }
      return mimeType === type
    })

    if (!isValid) {
      showError('不支持的文件类型')
      return false
    }
  }

  return true
}

// 处理文件选择
const handleFileSelect = (event: Event) => {
  const target = event.target as HTMLInputElement
  const files = target.files

  if (files && files.length > 0) {
    const selectedFile = files[0]
    if (validateFile(selectedFile)) {
      file.value = selectedFile
      emit('update:modelValue', selectedFile)

      if (props.autoUpload) {
        handleUpload()
      }
    }
  }

  // 清空input value,允许重复选择同一文件
  target.value = ''
}

// 处理拖拽放置
const handleDrop = (event: DragEvent) => {
  isDragging.value = false
  const files = event.dataTransfer?.files

  if (files && files.length > 0) {
    const droppedFile = files[0]
    if (validateFile(droppedFile)) {
      file.value = droppedFile
      emit('update:modelValue', droppedFile)

      if (props.autoUpload) {
        handleUpload()
      }
    }
  }
}

// 处理上传
const handleUpload = async () => {
  if (!file.value) return

  uploading.value = true
  uploadProgress.value = 0

  try {
    // 模拟上传进度
    const interval = setInterval(() => {
      uploadProgress.value += 10
      emit('progress', uploadProgress.value)

      if (uploadProgress.value >= 100) {
        clearInterval(interval)
        uploading.value = false
        uploaded.value = true
        emit('success', { url: 'https://example.com/file.glb' })
      }
    }, 200)

    // 触发上传事件
    emit('upload', file.value)

    // TODO: 实际的文件上传逻辑
    // const formData = new FormData()
    // formData.append('file', file.value)
    // const response = await api.post('/upload', formData, {
    //   onUploadProgress: (progressEvent) => {
    //     const percent = Math.round((progressEvent.loaded * 100) / progressEvent.total)
    //     uploadProgress.value = percent
    //     emit('progress', percent)
    //   }
    // })

  } catch (err) {
    uploading.value = false
    showError('上传失败,请重试')
    emit('error', err as Error)
  }
}

// 移除文件
const handleRemove = () => {
  file.value = null
  uploaded.value = false
  uploadProgress.value = 0
  emit('update:modelValue', null)
  emit('remove')
}
</script>

<style scoped>
.file-upload {
  width: 100%;
  border: 2px dashed var(--border-color);
  border-radius: var(--border-radius-lg);
  background: var(--gray-50);
  transition: all var(--transition-base);
}

.file-upload.is-dragging {
  border-color: var(--primary-color);
  background: var(--primary-lighter);
  transform: scale(1.02);
}

.upload-area {
  padding: 3rem 2rem;
  cursor: pointer;
  transition: all var(--transition-base);
}

.upload-area:hover {
  background: white;
  border-color: var(--primary-color);
}

.file-input {
  display: none;
}

.upload-content {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1rem;
}

.upload-icon {
  width: 64px;
  height: 64px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--gradient-primary-alt);
  color: white;
  border-radius: var(--border-radius-full);
  box-shadow: var(--shadow-colored);
}

.upload-text {
  margin: 0;
  font-size: var(--font-size-lg);
  color: var(--gray-700);
}

.upload-text-primary {
  color: var(--primary-color);
  font-weight: 600;
}

.upload-hint {
  margin: 0;
  font-size: var(--font-size-sm);
  color: var(--gray-500);
}

.file-preview {
  padding: 2rem;
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.file-info {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.file-icon {
  width: 48px;
  height: 48px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--primary-lighter);
  color: var(--primary-color);
  border-radius: var(--border-radius);
}

.file-details {
  flex: 1;
}

.file-name {
  font-size: var(--font-size-base);
  font-weight: 600;
  color: var(--gray-900);
  margin-bottom: 0.25rem;
}

.file-size {
  font-size: var(--font-size-sm);
  color: var(--gray-500);
}

.upload-progress {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.progress-bar {
  height: 8px;
  background: var(--gray-200);
  border-radius: var(--border-radius-full);
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  background: var(--gradient-primary-alt);
  border-radius: var(--border-radius-full);
  transition: width 0.3s ease;
}

.progress-text {
  font-size: var(--font-size-sm);
  color: var(--primary-color);
  font-weight: 600;
  text-align: center;
}

.file-actions {
  display: flex;
  gap: 0.75rem;
}

.btn {
  flex: 1;
  padding: 0.75rem 1.5rem;
  border: none;
  border-radius: var(--border-radius);
  font-size: var(--font-size-base);
  font-weight: 600;
  cursor: pointer;
  transition: all var(--transition-base);
}

.btn-upload {
  background: var(--gradient-primary-alt);
  color: white;
  box-shadow: var(--shadow-colored);
}

.btn-upload:hover {
  box-shadow: var(--shadow-xl);
  transform: translateY(-2px);
}

.btn-remove {
  background: var(--danger-light);
  color: var(--danger-color);
  border: 1px solid var(--danger-color);
}

.btn-remove:hover {
  background: var(--danger-color);
  color: white;
  transform: translateY(-2px);
}

.upload-success {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  padding: 1rem;
  background: var(--success-light);
  color: var(--success-color);
  border-radius: var(--border-radius);
  font-weight: 600;
}
</style>
