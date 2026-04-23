<template>
  <div class="scenes-create-page">
    <!-- 页面头部 -->
    <header class="page-header">
      <div class="header-left">
        <button @click="handleBack" class="btn-back" type="button">
          <span class="back-icon">←</span>
          <span>返回</span>
        </button>
        <h1>创建新场景</h1>
      </div>
    </header>

    <!-- 页面内容 -->
    <div class="page-content">
      <div class="form-container">
        <!-- 基本信息区域 -->
        <section class="form-section">
          <h2 class="section-title">基本信息</h2>
          <div class="form-row">
            <div class="form-group">
              <label>场景名称 <span class="required">*</span></label>
              <input
                v-model="sceneForm.name"
                type="text"
                class="form-input"
                placeholder="请输入场景名称"
              />
            </div>
            <div class="form-group">
              <label>边界GeoJSON</label>
              <input
                v-model="sceneForm.boundaryGeoJson"
                type="text"
                class="form-input"
                placeholder="地理边界框 (GeoJSON格式，可选)"
              />
            </div>
          </div>
          <div class="form-group">
            <label>场景描述</label>
            <textarea
              v-model="sceneForm.description"
              class="form-textarea"
              placeholder="请输入场景描述（可选）"
            ></textarea>
          </div>
        </section>

        <!-- 场景文件区域 -->
        <section class="form-section">
          <h2 class="section-title">
            <span class="title-icon">📁</span>
            场景文件
          </h2>
          <div class="form-group">
            <FileUpload
              v-model="sceneFile"
              accept=".gltf,.glb,.obj,.fbx,.dae"
              :max-size="500"
              :multiple="false"
              hint="支持GLTF、GLB、OBJ、FBX、DAE格式，单个文件不超过500MB"
              :auto-upload="false"
              @upload="handleSceneFileUpload"
              @remove="handleRemoveSceneFile"
            />
          </div>
        </section>

        <!-- 操作按钮 -->
        <div class="form-actions">
          <button @click="handleBack" class="btn btn-secondary">
            取消
          </button>
          <button @click="saveScene" class="btn btn-primary" :disabled="isSubmitting">
            {{ isSubmitting ? '保存中...' : '保存' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { sceneService } from '@/services/api'
import authStore from '@/stores/auth'
import { useMessage } from '@/composables/useMessage'
import FileUpload from '@/components/FileUpload.vue'

const { success: showSuccess, error: showError } = useMessage()
const router = useRouter()

// 场景文件上传状态
const sceneFile = ref<File | null>(null)
const isSubmitting = ref(false)

// 场景表单
const sceneForm = ref({
  name: '',
  description: '',
  boundaryGeoJson: '',
  sceneObjects: [] as any[] // 场景对象集合
})

// 返回列表页面
const handleBack = () => {
  router.push({ name: 'Scenes' })
}

// 处理场景文件上传
const handleSceneFileUpload = async (file: File) => {
  try {
    console.log('场景文件已选择:', file.name)
    showSuccess(`场景文件 ${file.name} 已选择，保存时将上传`)
  } catch (error) {
    console.error('场景文件处理失败:', error)
    showError('场景文件处理失败')
  }
}

// 移除场景文件
const handleRemoveSceneFile = () => {
  sceneFile.value = null
  showSuccess('文件已移除')
}

// 保存场景
const saveScene = async () => {
  if (isSubmitting.value) return
  
  // 验证必填字段
  if (!sceneForm.value.name?.trim()) {
    showError('请输入场景名称')
    return
  }

  try {
    isSubmitting.value = true
    
    // 构建场景数据
    const sceneData = {
      name: sceneForm.value.name,
      description: sceneForm.value.description || '',
      renderEngine: 'Mars3D', // 统一使用Mars3D
      boundaryGeoJson: sceneForm.value.boundaryGeoJson || null,
      sceneObjects: sceneForm.value.sceneObjects || []
    }

    console.log('创建场景数据:', sceneData)
    
    // 调用API创建场景
    const userId = authStore.currentUser.value?.id || '00000000-0000-0000-0000-000000000001'
    await sceneService.createScene(sceneData, userId)
    
    showSuccess('场景创建成功！')
    router.push({ name: 'Scenes' })
  } catch (error: any) {
    console.error('创建场景失败:', error)
    const errorMessage = error.response?.data?.message || error.message || '创建场景失败'
    showError(`创建场景失败: ${errorMessage}`)
  } finally {
    isSubmitting.value = false
  }
}
</script>

<style scoped>
.scenes-create-page {
  min-height: 100vh;
  background: #f5f7fa;
  display: flex;
  flex-direction: column;
}

/* 页面头部 */
.page-header {
  background: white;
  border-bottom: 1px solid #e1e5e9;
  padding: 1rem 2rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.05);
}

.header-left {
  display: flex;
  align-items: center;
  gap: 1.5rem;
}

.btn-back {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 1rem;
  border: 1px solid #e1e5e9;
  border-radius: 6px;
  background: white;
  cursor: pointer;
  font-size: 0.9rem;
  color: #666;
  transition: all 0.2s ease;
}

.btn-back:hover {
  border-color: #1890ff;
  color: #1890ff;
  background: #f0f7ff;
}

.back-icon {
  font-size: 1.1rem;
}

.page-header h1 {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 600;
  color: #333;
}

/* 页面内容 */
.page-content {
  flex: 1;
  padding: 1.5rem;
  max-width: 1000px;
  margin: 0 auto;
  width: 100%;
}

.form-container {
  background: white;
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
  overflow: hidden;
}

/* 表单区域 */
.form-section {
  padding: 1.5rem;
  border-bottom: 1px solid #f0f0f0;
}

.form-section:last-of-type {
  border-bottom: none;
}

.section-title {
  margin: 0 0 1.25rem 0;
  font-size: 1rem;
  font-weight: 600;
  color: #333;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.title-icon {
  font-size: 1.1rem;
}

/* 表单行 */
.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.5rem;
  margin-bottom: 1rem;
}

