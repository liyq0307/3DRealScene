<template>
  <div class="scene-objects">
    <!-- 页面标题和快捷操作 -->
    <header class="page-header">
      <div class="header-left">
        <h1>场景对象管理</h1>
        <p class="subtitle">管理3D场景中的对象、模型和元素</p>
      </div>
      <div class="header-right">
        <button @click="loadObjects" class="btn btn-primary">
          <span class="icon">🔄</span>
          刷新
        </button>
      </div>
    </header>

    <!-- 场景选择器 -->
    <div class="scene-selector">
      <label>选择场景:</label>
      <select v-model="selectedSceneId" @change="handleSceneChange" class="form-select">
        <option value="">请选择场景</option>
        <option v-for="scene in scenes" :key="scene.id" :value="scene.id">
          {{ scene.name }}
        </option>
      </select>
      <div v-if="selectedScene" class="scene-info">
        <span class="info-badge">{{ selectedScene.name }}</span>
        <span class="info-badge engine-mars3d">
          🌍 Mars3D
        </span>
        <span class="info-text">{{ objects.length }} 个对象</span>
      </div>
    </div>

    <!-- 对象列表 -->
    <div v-if="selectedSceneId" class="objects-section">
      <!-- 工具栏 -->
      <div class="toolbar">
        <div class="toolbar-left">
          <div class="view-mode">
            <button
              @click="viewMode = 'grid'"
              :class="['mode-btn', { active: viewMode === 'grid' }]"
              title="网格视图"
            >
              <span class="icon">⊞</span>
            </button>
            <button
              @click="viewMode = 'list'"
              :class="['mode-btn', { active: viewMode === 'list' }]"
              title="列表视图"
            >
              <span class="icon">☰</span>
            </button>
          </div>
        </div>
        <div class="toolbar-right">
          <input
            v-model="searchKeyword"
            type="text"
            placeholder="搜索对象..."
            class="search-input"
          />
          <select v-model="filterType" class="filter-select">
            <option value="">所有类型</option>
            <option value="Model3D">3D模型</option>
            <option value="PointCloud">点云</option>
            <option value="TileSet">瓦片集</option>
            <option value="Marker">标记</option>
          </select>
        </div>
      </div>

      <!-- 网格视图 -->
      <div v-if="viewMode === 'grid'" class="objects-grid">
        <div
          v-for="obj in filteredObjects"
          :key="obj.id"
          class="object-card"
          @click="selectObject(obj)"
          :class="{ selected: selectedObject?.id === obj.id }"
        >
          <div class="object-header">
            <span class="object-type-icon">{{ getTypeIcon(obj.objectType) }}</span>
            <span class="object-name" :title="obj.name">{{ obj.name }}</span>
          </div>
          <div class="object-meta">
            <span class="meta-item" v-if="obj.objectType">
              <span class="meta-label">类型:</span>
              {{ obj.objectType }}
            </span>
            <span class="meta-item path-item" v-if="obj.modelPath">
              <span class="meta-label">路径:</span>
              <span class="path-text" :title="obj.modelPath">{{ getShortPath(obj.modelPath) }}</span>
            </span>
            <span class="meta-item status-item">
              <span class="meta-label">切片状态:</span>
              <Badge :variant="getSlicingStatusVariant(obj.slicingTaskStatus)" :label="getSlicingStatusText(obj.slicingTaskStatus)" size="sm" />
            </span>
          </div>
          <div class="object-footer" @click.stop>
            <!-- 左侧：创建时间 -->
            <span class="created-time" v-if="obj.createdAt">
              {{ formatDateTime(obj.createdAt) }}
            </span>
            
            <!-- 右侧：三点菜单按钮 -->
            <button
              class="menu-trigger"
              @click="toggleMenu(obj.id)"
              @keydown.enter="toggleMenu(obj.id)"
              @keydown.space.prevent="toggleMenu(obj.id)"
              aria-label="操作菜单"
              :aria-expanded="activeMenuObjectId === obj.id"
            >
              <span class="dots">⋯</span>
            </button>

            <!-- 三点菜单下拉 -->
            <SceneObjectCardMenu
              v-if="activeMenuObjectId === obj.id"
              :preview-disabled="isPreviewDisabled(obj)"
              :slicing-disabled="isSlicingDisabled(obj)"
              :slicing-disabled-reason="getSlicingDisabledReason(obj)"
              @preview="previewObject(obj); closeMenu()"
              @edit="editObject(obj); closeMenu()"
              @slicing="startSlicing(obj); closeMenu()"
              @delete="deleteObject(obj.id); closeMenu()"
              @close="closeMenu"
            />
          </div>
        </div>
      </div>

      <!-- 列表视图 -->
      <div v-else class="objects-list">
        <table class="data-table">
          <thead>
            <tr>
              <th>名称</th>
              <th>类型</th>
              <th>位置</th>
              <th>旋转</th>
              <th>缩放</th>
              <th>切片状态</th>
              <th>创建时间</th>
              <th>操作</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="obj in filteredObjects"
              :key="obj.id"
              @click="selectObject(obj)"
              :class="{ selected: selectedObject?.id === obj.id }"
            >
              <td>
                <div class="object-name">
                  <span class="type-icon">{{ getTypeIcon(obj.objectType) }}</span>
                  {{ obj.name }}
                </div>
              </td>
              <td>{{ obj.objectType }}</td>
              <td>{{ formatVector(obj.position) }}</td>
              <td>{{ formatVector(obj.rotation) }}</td>
              <td>{{ formatVector(obj.scale) }}</td>
              <td>
                <span :class="getSlicingStatusClass(obj.slicingTaskStatus)">
                  {{ getSlicingStatusText(obj.slicingTaskStatus) }}
                </span>
              </td>
              <td>{{ formatDateTime(obj.createdAt) }}</td>
              <td>
                <div class="table-actions" @click.stop>
                  <button 
                    @click="previewObject(obj)" 
                    class="btn-sm"
                    :disabled="isPreviewDisabled(obj)"
                    :title="getPreviewTitle(obj)"
                  >预览</button>
                  <button @click="editObject(obj)" class="btn-sm">编辑</button>
                  <button @click="startSlicing(obj)" class="btn-sm">切片</button>
                  <button @click="deleteObject(obj.id)" class="btn-sm btn-danger">删除</button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- 空状态 -->
      <div v-if="filteredObjects.length === 0" class="empty-state">
        <p>{{ searchKeyword || filterType ? '没有符合条件的对象' : '此场景暂无对象，请在场景编辑页面中添加' }}</p>
      </div>
    </div>

    <!-- 未选择场景提示 -->
    <div v-else class="empty-state">
      <p>请先选择一个场景</p>
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
          :show-controls="true"
          :show-info="true"
          :auto-rotate="false"
        />
      </div>
    </Modal>

  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { sceneService, sceneObjectService } from '@/services/api'
