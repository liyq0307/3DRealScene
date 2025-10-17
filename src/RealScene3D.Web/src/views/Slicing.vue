<template>
  <div class="slicing">
    <!-- 页面标题和快捷操作 -->
    <header class="page-header">
      <div class="header-left">
        <h1>3D模型切片管理</h1>
        <p class="subtitle">管理3D模型切片任务,支持多种切片策略和LOD层级</p>
      </div>
      <div class="header-right">
        <button @click="refreshTasks" class="btn btn-primary">
          <span class="icon">🔄</span>
          刷新
        </button>
        <button @click="openCreateTaskDialog" class="btn btn-success">
          <span class="icon">➕</span>
          新建切片任务
        </button>
      </div>
    </header>

    <!-- 选项卡导航 -->
    <div class="tabs">
      <button
        v-for="tab in tabs"
        :key="tab.id"
        @click="activeTab = tab.id"
        :class="['tab', { active: activeTab === tab.id }]"
      >
        <span class="icon">{{ tab.icon }}</span>
        {{ tab.label }}
      </button>
    </div>

    <!-- 切片任务列表视图 -->
    <div v-if="activeTab === 'tasks'" class="tab-content">
      <div class="tasks-section">
        <!-- 搜索和筛选器 -->
        <SearchFilter
          v-model:searchText="searchKeyword"
          :filters="filterConfigs"
          placeholder="搜索任务名称或模型路径..."
          @search="(text, filters) => { searchKeyword = text; filterStatus = filters.status || ''; currentPage = 1 }"
        />

        <!-- 任务列表 -->
        <div class="tasks-grid">
          <div
            v-for="task in paginatedTasks"
            :key="task.id"
            class="task-card"
            @click="viewTaskDetail(task.id)"
          >
            <div class="task-header">
              <h3>{{ task.name }}</h3>
              <Badge
                :variant="getStatusVariant(task.status)"
                :label="getStatusText(task.status)"
              />
            </div>

            <div class="task-info">
              <div class="info-item">
                <span class="label">模型路径:</span>
                <span class="value">{{ task.modelPath }}</span>
              </div>
              <div class="info-item">
                <span class="label">切片策略:</span>
                <span class="value">{{ getStrategyName(task.slicingStrategy) }}</span>
              </div>
              <div class="info-item">
                <span class="label">LOD层级:</span>
                <span class="value">{{ task.lodLevels }}</span>
              </div>
              <div class="info-item">
                <span class="label">切片大小:</span>
                <span class="value">{{ task.tileSize }}m</span>
              </div>
            </div>

            <!-- 进度条 -->
            <div v-if="task.status === 'processing'" class="progress-section">
              <div class="progress-bar">
                <div
                  class="progress-fill"
                  :style="{ width: `${task.progress || 0}%` }"
                ></div>
              </div>
              <span class="progress-text">{{ task.progress || 0 }}%</span>
            </div>

            <div class="task-footer">
              <span class="task-time">
                创建时间: {{ formatDateTime(task.createdAt) }}
              </span>
              <div class="task-actions" @click.stop>
                <button
                  v-if="task.status === 'processing'"
                  @click="cancelTask(task.id)"
                  class="btn btn-sm btn-warning"
                >
                  取消
                </button>
                <button
                  v-if="task.status === 'completed'"
                  @click="viewSlices(task.id)"
                  class="btn btn-sm btn-primary"
                >
                  查看切片
                </button>
                <button
                  @click="deleteTask(task.id)"
                  class="btn btn-sm btn-danger"
                >
                  删除
                </button>
              </div>
            </div>
          </div>
        </div>

        <!-- 空状态 -->
        <div v-if="filteredTasks.length === 0" class="empty-state">
          <p>{{ searchKeyword || filterStatus ? '没有符合条件的任务' : '暂无切片任务' }}</p>
          <button v-if="!searchKeyword && !filterStatus" @click="openCreateTaskDialog" class="btn btn-primary">
            创建第一个切片任务
          </button>
        </div>

        <!-- 分页组件 -->
        <Pagination
          v-if="filteredTasks.length > 0"
          v-model:currentPage="currentPage"
          v-model:pageSize="pageSize"
          :total="filteredTasks.length"
        />
      </div>
    </div>

    <!-- 切片数据视图 -->
    <div v-if="activeTab === 'slices'" class="tab-content">
      <div class="slices-section">
        <div class="slice-viewer-header">
          <h2>切片数据浏览器</h2>
          <div class="viewer-controls">
            <label>选择任务:</label>
            <select v-model="selectedTaskId" @change="loadSliceMetadata" class="form-select">
              <option value="">请选择</option>
              <option v-for="task in completedTasks" :key="task.id" :value="task.id">
                {{ task.name }}
              </option>
            </select>
            <label>LOD层级:</label>
            <select v-model="selectedLevel" @change="loadSliceMetadata" class="form-select">
              <option v-for="level in availableLevels" :key="level" :value="level">
                Level {{ level }}
              </option>
            </select>
          </div>
        </div>

        <!-- 切片元数据网格 -->
        <div v-if="sliceMetadata.length > 0" class="slice-grid">
          <div
            v-for="slice in sliceMetadata"
            :key="`${slice.x}_${slice.y}_${slice.z}`"
            class="slice-item"
          >
            <div class="slice-coord">
              ({{ slice.x }}, {{ slice.y }}, {{ slice.z }})
            </div>
            <div class="slice-info">
              <span>文件大小: {{ formatFileSize(slice.fileSize) }}</span>
              <span>顶点数: {{ slice.vertexCount }}</span>
            </div>
            <div class="slice-actions">
              <button
                @click="downloadSlice(selectedTaskId, selectedLevel, slice.x, slice.y, slice.z)"
                class="btn btn-sm"
              >
                下载
              </button>
            </div>
          </div>
        </div>

        <div v-else-if="selectedTaskId" class="empty-state">
          <p>该任务暂无切片数据</p>
        </div>
      </div>
    </div>

    <!-- 切片策略视图 -->
    <div v-if="activeTab === 'strategies'" class="tab-content">
      <div class="strategies-section">
        <h2>切片策略说明</h2>
        <div class="strategies-grid">
          <div
            v-for="strategy in strategies"
            :key="strategy.id"
            class="strategy-card"
          >
            <div class="strategy-icon">{{ getStrategyIcon(strategy.name) }}</div>
            <h3>{{ strategy.name }}</h3>
            <p>{{ strategy.description }}</p>
            <div class="strategy-features">
              <span v-for="feature in getStrategyFeatures(strategy.name)" :key="feature">
                ✓ {{ feature }}
              </span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 创建切片任务对话框 -->
    <div v-if="showCreateTaskDialog" class="modal-overlay" @click="closeCreateTaskDialog">
      <div class="modal-content large" @click.stop>
        <h3>创建切片任务</h3>
        <div class="form-grid">
          <div class="form-group">
            <label>任务名称 *</label>
            <input
              v-model="taskForm.name"
              type="text"
              class="form-input"
              placeholder="输入任务名称"
            />
          </div>

          <div class="form-group">
            <label>描述</label>
            <textarea
              v-model="taskForm.description"
              class="form-textarea"
              placeholder="输入任务描述"
            ></textarea>
          </div>

          <div class="form-group">
            <label>模型路径 *</label>
            <input
              v-model="taskForm.modelPath"
              type="text"
              class="form-input"
              placeholder="输入模型文件路径"
            />
          </div>

          <div class="form-group">
            <label>输出路径</label>
            <input
              v-model="taskForm.outputPath"
              type="text"
              class="form-input"
              placeholder="切片数据输出路径"
            />
          </div>

          <div class="form-group">
            <label>切片策略 *</label>
            <select v-model.number="taskForm.slicingStrategy" class="form-select">
              <option :value="0">Grid - 规则网格</option>
              <option :value="1">Octree - 八叉树</option>
              <option :value="2">KdTree - KD树</option>
              <option :value="3">Adaptive - 自适应</option>
            </select>
          </div>

          <div class="form-group">
            <label>LOD层级数 *</label>
            <input
              v-model.number="taskForm.lodLevels"
              type="number"
              min="1"
              max="10"
              class="form-input"
              placeholder="1-10"
            />
          </div>

          <div class="form-group">
            <label>切片大小 (米) *</label>
            <input
              v-model.number="taskForm.tileSize"
              type="number"
              min="1"
              class="form-input"
              placeholder="默认100"
            />
          </div>

          <div class="form-group">
            <label>最大顶点数</label>
            <input
              v-model.number="taskForm.maxVerticesPerTile"
              type="number"
              class="form-input"
              placeholder="默认65536"
            />
          </div>

          <div class="form-group full-width">
            <label class="checkbox-label">
              <input
                v-model="taskForm.enableCompression"
                type="checkbox"
              />
              <span>启用压缩</span>
            </label>
          </div>

          <div class="form-group full-width">
            <label class="checkbox-label">
              <input
                v-model="taskForm.generateThumbnails"
                type="checkbox"
              />
              <span>生成缩略图</span>
            </label>
          </div>

          <div class="form-group full-width">
            <label class="checkbox-label">
              <input
                v-model="taskForm.enableIncrementalUpdate"
                type="checkbox"
              />
              <span>启用增量更新</span>
            </label>
          </div>
        </div>

        <div class="modal-actions">
          <button @click="closeCreateTaskDialog" class="btn btn-secondary">
            取消
          </button>
          <button @click="createTask" class="btn btn-primary">
            创建任务
          </button>
        </div>
      </div>
    </div>

    <!-- 任务详情对话框 -->
    <div v-if="showTaskDetailDialog" class="modal-overlay" @click="closeTaskDetailDialog">
      <div class="modal-content large" @click.stop>
        <h3>任务详情</h3>
        <div v-if="currentTask" class="task-detail">
          <div class="detail-section">
            <h4>基本信息</h4>
            <div class="detail-grid">
              <div class="detail-item">
                <span class="label">任务名称:</span>
                <span class="value">{{ currentTask.name }}</span>
              </div>
              <div class="detail-item">
                <span class="label">状态:</span>
                <Badge
                  :variant="getStatusVariant(currentTask.status)"
                  :label="getStatusText(currentTask.status)"
                />
              </div>
              <div class="detail-item">
                <span class="label">模型路径:</span>
                <span class="value">{{ currentTask.modelPath }}</span>
              </div>
              <div class="detail-item">
                <span class="label">输出路径:</span>
                <span class="value">{{ currentTask.outputPath }}</span>
              </div>
              <div class="detail-item">
                <span class="label">切片策略:</span>
                <span class="value">{{ getStrategyName(currentTask.slicingStrategy) }}</span>
              </div>
              <div class="detail-item">
                <span class="label">LOD层级:</span>
                <span class="value">{{ currentTask.lodLevels }}</span>
              </div>
              <div class="detail-item">
                <span class="label">切片大小:</span>
                <span class="value">{{ currentTask.tileSize }}m</span>
              </div>
              <div class="detail-item">
                <span class="label">创建时间:</span>
                <span class="value">{{ formatDateTime(currentTask.createdAt) }}</span>
              </div>
            </div>
          </div>

          <div v-if="taskProgress" class="detail-section">
            <h4>执行进度</h4>
            <div class="progress-detail">
              <div class="progress-bar large">
                <div
                  class="progress-fill"
                  :style="{ width: `${taskProgress.progress}%` }"
                ></div>
              </div>
              <div class="progress-info">
                <span>进度: {{ taskProgress.progress }}%</span>
                <span>当前阶段: {{ taskProgress.currentStage }}</span>
                <span v-if="taskProgress.estimatedTimeRemaining">
                  预计剩余: {{ formatDuration(taskProgress.estimatedTimeRemaining) }}
                </span>
              </div>
            </div>
          </div>

          <div class="detail-section">
            <h4>统计信息</h4>
            <div class="stats-grid">
              <div class="stat-item">
                <div class="stat-value">{{ currentTask.totalTileCount || 0 }}</div>
                <div class="stat-label">总切片数</div>
              </div>
              <div class="stat-item">
                <div class="stat-value">{{ formatFileSize(currentTask.totalDataSize || 0) }}</div>
                <div class="stat-label">数据大小</div>
              </div>
              <div class="stat-item">
                <div class="stat-value">{{ currentTask.processedTileCount || 0 }}</div>
                <div class="stat-label">已处理</div>
              </div>
              <div class="stat-item">
                <div class="stat-value">
                  {{ currentTask.completedAt ? formatDuration(
                    new Date(currentTask.completedAt).getTime() - new Date(currentTask.createdAt).getTime()
                  ) : '-' }}
                </div>
                <div class="stat-label">耗时</div>
              </div>
            </div>
          </div>
        </div>

        <div class="modal-actions">
          <button @click="closeTaskDetailDialog" class="btn btn-secondary">
            关闭
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { slicingService } from '@/services/api'
import SearchFilter from '@/components/SearchFilter.vue'
import Badge from '@/components/Badge.vue'
import Pagination from '@/components/Pagination.vue'
import type { Filter } from '@/components/SearchFilter.vue'

