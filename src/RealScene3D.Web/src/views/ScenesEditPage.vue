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

        <!-- 场景对象区域 -->
        <section class="form-section">
          <h2 class="section-title">
            <span class="title-icon">📦</span>
            场景对象
            <span v-if="sceneForm.sceneObjects && sceneForm.sceneObjects.length > 0" class="object-count">
              ({{ sceneForm.sceneObjects.length }})
            </span>
          </h2>

          <!-- 对象列表 -->
          <div v-if="sceneForm.sceneObjects && sceneForm.sceneObjects.length > 0" class="scene-objects-list">
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
                <div class="object-actions">
                  <button
                    @click="deleteSceneObject(obj.id)"
                    class="btn-delete-object"
                    title="删除场景对象"
                    type="button"
                  >
                    🗑️
                  </button>
                </div>
              </div>
              <div class="object-meta" v-if="obj.modelPath">
                <span class="meta-label">路径:</span>
                <span class="meta-value" :title="obj.modelPath">{{ getShortPath(obj.modelPath) }}</span>
              </div>
            </div>
          </div>

          <!-- 空状态 -->
          <div v-else class="empty-objects">
            <p>暂无场景对象，点击下方按钮添加</p>
          </div>

          <!-- 添加对象按钮 -->
          <button @click="showObjectDialog = true" class="btn btn-add-object" type="button">
            <span>➕</span> 添加场景对象
          </button>
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

    <!-- 场景对象配置对话框 -->
    <Modal v-model="showObjectDialog" title="添加场景对象" size="lg">
      <div class="object-dialog">
        <!-- 基本信息 -->
        <div class="form-group">
          <label>对象名称 <span class="required">*</span></label>
          <input v-model="objectForm.name" type="text" class="form-input" placeholder="请输入对象名称" />
        </div>

        <div class="form-row">
          <div class="form-group">
            <label>对象类型 <span class="required">*</span></label>
            <select v-model="objectForm.objectType" class="form-input">
              <option value="Model3D">3D模型</option>
              <option value="PointCloud">点云</option>
              <option value="TileSet">瓦片集</option>
              <option value="Marker">标记</option>
            </select>
          </div>
        </div>

        <!-- 模型路径 -->
        <div class="form-group">
          <label>模型/数据路径 <span class="required">*</span></label>
          <div class="model-path-selector">
            <input
              v-model="objectForm.modelPath"
              type="text"
              class="form-input"
              placeholder="输入本地文件路径或URL"
            />
            <div class="path-actions">
              <button @click="selectObjectFile" class="btn-action" type="button">
                <span>📁</span> 选择文件
              </button>
              <button @click="selectMultipleObjectFiles" class="btn-action btn-batch" type="button">
                <span>📦</span> 批量选择
              </button>
            </div>
          </div>
          <input
            ref="objectFileInputRef"
            type="file"
            accept=".gltf,.glb,.obj,.fbx,.dae,.3ds,.stl"
            @change="handleObjectFileSelect"
            style="display: none"
          />
          <input
            ref="multiObjectFileInputRef"
            type="file"
            accept=".gltf,.glb,.obj,.fbx,.dae,.3ds,.mtl,.jpg,.jpeg,.png,.webp,.bmp"
            multiple
            @change="handleMultipleObjectFilesSelect"
            style="display: none"
          />
          <div v-if="selectedObjectFile && !selectedObjectFiles.length" class="file-info">
            <span class="file-icon">📄</span>
            <div class="file-details">
              <div class="file-name">{{ selectedObjectFile.name }}</div>
              <div class="file-size">{{ formatFileSize(selectedObjectFile.size) }}</div>
            </div>
            <button @click="clearObjectFile" class="btn-clear" type="button">✕</button>
          </div>
          <div v-if="selectedObjectFiles.length > 0" class="files-list">
            <div class="files-list-header">
              <span class="files-count">已选择 {{ selectedObjectFiles.length }} 个文件</span>
              <button @click="clearAllObjectFiles" class="btn-clear-all" type="button">清空全部</button>
            </div>
            <div class="files-grid">
              <div v-for="(file, index) in selectedObjectFiles" :key="index" class="file-item">
                <span class="file-icon">{{ getFileIcon(file.name) }}</span>
                <div class="file-details">
                  <div class="file-name" :title="file.name">{{ file.name }}</div>
                  <div class="file-size">{{ formatFileSize(file.size) }}</div>
                </div>
                <button @click="removeObjectFile(index)" class="btn-remove" type="button">✕</button>
              </div>
            </div>
          </div>
        </div>

        <!-- 变换属性 -->
        <div class="transform-section">
          <h3 class="transform-title">变换属性</h3>
          <div class="transform-grid">
            <div class="transform-group">
              <label>位置 (X, Y, Z)</label>
              <div class="vector-input">
                <input v-model.number="objectForm.position.x" type="number" step="0.1" placeholder="X" />
                <input v-model.number="objectForm.position.y" type="number" step="0.1" placeholder="Y" />
                <input v-model.number="objectForm.position.z" type="number" step="0.1" placeholder="Z" />
              </div>
            </div>
            <div class="transform-group">
              <label>旋转 (X, Y, Z) 度</label>
              <div class="vector-input">
                <input v-model.number="objectForm.rotation.x" type="number" step="1" placeholder="X" />
                <input v-model.number="objectForm.rotation.y" type="number" step="1" placeholder="Y" />
                <input v-model.number="objectForm.rotation.z" type="number" step="1" placeholder="Z" />
              </div>
            </div>
            <div class="transform-group">
              <label>缩放 (X, Y, Z)</label>
              <div class="vector-input">
                <input v-model.number="objectForm.scale.x" type="number" step="0.1" min="0.01" placeholder="X" />
                <input v-model.number="objectForm.scale.y" type="number" step="0.1" min="0.01" placeholder="Y" />
                <input v-model.number="objectForm.scale.z" type="number" step="0.1" min="0.01" placeholder="Z" />
              </div>
            </div>
          </div>
        </div>
      </div>

      <template #footer>
        <button @click="cancelObjectDialog" class="btn btn-secondary">取消</button>
        <button @click="saveObject" class="btn btn-primary" :disabled="!isObjectFormValid">添加</button>
      </template>
    </Modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { sceneService, fileService, sceneObjectService } from '@/services/api'
