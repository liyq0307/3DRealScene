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
        <button @click="openCreateDialog" class="btn btn-success">
          <span class="icon">➕</span>
          添加对象
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
          <div class="object-thumbnail">
            <span class="object-type-icon">{{ getTypeIcon(obj.objectType) }}</span>
          </div>
          <div class="object-info">
            <h4>{{ obj.name }}</h4>
            <div class="object-meta">
              <span class="meta-item">
                <span class="meta-label">类型:</span>
                {{ obj.objectType }}
              </span>
              <span class="meta-item" v-if="obj.modelPath">
                <span class="meta-label">路径:</span>
                {{ getShortPath(obj.modelPath) }}
              </span>
              <span class="meta-item" v-if="obj.slicingTaskStatus">
                <span class="meta-label">切片状态:</span>
                <span :class="getSlicingStatusClass(obj.slicingTaskStatus)">{{ getSlicingStatusText(obj.slicingTaskStatus) }}</span>
              </span>
            </div>
            <div class="object-transform">
              <span class="transform-item" title="位置">
                📍 {{ formatVector(obj.position) }}
              </span>
            </div>
          </div>
          <div class="object-actions" @click.stop>
            <button @click="editObject(obj)" class="btn-icon" title="编辑">
              <span>✏️</span>
            </button>
            <button @click="duplicateObject(obj)" class="btn-icon" title="复制">
              <span>📋</span>
            </button>
            <button @click="startSlicing(obj)" class="btn-icon" title="切片">
              <span>🔪</span>
            </button>
            <button @click="deleteObject(obj.id)" class="btn-icon danger" title="删除">
              <span>🗑️</span>
            </button>
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
                <span v-if="obj.slicingTaskStatus" :class="getSlicingStatusClass(obj.slicingTaskStatus)">
                  {{ getSlicingStatusText(obj.slicingTaskStatus) }}
                </span>
                <span v-else>-</span>
              </td>
              <td>{{ formatDateTime(obj.createdAt) }}</td>
              <td>
                <div class="table-actions" @click.stop>
                  <button @click="editObject(obj)" class="btn-sm">编辑</button>
                  <button @click="duplicateObject(obj)" class="btn-sm">复制</button>
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
        <p>{{ searchKeyword || filterType ? '没有符合条件的对象' : '此场景暂无对象' }}</p>
        <button @click="openCreateDialog" class="btn btn-primary">
          添加第一个对象
        </button>
      </div>
    </div>

    <!-- 未选择场景提示 -->
    <div v-else class="empty-state">
      <p>请先选择一个场景</p>
    </div>

    <!-- 创建/编辑对象对话框 -->
    <div v-if="showCreateDialog" class="modal-overlay" @click="closeCreateDialog">
      <div class="modal-content large" @click.stop>
        <h3>{{ editingObject ? '编辑对象' : '添加对象' }}</h3>
        <div class="form-grid">
          <div class="form-group">
            <label>对象名称 *</label>
            <input
              v-model="objectForm.name"
              type="text"
              class="form-input"
              placeholder="输入对象名称"
            />
          </div>

          <div class="form-group">
            <label>对象类型 *</label>
            <select v-model="objectForm.objectType" class="form-select">
              <option value="Model3D">3D模型</option>
              <option value="PointCloud">点云</option>
              <option value="TileSet">瓦片集</option>
              <option value="Marker">标记</option>
            </select>
          </div>

          <div class="form-group full-width">
            <label>模型路径</label>
            <div class="model-path-selector">
              <!-- 路径输入（只读显示） -->
              <input
                v-model="objectForm.modelPath"
                type="text"
                class="form-input"
                placeholder="请选择本地文件或输入远程URL"
                readonly
                @click="objectForm.modelPath ? null : selectLocalFile()"
                :title="localPreviewUrl ? localPreviewUrl : objectForm.modelPath"
              />

              <!-- 操作按钮组 -->
              <div class="path-actions">
                <button @click="selectLocalFile" class="btn-action" type="button" title="从本地选择文件">
                  <span>📁</span>
                  本地文件
                </button>
                <button @click="openUrlDialog" class="btn-action" type="button" title="输入远程URL">
                  <span>🌐</span>
                  远程URL
                </button>
                <button
                  v-if="objectForm.modelPath"
                  @click="previewCurrentModel"
                  class="btn-action btn-preview"
                  type="button"
                  title="预览当前模型"
                >
                  <span>👁️</span>
                  预览
                </button>
              </div>
            </div>

            <!-- 文件选择器（隐藏） -->
            <input
              ref="fileInputRef"
              type="file"
              accept=".gltf,.glb,.obj,.fbx,.dae,.3ds"
              @change="handleFileSelect"
              style="display: none"
            />

            <!-- 已选择文件信息 -->
            <div v-if="selectedFile" class="file-info">
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
          </div>

          <div class="form-section full-width">
            <h4>变换属性</h4>
            <div class="transform-grid">
              <!-- 位置 -->
              <div class="transform-group">
                <label>位置 (X, Y, Z)</label>
                <div class="vector-input">
                  <input
                    v-model.number="objectForm.position.x"
                    type="number"
                    step="0.1"
                    placeholder="X"
                  />
                  <input
                    v-model.number="objectForm.position.y"
                    type="number"
                    step="0.1"
                    placeholder="Y"
                  />
                  <input
                    v-model.number="objectForm.position.z"
                    type="number"
                    step="0.1"
                    placeholder="Z"
                  />
                </div>
              </div>

              <!-- 旋转 -->
              <div class="transform-group">
                <label>旋转 (X, Y, Z) 度</label>
                <div class="vector-input">
                  <input
                    v-model.number="objectForm.rotation.x"
                    type="number"
                    step="1"
                    placeholder="X"
                  />
                  <input
                    v-model.number="objectForm.rotation.y"
                    type="number"
                    step="1"
                    placeholder="Y"
                  />
                  <input
                    v-model.number="objectForm.rotation.z"
                    type="number"
                    step="1"
                    placeholder="Z"
                  />
                </div>
              </div>

              <!-- 缩放 -->
              <div class="transform-group">
                <label>缩放 (X, Y, Z)</label>
                <div class="vector-input">
                  <input
                    v-model.number="objectForm.scale.x"
                    type="number"
                    step="0.1"
                    min="0.01"
                    placeholder="X"
                  />
                  <input
                    v-model.number="objectForm.scale.y"
                    type="number"
                    step="0.1"
                    min="0.01"
                    placeholder="Y"
                  />
                  <input
                    v-model.number="objectForm.scale.z"
                    type="number"
                    step="0.1"
                    min="0.01"
                    placeholder="Z"
                  />
                </div>
              </div>
            </div>
          </div>

          <div class="form-group full-width">
            <label class="checkbox-label">
              <input v-model="objectForm.isVisible" type="checkbox" />
              <span>可见</span>
            </label>
          </div>
        </div>

        <div class="modal-actions">
          <button @click="closeCreateDialog" class="btn btn-secondary">
            取消
          </button>
          <button @click="saveObject" class="btn btn-primary">
            保存
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
          :show-controls="true"
          :show-info="true"
          :auto-rotate="false"
        />
      </div>
    </Modal>

    <!-- 切片配置对话框 -->
    <Modal
      v-model="showSlicingDialog"
      title="配置切片任务"
      size="md"
    >
      <div class="slicing-dialog">
        <div class="form-group">
          <label>任务名称 *</label>
          <input v-model="slicingForm.name" type="text" class="form-input" />
        </div>
        <div class="form-group">
          <label>模型类型</label>
          <select v-model="slicingForm.modelType" class="form-select">
            <option value="Model3D">3D模型</option>
            <option value="PointCloud">点云</option>
            <!-- 其他类型 -->
          </select>
        </div>
        <div class="form-group">
          <label>切片策略</label>
          <select v-model="slicingForm.slicingStrategy" class="form-select">
            <option value="Octree">八叉树</option>
            <option value="Grid">网格</option>
            <option value="KdTree">KD树</option>
            <option value="Adaptive">自适应</option>
          </select>
        </div>
        <div class="form-group">
          <label>最大LOD级别 (建议≤8，过高会导致内存溢出)</label>
          <input v-model.number="slicingForm.maxLevel" type="number" class="form-input" min="0" max="10" />
          <small class="form-hint" v-if="slicingForm.maxLevel > 8" style="color: orange;">
            ⚠️ 级别{slicingForm.maxLevel}}将生成约 {{ estimateSliceCount(slicingForm.maxLevel) }} 个切片，可能导致内存不足
          </small>
        </div>
        <div class="form-group">
          <label>切片尺寸 (米)</label>
          <input v-model.number="slicingForm.tileSize" type="number" class="form-input" min="1" />
        </div>
        <div class="form-group">
          <label>几何误差阈值</label>
          <input v-model.number="slicingForm.geometricErrorThreshold" type="number" class="form-input" min="0.1" step="0.1" />
        </div>
        <div class="form-group">
          <label>并行处理数量</label>
          <input v-model.number="slicingForm.parallelProcessingCount" type="number" class="form-input" min="1" />
        </div>
        <div class="form-group">
          <label>输出格式</label>
          <select v-model="slicingForm.outputFormat" class="form-select">
            <option value="b3dm">B3DM</option>
            <option value="gltf">GLTF</option>
            <option value="glb">GLB</option>
            <option value="json">JSON</option>
          </select>
        </div>
        <div class="form-group">
          <label>压缩级别</label>
          <input v-model.number="slicingForm.compressionLevel" type="number" class="form-input" min="0" max="9" />
        </div>
        <div class="form-group">
          <label>存储位置</label>
          <select v-model="slicingForm.storageLocation" class="form-select">
            <option value="MinIO">MinIO</option>
            <option value="LocalFileSystem">本地文件系统</option>
          </select>
        </div>
        <div class="form-group">
          <label class="checkbox-label">
            <input v-model="slicingForm.enableIncrementalUpdates" type="checkbox" />
            <span>启用增量更新</span>
          </label>
        </div>
      </div>
      <template #footer>
        <button @click="closeSlicingDialog" class="btn btn-secondary">取消</button>
        <button @click="submitSlicingTask" class="btn btn-primary">开始切片</button>
      </template>
    </Modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { sceneService, sceneObjectService, fileService, slicingService } from '@/services/api'
