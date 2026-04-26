<template>
  <div class="scene-objects-edit-page">
    <!-- 页面头部 -->
    <header class="page-header">
      <div class="header-left">
        <button @click="handleBack" class="btn-back" type="button">
          <span class="back-icon">←</span>
          <span>返回</span>
        </button>
        <h1>编辑场景对象</h1>
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
        <!-- 基本信息 -->
        <section class="form-section">
          <h2 class="section-title">基本信息</h2>
          <div class="form-row">
            <div class="form-group">
              <label>对象名称 <span class="required">*</span></label>
              <input
                v-model="objectForm.name"
                type="text"
                class="form-input"
                placeholder="请输入对象名称"
              />
            </div>
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
          
          <!-- 模型路径选择器 -->
          <div class="form-group">
            <label>模型/数据路径 <span class="required">*</span></label>
            <div class="model-path-selector">
              <input
                v-model="objectForm.modelPath"
                type="text"
                class="form-input"
                placeholder="输入模型路径或切片数据路径"
              />
              <div class="path-actions">
                <button @click="selectLocalFile" class="btn-action" type="button">
                  <span>📁</span> 选择文件
                </button>
                <button @click="selectMultipleFiles" class="btn-action btn-batch" type="button">
                  <span>📦</span> 批量选择
                </button>
                <button 
                  v-if="objectForm.modelPath && isPreviewSupported" 
                  @click="previewCurrentModel" 
                  class="btn-action btn-preview" 
                  type="button"
                >
                  <span>👁️</span> 预览
                </button>
              </div>
            </div>
            <p class="form-hint">
              💡 提示：可以直接输入本地文件路径（如 C:\models\model.glb 或 /home/user/model.glb），也可以点击"选择文件"按钮选择文件
            </p>
            
            <!-- 隐藏的文件输入 -->
            <input
              ref="fileInputRef"
              type="file"
              accept=".gltf,.glb,.obj,.fbx,.dae,.3ds,.stl"
              @change="handleFileSelect"
              style="display: none"
            />
            
            <!-- 多文件选择器 -->
            <input
              ref="multiFileInputRef"
              type="file"
              accept=".gltf,.glb,.obj,.fbx,.dae,.3ds,.mtl,.jpg,.jpeg,.png,.webp,.bmp"
              multiple
              @change="handleMultipleFilesSelect"
              style="display: none"
            />
            
            <!-- 已选择单个文件信息 -->
            <div v-if="selectedFile && !selectedFiles.length" class="file-info">
              <span class="file-icon">📄</span>
              <div class="file-details">
                <div class="file-name">{{ selectedFile.name }}</div>
                <div class="file-meta">
                  <span>{{ formatFileSize(selectedFile.size) }}</span>
                  <span>{{ getFileExtension(selectedFile.name) }}</span>
                </div>
              </div>
              <button @click="clearFile" class="btn-clear" type="button">✕</button>
            </div>
            
            <!-- 批量选择文件列表 -->
            <div v-if="selectedFiles.length > 0" class="files-list">
              <div class="files-list-header">
                <span class="files-count">已选择 {{ selectedFiles.length }} 个文件</span>
                <button @click="clearAllFiles" class="btn-clear-all" type="button">清空全部</button>
              </div>
              <div class="files-grid">
                <div v-for="(file, index) in selectedFiles" :key="index" class="file-item">
                  <span class="file-icon">{{ getFileIcon(file.name) }}</span>
                  <div class="file-details">
                    <div class="file-name" :title="file.name">{{ file.name }}</div>
                    <div class="file-size">{{ formatFileSize(file.size) }}</div>
                  </div>
                  <button @click="removeFile(index)" class="btn-remove" type="button">✕</button>
                </div>
              </div>
              <!-- OBJ文件提示 -->
              <div v-if="hasOBJFile" class="upload-hint success">
                <span class="hint-icon">✓</span>
                <div class="hint-content">
                  <strong>OBJ模型上传提示</strong>
                  <p>请确保已选择：OBJ文件 + MTL材质文件 + 纹理图片（.jpg/.png等）</p>
                  <div class="file-check">
                    <span :class="['check-item', { checked: hasOBJFile }]">
                      {{ hasOBJFile ? '✓' : '○' }} OBJ文件 (.obj)
                    </span>
                    <span :class="['check-item', { checked: hasMTLFile }]">
                      {{ hasMTLFile ? '✓' : '○' }} MTL材质 (.mtl)
                    </span>
                    <span :class="['check-item', { checked: hasTextureFiles }]">
                      {{ hasTextureFiles ? '✓' : '○' }} 纹理图片
                    </span>
                  </div>
                </div>
              </div>
            </div>
            
            <div class="field-hint">
              <p><strong>支持的格式：</strong></p>
              <ul>
                <li><strong>模型数据：</strong>.glb, .gltf, .obj, .fbx, .dae, .stl 等</li>
                <li><strong>切片数据：</strong>3D Tiles (tileset.json), 点云切片等</li>
              </ul>
            </div>
          </div>
          
          <!-- 切片对象 -->
          <div v-if="showSlicingInfo" class="form-group">
            <label><span class="title-icon">✂️</span> 切片数据</label>
            <div class="slicing-objects-list">
              <div class="slicing-object-item">
                <div class="slicing-info">
                  <span class="slicing-icon">📦</span>
                  <div class="slicing-details">
                    <h4 class="slicing-name">{{ getSlicingName() }}</h4>
                    <p v-if="originalObject?.slicingOutputPath" class="slicing-path" :title="originalObject.slicingOutputPath">
                      {{ getShortPath(originalObject.slicingOutputPath) }}
                    </p>
                  </div>
                  <div class="slicing-actions">
                    <button
                      @click="deleteSlicing"
                      class="btn-delete-slicing"
                      title="删除切片"
                      type="button"
                    >
                      🗑️
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </section>

        <!-- 变换属性 -->
        <section class="form-section">
          <h2 class="section-title">
            <span class="title-icon">📍</span>
            位置设置
          </h2>
          <div class="form-group">
            <label>位置 (经度, 纬度, 高度)</label>
            <div class="vector-input">
              <input v-model.number="objectForm.position.x" type="number" step="0.0001" placeholder="经度" />
              <input v-model.number="objectForm.position.y" type="number" step="0.0001" placeholder="纬度" />
              <input v-model.number="objectForm.position.z" type="number" step="0.1" placeholder="高度(米)" />
            </div>
            <p class="form-hint">
              💡 提示：位置使用 WGS84 坐标系（经度、纬度、高度）。对于 3D Tiles 模型，通常使用默认位置 (0, 0, 0) 即可，模型会自动定位。
            </p>
          </div>
        </section>

        <!-- 显示设置 -->
        <section class="form-section">
          <h2 class="section-title">显示设置</h2>
          <div class="form-group">
            <label class="checkbox-label">
              <input v-model="objectForm.isVisible" type="checkbox" />
              <span>对象可见</span>
            </label>
          </div>
        </section>

        <!-- 操作按钮 -->
        <div class="form-actions">
          <button @click="handleBack" class="btn btn-secondary">
            取消
          </button>
          <button @click="saveObject" class="btn btn-primary" :disabled="isSubmitting">
            {{ isSubmitting ? '保存中...' : '保存' }}
          </button>
        </div>
      </div>
    </div>

    <!-- URL输入对话框 -->
    <Modal
      v-model="showUrlDialog"
      title="输入模型URL"
      size="md"
    >
      <div class="url-dialog">
        <div class="form-group">
          <label>模型URL地址</label>
          <input
            v-model="urlInput"
            type="url"
            class="form-input"
            placeholder="https://example.com/model.glb"
            @keyup.enter="confirmUrl"
          />
          <div class="url-hints">
            <p class="hint-title">支持的格式:</p>
            <div class="format-tags">
              <span class="tag">.gltf</span>
              <span class="tag">.glb</span>
              <span class="tag">.obj</span>
              <span class="tag">.fbx</span>
              <span class="tag">.dae</span>
            </div>
          </div>
        </div>
      </div>
      <template #footer>
        <button @click="showUrlDialog = false" class="btn btn-secondary">取消</button>
        <button @click="confirmUrl" :disabled="!urlInput" class="btn btn-primary">确认</button>
      </template>
    </Modal>

    <!-- 3D模型预览对话框 -->
    <Modal
      v-model="showPreviewDialog"
      title="3D模型预览"
      size="xl"
      :show-footer="false"
    >
      <div style="height: 600px;">
        <ModelViewer
          :model-url="previewModelUrl"
          :model-file="previewModelFile"
          :model-files="previewModelFiles"
          :show-controls="true"
          :show-info="true"
          :auto-rotate="false"
        />
      </div>
    </Modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { sceneObjectService, fileService, slicingService } from '@/services/api'