// 选项卡
const tabs = [
  { id: 'tasks', label: '切片任务', icon: '📋' },
  { id: 'slices', label: '切片数据', icon: '🧩' },
  { id: 'strategies', label: '切片策略', icon: '📖' }
]
const activeTab = ref('tasks')

// 任务数据
const tasks = ref<any[]>([])
const filterStatus = ref('')
const searchKeyword = ref('')

// 分页状态
const currentPage = ref(1)
const pageSize = ref(20)

// 搜索筛选器配置
const filterConfigs: Filter[] = [
  {
    key: 'status',
    label: '状态',
    options: [
      { label: '等待中', value: 'pending' },
      { label: '处理中', value: 'processing' },
      { label: '已完成', value: 'completed' },
      { label: '失败', value: 'failed' },
      { label: '已取消', value: 'cancelled' }
    ]
  }
]

// 切片数据
const selectedTaskId = ref('')
const selectedLevel = ref(0)
const sliceMetadata = ref<any[]>([])
const availableLevels = ref<number[]>([0, 1, 2, 3, 4])

// 切片策略
const strategies = ref<any[]>([])

// 对话框状态
const showCreateTaskDialog = ref(false)
const showTaskDetailDialog = ref(false)
const currentTask = ref<any>(null)
const taskProgress = ref<any>(null)