import { useMessage } from '@/composables/useMessage'
import Modal from '@/components/Modal.vue'
import ModelViewer from '@/components/ModelViewer.vue'
import SceneObjectCardMenu from '@/components/SceneObjectCardMenu.vue'
import Badge from '@/components/Badge.vue'
import { FileHandleStore } from '@/services/fileHandleStore'
import authStore from '@/stores/auth'

const { success: showSuccess, error: showError } = useMessage()
const router = useRouter()

// 创建FileHandleStore实例
const fileHandleStore = new FileHandleStore()

/**
 * 生成UUID
 * 使用crypto.getRandomValues确保随机性，优先使用原生crypto.randomUUID()如果可用
 *
 * @returns {string} UUID字符串，格式如: xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx
 * @throws {Error} 当crypto API不可用时抛出错误
 */
function generateUUID(): string {
  // 优先使用现代浏览器原生支持的crypto.randomUUID()
  if (typeof crypto !== 'undefined' && crypto.randomUUID) {
    return crypto.randomUUID();
  }

  // 如果不支持原生API，回退到自定义实现
  if (typeof crypto === 'undefined' || !crypto.getRandomValues) {
    throw new Error('crypto.getRandomValues is not available. UUID generation requires a secure context.');
  }

  // 预生成16个随机字节以提高性能
  const randomBytes = new Uint8Array(16);
  crypto.getRandomValues(randomBytes);

  // 设置版本为4 (第6个字节的高4位设为0100，即4)
  randomBytes[6] = (randomBytes[6] & 0x0f) | 0x40;

  // 设置变体为RFC 4122 (第8个字节的高4位设为1000，即8、9、a或b)
  randomBytes[8] = (randomBytes[8] & 0x3f) | 0x80;

  // 转换为UUID格式的字符串
  const hex = Array.from(randomBytes, byte => byte.toString(16).padStart(2, '0')).join('');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20, 32)}`;
}

// 数据状态
const scenes = ref<any[]>([])
const objects = ref<any[]>([])
const selectedSceneId = ref('')
const selectedObject = ref<any>(null)

// UI状态
const viewMode = ref<'grid' | 'list'>('grid')
const searchKeyword = ref('')
const filterType = ref('')
const showPreviewDialog = ref(false)
const showUrlDialog = ref(false)  // URL输入对话框
const previewModelUrl = ref('')
const previewModelFile = ref<File | undefined>(undefined)  // 用于预览的File对象
const activeMenuObjectId = ref<string | null>(null)  // 当前显示菜单的对象ID

// URL输入相关
const urlInput = ref('')

// 文件选择相关（用于URL对话框和预览功能）
const fileInputRef = ref<HTMLInputElement>()
const multiFileInputRef = ref<HTMLInputElement>()
const selectedFile = ref<File | null>(null)
const selectedFiles = ref<File[]>([])
const selectedFileHandle = ref<any | null>(null)
const localPreviewUrl = ref('')
const selectedFileExtension = ref('')

// 表单数据（用于文件选择功能）
const objectForm = ref({
  name: '',
  objectType: 'Model3D',
  modelPath: '',
  position: { x: 0, y: 0, z: 0 },
  rotation: { x: 0, y: 0, z: 0 },
  scale: { x: 1, y: 1, z: 1 },
  isVisible: true
})

// 计算属性
const selectedScene = computed(() => {
  return scenes.value.find(s => s.id === selectedSceneId.value)
})

const filteredObjects = computed(() => {
  let result = objects.value

  // 类型过滤
  if (filterType.value) {
    result = result.filter(obj => obj.objectType === filterType.value)
  }

  // 搜索过滤
  if (searchKeyword.value) {
    const keyword = searchKeyword.value.toLowerCase()
    result = result.filter(obj =>
      obj.name.toLowerCase().includes(keyword) ||
      obj.objectType.toLowerCase().includes(keyword) ||
      (obj.modelPath && obj.modelPath.toLowerCase().includes(keyword))
    )
  }

  return result
})

// 数据加载方法
const loadScenes = async () => {
  try {
    scenes.value = await sceneService.getAllScenes()
  } catch (error) {
    console.error('加载场景列表失败:', error)
    showError('加载场景列表失败')
  }
}

const loadObjects = async () => {
  if (!selectedSceneId.value) return

  try {
    objects.value = await sceneObjectService.getSceneObjects(selectedSceneId.value)
  } catch (error) {
    console.error('加载场景对象失败:', error)
    showError('加载场景对象失败')
  }
}

const handleSceneChange = async () => {
  selectedObject.value = null
  await loadObjects()
}

// 对象操作方法
const selectObject = (obj: any) => {
  selectedObject.value = obj
}

/**
 * 判断预览按钮是否应禁用
 * OBJ/FBX/DAE/STL/PLY 格式需要切片完成后才能预览（预览切片结果）
 */
const isPreviewDisabled = (obj: any): boolean => {
  const path = obj.modelPath || ''
  const threeJSExts = ['.obj', '.fbx', '.dae', '.stl', '.ply']
  const lowerPath = path.toLowerCase()
  const isThreeJS = threeJSExts.some(ext => lowerPath.includes(ext))
  // Three.js 格式：切片未完成时禁用
  if (isThreeJS) {
    return obj.slicingTaskStatus?.toLowerCase() !== 'completed'
  }
  return false
}

/**
 * 获取预览按钮的提示文字
 */
const getPreviewTitle = (obj: any): string => {
  if (isPreviewDisabled(obj)) {
    return '需要切片完成后才能预览'
  }
  return '预览'
}

/**
 * 判断切片按钮是否应禁用
 * 文件句柄路径不支持切片（临时性，后端无法访问）
 */
const isSlicingDisabled = (obj: any): boolean => {
  const modelPath = obj.modelPath || ''
  return modelPath.startsWith('local-file-handle://')
}

/**
 * 获取切片按钮的禁用原因提示
 */
const getSlicingDisabledReason = (obj: any): string => {
  if (isSlicingDisabled(obj)) {
    return '文件句柄不支持切片，请先上传到MinIO或使用其他路径'
  }
  return ''
}

/**
 * 预览场景对象
 * 跳转到场景对象预览页面,在独立的全屏3D环境中查看对象
 */
const previewObject = (obj: any) => {
  if (!selectedSceneId.value) {
    showError('未选择场景')
    return
  }

  console.log('[SceneObjects] 预览对象:', obj.id, '场景:', selectedSceneId.value)

  // 跳转到场景对象预览页面
  router.push({
    name: 'SceneObjectPreview',
    params: {
      sceneId: selectedSceneId.value,
      objectId: obj.id
    }
  })
}

const editObject = (obj: any) => {
  router.push({ name: 'SceneObjectsEdit', params: { id: obj.id } })
}

const duplicateObject = async (obj: any) => {
  try {
    // 转换数据格式以匹配后端DTO
    const data = {
      sceneId: selectedSceneId.value,
      name: `${obj.name} (副本)`,
      type: obj.objectType || obj.type,  // 兼容不同的属性名
      position: [  // 后端期望数组格式
        obj.position.x + 5,  // X方向偏移5个单位
        obj.position.y,
        obj.position.z
      ],
      rotation: typeof obj.rotation === 'string' ? obj.rotation : JSON.stringify(obj.rotation),
      scale: typeof obj.scale === 'string' ? obj.scale : JSON.stringify(obj.scale),
      modelPath: obj.modelPath || obj.ModelPath,  // 兼容不同的属性名
      isVisible: obj.isVisible ?? true
    }

    await sceneObjectService.createObject(data)
    showSuccess('对象复制成功')
    await loadObjects()
  } catch (error) {
    console.error('复制对象失败:', error)
    showError('复制对象失败')
  }
}

const deleteObject = async (id: string) => {
  const objectToDelete = objects.value.find(obj => obj.id === id);
  if (!objectToDelete) return;

  if (confirm('确定要删除此对象吗?')) {
    try {
      // 检查是否为本地句柄并从IndexedDB中删除
      if (objectToDelete.modelPath && objectToDelete.modelPath.startsWith('local-file-handle://')) {
        try {
          const uuid = objectToDelete.modelPath.replace('local-file-handle://', '');
          await fileHandleStore.deleteHandle(uuid);
          showSuccess('已从本地存储中移除文件权限。');
        } catch (handleError) {
          console.error('删除文件句柄失败:', handleError);
          showError('从本地存储移除文件句柄失败。');
        }
      }

      await sceneObjectService.deleteObject(id);
      showSuccess('对象删除成功');
      await loadObjects();
      if (selectedObject.value?.id === id) {
        selectedObject.value = null;
      }
    } catch (error) {
      console.error('删除对象失败:', error);
      showError('删除对象失败');
    }
  }
}

// 切片操作方法 - 跳转到切片任务创建页面
const startSlicing = (obj: any) => {
  if (!obj.modelPath) {
    showError('该对象没有关联的模型文件，无法切片。');
    return;
  }
  
  // 跳转到切片任务创建页面，通过 query 参数传递场景对象 ID
  router.push({ 
    name: 'SlicingCreate', 
    query: { sceneObjectId: obj.id } 
  });
};

// 预览3D模型
const previewModel = async (obj: any) => {
  if (!obj.modelPath) {
    showError('该对象没有关联的模型文件');
    return;
  }

  // 处理新的本地文件句柄
  if (obj.modelPath.startsWith('local-file-handle://')) {
    try {
      const uuid = obj.modelPath.replace('local-file-handle://', '');
      const handle = await fileHandleStore.getHandle<any>(uuid);
      if (handle && (await handle.queryPermission({ mode: 'read' }) === 'granted')) {
        const file = await handle.getFile();
        previewModelFile.value = file;
        previewModelUrl.value = '';
        showPreviewDialog.value = true;
      } else {
        showError('无法自动预览本地文件，请进入编辑模式重新选择文件。');
      }
    } catch (err) {
      showError('加载本地文件句柄失败。');
      console.error(err);
    }
  } 
  // 处理遗留的本地文件路径
  else if (obj.modelPath.startsWith('本地文件:')) {
    showError('无法直接预览，请进入编辑模式重新选择文件。');
  } 
  // 处理常规URL
  else {
    previewModelUrl.value = obj.modelPath;
    previewModelFile.value = undefined;
    showPreviewDialog.value = true;
  }
}

/**
 * 选择本地文件
 */
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
            'application/octet-stream': ['.fbx', '.dae', '.3ds'], // Broader category for others
          }
        }],
        multiple: false
      });

      selectedFileHandle.value = handle;
      const file = await handle.getFile();
      
      const maxSize = 500 * 1024 * 1024;
      if (file.size > maxSize) {
        showError('文件大小超过500MB限制');
        return;
      }

      selectedFile.value = file;
      objectForm.value.modelPath = `本地文件: ${file.name}`;
      showSuccess(`已选择文件: ${file.name}`);

    } catch (err) {
      if ((err as Error).name !== 'AbortError') {
        console.error('File selection error:', err);
        showError('选择文件时发生错误。');
      }
    }
  } else {
    showError('您的浏览器不支持持久化本地文件访问。将使用传统方式选择文件。');
    fileInputRef.value?.click();
  }
}

/**
 * 处理文件选择
 */
const handleFileSelect = async (event: Event) => {
  const target = event.target as HTMLInputElement;
  const file = target.files?.[0];
  if (!file) return;

  // 如果使用遗留回退，清除句柄
  selectedFileHandle.value = null;

  const maxSize = 500 * 1024 * 1024;
  if (file.size > maxSize) {
    showError('文件大小超过500MB限制');
    return;
  }

  selectedFile.value = file;
  objectForm.value.modelPath = `本地文件: ${file.name}`;
  showSuccess(`已选择文件: ${file.name}`);
};

/**
 * 打开URL输入对话框
 */
const openUrlDialog = () => {
  urlInput.value = objectForm.value.modelPath || ''
  showUrlDialog.value = true
}

/**
 * 确认URL输入
 */
const confirmUrl = () => {
  if (!urlInput.value) {
    showError('请输入模型URL')
    return
  }

  // 简单的URL验证
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

/**
 * 清除文件选择
 */
const clearFile = () => {
  selectedFile.value = null
  selectedFileHandle.value = null
  objectForm.value.modelPath = ''
  selectedFileExtension.value = ''

  // 释放blob URL
  if (localPreviewUrl.value) {
    URL.revokeObjectURL(localPreviewUrl.value)
    localPreviewUrl.value = ''
  }

  if (fileInputRef.value) {
    fileInputRef.value.value = ''
  }
}

/**
 * 选择多个文件（批量上传）
 */
const selectMultipleFiles = () => {
  multiFileInputRef.value?.click()
}

/**
 * 处理多文件选择
 */
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

  // 清除单文件选择
  selectedFile.value = null
  selectedFileHandle.value = null

  // 设置批量文件
  selectedFiles.value = files

  // 查找OBJ文件并设置为主模型路径
  const objFile = files.find(f => f.name.toLowerCase().endsWith('.obj'))
  if (objFile) {
    objectForm.value.modelPath = `批量上传: ${objFile.name} + ${files.length - 1} 个相关文件`
    showSuccess(`已选择 ${files.length} 个文件，主模型: ${objFile.name}`)
  } else {
    objectForm.value.modelPath = `批量上传: ${files.length} 个文件`
    showSuccess(`已选择 ${files.length} 个文件`)
  }
}

/**
 * 清空所有选中的文件
 */
const clearAllFiles = () => {
  selectedFiles.value = []
  objectForm.value.modelPath = ''

  if (multiFileInputRef.value) {
    multiFileInputRef.value.value = ''
  }
}

/**
 * 移除指定索引的文件
 */
const removeFile = (index: number) => {
  selectedFiles.value.splice(index, 1)

  if (selectedFiles.value.length === 0) {
    objectForm.value.modelPath = ''
  } else {
    const objFile = selectedFiles.value.find(f => f.name.toLowerCase().endsWith('.obj'))
    if (objFile) {
      objectForm.value.modelPath = `批量上传: ${objFile.name} + ${selectedFiles.value.length - 1} 个相关文件`
    } else {
      objectForm.value.modelPath = `批量上传: ${selectedFiles.value.length} 个文件`
    }
  }
}

/**
 * 根据文件名获取文件图标
 */
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

/**
 * 预览当前选择的模型
 */
const previewCurrentModel = () => {
  // 如果是本地文件，直接传递File对象
  if (selectedFile.value) {
    previewModelFile.value = selectedFile.value
    previewModelUrl.value = ''  // 清除URL
    showPreviewDialog.value = true
  }
  // 否则使用modelPath中的URL
  else if (objectForm.value.modelPath && !objectForm.value.modelPath.startsWith('本地文件:') && !objectForm.value.modelPath.startsWith('blob:')) {
    previewModelUrl.value = objectForm.value.modelPath
    previewModelFile.value = undefined  // 清除File对象
    showPreviewDialog.value = true
  }
  else {
    showError('没有可预览的模型')
  }
}

/**
 * 格式化文件大小
 */
const formatFileSize = (bytes: number): string => {
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB'
  return (bytes / 1024 / 1024).toFixed(1) + ' MB'
}

/**
 * 获取文件扩展名
 */
const getFileExtension = (filename: string): string => {
  return '.' + filename.split('.').pop()?.toUpperCase()
}

// 工具方法
const getTypeIcon = (type: string): string => {
  const iconMap: Record<string, string> = {
    Model3D: '🎨',
    PointCloud: '☁️',
    TileSet: '🧱',
    Marker: '📍'
  }
  return iconMap[type] || '📦'
}

const getSlicingStatusClass = (status: string): string => {
  if (!status) return 'status-none'
  switch (status.toLowerCase()) {
    case 'created':
    case 'queued':
      return 'status-pending';
    case 'processing':
      return 'status-processing';
    case 'completed':
      return 'status-completed';
    case 'failed':
    case 'cancelled':
      return 'status-failed';
    default:
      return '';
  }
};

const getSlicingStatusVariant = (status: string): 'primary' | 'warning' | 'success' | 'danger' | 'gray' => {
  if (!status) return 'gray'
  const variantMap: Record<string, 'primary' | 'warning' | 'success' | 'danger' | 'gray'> = {
    created: 'primary',
    queued: 'primary',
    processing: 'warning',
    completed: 'success',
    failed: 'danger',
    cancelled: 'gray'
  }
  return variantMap[status.toLowerCase()] || 'gray'
};

const getSlicingStatusText = (status: string): string => {
  if (!status) return '未切片'
  switch (status.toLowerCase()) {
    case 'created': return '已创建';
    case 'queued': return '排队中';
    case 'processing': return '处理中';
    case 'completed': return '已完成';
    case 'failed': return '失败';
    case 'cancelled': return '已取消';
    default: return '未知';
  }
};

const getShortPath = (path: string): string => {
  if (!path) return ''
  const parts = path.split('/')
  return parts.length > 3 ? `.../${parts.slice(-2).join('/')}` : path
}

const formatVector = (vec: any): string => {
  if (!vec) return '-'

  // 处理数组格式 [x, y, z]
  if (Array.isArray(vec)) {
    if (vec.length >= 3) {
      return `(${vec[0]?.toFixed(2) || 0}, ${vec[1]?.toFixed(2) || 0}, ${vec[2]?.toFixed(2) || 0})`
    }
    return '-'
  }

  // 处理对象格式 {x, y, z}
  if (typeof vec === 'object') {
    return `(${vec.x?.toFixed(2) || 0}, ${vec.y?.toFixed(2) || 0}, ${vec.z?.toFixed(2) || 0})`
  }

  // 处理JSON字符串格式
  if (typeof vec === 'string') {
    try {
      const parsed = JSON.parse(vec)
      return `(${parsed.x?.toFixed(2) || 0}, ${parsed.y?.toFixed(2) || 0}, ${parsed.z?.toFixed(2) || 0})`
    } catch {
      return '-'
    }
  }

  return '-'
}

const formatDateTime = (dateStr: string): string => {
  if (!dateStr) return '-'
  return new Date(dateStr).toLocaleString('zh-CN')
}

// 菜单操作方法
const toggleMenu = (objId: string): void => {
  activeMenuObjectId.value = activeMenuObjectId.value === objId ? null : objId
}

const closeMenu = (): void => {
  activeMenuObjectId.value = null
}

const handleOutsideClick = (event: MouseEvent): void => {
  const target = event.target as HTMLElement
  const menuElements = document.querySelectorAll('.object-card')
  let clickedInside = false

  menuElements.forEach(el => {
    if (el.contains(target)) {
      clickedInside = true
    }
  })

  if (!clickedInside) {
    closeMenu()
  }
}

// 生命周期钩子
onMounted(async () => {
  console.log('[SceneObjects] 组件已挂载，开始加载场景...')
  await loadScenes()
  document.addEventListener('click', handleOutsideClick)
})

onUnmounted(() => {
  document.removeEventListener('click', handleOutsideClick)
})
</script>

<style scoped>
.scene-objects {
  padding: 2rem;
  background: #f5f5f5;
  min-height: 100vh;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
  background: white;
  padding: 1.5rem;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.header-left h1 {
  margin: 0 0 0.5rem 0;
  font-size: 1.75rem;
  color: #333;
}

.subtitle {
  margin: 0;
  color: #666;
  font-size: 0.9rem;
}

.header-right {
  display: flex;
  gap: 1rem;
}

.scene-selector {
  display: flex;
  align-items: center;
  gap: 1rem;
  margin-bottom: 2rem;
  background: white;
  padding: 1rem 1.5rem;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.scene-selector label {
  font-weight: 500;
  color: #333;
}

.scene-info {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.info-badge {
  padding: 0.25rem 0.75rem;
  background: #e3f2fd;
  color: #1976d2;
  border-radius: 12px;
  font-size: 0.85rem;
  font-weight: 500;
}

.info-badge.engine-threejs {
  background: linear-gradient(135deg, rgba(168, 85, 247, 0.2) 0%, rgba(217, 70, 239, 0.2) 100%);
  color: #7c3aed;
  border: 1px solid rgba(168, 85, 247, 0.3);
}

.info-badge.engine-cesium {
  background: linear-gradient(135deg, rgba(34, 197, 94, 0.2) 0%, rgba(16, 185, 129, 0.2) 100%);
  color: #15803d;
  border: 1px solid rgba(34, 197, 94, 0.3);
}

.info-badge.engine-mars3d {
  background: linear-gradient(135deg, rgba(59, 130, 246, 0.2) 0%, rgba(37, 99, 235, 0.2) 100%);
  color: #1e40af;
  border: 1px solid rgba(59, 130, 246, 0.3);
}

.info-text {
  color: #666;
  font-size: 0.9rem;
}

.objects-section {
  background: white;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
  overflow: hidden;
}

.toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem 1.5rem;
  border-bottom: 1px solid #e1e5e9;
  background: #f8f9fa;
}

.toolbar-left,
.toolbar-right {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.view-mode {
  display: flex;
  gap: 0.5rem;
}

.mode-btn {
  padding: 0.5rem 0.75rem;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  background: white;
  cursor: pointer;
  transition: all 0.2s ease;
}

.mode-btn:hover {
  background: #f8f9fa;
  border-color: #007acc;
}

.mode-btn.active {
  background: #007acc;
  color: white;
  border-color: #007acc;
}

.search-input {
  padding: 0.5rem 1rem;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  width: 250px;
}

.filter-select {
  padding: 0.5rem;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
}

/* 网格视图 */
.objects-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 1.5rem;
  padding: 1.5rem;
}

.object-card {
  border: 1px solid #e1e5e9;
  border-radius: 8px;
  padding: 1rem;
  cursor: pointer;
  transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1);
  background: white;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.08);
}

.object-card:hover {
  border-color: #007acc;
  box-shadow: 0 4px 12px rgba(0, 122, 204, 0.15);
  transform: translateY(-2px);
}

.object-card.selected {
  border-color: #007acc;
  background: linear-gradient(135deg, rgba(0, 122, 204, 0.03) 0%, rgba(0, 122, 204, 0.05) 100%);
  box-shadow: 0 4px 12px rgba(0, 122, 204, 0.2);
}

/* Logo和名称同行布局 */
.object-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.75rem;
}

.object-type-icon {
  font-size: 1.5rem;
  flex-shrink: 0;
}

.object-name {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 1.1rem;
  font-weight: 600;
  color: #333;
}

.object-meta {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  margin-bottom: 0.5rem;
}

.meta-item {
  font-size: 0.85rem;
  color: #666;
  display: flex;
  align-items: center;
  gap: 0.25rem;
}

.meta-label {
  font-weight: 500;
  color: #999;
  flex-shrink: 0;
}

/* 路径项样式 */
.path-item {
  align-items: flex-start;
}

.path-text {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 0.8rem;
  color: #555;
  background: #f8f9fa;
  padding: 0.15rem 0.4rem;
  border-radius: 3px;
  cursor: help;
  transition: all 0.2s ease;
}

.path-text:hover {
  background: #e9ecef;
  color: #333;
}

/* 卡片底部区域 */
.object-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 0.5rem;
  padding-top: 0.5rem;
  border-top: 1px solid #f0f0f0;
  position: relative;
}

/* 创建时间 */
.created-time {
  font-size: 0.75rem;
  color: #999;
  white-space: nowrap;
}

/* 三点菜单按钮 */
.menu-trigger {
  width: 32px;
  height: 32px;
  border: none;
  background: transparent;
  color: #666;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: all 0.2s ease;
}

.menu-trigger:hover {
  background: #f5f5f5;
  color: #333;
}

.menu-trigger:focus {
  outline: 2px solid rgba(0, 122, 204, 0.5);
  outline-offset: 2px;
}

.dots {
  font-size: 1.25rem;
  line-height: 1;
  letter-spacing: 0.05em;
}

.btn-sm:disabled {
  opacity: 0.4;
  cursor: not-allowed;
  background: #f5f5f5;
}

/* 切片状态样式 */
.status-none {
  color: #999;
}

.status-pending {
  color: #007acc;
}

.status-processing {
  color: #ff9800;
}

.status-completed {
  color: #28a745;
}

.status-failed {
  color: #dc3545;
}

/* 列表视图 */
.objects-list {
  padding: 1.5rem;
}

.data-table {
  width: 100%;
  border-collapse: collapse;
}

.data-table th,
.data-table td {
  padding: 0.75rem;
  text-align: left;
  border-bottom: 1px solid #e1e5e9;
}

.data-table th {
  background: #f8f9fa;
  font-weight: 600;
  color: #333;
  font-size: 0.9rem;
}

.data-table tr {
  transition: background 0.2s ease;
  cursor: pointer;
}

.data-table tr:hover {
  background: #f8f9fa;
}

.data-table tr.selected {
  background: #f0f8ff;
}

.object-name {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-weight: 500;
}

.type-icon {
  font-size: 1.2rem;
}

.table-actions {
  display: flex;
  gap: 0.5rem;
}

/* 空状态 */
.empty-state {
  text-align: center;
  padding: 4rem;
  color: #999;
}

.empty-state p {
  margin-bottom: 1.5rem;
  font-size: 1.1rem;
}

/* 按钮样式 */
.btn {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 1rem;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  background: white;
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 0.9rem;
}

.btn:hover {
  background: #f8f9fa;
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

.btn-success {
  background: #28a745;
  color: white;
  border-color: #28a745;
}

.btn-success:hover {
  background: #218838;
}

.btn-sm {
  padding: 0.25rem 0.75rem;
  font-size: 0.85rem;
}

.btn-danger {
  background: #dc3545;
  color: white;
  border-color: #dc3545;
}

.btn-danger:hover {
  background: #c82333;
}

/* 表单样式 */
.form-select,
.form-input,
.form-textarea {
  padding: 0.5rem;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  font-size: 0.9rem;
}

.form-textarea {
  min-height: 80px;
  resize: vertical;
  width: 100%;
}

.form-input[readonly] {
  background-color: #f8f9fa;
  cursor: pointer;
  color: #495057;
}

.form-input[readonly]:hover {
  background-color: #e9ecef;
}

.form-select:focus,
.form-input:focus,
.form-textarea:focus {
  outline: none;
  border-color: #007acc;
}

/* 表单提示样式 */
.form-hint {
  display: block;
  font-size: 0.85rem;
  color: #666;
  margin-top: 0.25rem;
  line-height: 1.4;
}

/* 模态框样式 */
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
  padding: 2rem;
  width: 600px;
  max-width: 90vw;
  max-height: 80vh;
  overflow-y: auto;
}

.modal-content.large {
  width: 800px;
}

.modal-content h3 {
  margin: 0 0 1.5rem 0;
  font-size: 1.25rem;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 1rem;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.form-group.full-width {
  grid-column: 1 / -1;
}

.form-group label {
  font-weight: 500;
  color: #333;
  font-size: 0.9rem;
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

.form-section {
  grid-column: 1 / -1;
  padding: 1rem;
  background: #f8f9fa;
  border-radius: 6px;
}

.form-section h4 {
  margin: 0 0 1rem 0;
  font-size: 1rem;
  color: #333;
}

.transform-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 1rem;
}

.transform-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-size: 0.85rem;
  font-weight: 500;
  color: #666;
}

.vector-input {
  display: flex;
  gap: 0.5rem;
}

.vector-input input {
  flex: 1;
  padding: 0.5rem;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  font-size: 0.85rem;
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 1rem;
  margin-top: 2rem;
}

.icon {
  font-size: 1.1em;
}

/* 模型路径选择器 */
.model-path-selector {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.model-path-selector .form-input {
  flex: 1;
}

.path-actions {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
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
  white-space: nowrap;
  transition: all 0.2s;
}

.btn-action:hover {
  background: #005999;
}

.btn-action.btn-preview {
  background: #28a745;
}

.btn-action.btn-preview:hover {
  background: #218838;
}

.btn-action span {
  font-size: 1rem;
}

/* 文件信息显示 */
.file-info {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem;
  background: #f8f9fa;
  border: 1px solid #e1e5e9;
  border-radius: 6px;
}

.file-icon {
  font-size: 2rem;
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

.file-meta {
  display: flex;
  gap: 1rem;
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
  font-size: 1rem;
  transition: all 0.2s;
}

.btn-clear:hover {
  background: #c82333;
}

/* URL对话框样式 */
.url-dialog {
  padding: 1rem 0;
}

.url-hints {
  margin-top: 1rem;
  padding: 0.75rem;
  background: #f8f9fa;
  border-radius: 4px;
}

.hint-title {
  margin: 0 0 0.5rem 0;
  font-size: 0.85rem;
  font-weight: 600;
  color: #666;
}

.format-tags {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.format-tags .tag {
  padding: 0.25rem 0.75rem;
  background: #007acc;
  color: white;
  border-radius: 12px;
  font-size: 0.8rem;
  font-weight: 500;
}

/* 批量文件上传样式 */
.btn-batch {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.btn-batch:hover {
  background: linear-gradient(135deg, #5a67d8 0%, #6b42ad 100%);
}

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

/* OBJ文件上传提示 */
.upload-hint {
  margin-top: 1rem;
  padding: 1rem;
  border-radius: 6px;
  border-left: 4px solid;
  display: flex;
  gap: 0.75rem;
  animation: fadeIn 0.3s ease;
}

.upload-hint.success {
  background: linear-gradient(135deg, rgba(34, 197, 94, 0.1) 0%, rgba(16, 185, 129, 0.1) 100%);
  border-left-color: #22c55e;
}

.hint-icon {
  font-size: 1.25rem;
  color: #22c55e;
  flex-shrink: 0;
}

.hint-content {
  flex: 1;
}

.hint-content strong {
  display: block;
  margin-bottom: 0.5rem;
  color: #15803d;
  font-size: 0.95rem;
}

.hint-content p {
  margin: 0 0 0.75rem 0;
  color: #166534;
  font-size: 0.85rem;
  line-height: 1.5;
}

.file-check {
  display: flex;
  gap: 1rem;
  flex-wrap: wrap;
}

.check-item {
  padding: 0.4rem 0.75rem;
  background: rgba(255, 255, 255, 0.6);
  border: 1px solid rgba(34, 197, 94, 0.2);
  border-radius: 4px;
  font-size: 0.85rem;
  color: #666;
  font-weight: 500;
  transition: all 0.2s;
}

.check-item.checked {
  background: rgba(34, 197, 94, 0.15);
  border-color: #22c55e;
  color: #15803d;
}

@keyframes fadeIn {
  from {
    opacity: 0;
    transform: translateY(-10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

</style>