import { useMessage } from '@/composables/useMessage'
import { useAuthStore } from '@/stores/auth'
import Modal from '@/components/Modal.vue'
import ModelViewer from '@/components/ModelViewer.vue'
import { FileHandleStore } from '@/services/fileHandleStore'

const { success: showSuccess, error: showError } = useMessage()
const { currentUser } = useAuthStore()
const router = useRouter()
const route = useRoute()

// 创建FileHandleStore实例
const fileHandleStore = new FileHandleStore()

// 状态
const isLoading = ref(true)
const isSubmitting = ref(false)
const objectId = ref<string>('')
const originalObject = ref<any>(null)

// 预览状态
const showPreviewDialog = ref(false)
const showUrlDialog = ref(false)
const previewModelUrl = ref('')
const previewModelFile = ref<File | undefined>(undefined)
const previewModelFiles = ref<File[] | undefined>(undefined)

// URL输入
const urlInput = ref('')

// 文件选择相关
const fileInputRef = ref<HTMLInputElement>()
const multiFileInputRef = ref<HTMLInputElement>()
const selectedFile = ref<File | null>(null)
const selectedFiles = ref<File[]>([])
const selectedFileHandle = ref<any | null>(null)
const localPreviewUrl = ref('')
const selectedFileExtension = ref('')

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

// 计算属性：检查批量文件中的类型
const hasOBJFile = computed(() => selectedFiles.value.some(f => f.name.toLowerCase().endsWith('.obj')))
const hasMTLFile = computed(() => selectedFiles.value.some(f => f.name.toLowerCase().endsWith('.mtl')))
const hasTextureFiles = computed(() => selectedFiles.value.some(f => {
  const ext = f.name.toLowerCase()
  return ext.endsWith('.jpg') || ext.endsWith('.jpeg') || ext.endsWith('.png') || ext.endsWith('.webp') || ext.endsWith('.bmp')
}))

/**
 * 判断当前模型是否支持预览
 * ThreeViewer.vue 支持的格式：.gltf, .glb, .obj, .fbx, .stl, .ply, .dae, .json (3D Tiles)
 * 不支持的格式应该隐藏预览按钮
 */
const isPreviewSupported = computed(() => {
  // 如果有选中的文件，检查文件扩展名
  if (selectedFiles.value.length > 0) {
    // 检查是否有支持的格式
    const supportedExts = ['.gltf', '.glb', '.obj', '.fbx', '.stl', '.ply', '.dae', '.json']
    return selectedFiles.value.some(f => {
      const ext = '.' + f.name.split('.').pop()?.toLowerCase()
      return supportedExts.includes(ext)
    })
  }

  if (selectedFile.value) {
    const supportedExts = ['.gltf', '.glb', '.obj', '.fbx', '.stl', '.ply', '.dae', '.json']
    const ext = '.' + selectedFile.value.name.split('.').pop()?.toLowerCase()
    return supportedExts.includes(ext)
  }

  // 检查 modelPath
  const path = objectForm.value.modelPath
  if (!path) return false

  const lowerPath = path.toLowerCase()
  const supportedExts = ['.gltf', '.glb', '.obj', '.fbx', '.stl', '.ply', '.dae', '.json']
  return supportedExts.some(ext => lowerPath.includes(ext))
})

