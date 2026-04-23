<template>
  <div class="scenes-edit-page">
    <!-- 页面头部 -->
    <header class="page-header">
      <div class="header-left">
        <button @click="handleBack" class="btn-back" type="button">
          <span class="back-icon">←</span>
          <span>返回</span>
        </button>
        <h1>编辑场景</h1>
      </div>
    </header>

    <!-- 页面内容 -->
    <div class="page-content">
      <!-- 加载状态 -->
      <div v-if="isLoading" class="loading-state">
        <p>加载中...</p>
      </div>

      <!-- 编辑表单 -->
      <div v-else class="form-container">
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
              <label>场景描述</label>
              <textarea
                v-model="sceneForm.description"
                class="form-textarea"
                placeholder="请输入场景描述（可选）"
                rows="1"
              ></textarea>
            </div>
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
        </section>

        <!-- 场景对象区域 -->
        <section class="form-section" v-if="sceneForm.sceneObjects && sceneForm.sceneObjects.length > 0">
          <h2 class="section-title">
            <span class="title-icon">📦</span>
            场景对象 ({{ sceneForm.sceneObjects.length }})
          </h2>
          <div class="scene-objects-list">
            <div 
              v-for="obj in sceneForm.sceneObjects" 
              :key="obj.id" 
              class="scene-object-item"
            >
              <div class="object-info">
                <span class="object-icon">{{ getObjectIcon(obj.type || obj.objectType) }}</span>
                <div class="object-details">
                  <h4 class="object-name">{{ obj.name }}</h4>
                  <p class="object-type">{{ obj.type || obj.objectType }}</p>
                </div>
                <button 
                  @click="deleteSceneObject(obj.id)" 
                  class="btn-delete-object"
                  title="删除场景对象"
                >
                  🗑️
                </button>
              </div>
              <div class="object-meta" v-if="obj.modelPath">
                <span class="meta-label">路径:</span>
                <span class="meta-value" :title="obj.modelPath">{{ getShortPath(obj.modelPath) }}</span>
              </div>
            </div>
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
import { ref, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { sceneService, fileService, sceneObjectService } from '@/services/api'
import authStore from '@/stores/auth'
import { useMessage } from '@/composables/useMessage'
import FileUpload from '@/components/FileUpload.vue'

const { success: showSuccess, error: showError } = useMessage()
const router = useRouter()
const route = useRoute()

// 状态
const isLoading = ref(true)
const sceneFile = ref<File | null>(null)
const isSubmitting = ref(false)
const sceneId = ref<string>('')

// 场景表单
const sceneForm = ref({
  name: '',
  description: '',
  boundaryGeoJson: '',
  sceneObjects: [] as any[]
})

// 加载场景数据
const loadScene = async () => {
  const id = route.params.id as string
  if (!id) {
    showError('场景ID不存在')
    router.push({ name: 'Scenes' })
    return
  }

  sceneId.value = id

  try {
    isLoading.value = true
    const scene = await sceneService.getScene(id)
    
    sceneForm.value = {
      name: scene.name || '',
      description: scene.description || '',
      boundaryGeoJson: scene.boundaryGeoJson || '',
      sceneObjects: scene.sceneObjects || []
    }
  } catch (error) {
    console.error('加载场景失败:', error)
    showError('加载场景信息失败')
    router.push({ name: 'Scenes' })
  } finally {
    isLoading.value = false
  }
}

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

// 获取对象图标
const getObjectIcon = (type: string): string => {
  const iconMap: Record<string, string> = {
    Model3D: '🎨',
    PointCloud: '☁️',
    TileSet: '🧱',
    Marker: '📍'
  }
  return iconMap[type] || '📦'
}

// 获取短路径
const getShortPath = (path: string): string => {
  if (!path) return ''
  const parts = path.split('/')
  return parts.length > 3 ? `.../${parts.slice(-2).join('/')}` : path
}

// 删除场景对象
const deleteSceneObject = async (objectId: string) => {
  if (!confirm('确定要删除此场景对象吗？')) {
    return
  }

  try {
    await sceneObjectService.deleteObject(objectId)
    showSuccess('场景对象删除成功')
    
    // 从本地列表中移除
    sceneForm.value.sceneObjects = sceneForm.value.sceneObjects.filter(obj => obj.id !== objectId)
  } catch (error) {
    console.error('删除场景对象失败:', error)
    showError('删除场景对象失败')
  }
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
    
    // 如果有场景文件，先上传
    if (sceneFile.value) {
      try {
        console.log('开始上传场景文件:', sceneFile.value.name)
        
        // 上传文件到MinIO
        const uploadResult = await fileService.uploadFile(sceneFile.value, 'models-3d')
        console.log('文件上传成功:', uploadResult)
        
        // 提取对象名称
        let objectName = uploadResult.filePath
        if (objectName.startsWith('models-3d/')) {
          objectName = objectName.substring('models-3d/'.length)
        }
        
        // 创建场景对象
        const sceneObject = {
          sceneId: sceneId.value,
          name: sceneFile.value.name.replace(/\.[^/.]+$/, ''),
          type: 'Model3D',
          position: [0, 0, 0],
          rotation: '{"x":0,"y":0,"z":0}',
          scale: '{"x":1,"y":1,"z":1}',
          modelPath: objectName,
          materialData: '{}',
          properties: '{}'
        }
        
        sceneForm.value.sceneObjects = [...(sceneForm.value.sceneObjects || []), sceneObject]
        showSuccess(`场景文件 ${sceneFile.value.name} 上传成功`)
      } catch (error) {
        console.error('场景文件上传失败:', error)
        showError('场景文件上传失败，请重试')
        return
      }
    }
    
    // 构建场景数据
    const sceneData = {
      name: sceneForm.value.name,
      description: sceneForm.value.description || '',
      renderEngine: 'Mars3D',
      boundaryGeoJson: sceneForm.value.boundaryGeoJson || null,
      sceneObjects: sceneForm.value.sceneObjects || []
    }

    console.log('更新场景数据:', sceneData)
    
    // 调用API更新场景
    const userId = authStore.currentUser.value?.id || '00000000-0000-0000-0000-000000000001'
    await sceneService.updateScene(sceneId.value, sceneData, userId)
    
    showSuccess('场景更新成功！')
    router.push({ name: 'Scenes' })
  } catch (error: any) {
    console.error('更新场景失败:', error)
    const errorMessage = error.response?.data?.message || error.message || '更新场景失败'
    showError(`更新场景失败: ${errorMessage}`)
  } finally {
    isSubmitting.value = false
  }
}