// 任务表单
const taskForm = ref({
  name: '',
  description: '',
  modelPath: '',
  outputPath: '',
  slicingStrategy: 0,
  lodLevels: 3,
  tileSize: 100,
  maxVerticesPerTile: 65536,
  enableCompression: true,
  generateThumbnails: true,
  enableIncrementalUpdate: false
})

// 计算属性
const filteredTasks = computed(() => {
  let result = tasks.value

  if (filterStatus.value) {
    result = result.filter(t => t.status === filterStatus.value)
  }

  if (searchKeyword.value) {
    const keyword = searchKeyword.value.toLowerCase()
    result = result.filter(t =>
      t.name.toLowerCase().includes(keyword) ||
      t.modelPath.toLowerCase().includes(keyword)
    )
  }

  return result
})

const paginatedTasks = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value
  const end = start + pageSize.value
  return filteredTasks.value.slice(start, end)
})

const completedTasks = computed(() => {
  return tasks.value.filter(t => t.status === 'completed')
})

// 格式化方法
const formatDateTime = (dateStr: string) => {
  return new Date(dateStr).toLocaleString('zh-CN')
}

const formatFileSize = (bytes: number) => {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i]
}

const formatDuration = (ms: number) => {
  const seconds = Math.floor(ms / 1000)
  const minutes = Math.floor(seconds / 60)
  const hours = Math.floor(minutes / 60)

  if (hours > 0) {
    return `${hours}小时${minutes % 60}分钟`
  } else if (minutes > 0) {
    return `${minutes}分钟${seconds % 60}秒`
  } else {
    return `${seconds}秒`
  }
}