// 计算属性：是否显示切片信息
const showSlicingInfo = computed(() => {
  return originalObject.value?.slicingOutputPath && originalObject.value?.slicingOutputPath.trim() !== ''
})

// 加载对象数据
const loadObject = async () => {
  const id = route.params.id as string
  if (!id) {
    showError('对象ID不存在')
    router.push({ name: 'SceneObjects' })
    return
  }

  objectId.value = id

  try {
    isLoading.value = true
    const obj = await sceneObjectService.getObject(id)

    // 保存原始对象数据（用于切片信息显示）
    originalObject.value = obj

    // 解析位置数据
    let position = { x: 0, y: 0, z: 0 }
    if (obj.position) {
      if (Array.isArray(obj.position)) {
        position = { x: obj.position[0] || 0, y: obj.position[1] || 0, z: obj.position[2] || 0 }
      } else if (typeof obj.position === 'object') {
        position = { ...obj.position }
      }
    }

    // 解析旋转数据
    let rotation = { x: 0, y: 0, z: 0 }
    if (obj.rotation) {
      if (typeof obj.rotation === 'string') {
        try {
          rotation = JSON.parse(obj.rotation)
        } catch {
          rotation = { x: 0, y: 0, z: 0 }
        }
      } else if (typeof obj.rotation === 'object') {
        rotation = { ...obj.rotation }
      }
    }

    // 解析缩放数据
    let scale = { x: 1, y: 1, z: 1 }
    if (obj.scale) {
      if (typeof obj.scale === 'string') {
        try {
          scale = JSON.parse(obj.scale)
        } catch {
          scale = { x: 1, y: 1, z: 1 }
        }
      } else if (typeof obj.scale === 'object') {
        scale = { ...obj.scale }
      }
    }

    objectForm.value = {
      name: obj.name || '',
      objectType: obj.objectType || obj.type || 'Model3D',
      modelPath: obj.modelPath || '',
      position,
      rotation,
      scale,
      isVisible: obj.isVisible ?? true
    }
  } catch (error) {
    console.error('加载对象失败:', error)
    showError('加载对象信息失败')
    router.push({ name: 'SceneObjects' })
  } finally {
    isLoading.value = false
  }
}

// 返回列表页面
const handleBack = () => {
  router.push({ name: 'SceneObjects' })
}

// 选择本地文件
const selectLocalFile = async () => {
  // 检查文件系统访问API支持
  if ('showOpenFilePicker' in window && window.showOpenFilePicker) {
    try {
      const [handle] = await window.showOpenFilePicker({
        types: [{
          description: '3D Models',
          accept: {
            'model/gltf-binary': ['.glb'],
            'model/gltf+json': ['.gltf'],
            'model/obj': ['.obj'],
            'application/octet-stream': ['.fbx', '.dae', '.3ds'],
          }
        }],
        multiple: false
      })

      selectedFileHandle.value = handle
      const file = await handle.getFile()
      
      const maxSize = 500 * 1024 * 1024
      if (file.size > maxSize) {
        showError('文件大小超过500MB限制')
        return
      }

      selectedFile.value = file
      objectForm.value.modelPath = file.name
      showSuccess(`已选择文件: ${file.name}。如需完整路径，请手动编辑路径字段。`)

    } catch (err) {
      if ((err as Error).name !== 'AbortError') {
        console.error('File selection error:', err)
        showError('选择文件时发生错误。')
      }
    }
  } else {
    showError('您的浏览器不支持持久化本地文件访问。将使用传统方式选择文件。')
    fileInputRef.value?.click()
  }
}

// 处理文件选择
const handleFileSelect = async (event: Event) => {
  const target = event.target as HTMLInputElement
  const file = target.files?.[0]
  if (!file) return

  selectedFileHandle.value = null

  const maxSize = 500 * 1024 * 1024
  if (file.size > maxSize) {
    showError('文件大小超过500MB限制')
    return
  }

  selectedFile.value = file
  objectForm.value.modelPath = file.name
  showSuccess(`已选择文件: ${file.name}。如需完整路径，请手动编辑路径字段。`)
  
  // 如果是 OBJ 文件，提示用户选择关联文件
  if (file.name.toLowerCase().endsWith('.obj')) {
    const shouldSelectRelated = confirm(
      '检测到 OBJ 文件。\n\n' +
      'OBJ 文件通常需要 MTL 材质文件和纹理图片。\n' +
      '点击"确定"选择关联文件（MTL、纹理等）。\n' +
      '点击"取消"仅使用当前 OBJ 文件。'
    )
    
    if (shouldSelectRelated) {
      // 打开多文件选择对话框
      setTimeout(() => {
        selectMultipleFiles()
      }, 100)
    }
  }
}

// 打开URL输入对话框
const openUrlDialog = () => {
  urlInput.value = objectForm.value.modelPath || ''
  showUrlDialog.value = true
}

// 确认URL输入
const confirmUrl = () => {
  if (!urlInput.value) {
    showError('请输入模型URL')
    return
  }

  try {
    new URL(urlInput.value)
    objectForm.value.modelPath = urlInput.value
    showUrlDialog.value = false

    // 清除本地文件选择
    selectedFile.value = null
    selectedFileHandle.value = null
    selectedFileExtension.value = ''
    if (localPreviewUrl.value) {
      URL.revokeObjectURL(localPreviewUrl.value)
      localPreviewUrl.value = ''
    }

    showSuccess('已设置模型URL')
  } catch (error) {
    showError('无效的URL格式')
  }
}

// 选择多个文件（批量上传）
const selectMultipleFiles = () => {
  multiFileInputRef.value?.click()
}

