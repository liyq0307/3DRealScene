<template>
  <div class="bim-metadata-page">
    <!-- 页面头部 -->
    <div class="page-header">
      <div class="header-content">
        <h1 class="page-title">🏗️ BIM模型元数据管理</h1>
        <p class="page-description">管理建筑信息模型(BIM)元数据，支持多学科协同</p>
      </div>
      <button @click="showCreateDialog = true" class="btn btn-primary">
        ➕ 添加BIM模型
      </button>
    </div>

    <!-- 搜索和筛选区域 -->
    <div class="filters-section">
      <div class="filter-grid">
        <select v-model="selectedSceneFilter" @change="handleFilterChange" class="filter-select">
          <option value="">全部场景</option>
          <option v-for="scene in scenes" :key="scene.id" :value="scene.id">
            {{ scene.name }}
          </option>
        </select>

        <select v-model="selectedDiscipline" @change="handleFilterChange" class="filter-select">
          <option value="">全部学科</option>
          <option value="建筑">🏛️ 建筑</option>
          <option value="结构">🏗️ 结构</option>
          <option value="机电">⚡ 机电</option>
          <option value="给排水">💧 给排水</option>
          <option value="暖通">🌡️ 暖通</option>
          <option value="景观">🌳 景观</option>
        </select>

        <select v-model="selectedFormat" @change="handleFilterChange" class="filter-select">
          <option value="">全部格式</option>
          <option value="IFC">IFC</option>
          <option value="RVT">RVT (Revit)</option>
          <option value="RFA">RFA (Revit Family)</option>
          <option value="DWG">DWG</option>
          <option value="NWD">NWD (Navisworks)</option>
        </select>
      </div>
    </div>

    <!-- 统计信息 -->
    <div class="stats-section">
      <div class="stat-card">
        <div class="stat-icon">📊</div>
        <div class="stat-content">
          <div class="stat-label">总模型数</div>
          <div class="stat-value">{{ models.length }}</div>
        </div>
      </div>
      <div class="stat-card" v-if="selectedModel">
        <div class="stat-icon">🧱</div>
        <div class="stat-content">
          <div class="stat-label">当前模型构件</div>
          <div class="stat-value">{{ elementStats?.totalElements || 0 }}</div>
        </div>
      </div>
    </div>

    <!-- 加载状态 -->
    <div v-if="loading" class="loading-container">
      <LoadingSpinner :loading="loading" text="加载BIM模型数据..." />
      <p>加载中...</p>
    </div>

    <!-- 错误提示 -->
    <ErrorDisplay v-if="error" :message="error" :onRetry="loadModels" />

    <!-- BIM模型列表 -->
    <div v-if="!loading && !error" class="models-grid">
      <div
        v-for="model in models"
        :key="model.id"
        class="model-card"
        :class="{ 'selected': selectedModel?.id === model.id }"
        @click="selectModel(model)"
      >
        <div class="model-card-header">
          <div class="discipline-badge" :class="`discipline-${getDisciplineClass(model.discipline)}`">
            {{ getDisciplineIcon(model.discipline) }}
          </div>
          <div class="model-info">
            <h3 class="model-title">{{ model.modelName }}</h3>
            <p class="model-meta">{{ model.discipline }} · {{ model.format }}</p>
          </div>
        </div>

        <div class="model-card-body">
          <div class="model-detail">
            <span class="detail-label">版本:</span>
            <span class="detail-value">{{ model.version || 'N/A' }}</span>
          </div>
          <div class="model-detail">
            <span class="detail-label">作者:</span>
            <span class="detail-value">{{ model.author || 'N/A' }}</span>
          </div>
          <div class="model-detail">
            <span class="detail-label">创建时间:</span>
            <span class="detail-value">{{ formatDate(model.createdDate) }}</span>
          </div>
          <div v-if="model.fileSize" class="model-detail">
            <span class="detail-label">文件大小:</span>
            <span class="detail-value">{{ formatFileSize(model.fileSize) }}</span>
          </div>
        </div>

        <div class="model-card-footer">
          <button @click.stop="viewModelStats(model)" class="btn-action btn-stats">统计</button>
          <button @click.stop="editModel(model)" class="btn-action btn-edit">编辑</button>
          <button @click.stop="deleteModel(model)" class="btn-action btn-delete">删除</button>
        </div>
      </div>

      <!-- 空状态 -->
      <div v-if="models.length === 0" class="empty-state">
        <div class="empty-icon">🏗️</div>
        <h3>暂无BIM模型数据</h3>
        <p>点击右上角"添加BIM模型"按钮创建第一个BIM模型元数据</p>
      </div>
    </div>

    <!-- 构件统计面板 -->
    <div v-if="showStatsPanel && elementStats" class="stats-panel">
      <div class="stats-panel-header">
        <h3>📊 构件统计信息</h3>
        <button @click="showStatsPanel = false" class="btn-close">×</button>
      </div>
      <div class="stats-panel-body">
        <div class="stats-grid">
          <div class="stats-item">
            <div class="stats-label">总构件数</div>
            <div class="stats-number">{{ elementStats.totalElements }}</div>
          </div>
          <div v-for="(count, type) in elementStats.elementsByType" :key="type" class="stats-item">
            <div class="stats-label">{{ type }}</div>
            <div class="stats-number">{{ count }}</div>
          </div>
        </div>
      </div>
    </div>

    <!-- 创建/编辑对话框 -->
    <Modal :model-value="showCreateDialog || showEditDialog" @close="closeDialog">
      <template #header>
        <h2>{{ editingModel ? '编辑BIM模型' : '添加BIM模型' }}</h2>
      </template>
      <template #body>
        <form @submit.prevent="submitForm" class="model-form">
          <div class="form-group">
            <label>模型名称 *</label>
            <input
              v-model="formData.modelName"
              type="text"
              required
              placeholder="请输入模型名称"
              class="form-input"
            />
          </div>

          <div class="form-row">
            <div class="form-group">
              <label>学科类型 *</label>
              <select v-model="formData.discipline" required class="form-input">
                <option value="">请选择</option>
                <option value="建筑">建筑</option>
                <option value="结构">结构</option>
                <option value="机电">机电</option>
                <option value="给排水">给排水</option>
                <option value="暖通">暖通</option>
                <option value="景观">景观</option>
              </select>
            </div>

            <div class="form-group">
              <label>文件格式 *</label>
              <select v-model="formData.format" required class="form-input">
                <option value="">请选择</option>
                <option value="IFC">IFC</option>
                <option value="RVT">RVT (Revit)</option>
                <option value="RFA">RFA (Revit Family)</option>
                <option value="DWG">DWG</option>
                <option value="NWD">NWD (Navisworks)</option>
              </select>
            </div>
          </div>

          <div class="form-group">
            <label>文件路径 *</label>
            <input
              v-model="formData.filePath"
              type="text"
              required
              placeholder="请输入文件路径"
              class="form-input"
            />
          </div>

          <div class="form-row">
            <div class="form-group">
              <label>版本号</label>
              <input
                v-model="formData.version"
                type="text"
                placeholder="例如: 1.0.0"
                class="form-input"
              />
            </div>

            <div class="form-group">
              <label>作者</label>
              <input
                v-model="formData.author"
                type="text"
                placeholder="模型创建者"
                class="form-input"
              />
            </div>
          </div>

          <div class="form-group">
            <label>关联场景</label>
            <select v-model="formData.sceneId" class="form-input">
              <option value="">无关联</option>
              <option v-for="scene in scenes" :key="scene.id" :value="scene.id">
                {{ scene.name }}
              </option>
            </select>
          </div>

          <div class="form-group">
            <label>描述</label>
            <textarea
              v-model="formData.description"
              rows="3"
              placeholder="模型描述信息"
              class="form-input"
            ></textarea>
          </div>

          <div class="form-actions">
            <button type="button" @click="closeDialog" class="btn btn-secondary">
              取消
            </button>
            <button type="submit" class="btn btn-primary" :disabled="submitting">
              {{ submitting ? '提交中...' : '提交' }}
            </button>
          </div>
        </form>
      </template>
    </Modal>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { bimModelMetadataService, sceneService } from '@/services/api'