const getStatusText = (status: string) => {
  const statusMap: Record<string, string> = {
    pending: '等待中',
    processing: '处理中',
    completed: '已完成',
    failed: '失败',
    cancelled: '已取消'
  }
  return statusMap[status] || status
}

const getStatusVariant = (status: string): 'primary' | 'warning' | 'success' | 'danger' | 'gray' => {
  const variantMap: Record<string, 'primary' | 'warning' | 'success' | 'danger' | 'gray'> = {
    pending: 'primary',
    processing: 'warning',
    completed: 'success',
    failed: 'danger',
    cancelled: 'gray'
  }
  return variantMap[status] || 'gray'
}

const getStrategyName = (strategy: number) => {
  const strategyMap: Record<number, string> = {
    0: 'Grid',
    1: 'Octree',
    2: 'KdTree',
    3: 'Adaptive'
  }
  return strategyMap[strategy] || '未知'
}

const getStrategyIcon = (name: string) => {
  const iconMap: Record<string, string> = {
    'Grid': '📐',
    'Octree': '🌳',
    'KdTree': '🔷',
    'Adaptive': '🎯'
  }
  return iconMap[name] || '📦'
}

const getStrategyFeatures = (name: string) => {
  const featuresMap: Record<string, string[]> = {
    'Grid': ['规则网格划分', '适用于规则地形', '处理速度快'],
    'Octree': ['八叉树结构', '自适应精度', '适用于不规则模型'],
    'KdTree': ['KD树空间剖分', '高效空间查询', '适用于复杂场景'],
    'Adaptive': ['基于密度自适应', '动态调整大小', '最优存储效率']
  }
  return featuresMap[name] || []
}