// 处理多文件选择
const handleMultipleFilesSelect = (event: Event) => {
  const target = event.target as HTMLInputElement
  const files = Array.from(target.files || [])

  if (files.length === 0) return

  // 验证文件大小（单个文件不超过500MB）
  const maxSize = 500 * 1024 * 1024
  const oversizedFiles = files.filter(f => f.size > maxSize)

  if (oversizedFiles.length > 0) {
    showError(`以下文件超过500MB限制:\n${oversizedFiles.map(f => f.name).join('\n')}`)
    return
  }

  // 如果已有单个 OBJ 文件选择，将其合并到批量文件中
  let finalFiles = files
  if (selectedFile.value && selectedFile.value.name.toLowerCase().endsWith('.obj')) {
    // 检查是否已包含该 OBJ 文件
    const existingObj = files.find(f => f.name === selectedFile.value!.name)
    if (!existingObj) {
      finalFiles = [selectedFile.value, ...files]
      console.log('[handleMultipleFilesSelect] 将已选 OBJ 文件合并到批量列表')
    }
  }

  // 清除单文件选择
  selectedFile.value = null
  selectedFileHandle.value = null

  // 设置批量文件
  selectedFiles.value = finalFiles

  // 查找OBJ文件并设置为主模型路径
  const objFile = finalFiles.find(f => f.name.toLowerCase().endsWith('.obj'))
  if (objFile) {
    objectForm.value.modelPath = objFile.name
    showSuccess(`已选择 ${finalFiles.length} 个文件，主模型: ${objFile.name}`)
  } else {
    objectForm.value.modelPath = finalFiles[0].name
    showSuccess(`已选择 ${finalFiles.length} 个文件`)
  }
}

// 清除文件选择
const clearFile = () => {
  selectedFile.value = null
  selectedFileHandle.value = null
  objectForm.value.modelPath = ''
  selectedFileExtension.value = ''

  if (localPreviewUrl.value) {
    URL.revokeObjectURL(localPreviewUrl.value)
    localPreviewUrl.value = ''
  }

  if (fileInputRef.value) {
    fileInputRef.value.value = ''
  }
}

// 清空所有选中的文件
const clearAllFiles = () => {
  selectedFiles.value = []
  objectForm.value.modelPath = ''

  if (multiFileInputRef.value) {
    multiFileInputRef.value.value = ''
  }
}

// 移除指定索引的文件
const removeFile = (index: number) => {
  selectedFiles.value.splice(index, 1)

  if (selectedFiles.value.length === 0) {
    objectForm.value.modelPath = ''
  } else {
    const objFile = selectedFiles.value.find(f => f.name.toLowerCase().endsWith('.obj'))
    if (objFile) {
      objectForm.value.modelPath = objFile.name
    } else {
      objectForm.value.modelPath = selectedFiles.value[0].name
    }
  }
}

// 预览当前选择的模型
const previewCurrentModel = async () => {
  // 重置预览状态
  previewModelFile.value = undefined
  previewModelFiles.value = undefined
  previewModelUrl.value = ''

  // 优先使用批量文件（支持 OBJ + MTL + 纹理）
  if (selectedFiles.value.length > 0) {
    previewModelFiles.value = selectedFiles.value
    showPreviewDialog.value = true
  } else if (selectedFile.value) {
    previewModelFile.value = selectedFile.value
    showPreviewDialog.value = true
  } else if (objectForm.value.modelPath) {
    const path = objectForm.value.modelPath
    
    // 处理文件句柄路径
    if (path.startsWith('local-file-handle://')) {
      try {
        const uuid = path.replace('local-file-handle://', '')
        const handle = await fileHandleStore.getHandle<any>(uuid)
        
        if (handle) {
          // 检查权限
          let permission = await handle.queryPermission({ mode: 'read' })
          if (permission !== 'granted') {
            permission = await handle.requestPermission({ mode: 'read' })
          }
          
          if (permission === 'granted') {
            const file = await handle.getFile()
            previewModelFile.value = file
            showPreviewDialog.value = true
          } else {
            showError('无法访问文件句柄，请重新选择文件')
          }
        } else {
          showError('文件句柄已失效，请重新选择文件')
        }
      } catch (err) {
        console.error('[SceneObjectsEdit] 文件句柄处理失败:', err)
        showError('文件句柄访问失败，请重新选择文件')
      }
      return
    }
    
    // 如果是本地文件路径（Windows路径如 E:\Data\... 或 Linux路径如 /home/user/...）
    // 转换为后端代理URL进行预览
    if (path.match(/^[A-Za-z]:[\\\/]/) || path.match(/^\/[^\/]/)) {
      // 标准化路径：将反斜杠转换为正斜杠
      const normalizedPath = path.replace(/\\/g, '/')

      // 处理Windows路径：E:/Data/3D/model.glb -> /api/files/local/E:/Data/3D/model.glb
      const driveMatch = normalizedPath.match(/^([A-Za-z]:)(.*)/)
      if (driveMatch) {
        const drive = driveMatch[1].replace(':', '%3A')
        const rest = driveMatch[2]
        // 对剩余部分进行编码，但保留正斜杠
        const encodedRest = rest
          .replace(/\s/g, '%20')
          .replace(/#/g, '%23')
          .replace(/\?/g, '%3F')

        const proxyUrl = `/api/files/local/${drive}${encodedRest}`
        console.log('[SceneObjectsEditPage] 本地文件路径转换为代理URL:', { original: path, proxyUrl })

        previewModelUrl.value = proxyUrl
        showPreviewDialog.value = true
      } else {
        // Linux路径：/home/user/model.glb -> /api/files/local/home/user/model.glb
        const proxyUrl = `/api/files/local${normalizedPath}`
        console.log('[SceneObjectsEditPage] Linux本地文件路径转换为代理URL:', { original: path, proxyUrl })

        previewModelUrl.value = proxyUrl
        showPreviewDialog.value = true
      }
      return
    }
    
    // 如果是URL（以http://或https://开头），直接预览
    if (path.startsWith('http://') || path.startsWith('https://')) {
      previewModelUrl.value = path
      showPreviewDialog.value = true
    } else {
      // 其他情况（文件名等），提示用户重新选择
      showError('请重新选择文件进行预览，或输入完整的URL地址')
    }
  } else {
    showError('没有可预览的模型')
  }
}

// 格式化文件大小
const formatFileSize = (bytes: number): string => {
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB'
  return (bytes / 1024 / 1024).toFixed(1) + ' MB'
}

// 获取文件扩展名
const getFileExtension = (filename: string): string => {
  return '.' + filename.split('.').pop()?.toUpperCase() || ''
}

// 根据文件名获取文件图标
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

// 生成UUID
const generateUUID = (): string => {
  if (typeof crypto !== 'undefined' && crypto.randomUUID) {
    return crypto.randomUUID()
  }
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0
    const v = c === 'x' ? r : (r & 0x3) | 0x8
    return v.toString(16)
  })
}

