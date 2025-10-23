<template>
  <div class="model-browser">
    <!-- 浏览器头部 -->
    <div class="browser-header">
      <h4>浏览模型</h4>
      <button @click="close" class="close-btn">✕</button>
    </div>

    <!-- 搜索和过滤 -->
    <div class="browser-filters">
      <input
        v-model="searchText"
        type="text"
        class="search-input"
        placeholder="搜索模型名称..."
      />
      <select v-model="selectedType" class="type-filter">
        <option value="">所有类型</option>
        <option value=".gltf">.gltf</option>
        <option value=".glb">.glb</option>
        <option value=".obj">.obj</option>
        <option value=".fbx">.fbx</option>
      </select>
    </div>

    <!-- 模型列表 -->
    <div class="model-list">
      <div
        v-for="model in filteredModels"
        :key="model.path"
        class="model-item"
        :class="{ selected: selectedModel?.path === model.path }"
        @click="selectModel(model)"
        @dblclick="confirmSelection"
      >
        <div class="model-icon">
          {{ getModelIcon(model.type) }}
        </div>
        <div class="model-info">
          <div class="model-name">{{ model.name }}</div>
          <div class="model-meta">
            <span class="model-type">{{ model.type }}</span>
            <span class="model-size">{{ formatSize(model.size) }}</span>
          </div>
          <div class="model-path">{{ model.path }}</div>
        </div>
        <button
          v-if="showPreview"
          @click.stop="previewModel(model)"
          class="preview-btn"
          title="预览模型"
        >
          👁️
        </button>
      </div>
    </div>

    <!-- 空状态 -->
    <div v-if="filteredModels.length === 0" class="empty-state">
      <p>{{ searchText ? '未找到匹配的模型' : '暂无模型文件' }}</p>
      <button @click="openUploadDialog" class="btn-upload">
        <span>📤</span>
        上传模型
      </button>
    </div>

    <!-- 底部操作栏 -->
    <div class="browser-footer">
      <div class="selected-info">
        <span v-if="selectedModel">已选择: {{ selectedModel.name }}</span>
        <span v-else class="hint">双击或点击确认按钮选择模型</span>
      </div>
      <div class="footer-actions">
        <button @click="close" class="btn btn-secondary">取消</button>
        <button
          @click="confirmSelection"
          :disabled="!selectedModel"
          class="btn btn-primary"
        >
          确认选择
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * 模型浏览器组件
 *
 * 功能说明:
 * - 浏览和选择3D模型文件
 * - 支持搜索和类型过滤
 * - 显示模型预览
 * - 文件上传集成
 *
 * 作者: liyq
 * 创建时间: 2025-10-22
 */
import { ref, computed, onMounted } from 'vue'

// ==================== Props & Emits ====================

interface Props {
  showPreview?: boolean  // 是否显示预览按钮
  allowedTypes?: string[] // 允许的文件类型
}

const props = withDefaults(defineProps<Props>(), {
  showPreview: true,
  allowedTypes: () => ['.gltf', '.glb', '.obj', '.fbx', '.dae']
})

const emit = defineEmits<{
  select: [model: ModelFile]
  close: []
  preview: [model: ModelFile]
}>()

// ==================== 类型定义 ====================

interface ModelFile {
  path: string
  name: string
  type: string
  size: number
  uploadedAt?: string
  thumbnail?: string
}

// ==================== 响应式数据 ====================

const searchText = ref('')
const selectedType = ref('')
const selectedModel = ref<ModelFile | null>(null)
const models = ref<ModelFile[]>([])
const loading = ref(false)

// ==================== 计算属性 ====================

/**
 * 过滤后的模型列表
 */
const filteredModels = computed(() => {
  let result = models.value

  // 类型过滤
  if (selectedType.value) {
    result = result.filter(m => m.type === selectedType.value)
  }

  // 搜索过滤
  if (searchText.value) {
    const search = searchText.value.toLowerCase()
    result = result.filter(m =>
      m.name.toLowerCase().includes(search) ||
      m.path.toLowerCase().includes(search)
    )
  }

  // 只显示允许的类型
  result = result.filter(m => props.allowedTypes.includes(m.type))

  return result
})

// ==================== 方法 ====================

/**
 * 加载模型列表
 */
const loadModels = async () => {
  loading.value = true
  try {
    // TODO: 实现从后端API获取模型列表
    // const response = await fileService.getModelFiles()
    // models.value = response.data

    // 模拟数据（实际应从API获取）
    models.value = [
      {
        path: '/models/building_a.glb',
        name: 'building_a.glb',
        type: '.glb',
        size: 2048576,
        uploadedAt: '2025-10-20'
      },
      {
        path: '/models/car.gltf',
        name: 'car.gltf',
        type: '.gltf',
        size: 512000,
        uploadedAt: '2025-10-21'
      },
      {
        path: '/models/tree.obj',
        name: 'tree.obj',
        type: '.obj',
        size: 1024000,
        uploadedAt: '2025-10-19'
      },
      {
        path: '/models/street_light.fbx',
        name: 'street_light.fbx',
        type: '.fbx',
        size: 768000,
        uploadedAt: '2025-10-22'
      }
    ]
  } catch (error) {
    console.error('加载模型列表失败:', error)
  } finally {
    loading.value = false
  }
}