// 数据加载方法
const loadTasks = async () => {
  try {
    // 使用默认GUID作为用户ID
    tasks.value = await slicingService.getUserSlicingTasks('00000000-0000-0000-0000-000000000001')
  } catch (error) {
    console.error('加载切片任务失败:', error)
  }
}

const refreshTasks = async () => {
  await loadTasks()
}

const loadSliceMetadata = async () => {
  if (!selectedTaskId.value) return
  try {
    sliceMetadata.value = await slicingService.getSliceMetadata(
      selectedTaskId.value,
      selectedLevel.value
    )
  } catch (error) {
    console.error('加载切片元数据失败:', error)
  }
}

const loadStrategies = async () => {
  try {
    strategies.value = await slicingService.getSlicingStrategies()
  } catch (error) {
    console.error('加载切片策略失败:', error)
  }
}

// 任务操作
const openCreateTaskDialog = () => {
  taskForm.value = {
    name: '',
    description: '',
    modelPath: '',
    outputPath: '',
    slicingStrategy: 0,
    lodLevels: 3,
    tileSize: 100,
    maxVerticesPerTile: 65536,
    enableCompression: true,
    generateThumbnails: true,
    enableIncrementalUpdate: false
  }
  showCreateTaskDialog.value = true
}

const closeCreateTaskDialog = () => {
  showCreateTaskDialog.value = false
}

const createTask = async () => {
  try {
    if (!taskForm.value.name || !taskForm.value.modelPath) {
      alert('请填写必填字段')
      return
    }

    await slicingService.createSlicingTask(taskForm.value, '00000000-0000-0000-0000-000000000001')
    await loadTasks()
    closeCreateTaskDialog()
  } catch (error) {
    console.error('创建切片任务失败:', error)
    alert('创建任务失败')
  }
}