// 获取切片名称
const getSlicingName = (): string => {
  if (!originalObject.value) return '切片数据'
  return originalObject.value.slicingName || originalObject.value.name || '切片数据'
}

// 获取短路径（用于显示）
const getShortPath = (path: string): string => {
  if (!path) return ''
  const maxLength = 50
  if (path.length <= maxLength) return path

  // 尝试保留文件名
  const lastSlash = Math.max(path.lastIndexOf('/'), path.lastIndexOf('\\'))
  if (lastSlash !== -1) {
    const fileName = path.substring(lastSlash + 1)
    const directory = path.substring(0, lastSlash)
    if (fileName.length >= maxLength - 3) {
      return '...' + fileName.substring(fileName.length - maxLength + 3)
    }
    return directory.substring(0, maxLength - fileName.length - 4) + '/.../' + fileName
  }

  return path.substring(0, maxLength - 3) + '...'
}

// 删除切片
const deleteSlicing = async () => {
  if (!originalObject.value?.slicingTaskId) {
    showError('没有可删除的切片数据')
    return
  }

  const confirmed = confirm('确定要删除切片数据吗？此操作将删除切片任务及其生成的所有切片文件，不可撤销。')
  if (!confirmed) return

  try {
    isSubmitting.value = true

    // 获取当前用户ID
    const userId = currentUser.value?.id
    if (!userId) {
      showError('无法获取用户信息，请重新登录')
      return
    }

    // 调用切片任务删除 API
    await slicingService.deleteSlicingTask(
      originalObject.value.slicingTaskId,
      userId
    )

    // 更新本地状态
    originalObject.value = {
      ...originalObject.value,
      slicingTaskId: null,
      slicingTaskStatus: null,
      slicingOutputPath: null,
      displayPath: objectForm.value.modelPath
    }

    showSuccess('切片数据已删除')
  } catch (error: any) {
    console.error('删除切片失败:', error)
    const errorMessage = error.response?.data?.message || error.message || '删除切片失败'
    showError(`删除切片失败: ${errorMessage}`)
  } finally {
    isSubmitting.value = false
  }
}