import { useMessage } from '@/composables/useMessage'
import LoadingSpinner from '@/components/LoadingSpinner.vue'
import ErrorDisplay from '@/components/ErrorDisplay.vue'
import Modal from '@/components/Modal.vue'

const { success, error: showError } = useMessage()

// 状态
const loading = ref(false)
const error = ref('')
const models = ref<any[]>([])
const scenes = ref<any[]>([])
const selectedSceneFilter = ref('')
const selectedDiscipline = ref('')
const selectedFormat = ref('')
const selectedModel = ref<any>(null)
const elementStats = ref<any>(null)
const showStatsPanel = ref(false)

// 对话框状态
const showCreateDialog = ref(false)
const showEditDialog = ref(false)
const editingModel = ref<any>(null)
const submitting = ref(false)

// 表单数据
const formData = ref({
  modelName: '',
  discipline: '',
  format: '',
  filePath: '',
  version: '',
  author: '',
  sceneId: '',
  description: ''
})

// 加载BIM模型列表
const loadModels = async () => {
  loading.value = true
  error.value = ''

  try {
    if (selectedSceneFilter.value && selectedDiscipline.value) {
      models.value = await bimModelMetadataService.getBimModelsBySceneAndDiscipline(
        selectedSceneFilter.value,
        selectedDiscipline.value
      )
    } else if (selectedSceneFilter.value) {
      models.value = await bimModelMetadataService.getBimModelsBySceneId(selectedSceneFilter.value)
    } else if (selectedDiscipline.value) {
      models.value = await bimModelMetadataService.getBimModelsByDiscipline(selectedDiscipline.value)
    } else if (selectedFormat.value) {
      models.value = await bimModelMetadataService.getBimModelsByFormat(selectedFormat.value)
    } else {
      models.value = await bimModelMetadataService.getAllBimModels()
    }
  } catch (err: any) {
    error.value = err.message || '加载BIM模型列表失败'
    showError(error.value)
  } finally {
    loading.value = false
  }
}