const viewTaskDetail = async (taskId: string) => {
  try {
    currentTask.value = await slicingService.getSlicingTask(taskId)
    if (currentTask.value.status === 'processing') {
      taskProgress.value = await slicingService.getSlicingProgress(taskId)
    } else {
      taskProgress.value = null
    }
    showTaskDetailDialog.value = true
  } catch (error) {
    console.error('加载任务详情失败:', error)
  }
}

const closeTaskDetailDialog = () => {
  showTaskDetailDialog.value = false
  currentTask.value = null
  taskProgress.value = null
}

const cancelTask = async (taskId: string) => {
  if (confirm('确定要取消此任务吗?')) {
    try {
      await slicingService.cancelSlicingTask(taskId, '00000000-0000-0000-0000-000000000001')
      await loadTasks()
    } catch (error) {
      console.error('取消任务失败:', error)
    }
  }
}

const deleteTask = async (taskId: string) => {
  if (confirm('确定要删除此任务吗? 这将删除所有切片数据。')) {
    try {
      await slicingService.deleteSlicingTask(taskId, '00000000-0000-0000-0000-000000000001')
      await loadTasks()
    } catch (error) {
      console.error('删除任务失败:', error)
    }
  }
}

const viewSlices = (taskId: string) => {
  selectedTaskId.value = taskId
  activeTab.value = 'slices'
  loadSliceMetadata()
}

const downloadSlice = async (taskId: string, level: number, x: number, y: number, z: number) => {
  try {
    await slicingService.downloadSlice(taskId, level, x, y, z)
  } catch (error) {
    console.error('下载切片失败:', error)
  }
}

// 生命周期
onMounted(async () => {
  await Promise.all([
    loadTasks(),
    loadStrategies()
  ])
})
</script>

<style scoped>
.slicing {
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

.tabs {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 2rem;
  background: white;
  padding: 0.5rem;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.tab {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem 1.5rem;
  border: none;
  background: transparent;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 0.95rem;
  color: #666;
}

.tab:hover {
  background: #f8f9fa;
  color: #333;
}

.tab.active {
  background: #007acc;
  color: white;
}

.tab-content {
  background: white;
  padding: 2rem;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

/* 任务列表样式 */
.filters {
  display: flex;
  gap: 1rem;
  margin-bottom: 2rem;
}

.tasks-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(400px, 1fr));
  gap: 1.5rem;
}

.task-card {
  padding: 1.5rem;
  border: 1px solid #e1e5e9;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.task-card:hover {
  border-color: #007acc;
  box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
}

.task-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}

.task-header h3 {
  margin: 0;
  font-size: 1.1rem;
}

.task-status {
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-size: 0.8rem;
  font-weight: 500;
}

.task-status.status-pending {
  background: #e3f2fd;
  color: #1976d2;
}

.task-status.status-processing {
  background: #fff3e0;
  color: #ef6c00;
}

.task-status.status-completed {
  background: #e8f5e8;
  color: #2e7d32;
}

.task-status.status-failed {
  background: #ffebee;
  color: #c62828;
}

.task-status.status-cancelled {
  background: #f5f5f5;
  color: #757575;
}

.task-info {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-bottom: 1rem;
}

.info-item {
  display: flex;
  gap: 0.5rem;
  font-size: 0.9rem;
}

.info-item .label {
  color: #666;
  min-width: 80px;
}

.info-item .value {
  color: #333;
  word-break: break-all;
}

.progress-section {
  margin: 1rem 0;
}

.progress-bar {
  height: 8px;
  background: #e1e5e9;
  border-radius: 4px;
  overflow: hidden;
  margin-bottom: 0.5rem;
}

.progress-bar.large {
  height: 12px;
}