// 保存对象
const saveObject = async () => {
  if (isSubmitting.value) return
  
  // 验证必填字段
  if (!objectForm.value.name?.trim()) {
    showError('请输入对象名称')
    return
  }

  if (!objectForm.value.modelPath?.trim()) {
    showError('请输入模型/数据路径')
    return
  }

  try {
    isSubmitting.value = true
    
    let finalModelPath = objectForm.value.modelPath
    
    // 处理批量文件 - 上传到MinIO，失败时使用文件句柄
    if (selectedFiles.value.length > 0) {
      showSuccess(`正在上传 ${selectedFiles.value.length} 个文件到服务器...`)
      
      console.log('[SceneObjectsEditPage] 准备批量上传文件:', selectedFiles.value.map(f => ({
        name: f.name,
        size: f.size,
        type: f.type
      })))
      
      try {
        // 使用批量上传API
        const uploadResult = await fileService.uploadFilesBatch(
          selectedFiles.value,
          'models-3d',
          objectForm.value.name
        )
        
        console.log('[SceneObjectsEditPage] 批量上传API响应:', uploadResult)
        
        if (!uploadResult.success) {
          throw new Error(`批量上传失败: ${uploadResult.failedCount} 个文件上传失败`)
        }
        
        console.log(`[SceneObjectsEditPage] 批量上传成功: ${uploadResult.successCount}/${uploadResult.totalFiles}`)
        
        // 查找主模型文件的上传结果
        const objFile = selectedFiles.value.find(f => f.name.toLowerCase().endsWith('.obj'))
        const glbFile = selectedFiles.value.find(f => f.name.toLowerCase().endsWith('.glb'))
        const gltfFile = selectedFiles.value.find(f => f.name.toLowerCase().endsWith('.gltf'))
        
        // 确定主模型路径 - 使用MinIO的URL
        if (objFile && uploadResult.results) {
          const objResult = uploadResult.results.find((r: any) => r.originalFileName === objFile.name)
          if (objResult) {
            finalModelPath = objResult.downloadUrl || objResult.filePath
            console.log('[SceneObjectsEditPage] OBJ主模型路径:', finalModelPath)
          }
        } else if (glbFile && uploadResult.results) {
          const glbResult = uploadResult.results.find((r: any) => r.originalFileName === glbFile.name)
          if (glbResult) {
            finalModelPath = glbResult.downloadUrl || glbResult.filePath
            console.log('[SceneObjectsEditPage] GLB主模型路径:', finalModelPath)
          }
        } else if (gltfFile && uploadResult.results) {
          const gltfResult = uploadResult.results.find((r: any) => r.originalFileName === gltfFile.name)
          if (gltfResult) {
            finalModelPath = gltfResult.downloadUrl || gltfResult.filePath
            console.log('[SceneObjectsEditPage] GLTF主模型路径:', finalModelPath)
          }
        } else if (uploadResult.results && uploadResult.results.length > 0) {
          finalModelPath = uploadResult.results[0].downloadUrl || uploadResult.results[0].filePath
          console.log('[SceneObjectsEditPage] 使用第一个文件作为主模型:', finalModelPath)
        }
        
        showSuccess(`批量上传成功！共 ${uploadResult.successCount} 个文件`)
      } catch (uploadError) {
        console.error('[SceneObjectsEditPage] MinIO上传失败，尝试使用文件句柄:', uploadError)
        
        // MinIO上传失败，使用文件句柄作为备选方案
        if (selectedFileHandle.value) {
          try {
            const uuid = generateUUID()
            await fileHandleStore.saveHandle(uuid, selectedFileHandle.value)
            finalModelPath = `local-file-handle://${uuid}`
            console.log('[SceneObjectsEditPage] 使用文件句柄路径:', finalModelPath)
            showSuccess('MinIO上传失败，已保存文件句柄作为备选方案')
          } catch (handleError) {
            console.error('[SceneObjectsEditPage] 保存文件句柄也失败:', handleError)
            showError('文件上传失败，请检查网络连接或手动输入文件路径')
            return
          }
        } else {
          showError('MinIO上传失败，且不支持文件句柄，请手动输入文件路径')
          return
        }
      }
    }
    // 处理单个文件 - 上传到MinIO，失败时使用文件句柄
    else if (selectedFile.value || selectedFileHandle.value) {
      const file = selectedFile.value || (selectedFileHandle.value ? await selectedFileHandle.value.getFile() : null)
      
      if (!file) {
        showError('未选择文件')
        return
      }
      
      showSuccess('正在上传文件到服务器...')
      
      try {
        // 上传文件到MinIO
        const uploadResult = await fileService.uploadFile(
          file,
          'models-3d',
          (percent) => {
            console.log(`上传进度: ${percent}%`)
          }
        )
        
        finalModelPath = uploadResult.downloadUrl || uploadResult.filePath
        showSuccess('文件上传成功')
      } catch (uploadError) {
        console.error('[SceneObjectsEditPage] MinIO上传失败，尝试使用文件句柄:', uploadError)
        
        // MinIO上传失败，使用文件句柄作为备选方案
        if (selectedFileHandle.value) {
          try {
            const uuid = generateUUID()
            await fileHandleStore.saveHandle(uuid, selectedFileHandle.value)
            finalModelPath = `local-file-handle://${uuid}`
            console.log('[SceneObjectsEditPage] 使用文件句柄路径:', finalModelPath)
            showSuccess('MinIO上传失败，已保存文件句柄作为备选方案')
          } catch (handleError) {
            console.error('[SceneObjectsEditPage] 保存文件句柄也失败:', handleError)
            showError('文件上传失败，请检查网络连接或手动输入文件路径')
            return
          }
        } else {
          showError('MinIO上传失败，且不支持文件句柄，请手动输入文件路径')
          return
        }
      }
    }
    // 使用用户手动输入的路径 - 不上传，直接保存
    else if (objectForm.value.modelPath) {
      finalModelPath = objectForm.value.modelPath
      showSuccess('已保存路径')
    }
    
    // 构建对象数据
    const objectData = {
      name: objectForm.value.name,
      type: objectForm.value.objectType,
      position: [
        objectForm.value.position.x,
        objectForm.value.position.y,
        objectForm.value.position.z
      ],
      rotation: JSON.stringify(objectForm.value.rotation),
      scale: JSON.stringify(objectForm.value.scale),
      modelPath: finalModelPath,
      isVisible: objectForm.value.isVisible,
      materialData: '{}',
      properties: '{}'
    }

    console.log('更新场景对象数据:', objectData)
    
    // 调用API更新对象
    await sceneObjectService.updateObject(objectId.value, objectData)
    
    showSuccess('场景对象更新成功！')
    router.push({ name: 'SceneObjects' })
  } catch (error: any) {
    console.error('更新场景对象失败:', error)
    const errorMessage = error.response?.data?.message || error.message || '更新场景对象失败'
    showError(`更新场景对象失败: ${errorMessage}`)
  } finally {
    isSubmitting.value = false
  }
}

// 初始化
onMounted(() => {
  loadObject()
})
</script>

<style scoped>
/* 遵循项目设计规范 - 浅色主题，主色调 #6366f1 */

.scene-objects-edit-page {
  min-height: 100vh;
  background: linear-gradient(to bottom, #f9fafb 0%, #ffffff 100%);
  display: flex;
  flex-direction: column;
}

/* 页面头部 */
.page-header {
  background: white;
  border-bottom: 1px solid #e5e7eb;
  padding: 1.25rem 2rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.06);
  position: sticky;
  top: 0;
  z-index: 100;
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
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  background: white;
  cursor: pointer;
  font-size: 0.875rem;
  color: #6b7280;
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
  font-weight: 500;
}

.btn-back:hover {
  border-color: #6366f1;
  color: #6366f1;
  background: #f5f7ff;
}

.back-icon {
  font-size: 1rem;
}

.page-header h1 {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 600;
  color: #1f2937;
  letter-spacing: -0.02em;
}

/* 页面内容 */
.page-content {
  flex: 1;
  padding: 2rem;
  max-width: 900px;
  margin: 0 auto;
  width: 100%;
}

.loading-state {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 400px;
  color: #9ca3af;
}

.loading-state p {
  font-size: 1rem;
}

/* 表单容器 */
.form-container {
  background: white;
  border-radius: 14px;
  border: 1px solid #e5e7eb;
  overflow: hidden;
  box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06);
}

/* 表单区域 */
.form-section {
  padding: 1.75rem 2rem;
  border-bottom: 1px solid #f3f4f6;
}

.form-section:last-of-type {
  border-bottom: none;
}