import authStore from '@/stores/auth'
import { useMessage } from '@/composables/useMessage'
import Modal from '@/components/Modal.vue'

const { success: showSuccess, error: showError } = useMessage()
const router = useRouter()
const route = useRoute()

// 状态
const isLoading = ref(true)
const isSubmitting = ref(false)
const sceneId = ref<string>('')

// 场景表单
const sceneForm = ref({
  name: '',
  description: '',
  boundaryGeoJson: '',
  sceneObjects: [] as any[]
})

// 场景对象管理状态
const showObjectDialog = ref(false)
const objectFileInputRef = ref<HTMLInputElement>()
const multiObjectFileInputRef = ref<HTMLInputElement>()
const selectedObjectFile = ref<File | null>(null)
const selectedObjectFiles = ref<File[]>([])

// 对象表单
const objectForm = ref({
  name: '',
  objectType: 'Model3D',
  modelPath: '',
  position: { x: 0, y: 0, z: 0 },
  rotation: { x: 0, y: 0, z: 0 },
  scale: { x: 1, y: 1, z: 1 },
  isVisible: true
})

// 计算属性：对象表单验证
const isObjectFormValid = computed(() => {
  return objectForm.value.name?.trim() && objectForm.value.modelPath?.trim()
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
      sceneObjects: []
    }

    // 加载场景对象列表
    await loadSceneObjects()
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

// 格式化文件大小
const formatFileSize = (bytes: number): string => {
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB'
  return (bytes / 1024 / 1024).toFixed(1) + ' MB'
}

// 选择对象文件
const selectObjectFile = () => {
  objectFileInputRef.value?.click()
}

// 选择多个对象文件（批量上传）
const selectMultipleObjectFiles = () => {
  multiObjectFileInputRef.value?.click()
}

// 处理对象文件选择
const handleObjectFileSelect = (event: Event) => {
  const target = event.target as HTMLInputElement
  const file = target.files?.[0]
  if (!file) return

  const maxSize = 500 * 1024 * 1024
  if (file.size > maxSize) {
    showError('文件大小超过500MB限制')
    return
  }

  selectedObjectFile.value = file
  selectedObjectFiles.value = []
  objectForm.value.modelPath = file.name
  showSuccess(`已选择文件: ${file.name}`)
}

// 处理多个对象文件选择
const handleMultipleObjectFilesSelect = (event: Event) => {
  const target = event.target as HTMLInputElement
  const files = Array.from(target.files || [])

  if (files.length === 0) return

  const maxSize = 500 * 1024 * 1024
  const oversizedFiles = files.filter(f => f.size > maxSize)

  if (oversizedFiles.length > 0) {
    showError(`以下文件超过500MB限制:\n${oversizedFiles.map(f => f.name).join('\n')}`)
    return
  }

  selectedObjectFile.value = null
  selectedObjectFiles.value = files

  const objFile = files.find(f => f.name.toLowerCase().endsWith('.obj'))
  if (objFile) {
    objectForm.value.modelPath = `批量上传: ${objFile.name} + ${files.length - 1} 个相关文件`
    showSuccess(`已选择 ${files.length} 个文件，主模型: ${objFile.name}`)
  } else {
    objectForm.value.modelPath = `批量上传: ${files.length} 个文件`
    showSuccess(`已选择 ${files.length} 个文件`)
  }
}

// 清除对象文件
const clearObjectFile = () => {
  selectedObjectFile.value = null
  selectedObjectFiles.value = []
  objectForm.value.modelPath = ''
  if (objectFileInputRef.value) {
    objectFileInputRef.value.value = ''
  }
  if (multiObjectFileInputRef.value) {
    multiObjectFileInputRef.value.value = ''
  }
}

// 清空所有选中的对象文件
const clearAllObjectFiles = () => {
  selectedObjectFiles.value = []
  objectForm.value.modelPath = ''
  if (multiObjectFileInputRef.value) {
    multiObjectFileInputRef.value.value = ''
  }
}

// 移除指定索引的对象文件
const removeObjectFile = (index: number) => {
  selectedObjectFiles.value.splice(index, 1)

  if (selectedObjectFiles.value.length === 0) {
    objectForm.value.modelPath = ''
  } else {
    const objFile = selectedObjectFiles.value.find(f => f.name.toLowerCase().endsWith('.obj'))
    if (objFile) {
      objectForm.value.modelPath = `批量上传: ${objFile.name} + ${selectedObjectFiles.value.length - 1} 个相关文件`
    } else {
      objectForm.value.modelPath = `批量上传: ${selectedObjectFiles.value.length} 个文件`
    }
  }
}

// 获取文件图标
const getFileIcon = (filename: string): string => {
  const ext = filename.split('.').pop()?.toLowerCase()
  const iconMap: Record<string, string> = {
    'obj': '🎨',
    'mtl': '🎭',
    'jpg': '🖼️',
    'jpeg': '🖼️',
    'png': '🖼️',
    'webp': '🖼️',
    'bmp': '🖼️',
    'glb': '📦',
    'gltf': '📦',
    'fbx': '🔷',
    'dae': '⭕',
  }
  return iconMap[ext || ''] || '📄'
}

// 取消对象对话框
const cancelObjectDialog = () => {
  showObjectDialog.value = false
  selectedObjectFile.value = null
  selectedObjectFiles.value = []

  // 重置表单
  objectForm.value = {
    name: '',
    objectType: 'Model3D',
    modelPath: '',
    position: { x: 0, y: 0, z: 0 },
    rotation: { x: 0, y: 0, z: 0 },
    scale: { x: 1, y: 1, z: 1 },
    isVisible: true
  }
}

// 保存对象（添加或编辑）
const saveObject = async () => {
  if (!isObjectFormValid.value) {
    showError('请填写对象名称和模型路径')
    return
  }

  try {
    let finalModelPath = objectForm.value.modelPath

    // 处理批量文件上传
    if (selectedObjectFiles.value.length > 0) {
      try {
        showSuccess(`正在上传 ${selectedObjectFiles.value.length} 个文件...`)
        const uploadResult = await fileService.uploadFilesBatch(
          selectedObjectFiles.value,
          'models-3d',
          objectForm.value.name
        )

        if (!uploadResult.success) {
          throw new Error(`批量上传失败: ${uploadResult.failedCount} 个文件上传失败`)
        }

        // 查找主模型文件的上传结果
        const objFile = selectedObjectFiles.value.find(f => f.name.toLowerCase().endsWith('.obj'))
        const glbFile = selectedObjectFiles.value.find(f => f.name.toLowerCase().endsWith('.glb'))
        const gltfFile = selectedObjectFiles.value.find(f => f.name.toLowerCase().endsWith('.gltf'))

        if (objFile && uploadResult.results) {
          const objResult = uploadResult.results.find((r: any) => r.originalFileName === objFile.name)
          if (objResult) {
            finalModelPath = objResult.downloadUrl || objResult.filePath
          }
        } else if (glbFile && uploadResult.results) {
          const glbResult = uploadResult.results.find((r: any) => r.originalFileName === glbFile.name)
          if (glbResult) {
            finalModelPath = glbResult.downloadUrl || glbResult.filePath
          }
        } else if (gltfFile && uploadResult.results) {
          const gltfResult = uploadResult.results.find((r: any) => r.originalFileName === gltfFile.name)
          if (gltfResult) {
            finalModelPath = gltfResult.downloadUrl || gltfResult.filePath
          }
        } else if (uploadResult.results && uploadResult.results.length > 0) {
          finalModelPath = uploadResult.results[0].downloadUrl || uploadResult.results[0].filePath
        }

        showSuccess(`批量上传成功！共 ${uploadResult.successCount} 个文件`)
      } catch (uploadError) {
        console.error('批量文件上传失败:', uploadError)
        showError('文件上传失败，请重试')
        return
      }
    }
    // 处理单个文件上传
    else if (selectedObjectFile.value) {
      try {
        showSuccess('正在上传文件...')
        const uploadResult = await fileService.uploadFile(selectedObjectFile.value, 'models-3d')
        finalModelPath = uploadResult.downloadUrl || uploadResult.filePath
        showSuccess('文件上传成功')
      } catch (uploadError) {
        console.error('文件上传失败:', uploadError)
        showError('文件上传失败，请重试')
        return
      }
    }

    // 创建对象
    const objectData = {
      name: objectForm.value.name,
      type: objectForm.value.objectType,
      modelPath: finalModelPath,
      position: [objectForm.value.position.x, objectForm.value.position.y, objectForm.value.position.z],
      rotation: JSON.stringify(objectForm.value.rotation),
      scale: JSON.stringify(objectForm.value.scale),
      isVisible: objectForm.value.isVisible,
      materialData: '{}',
      properties: '{}',
      sceneId: sceneId.value
    }

    await sceneObjectService.createObject(objectData)
    showSuccess('场景对象添加成功')

    // 刷新场景对象列表
    await loadSceneObjects()

    cancelObjectDialog()
  } catch (error) {
    console.error('保存场景对象失败:', error)
    showError('保存场景对象失败')
  }
}

// 加载场景对象列表
const loadSceneObjects = async () => {
  try {
    const objects = await sceneObjectService.getSceneObjects(sceneId.value)
    sceneForm.value.sceneObjects = objects || []
  } catch (error) {
    console.error('加载场景对象失败:', error)
  }
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
.object-count {
  font-size: 0.85rem;
  color: #666;
  font-weight: normal;
}

.scene-objects-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 1rem;
  margin-bottom: 1rem;
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

.object-actions {
  display: flex;
  gap: 0.5rem;
  flex-shrink: 0;
}

.btn-delete-object {
  width: 28px;
  height: 28px;
  border: none;
  background: transparent;
  color: #999;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: all 0.2s ease;
  font-size: 0.9rem;
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

.empty-objects {
  text-align: center;
  padding: 2rem;
  color: #999;
  background: #f8f9fa;
  border-radius: 6px;
  margin-bottom: 1rem;
}

.empty-objects p {
  margin: 0;
  font-size: 0.9rem;
}

.btn-add-object {
  width: 100%;
  padding: 0.75rem;
  background: #f0f7ff;
  color: #1890ff;
  border: 1px dashed #1890ff;
  border-radius: 6px;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 500;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
}

.btn-add-object:hover {
  background: #e6f4ff;
  border-style: solid;
}

/* 对象对话框样式 */
.object-dialog {
  padding: 0.5rem 0;
}

.model-path-selector {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.path-actions {
  display: flex;
  gap: 0.5rem;
}

.btn-action {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.5rem 1rem;
  background: #007acc;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.85rem;
  font-weight: 500;
  transition: all 0.2s;
}

.btn-action:hover {
  background: #005999;
}

.btn-action.btn-batch {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.btn-action.btn-batch:hover {
  background: linear-gradient(135deg, #5a67d8 0%, #6b42ad 100%);
}

.file-info {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem;
  background: #f8f9fa;
  border: 1px solid #e1e5e9;
  border-radius: 6px;
  margin-top: 0.5rem;
}

.file-icon {
  font-size: 1.5rem;
}

.file-details {
  flex: 1;
  min-width: 0;
}

.file-name {
  font-weight: 600;
  color: #333;
  margin-bottom: 0.25rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.file-size {
  font-size: 0.85rem;
  color: #666;
}

.btn-clear {
  padding: 0.25rem 0.5rem;
  background: #dc3545;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  transition: all 0.2s;
}

.btn-clear:hover {
  background: #c82333;
}

/* 批量文件上传样式 */
.files-list {
  margin-top: 1rem;
  padding: 1rem;
  background: #f8f9fa;
  border: 1px solid #e1e5e9;
  border-radius: 6px;
}

.files-list-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
  padding-bottom: 0.75rem;
  border-bottom: 2px solid #e1e5e9;
}

.files-count {
  font-weight: 600;
  color: #333;
  font-size: 0.95rem;
}

.btn-clear-all {
  padding: 0.4rem 0.8rem;
  background: #dc3545;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.85rem;
  font-weight: 500;
  transition: all 0.2s;
}

.btn-clear-all:hover {
  background: #c82333;
  transform: translateY(-1px);
}

.files-grid {
  display: grid;
  gap: 0.75rem;
  max-height: 300px;
  overflow-y: auto;
  padding-right: 0.5rem;
}

.files-grid::-webkit-scrollbar {
  width: 6px;
}

.files-grid::-webkit-scrollbar-thumb {
  background: #cbd5e0;
  border-radius: 3px;
}

.files-grid::-webkit-scrollbar-track {
  background: #f7fafc;
}

.file-item {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem;
  background: white;
  border: 1px solid #e1e5e9;
  border-radius: 6px;
  transition: all 0.2s;
}

.file-item:hover {
  border-color: #007acc;
  box-shadow: 0 2px 4px rgba(0, 122, 204, 0.1);
}

.file-item .file-icon {
  font-size: 1.5rem;
  flex-shrink: 0;
}

.file-item .file-details {
  flex: 1;
  min-width: 0;
}

.file-item .file-name {
  font-size: 0.9rem;
  font-weight: 500;
  margin-bottom: 0.25rem;
}

.file-item .file-size {
  font-size: 0.8rem;
  color: #666;
}

.btn-remove {
  padding: 0.25rem 0.5rem;
  background: #f8f9fa;
  color: #dc3545;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 600;
  transition: all 0.2s;
  flex-shrink: 0;
}

.btn-remove:hover {
  background: #dc3545;
  color: white;
  border-color: #dc3545;
}

.transform-section {
  margin-top: 1.5rem;
  padding-top: 1rem;
  border-top: 1px solid #f0f0f0;
}

.transform-title {
  margin: 0 0 1rem 0;
  font-size: 0.95rem;
  font-weight: 600;
  color: #333;
}

.transform-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 1rem;
}

.transform-group {
  background: #fafafa;
  padding: 1rem;
  border-radius: 6px;
}

.transform-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-size: 0.85rem;
  font-weight: 500;
  color: #666;
}

.vector-input {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 0.5rem;
}

.vector-input input {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid #d9d9d9;
  border-radius: 6px;
  font-size: 0.85rem;
  text-align: center;
  transition: all 0.2s ease;
  box-sizing: border-box;
}

.vector-input input:focus {
  outline: none;
  border-color: #1890ff;
  box-shadow: 0 0 0 2px rgba(24, 144, 255, 0.1);
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