// 加载场景列表
const loadScenes = async () => {
  try {
    scenes.value = await sceneService.getAllScenes()
  } catch (err: any) {
    console.error('加载场景列表失败:', err)
  }
}

// 筛选处理
const handleFilterChange = async () => {
  await loadModels()
}

// 选择模型
const selectModel = (model: any) => {
  selectedModel.value = model
}

// 查看模型统计
const viewModelStats = async (model: any) => {
  try {
    elementStats.value = await bimModelMetadataService.getBimModelElementStats(model.id)
    selectedModel.value = model
    showStatsPanel.value = true
  } catch (err: any) {
    showError('获取构件统计失败: ' + err.message)
  }
}

// 编辑模型
const editModel = (model: any) => {
  editingModel.value = model
  formData.value = {
    modelName: model.modelName,
    discipline: model.discipline,
    format: model.format,
    filePath: model.filePath,
    version: model.version || '',
    author: model.author || '',
    sceneId: model.sceneId || '',
    description: model.description || ''
  }
  showEditDialog.value = true
}

// 删除模型
const deleteModel = async (model: any) => {
  if (!confirm(`确定要删除BIM模型"${model.modelName}"吗？`)) {
    return
  }

  try {
    await bimModelMetadataService.deleteBimModel(model.id)
    success('删除成功')
    await loadModels()
  } catch (err: any) {
    showError('删除失败: ' + err.message)
  }
}

// 提交表单
const submitForm = async () => {
  submitting.value = true

  try {
    const modelData = {
      modelName: formData.value.modelName,
      discipline: formData.value.discipline,
      format: formData.value.format,
      filePath: formData.value.filePath,
      version: formData.value.version,
      author: formData.value.author,
      sceneId: formData.value.sceneId || null,
      description: formData.value.description,
      createdDate: new Date().toISOString()
    }

    if (editingModel.value) {
      await bimModelMetadataService.updateBimModel(editingModel.value.id, modelData)
      success('更新成功')
    } else {
      await bimModelMetadataService.createBimModel(modelData)
      success('创建成功')
    }

    closeDialog()
    await loadModels()
  } catch (err: any) {
    showError('提交失败: ' + err.message)
  } finally {
    submitting.value = false
  }
}

// 关闭对话框
const closeDialog = () => {
  showCreateDialog.value = false
  showEditDialog.value = false
  editingModel.value = null
  formData.value = {
    modelName: '',
    discipline: '',
    format: '',
    filePath: '',
    version: '',
    author: '',
    sceneId: '',
    description: ''
  }
}