.section-title {
  margin: 0 0 1.25rem 0;
  font-size: 0.875rem;
  font-weight: 600;
  color: #374151;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.title-icon {
  font-size: 1rem;
  color: #6366f1;
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
  margin-bottom: 1.25rem;
}

.form-group:last-child {
  margin-bottom: 0;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 500;
  font-size: 0.875rem;
  color: #374151;
}

.required {
  color: #ef4444;
  margin-left: 2px;
}

/* 输入框 */
.form-input {
  width: 100%;
  padding: 0.625rem 1rem;
  border: 1.5px solid #e5e7eb;
  border-radius: 8px;
  font-size: 0.875rem;
  background: white;
  color: #1f2937;
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
}

.form-input::placeholder {
  color: #9ca3af;
}

.form-input:hover {
  border-color: #d1d5db;
}

.form-input:focus {
  outline: none;
  border-color: #6366f1;
  box-shadow: 0 0 0 3px rgba(99, 102, 241, 0.1);
}

.form-input[readonly] {
  background: #f9fafb;
  cursor: pointer;
  color: #6b7280;
}

.form-input[readonly]:hover {
  background: #f3f4f6;
}

/* 下拉选择框 */
select.form-input {
  cursor: pointer;
  appearance: none;
  background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 12 12'%3E%3Cpath fill='%236b7280' d='M6 8L1 3h10z'/%3E%3C/svg%3E");
  background-repeat: no-repeat;
  background-position: right 1rem center;
  padding-right: 2.5rem;
}

select.form-input option {
  background: white;
  color: #1f2937;
}

.field-hint {
  margin-top: 0.75rem;
  padding: 0.75rem 1rem;
  background: #f5f7ff;
  border-radius: 8px;
  border-left: 3px solid #6366f1;
  font-size: 0.8rem;
  color: #4b5563;
}

.field-hint p {
  margin: 0.25rem 0;
}

.field-hint ul {
  margin: 0.5rem 0;
  padding-left: 1.25rem;
}

.field-hint li {
  margin: 0.25rem 0;
}

.field-hint strong {
  color: #374151;
}

.form-hint {
  margin-top: 0.5rem;
  font-size: 0.8rem;
  color: #6b7280;
}

/* 模型路径选择器 */
.model-path-selector {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.path-actions {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

/* 操作按钮 */
.btn-action {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.5rem 1rem;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  border: none;
  border-radius: 8px;
  cursor: pointer;
  font-size: 0.8rem;
  font-weight: 500;
  white-space: nowrap;
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
  box-shadow: 0 2px 4px rgba(102, 126, 234, 0.3);
}

.btn-action:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4);
}