.progress-fill {
  height: 100%;
  background: linear-gradient(90deg, #007acc, #00bcd4);
  transition: width 0.3s ease;
}

.progress-text {
  font-size: 0.85rem;
  color: #666;
}

.task-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 1rem;
  padding-top: 1rem;
  border-top: 1px solid #e1e5e9;
}

.task-time {
  font-size: 0.85rem;
  color: #999;
}

.task-actions {
  display: flex;
  gap: 0.5rem;
}

/* 切片数据样式 */
.slice-viewer-header {
  margin-bottom: 2rem;
}

.viewer-controls {
  display: flex;
  align-items: center;
  gap: 1rem;
  margin-top: 1rem;
}

.viewer-controls label {
  font-weight: 500;
}

.slice-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
  gap: 1rem;
}

.slice-item {
  padding: 1rem;
  border: 1px solid #e1e5e9;
  border-radius: 6px;
  background: #fafafa;
}

.slice-coord {
  font-weight: bold;
  margin-bottom: 0.5rem;
  color: #333;
}

.slice-info {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  margin-bottom: 0.75rem;
  font-size: 0.85rem;
  color: #666;
}

.slice-actions {
  display: flex;
  gap: 0.5rem;
}

/* 切片策略样式 */
.strategies-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 2rem;
  margin-top: 2rem;
}

.strategy-card {
  padding: 2rem;
  border: 2px solid #e1e5e9;
  border-radius: 12px;
  text-align: center;
  transition: all 0.2s ease;
}

.strategy-card:hover {
  border-color: #007acc;
  box-shadow: 0 6px 12px rgba(0, 0, 0, 0.1);
}

.strategy-icon {
  font-size: 3rem;
  margin-bottom: 1rem;
}

.strategy-card h3 {
  margin: 0 0 1rem 0;
  color: #333;
}

.strategy-card p {
  color: #666;
  margin-bottom: 1.5rem;
  line-height: 1.6;
}

.strategy-features {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  text-align: left;
}

.strategy-features span {
  color: #2e7d32;
  font-size: 0.9rem;
}

/* 任务详情样式 */
.task-detail {
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

.detail-section h4 {
  margin: 0 0 1rem 0;
  color: #333;
  font-size: 1.1rem;
}

.detail-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 1rem;
}

.detail-item {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.detail-item .label {
  font-size: 0.85rem;
  color: #999;
}

.detail-item .value {
  font-size: 0.95rem;
  color: #333;
  word-break: break-all;
}

.progress-detail {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.progress-info {
  display: flex;
  gap: 2rem;
  font-size: 0.9rem;
  color: #666;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 1.5rem;
}

.stat-item {
  text-align: center;
  padding: 1.5rem;
  background: #f8f9fa;
  border-radius: 8px;
}

.stat-value {
  font-size: 2rem;
  font-weight: bold;
  color: #007acc;
  margin-bottom: 0.5rem;
}

.stat-label {
  font-size: 0.85rem;
  color: #666;
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

.btn-warning {
  background: #ffc107;
  color: #333;
  border-color: #ffc107;
}

.btn-warning:hover {
  background: #e0a800;
}

.btn-danger {
  background: #dc3545;
  color: white;
  border-color: #dc3545;
}

.btn-danger:hover {
  background: #c82333;
}

.btn-sm {
  padding: 0.25rem 0.75rem;
  font-size: 0.85rem;
}

/* 表单样式 */
.form-input,
.form-select,
.form-textarea {
  padding: 0.5rem;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  font-size: 0.9rem;
  width: 100%;
}

.form-input:focus,
.form-select:focus,
.form-textarea:focus {
  outline: none;
  border-color: #007acc;
}

.form-textarea {
  min-height: 80px;
  resize: vertical;
}

.form-group {
  margin-bottom: 1rem;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 500;
  color: #333;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 1rem;
}

.form-group.full-width {
  grid-column: 1 / -1;
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

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 1rem;
  margin-top: 2rem;
}

.icon {
  font-size: 1.1em;
}

.badge {
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-size: 0.85rem;
  font-weight: 500;
}
</style>