// 初始化
onMounted(() => {
  loadScene()
})
</script>

<style scoped>
.scenes-edit-page {
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

.loading-state {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 300px;
  color: #999;
}

.loading-state p {
  font-size: 1rem;
}

.form-container {
  background: white;
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
  overflow: hidden;
}

/* 表单区域 */
.form-section {
  padding: 1rem 1.5rem;
  border-bottom: 1px solid #f0f0f0;
}

.form-section:last-of-type {
  border-bottom: none;
}

.section-title {
  margin: 0 0 0.75rem 0;
  font-size: 0.95rem;
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
  gap: 1rem;
  margin-bottom: 0.5rem;
}

/* 表单组 */
.form-group {
  margin-bottom: 0.5rem;
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
  min-height: 36px;
  max-height: 80px;
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

/* 场景对象列表 */
.scene-objects-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 1rem;
}

.scene-object-item {
  background: #f8f9fa;
  border: 1px solid #e1e5e9;
  border-radius: 8px;
  padding: 1rem;
  transition: all 0.2s ease;
}

.scene-object-item:hover {
  border-color: #1890ff;
  box-shadow: 0 2px 8px rgba(24, 144, 255, 0.1);
}

.object-info {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.5rem;
}

.object-icon {
  font-size: 1.5rem;
  flex-shrink: 0;
}

.object-details {
  flex: 1;
  min-width: 0;
}

.btn-delete-object {
  flex-shrink: 0;
  width: 32px;
  height: 32px;
  border: none;
  background: transparent;
  color: #999;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: all 0.2s ease;
  font-size: 1rem;
}

.btn-delete-object:hover {
  background: #fee2e2;
  color: #ef4444;
}

.object-name {
  margin: 0;
  font-size: 0.95rem;
  font-weight: 600;
  color: #333;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.object-type {
  margin: 0;
  font-size: 0.8rem;
  color: #666;
}

.object-meta {
  display: flex;
  align-items: center;
  gap: 0.25rem;
  font-size: 0.8rem;
  color: #999;
  margin-top: 0.5rem;
}

.meta-label {
  font-weight: 500;
  color: #999;
}

.meta-value {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 0.75rem;
  color: #666;
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