// 获取学科图标
const getDisciplineIcon = (discipline: string) => {
  const icons: any = {
    '建筑': '🏛️',
    '结构': '🏗️',
    '机电': '⚡',
    '给排水': '💧',
    '暖通': '🌡️',
    '景观': '🌳'
  }
  return icons[discipline] || '📦'
}

// 获取学科样式类
const getDisciplineClass = (discipline: string) => {
  const classes: any = {
    '建筑': 'architecture',
    '结构': 'structure',
    '机电': 'mep',
    '给排水': 'plumbing',
    '暖通': 'hvac',
    '景观': 'landscape'
  }
  return classes[discipline] || 'default'
}

// 格式化日期
const formatDate = (date: string) => {
  if (!date) return 'N/A'
  return new Date(date).toLocaleDateString('zh-CN')
}

// 格式化文件大小
const formatFileSize = (bytes: number) => {
  if (!bytes) return 'N/A'
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(2) + ' KB'
  if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(2) + ' MB'
  return (bytes / (1024 * 1024 * 1024)).toFixed(2) + ' GB'
}

// 初始化
onMounted(() => {
  loadModels()
  loadScenes()
})
</script>

<style scoped>
.bim-metadata-page {
  padding: 2rem;
  max-width: 1400px;
  margin: 0 auto;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
  gap: 1rem;
}

.header-content h1 {
  margin: 0;
  font-size: 1.75rem;
  color: var(--gray-900);
}

.page-description {
  margin: 0.5rem 0 0 0;
  color: var(--gray-600);
  font-size: 0.95rem;
}

.filters-section {
  background: white;
  padding: 1.5rem;
  border-radius: var(--border-radius);
  box-shadow: var(--shadow-sm);
  margin-bottom: 1.5rem;
}

.filter-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
}

.filter-select {
  padding: 0.75rem;
  border: 1px solid var(--gray-300);
  border-radius: var(--border-radius);
  font-size: 0.9rem;
}

.stats-section {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
  margin-bottom: 1.5rem;
}

.stat-card {
  display: flex;
  align-items: center;
  gap: 1rem;
  background: white;
  padding: 1.5rem;
  border-radius: var(--border-radius);
  box-shadow: var(--shadow-sm);
}

.stat-icon {
  font-size: 2rem;
}

.stat-label {
  font-size: 0.85rem;
  color: var(--gray-600);
  margin-bottom: 0.25rem;
}

.stat-value {
  font-size: 1.5rem;
  font-weight: 700;
  color: var(--gray-900);
}

.models-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 1.5rem;
  margin-bottom: 2rem;
}

.model-card {
  background: white;
  border-radius: var(--border-radius);
  box-shadow: var(--shadow-sm);
  transition: all var(--transition-base);
  overflow: hidden;
  cursor: pointer;
}

.model-card:hover {
  box-shadow: var(--shadow-md);
  transform: translateY(-4px);
}

.model-card.selected {
  border: 2px solid var(--primary-color);
  box-shadow: var(--shadow-colored);
}

.model-card-header {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1.25rem;
  border-bottom: 1px solid var(--gray-200);
}

.discipline-badge {
  font-size: 2rem;
  width: 48px;
  height: 48px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: var(--border-radius);
  flex-shrink: 0;
}

.discipline-architecture {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.discipline-structure {
  background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
}

.discipline-mep {
  background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%);
}