.btn-action.btn-preview {
  background: linear-gradient(135deg, #10b981 0%, #34d399 100%);
  box-shadow: 0 2px 4px rgba(16, 185, 129, 0.3);
}

.btn-action.btn-preview:hover {
  box-shadow: 0 4px 12px rgba(16, 185, 129, 0.4);
}

.btn-action.btn-batch {
  background: linear-gradient(135deg, #8b5cf6 0%, #a78bfa 100%);
  box-shadow: 0 2px 4px rgba(139, 92, 246, 0.3);
}

.btn-action.btn-batch:hover {
  box-shadow: 0 4px 12px rgba(139, 92, 246, 0.4);
}

.btn-action span {
  font-size: 0.9rem;
}

/* 文件信息显示 */
.file-info {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1rem 1.25rem;
  background: #f5f7ff;
  border: 1px solid #e0e7ff;
  border-radius: 10px;
  margin-top: 0.75rem;
}

.file-icon {
  font-size: 1.75rem;
}

.file-details {
  flex: 1;
  min-width: 0;
}

.file-name {
  font-weight: 600;
  color: #1f2937;
  margin-bottom: 0.25rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 0.9rem;
}

.file-meta {
  display: flex;
  gap: 1rem;
  font-size: 0.75rem;
  color: #6b7280;
}

.btn-clear {
  padding: 0.35rem 0.6rem;
  background: #fee2e2;
  color: #ef4444;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-size: 0.85rem;
  transition: all 0.2s;
}

.btn-clear:hover {
  background: #fecaca;
}

/* 批量文件上传样式 */
.files-list {
  margin-top: 1rem;
  padding: 1.25rem;
  background: #f9fafb;
  border: 1px solid #e5e7eb;
  border-radius: 12px;
}

.files-list-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
  padding-bottom: 0.75rem;
  border-bottom: 1px solid #e5e7eb;
}

.files-count {
  font-weight: 600;
  color: #374151;
  font-size: 0.875rem;
}

.btn-clear-all {
  padding: 0.4rem 0.875rem;
  background: #ef4444;
  color: white;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-size: 0.8rem;
  font-weight: 500;
  transition: all 0.2s;
}

.btn-clear-all:hover {
  background: #dc2626;
  transform: translateY(-1px);
}

.files-grid {
  display: grid;
  gap: 0.5rem;
  max-height: 280px;
  overflow-y: auto;
  padding-right: 0.5rem;
}

.files-grid::-webkit-scrollbar {
  width: 4px;
}

.files-grid::-webkit-scrollbar-thumb {
  background: #d1d5db;
  border-radius: 2px;
}

.files-grid::-webkit-scrollbar-track {
  background: #f3f4f6;
}

.file-item {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem 1rem;
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  transition: all 0.2s;
}

.file-item:hover {
  border-color: #6366f1;
  box-shadow: 0 2px 8px rgba(99, 102, 241, 0.1);
}

.file-item .file-icon {
  font-size: 1.25rem;
  flex-shrink: 0;
}

.file-item .file-details {
  flex: 1;
  min-width: 0;
}

.file-item .file-name {
  font-size: 0.85rem;
  font-weight: 500;
  margin-bottom: 0.15rem;
}

.file-item .file-size {
  font-size: 0.75rem;
  color: #6b7280;
}

.btn-remove {
  padding: 0.25rem 0.5rem;
  background: #f3f4f6;
  color: #ef4444;
  border: 1px solid #e5e7eb;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.8rem;
  font-weight: 600;
  transition: all 0.2s;
  flex-shrink: 0;
}

.btn-remove:hover {
  background: #fee2e2;
  border-color: #fca5a5;
}

/* OBJ文件上传提示 */
.upload-hint {
  margin-top: 1rem;
  padding: 1rem 1.25rem;
  border-radius: 10px;
  border-left: 3px solid;
  display: flex;
  gap: 1rem;
  animation: fadeIn 0.3s ease;
}

.upload-hint.success {
  background: #d1fae5;
  border-left-color: #10b981;
}

.hint-icon {
  font-size: 1.25rem;
  color: #10b981;
  flex-shrink: 0;
}

.hint-content {
  flex: 1;
}

.hint-content strong {
  display: block;
  margin-bottom: 0.5rem;
  color: #065f46;
  font-size: 0.875rem;
}

.hint-content p {
  margin: 0 0 0.75rem 0;
  color: #047857;
  font-size: 0.8rem;
  line-height: 1.5;
}

.file-check {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.check-item {
  padding: 0.35rem 0.75rem;
  background: white;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  font-size: 0.75rem;
  color: #6b7280;
  font-weight: 500;
  transition: all 0.2s;
}

.check-item.checked {
  background: #d1fae5;
  border-color: #10b981;
  color: #065f46;
}

@keyframes fadeIn {
  from {
    opacity: 0;
    transform: translateY(-8px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

/* 变换属性 */
.transform-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 1.5rem;
}

.transform-group {
  background: #f9fafb;
  padding: 1rem;
  border-radius: 8px;
}

.transform-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-size: 0.8rem;
  font-weight: 500;
  color: #6b7280;
}

.vector-input {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 0.5rem;
}

.vector-input input {
  width: 100%;
  padding: 0.5rem 0.75rem;
  border: 1px solid #e5e7eb;
  border-radius: 6px;
  font-size: 0.8rem;
  text-align: center;
  background: white;
  color: #1f2937;
  transition: all 0.2s ease;
  box-sizing: border-box;
}

.vector-input input:focus {
  outline: none;
  border-color: #6366f1;
  box-shadow: 0 0 0 2px rgba(99, 102, 241, 0.1);
}

/* 复选框 */
.checkbox-label {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  cursor: pointer;
  font-weight: 500;
  font-size: 0.875rem;
  color: #374151;
  padding: 0.75rem 1rem;
  background: #f9fafb;
  border-radius: 8px;
  border: 1px solid #e5e7eb;
  transition: all 0.2s;
}

.checkbox-label:hover {
  border-color: #6366f1;
  background: #f5f7ff;
}

.checkbox-label input[type="checkbox"] {
  width: 18px;
  height: 18px;
  cursor: pointer;
  accent-color: #6366f1;
}

/* 操作按钮区域 */
.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
  padding: 1.5rem 2rem;
  background: #f9fafb;
  border-top: 1px solid #e5e7eb;
}

.btn {
  padding: 0.625rem 1.5rem;
  border-radius: 8px;
  cursor: pointer;
  font-size: 0.875rem;
  font-weight: 500;
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
}

.btn-primary {
  background: linear-gradient(135deg, #6366f1 0%, #4f46e5 100%);
  color: white;
  border: none;
  box-shadow: 0 4px 14px rgba(99, 102, 241, 0.3);
}

.btn-primary:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 6px 20px rgba(99, 102, 241, 0.4);
}

.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
  transform: none;
}

.btn-secondary {
  background: white;
  color: #6b7280;
  border: 1px solid #e5e7eb;
}

.btn-secondary:hover {
  border-color: #6366f1;
  color: #6366f1;
  background: #f5f7ff;
}

/* URL对话框样式 */
.url-dialog {
  padding: 1rem 0;
}

.url-hints {
  margin-top: 1rem;
  padding: 1rem;
  background: #f9fafb;
  border-radius: 8px;
}

.hint-title {
  margin: 0 0 0.75rem 0;
  font-size: 0.8rem;
  font-weight: 600;
  color: #6b7280;
}

.format-tags {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.format-tags .tag {
  padding: 0.35rem 0.75rem;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  border-radius: 6px;
  font-size: 0.75rem;
  font-weight: 500;
}

/* 切片对象样式 */
.slicing-objects-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.slicing-object-item {
  background: #f8f9fa;
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  padding: 1.25rem;
}

.slicing-info {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.slicing-icon {
  font-size: 2rem;
  flex-shrink: 0;
}

.slicing-details {
  flex: 1;
  min-width: 0;
}

.slicing-name {
  margin: 0 0 0.35rem 0;
  font-size: 1rem;
  font-weight: 600;
  color: #374151;
}

.slicing-path {
  margin: 0;
  font-size: 0.8rem;
  color: #6b7280;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-family: 'SFMono-Regular', 'Consolas', monospace;
}

.slicing-actions {
  display: flex;
  gap: 0.5rem;
}

.btn-delete-slicing {
  padding: 0.5rem 0.75rem;
  background: white;
  border: 1px solid #ef4444;
  border-radius: 6px;
  cursor: pointer;
  font-size: 1rem;
  transition: all 0.2s;
}

.btn-delete-slicing:hover {
  background: #ef4444;
  color: white;
}

.empty-slicing {
  padding: 2.5rem;
  text-align: center;
  background: #f9fafb;
  border: 1px dashed #d1d5db;
  border-radius: 12px;
  color: #9ca3af;
}

.empty-slicing p {
  margin: 0;
  font-size: 0.9rem;
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
    padding: 1.25rem 1.5rem;
  }

  .form-row {
    grid-template-columns: 1fr;
    gap: 1rem;
  }

  .vector-input {
    grid-template-columns: 1fr;
    gap: 0.5rem;
  }

  .form-actions {
    padding: 1rem 1.5rem;
  }
}
</style>