import { useMessage } from '@/composables/useMessage'
import Modal from '@/components/Modal.vue'
import ModelViewer from '@/components/ModelViewer.vue'
import { FileHandleStore } from '@/services/fileHandleStore'
import authStore from '@/stores/auth'

const { success: showSuccess, error: showError } = useMessage()

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
const showCreateDialog = ref(false)
const showPreviewDialog = ref(false)
const showUrlDialog = ref(false)  // URL输入对话框
const showSlicingDialog = ref(false) // 新增：切片对话框
const editingObject = ref<any>(null)
const previewModelUrl = ref('')
const previewModelFile = ref<File | undefined>(undefined)  // 用于预览的File对象
const objectToSlice = ref<any>(null) // 新增：待切片的对象

// 切片表单数据
const slicingForm = ref({
  name: '',
  modelType: 'Model3D',
  slicingStrategy: 'Octree',
  maxLevel: 10,
  tileSize: 100,
  geometricErrorThreshold: 1,
  parallelProcessingCount: 4,
  outputFormat: 'b3dm',
  compressionLevel: 6,
  enableIncrementalUpdates: false,
  storageLocation: 'MinIO'
})

// 文件选择相关
const fileInputRef = ref<HTMLInputElement>()
const selectedFile = ref<File | null>(null)
const selectedFileHandle = ref<any | null>(null)
const urlInput = ref('')
const localPreviewUrl = ref('')  // 存储本地文件的blob URL用于预览
const selectedFileExtension = ref('')  // 存储文件扩展名
//const realFilePath = ref('')  // 存储文件的真实路径