.discipline-plumbing {
  background: linear-gradient(135deg, #43e97b 0%, #38f9d7 100%);
}

.discipline-hvac {
  background: linear-gradient(135deg, #fa709a 0%, #fee140 100%);
}

.discipline-landscape {
  background: linear-gradient(135deg, #30cfd0 0%, #330867 100%);
}

.discipline-default {
  background: linear-gradient(135deg, #a8edea 0%, #fed6e3 100%);
}

.model-title {
  margin: 0;
  font-size: 1rem;
  font-weight: 600;
  color: var(--gray-900);
}

.model-meta {
  margin: 0.25rem 0 0 0;
  font-size: 0.85rem;
  color: var(--gray-600);
}

.model-card-body {
  padding: 1.25rem;
}

.model-detail {
  display: flex;
  justify-content: space-between;
  margin-bottom: 0.75rem;
  font-size: 0.9rem;
}

.detail-label {
  color: var(--gray-600);
}

.detail-value {
  font-weight: 600;
  color: var(--gray-900);
}

.model-card-footer {
  display: flex;
  gap: 0.5rem;
  padding: 1rem 1.25rem;
  background: var(--gray-50);
  border-top: 1px solid var(--gray-200);
}

.btn-action {
  flex: 1;
  padding: 0.5rem;
  border: none;
  border-radius: var(--border-radius);
  cursor: pointer;
  font-size: 0.85rem;
  font-weight: 500;
  transition: all var(--transition-base);
}

.btn-stats {
  background: var(--info-light);
  color: var(--info-color);
}

.btn-stats:hover {
  background: var(--info-color);
  color: white;
}

.btn-edit {
  background: var(--warning-light);
  color: var(--warning-color);
}

.btn-edit:hover {
  background: var(--warning-color);
  color: white;
}

.btn-delete {
  background: var(--danger-light);
  color: var(--danger-color);
}

.btn-delete:hover {
  background: var(--danger-color);
  color: white;
}

.stats-panel {
  position: fixed;
  right: 2rem;
  top: 50%;
  transform: translateY(-50%);
  width: 320px;
  background: white;
  border-radius: var(--border-radius);
  box-shadow: var(--shadow-xl);
  z-index: 100;
}

.stats-panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.25rem;
  border-bottom: 1px solid var(--gray-200);
}

.stats-panel-header h3 {
  margin: 0;
  font-size: 1.1rem;
}

.btn-close {
  background: none;
  border: none;
  font-size: 1.5rem;
  cursor: pointer;
  color: var(--gray-500);
  transition: color var(--transition-base);
}

.btn-close:hover {
  color: var(--gray-900);
}

.stats-panel-body {
  padding: 1.25rem;
}

.stats-grid {
  display: grid;
  gap: 1rem;
}

.stats-item {
  padding: 0.75rem;
  background: var(--gray-50);
  border-radius: var(--border-radius);
}

.stats-label {
  font-size: 0.85rem;
  color: var(--gray-600);
  margin-bottom: 0.25rem;
}

.stats-number {
  font-size: 1.25rem;
  font-weight: 700;
  color: var(--primary-color);
}

.empty-state {
  grid-column: 1 / -1;
  text-align: center;
  padding: 4rem 2rem;
}

.empty-icon {
  font-size: 4rem;
  margin-bottom: 1rem;
}

.empty-state h3 {
  margin: 0 0 0.5rem 0;
  color: var(--gray-700);
}

.empty-state p {
  margin: 0;
  color: var(--gray-500);
}

.loading-container {
  text-align: center;
  padding: 4rem 2rem;
}

.model-form {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.form-group label {
  font-weight: 600;
  color: var(--gray-700);
  font-size: 0.9rem;
}

.form-input {
  padding: 0.75rem;
  border: 1px solid var(--gray-300);
  border-radius: var(--border-radius);
  font-size: 0.95rem;
}

.form-input:focus {
  outline: none;
  border-color: var(--primary-color);
  box-shadow: 0 0 0 3px var(--primary-light);
}

textarea.form-input {
  resize: vertical;
  font-family: inherit;
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 1rem;
  margin-top: 1rem;
}

.btn {
  padding: 0.75rem 1.5rem;
  border: none;
  border-radius: var(--border-radius);
  cursor: pointer;
  font-size: 0.95rem;
  font-weight: 600;
  transition: all var(--transition-base);
}

.btn-primary {
  background: var(--primary-color);
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: var(--primary-hover);
  box-shadow: var(--shadow-md);
}

.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-secondary {
  background: var(--gray-200);
  color: var(--gray-700);
}

.btn-secondary:hover {
  background: var(--gray-300);
}

@media (max-width: 768px) {
  .bim-metadata-page {
    padding: 1rem;
  }

  .page-header {
    flex-direction: column;
    align-items: flex-start;
  }

  .models-grid {
    grid-template-columns: 1fr;
  }

  .form-row {
    grid-template-columns: 1fr;
  }

  .stats-panel {
    right: 1rem;
    left: 1rem;
    width: auto;
  }
}
</style>