/* 表单组 */
.form-group {
  margin-bottom: 1rem;
}

.form-group:last-child {
  margin-bottom: 0;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 500;
  font-size: 0.9rem;
  color: #333;
}

.required {
  color: #ff4d4f;
}

.form-input,
.form-textarea {
  width: 100%;
  padding: 0.5rem 0.75rem;
  border: 1px solid #d9d9d9;
  border-radius: 6px;
  font-size: 0.9rem;
  transition: all 0.2s ease;
}

.form-input:focus,
.form-textarea:focus {
  outline: none;
  border-color: #1890ff;
  box-shadow: 0 0 0 2px rgba(24, 144, 255, 0.1);
}

.form-textarea {
  min-height: 80px;
  max-height: 120px;
  resize: vertical;
  line-height: 1.5;
}

/* 操作按钮 */
.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
  padding: 1rem 1.5rem;
  background: #fafafa;
  border-top: 1px solid #f0f0f0;
}

.btn {
  padding: 0.5rem 1.5rem;
  border-radius: 6px;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 500;
  transition: all 0.2s ease;
}

.btn-primary {
  background: #1890ff;
  color: white;
  border: none;
}

.btn-primary:hover:not(:disabled) {
  background: #40a9ff;
}

.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-secondary {
  background: white;
  color: #666;
  border: 1px solid #d9d9d9;
}

.btn-secondary:hover {
  border-color: #1890ff;
  color: #1890ff;
}

/* 响应式设计 */
@media (max-width: 768px) {
  .page-header {
    padding: 1rem;
  }

  .page-content {
    padding: 1rem;
  }

  .form-section {
    padding: 1rem;
  }

  .form-row {
    grid-template-columns: 1fr;
    gap: 1rem;
  }
}
</style>
