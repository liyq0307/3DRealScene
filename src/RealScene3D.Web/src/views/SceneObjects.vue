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
            </div>
            <div class="object-transform">
              <span class="transform-item" title="位置">
                📍 ({{ obj.position.x.toFixed(1) }}, {{ obj.position.y.toFixed(1) }}, {{ obj.position.z.toFixed(1) }})
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
              <td>{{ formatDateTime(obj.createdAt) }}</td>
              <td>
                <div class="table-actions" @click.stop>
                  <button @click="editObject(obj)" class="btn-sm">编辑</button>
                  <button @click="duplicateObject(obj)" class="btn-sm">复制</button>
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
            <input
              v-model="objectForm.modelPath"
              type="text"
              class="form-input"
              placeholder="输入模型文件路径或URL"
            />
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
import { sceneService, sceneObjectService } from '@/services/api'
import { useMessage } from '@/composables/useMessage'
import Modal from '@/components/Modal.vue'
import ModelViewer from '@/components/ModelViewer.vue'

const { success: showSuccess, error: showError } = useMessage()

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
const editingObject = ref<any>(null)
const previewModelUrl = ref('')

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
  showCreateDialog.value = true
}

const closeCreateDialog = () => {
  showCreateDialog.value = false
}

const editObject = (obj: any) => {
  editingObject.value = obj
  objectForm.value = {
    name: obj.name,
    objectType: obj.objectType,
    modelPath: obj.modelPath || '',
    position: { ...obj.position },
    rotation: { ...obj.rotation },
    scale: { ...obj.scale },
    isVisible: obj.isVisible
  }
  showCreateDialog.value = true
}

const saveObject = async () => {
  try {
    if (!objectForm.value.name) {
      showError('请输入对象名称')
      return
    }

    const data = {
      ...objectForm.value,
      sceneId: selectedSceneId.value
    }

    if (editingObject.value) {
      // TODO: 实现更新对象API
      showError('更新对象功能待实现')
    } else {
      await sceneObjectService.createObject(data)
      showSuccess('对象创建成功')
      await loadObjects()
      closeCreateDialog()
    }
  } catch (error) {
    console.error('保存对象失败:', error)
    showError('保存对象失败')
  }
}

const duplicateObject = async (obj: any) => {
  try {
    const data = {
      ...obj,
      name: `${obj.name} (副本)`,
      position: {
        x: obj.position.x + 5,
        y: obj.position.y,
        z: obj.position.z
      },
      sceneId: selectedSceneId.value
    }
    delete data.id
    delete data.createdAt
    delete data.updatedAt

    await sceneObjectService.createObject(data)
    showSuccess('对象复制成功')
    await loadObjects()
  } catch (error) {
    console.error('复制对象失败:', error)
    showError('复制对象失败')
  }
}

const deleteObject = async (id: string) => {
  if (confirm('确定要删除此对象吗?')) {
    try {
      await sceneObjectService.deleteObject(id)
      showSuccess('对象删除成功')
      await loadObjects()
      if (selectedObject.value?.id === id) {
        selectedObject.value = null
      }
    } catch (error) {
      console.error('删除对象失败:', error)
      showError('删除对象失败')
    }
  }
}

// 预览3D模型
const previewModel = (obj: any) => {
  if (obj.modelPath) {
    previewModelUrl.value = obj.modelPath
    showPreviewDialog.value = true
  } else {
    showError('该对象没有关联的模型文件')
  }
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

const getShortPath = (path: string): string => {
  if (!path) return ''
  const parts = path.split('/')
  return parts.length > 3 ? `.../${parts.slice(-2).join('/')}` : path
}

const formatVector = (vec: any): string => {
  if (!vec) return '-'
  return `(${vec.x?.toFixed(2) || 0}, ${vec.y?.toFixed(2) || 0}, ${vec.z?.toFixed(2) || 0})`
}

const formatDateTime = (dateStr: string): string => {
  if (!dateStr) return '-'
  return new Date(dateStr).toLocaleString('zh-CN')
}

// 生命周期
onMounted(async () => {
  console.log('[SceneObjects] Component mounted, loading scenes...')
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
</style>