// 表单数据
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

const openCreateDialog = () => {
  if (!selectedSceneId.value) {
    showError('请先选择一个场景再添加对象')
    return
  }
  editingObject.value = null
  objectForm.value = {
    name: '',
    objectType: 'Model3D',
    modelPath: '',
    position: { x: 0, y: 0, z: 0 },
    rotation: { x: 0, y: 0, z: 0 },
    scale: { x: 1, y: 1, z: 1 },
    isVisible: true
  }
  selectedFile.value = null
  selectedFileHandle.value = null
  selectedFileExtension.value = ''
  // 释放之前的blob URL
  if (localPreviewUrl.value) {
    URL.revokeObjectURL(localPreviewUrl.value)
    localPreviewUrl.value = ''
  }
  showCreateDialog.value = true
}

const closeCreateDialog = () => {
  showCreateDialog.value = false
  selectedFile.value = null
  selectedFileHandle.value = null
  selectedFileExtension.value = ''
  // 释放blob URL
  if (localPreviewUrl.value) {
    URL.revokeObjectURL(localPreviewUrl.value)
    localPreviewUrl.value = ''
  }
}

const editObject = async (obj: any) => {
  editingObject.value = obj;
  objectForm.value = {
    name: obj.name,
    objectType: obj.objectType || obj.type,  // 兼容不同字段名
    modelPath: obj.modelPath || '',
    position: obj.position ? (Array.isArray(obj.position)
      ? { x: obj.position[0] || 0, y: obj.position[1] || 0, z: obj.position[2] || 0 }
      : { ...obj.position })
      : { x: 0, y: 0, z: 0 },
    rotation: typeof obj.rotation === 'string'
      ? JSON.parse(obj.rotation || '{"x":0,"y":0,"z":0}')
      : { ...obj.rotation },
    scale: typeof obj.scale === 'string'
      ? JSON.parse(obj.scale || '{"x":1,"y":1,"z":1}')
      : { ...obj.scale },
    isVisible: obj.isVisible ?? true
  };

  // 清除之前的选择
  selectedFile.value = null;
  selectedFileHandle.value = null;

  // 如果是本地文件句柄，尝试检索
  if (obj.modelPath && obj.modelPath.startsWith('local-file-handle://')) {
    try {
      const uuid = obj.modelPath.replace('local-file-handle://', '');
      const handle = await fileHandleStore.getHandle<any>(uuid);
      if (handle) {
        // 尝试验证权限，如果失败则请求权限
        let permission = await handle.queryPermission({ mode: 'read' });
        if (permission !== 'granted') {
          // 尝试请求权限
          permission = await handle.requestPermission({ mode: 'read' });
        }

        if (permission === 'granted') {
          selectedFileHandle.value = handle;
          const file = await handle.getFile();
          selectedFile.value = file;
          objectForm.value.modelPath = `本地文件: ${file.name} (已授权)`;
          showSuccess('已加载本地文件访问权限。');
        } else {
          showError('无法获取文件权限，请重新选择文件。');
          objectForm.value.modelPath = `本地文件: ${handle.name} (需要授权)`;
        }
      } else {
        showError('在本地找不到对应的文件句柄，请重新选择文件。');
      }
    } catch (err) {
      console.error('检索文件句柄失败:', err);
      showError('加载本地文件句柄失败，请重新选择文件。');
    }
  }

  showCreateDialog.value = true;
}