/**
 * 选择模型
 */
const selectModel = (model: ModelFile) => {
  selectedModel.value = model
}

/**
 * 确认选择
 */
const confirmSelection = () => {
  if (selectedModel.value) {
    emit('select', selectedModel.value)
  }
}

/**
 * 预览模型
 */
const previewModel = (model: ModelFile) => {
  emit('preview', model)
}

/**
 * 关闭浏览器
 */
const close = () => {
  emit('close')
}

/**
 * 打开上传对话框
 */
const openUploadDialog = () => {
  // TODO: 实现文件上传功能
  console.log('打开上传对话框')
}

/**
 * 获取模型图标
 */
const getModelIcon = (type: string): string => {
  const icons: Record<string, string> = {
    '.gltf': '📦',
    '.glb': '📦',
    '.obj': '🎲',
    '.fbx': '🎭',
    '.dae': '🗿'
  }
  return icons[type] || '📄'
}

/**
 * 格式化文件大小
 */
const formatSize = (bytes: number): string => {
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB'
  return (bytes / 1024 / 1024).toFixed(1) + ' MB'
}

// ==================== 生命周期 ====================

onMounted(() => {
  loadModels()
})
</script>

<style scoped>
.model-browser {
  display: flex;
  flex-direction: column;
  height: 600px;
  background: white;
  border-radius: 8px;
  overflow: hidden;
}

/* 头部 */
.browser-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem 1.5rem;
  background: #f8f9fa;
  border-bottom: 1px solid #e1e5e9;
}

.browser-header h4 {
  margin: 0;
  font-size: 1.1rem;
  color: #333;
}

.close-btn {
  background: none;
  border: none;
  font-size: 1.5rem;
  color: #666;
  cursor: pointer;
  padding: 0;
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: all 0.2s;
}

.close-btn:hover {
  background: #e1e5e9;
  color: #333;
}

/* 过滤器 */
.browser-filters {
  display: flex;
  gap: 1rem;
  padding: 1rem 1.5rem;
  background: white;
  border-bottom: 1px solid #e1e5e9;
}

.search-input,
.type-filter {
  padding: 0.5rem 0.75rem;
  border: 1px solid #d1d5db;
  border-radius: 4px;
  font-size: 0.9rem;
}

.search-input {
  flex: 1;
}

.type-filter {
  min-width: 150px;
}

/* 模型列表 */
.model-list {
  flex: 1;
  overflow-y: auto;
  padding: 1rem;
}

.model-item {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1rem;
  border: 1px solid #e1e5e9;
  border-radius: 6px;
  margin-bottom: 0.75rem;
  cursor: pointer;
  transition: all 0.2s;
}

.model-item:hover {
  background: #f8f9fa;
  border-color: #007acc;
}

.model-item.selected {
  background: #e3f2fd;
  border-color: #007acc;
  box-shadow: 0 2px 4px rgba(0, 122, 204, 0.1);
}

.model-icon {
  font-size: 2rem;
  width: 60px;
  height: 60px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #f0f0f0;
  border-radius: 8px;
}

.model-info {
  flex: 1;
  min-width: 0;
}

.model-name {
  font-weight: 600;
  color: #333;
  margin-bottom: 0.25rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.model-meta {
  display: flex;
  gap: 1rem;
  margin-bottom: 0.25rem;
}

.model-type {
  color: #007acc;
  font-size: 0.85rem;
  font-weight: 500;
}

.model-size {
  color: #666;
  font-size: 0.85rem;
}

.model-path {
  font-size: 0.8rem;
  color: #999;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.preview-btn {
  padding: 0.5rem 1rem;
  background: #007acc;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 1.2rem;
  transition: all 0.2s;
}

.preview-btn:hover {
  background: #005999;
}

/* 空状态 */
.empty-state {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 3rem;
  color: #666;
}

.empty-state p {
  margin-bottom: 1.5rem;
  font-size: 1rem;
}

.btn-upload {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem 1.5rem;
  background: #007acc;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.95rem;
  transition: all 0.2s;
}

.btn-upload:hover {
  background: #005999;
}

/* 底部 */
.browser-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem 1.5rem;
  background: #f8f9fa;
  border-top: 1px solid #e1e5e9;
}

.selected-info {
  font-size: 0.9rem;
  color: #333;
}

.selected-info .hint {
  color: #999;
  font-style: italic;
}

.footer-actions {
  display: flex;
  gap: 0.75rem;
}

.btn {
  padding: 0.5rem 1.25rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 500;
  transition: all 0.2s;
}

.btn-secondary {
  background: #e1e5e9;
  color: #333;
}

.btn-secondary:hover {
  background: #d1d5db;
}

.btn-primary {
  background: #007acc;
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: #005999;
}

.btn-primary:disabled {
  background: #ccc;
  cursor: not-allowed;
}
</style>