const saveObject = async () => {
  try {
    if (!objectForm.value.name) {
      showError('请输入对象名称')
      return
    }

    if (!editingObject.value && !selectedSceneId.value) {
      showError('请先选择一个场景')
      return
    }

    let finalModelPath = objectForm.value.modelPath

    // 如果选择了新的本地文件，询问用户是上传还是直接使用本地路径
    if (selectedFile.value && objectForm.value.modelPath.startsWith('本地文件:')) {
      const shouldUpload = confirm(
        '您选择了本地文件。\n\n' +
        '点击"确定"将文件上传到服务器(推荐)。\n' +
        '点击"取消"在本地保存文件访问权限(仅限本机、部分浏览器支持)。'
      );

      if (shouldUpload) {
        // 用户选择上传
        try {
          showSuccess('正在上传文件...')
          const uploadResult = await fileService.uploadFile(
            selectedFile.value,
            'models',  // 使用models存储桶
            (percent) => {
              console.log(`上传进度: ${percent}%`)
            }
          )
          // 使用downloadUrl而不是filePath,因为downloadUrl是可访问的完整URL
          finalModelPath = uploadResult.downloadUrl || uploadResult.filePath
          showSuccess('文件上传成功')
        } catch (uploadError) {
          console.error('文件上传失败:', uploadError)
          showError('文件上传失败,请稍后重试')
          return
        }
      } else {
        // 用户选择在本地保存句柄
        if (selectedFileHandle.value) {
          try {
            const uuid = editingObject.value?.modelPath?.startsWith('local-file-handle://')
              ? editingObject.value.modelPath.replace('local-file-handle://', '')
              : generateUUID();
            await fileHandleStore.saveHandle(uuid, selectedFileHandle.value);
            finalModelPath = `local-file-handle://${uuid}`;
            showSuccess('已在本地保存文件访问权限。');
          } catch (handleError) {
            console.error('保存文件句柄失败:', handleError);
            showError('保存本地文件句柄失败，将仅保存文件名。');
            finalModelPath = objectForm.value.modelPath;
          }
        } else {
          // 对于不支持文件系统访问API的浏览器，回退处理
          showError('您的浏览器不支持保存本地文件访问权限，仅保存文件名。');
          finalModelPath = objectForm.value.modelPath;
        }
      }
    } else if (editingObject.value && objectForm.value.modelPath.startsWith('local-file-handle://')) {
      // 编辑模式且路径未改变，保持原有路径
      finalModelPath = objectForm.value.modelPath.replace(' (已授权)', '').replace(' (需要授权)', '');
    }

    // 转换数据格式以匹配后端DTO
    const data: any = {
      name: objectForm.value.name,
      type: objectForm.value.objectType,  // 后端期望 Type
      position: [  // 后端期望数组格式 double[]
        objectForm.value.position.x,
        objectForm.value.position.y,
        objectForm.value.position.z
      ],
      rotation: JSON.stringify(objectForm.value.rotation),  // 后端期望JSON字符串
      scale: JSON.stringify(objectForm.value.scale),        // 后端期望JSON字符串
      modelPath: finalModelPath || '',
      materialData: '{}',  // 默认空材质数据
      properties: '{}',    // 默认空属性数据
    }

    // 如果是创建操作，添加sceneId
    if (!editingObject.value) {
      data.sceneId = selectedSceneId.value;
    }

    // 调试日志
    console.log('=== 保存场景对象数据 ===')
    console.log('操作类型:', editingObject.value ? '更新' : '创建')
    console.log('发送数据:', JSON.stringify(data, null, 2))

    if (editingObject.value) {
      // 更新对象
      await sceneObjectService.updateObject(editingObject.value.id, data)
      showSuccess('对象更新成功')
    } else {
      // 创建对象
      await sceneObjectService.createObject(data)
      showSuccess('对象创建成功')
    }

    await loadObjects()
    closeCreateDialog()
  } catch (error) {
    console.error('保存对象失败:', error)
    showError('保存对象失败')
  }
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

// 切片操作方法
const startSlicing = (obj: any) => {
  if (!obj.modelPath) {
    showError('该对象没有关联的模型文件，无法切片。');
    return;
  }
  objectToSlice.value = obj;
  slicingForm.value.name = `切片任务 - ${obj.name}`;
  slicingForm.value.modelType = obj.objectType || obj.type; // 继承对象类型
  openSlicingDialog();
};

const openSlicingDialog = () => {
  showSlicingDialog.value = true;
};

const closeSlicingDialog = () => {
  showSlicingDialog.value = false;
  objectToSlice.value = null;
};

// 估算切片数量
const estimateSliceCount = (level: number): string => {
  const tilesInLevel = Math.pow(2, level);
  const zTiles = level === 0 ? 1 : tilesInLevel / 2;
  const count = tilesInLevel * tilesInLevel * zTiles;

  if (count >= 1000000) {
    return `${(count / 1000000).toFixed(1)}百万`;
  } else if (count >= 1000) {
    return `${(count / 1000).toFixed(1)}千`;
  }
  return count.toString();
};

const submitSlicingTask = async () => {
  if (!objectToSlice.value) {
    showError('没有选择要切片的对象。');
    return;
  }

  if (!slicingForm.value.name) {
    showError('请输入切片任务名称。');
    return;
  }

  // 验证最大LOD级别，防止内存溢出
  if (slicingForm.value.maxLevel > 10) {
    showError('最大LOD级别不能超过10，以防止内存溢出。请降低级别或增大切片尺寸。');
    return;
  }

  try {
    // 获取当前用户ID
    const userId = authStore.currentUser.value?.id || '9055f06c-20d2-4e67-8a89-069887a2c4e8';

    const requestData = {
      name: slicingForm.value.name,
      sourceModelPath: objectToSlice.value.modelPath,
      modelType: slicingForm.value.modelType,
      sceneObjectId: objectToSlice.value.id, // 关联场景对象ID
      slicingConfig: {
        strategy: slicingForm.value.slicingStrategy,
        maxLevel: slicingForm.value.maxLevel,
        tileSize: slicingForm.value.tileSize,
        geometricErrorThreshold: slicingForm.value.geometricErrorThreshold,
        parallelProcessingCount: slicingForm.value.parallelProcessingCount,
        outputFormat: slicingForm.value.outputFormat,
        compressionLevel: slicingForm.value.compressionLevel,
        enableIncrementalUpdates: slicingForm.value.enableIncrementalUpdates,
        storageLocation: slicingForm.value.storageLocation
      }
    };

    await slicingService.createSlicingTask(requestData, userId);
    showSuccess('切片任务已成功创建！');
    closeSlicingDialog();
    await loadObjects(); // 刷新对象列表以显示切片状态
  } catch (error) {
    console.error('创建切片任务失败:', error);
    showError('创建切片任务失败，请稍后重试。');
  }
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
  switch (status?.toLowerCase()) {
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

const getSlicingStatusText = (status: string): string => {
  switch (status?.toLowerCase()) {
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

// 生命周期钩子
onMounted(async () => {
  console.log('[SceneObjects] 组件已挂载，开始加载场景...')
  await loadScenes()
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
  border: 2px solid #e1e5e9;
  border-radius: 8px;
  padding: 1rem;
  cursor: pointer;
  transition: all 0.2s ease;
  background: white;
}

.object-card:hover {
  border-color: #007acc;
  box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
}

.object-card.selected {
  border-color: #007acc;
  background: #f0f8ff;
}

.object-thumbnail {
  height: 80px;
  background: #f8f9fa;
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 1rem;
}

.object-type-icon {
  font-size: 2.5rem;
}

.object-info h4 {
  margin: 0 0 0.5rem 0;
  font-size: 1.1rem;
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
}

.meta-label {
  font-weight: 500;
  color: #999;
}

.object-transform {
  font-size: 0.8rem;
  color: #999;
  margin-top: 0.5rem;
  padding-top: 0.5rem;
  border-top: 1px solid #f0f0f0;
}

.object-actions {
  display: flex;
  gap: 0.5rem;
  margin-top: 1rem;
  padding-top: 1rem;
  border-top: 1px solid #e1e5e9;
}

.btn-icon {
  flex: 1;
  padding: 0.5rem;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  background: white;
  cursor: pointer;
  transition: all 0.2s ease;
}

.btn-icon:hover {
  background: #f8f9fa;
  border-color: #007acc;
}

.btn-icon.danger:hover {
  background: #ffebee;
  border-color: #dc3545;
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
.form-input {
  padding: 0.5rem;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  font-size: 0.9rem;
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
.form-input:focus {
  outline: none;
  border-color: #007acc;
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

/* 切片状态样式 */
.status-pending {
  color: #ffc107;
  font-weight: 600;
}

.status-processing {
  color: #17a2b8;
  font-weight: 600;
}

.status-completed {
  color: #28a745;
  font-weight: 600;
}

.status-failed,
.status-cancelled {
  color: #dc3545;
  font-weight: 600;
}
</style>
