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
      <button v-for="tab in tabs" :key="tab.id" @click="activeTab = tab.id"
        :class="['tab', { active: activeTab === tab.id }]">
        <span class="icon">{{ tab.icon }}</span>
        {{ tab.label }}
      </button>
    </div>

    <!-- 切片任务列表视图 -->
    <div v-if="activeTab === 'tasks'" class="tab-content">
      <div class="tasks-section">
        <!-- 搜索和筛选器 -->
        <SearchFilter v-model:searchText="searchKeyword" :filters="filterConfigs" placeholder="搜索任务名称或模型路径..."
          @search="(text, filters) => { searchKeyword = text; filterStatus = filters.status || ''; currentPage = 1 }" />

        <!-- 任务列表 -->
        <div class="tasks-grid">
          <div v-for="task in paginatedTasks" :key="task.id" class="task-card" @click="viewTaskDetail(task.id)">
            <div class="task-header">
              <h3>{{ task.name }}</h3>
              <Badge :variant="getStatusVariant(task.status)" :label="getStatusText(task.status)" />
            </div>

            <div class="task-info">
              <div class="info-item">
                <span class="label">模型路径:</span>
                <span class="value">{{ task.sourceModelPath }}</span>
              </div>
              <div class="info-item">
                <span class="label">输出路径:</span>
                <span class="value">{{ task.outputPath || '(MinIO存储)' }}</span>
              </div>
              <div class="info-item">
                <span class="label">输出格式:</span>
                <span class="value">{{ (task.slicingConfig?.outputFormat || 'b3dm').toUpperCase() }}</span>
              </div>
              <div class="info-item">
                <span class="label">纹理策略:</span>
                <span class="value">{{ getTextureStrategyName(task.slicingConfig?.textureStrategy) }}</span>
              </div>
              <div class="info-item">
                <span class="label">LOD层级:</span>
                <span class="value">{{ task.slicingConfig?.lodLevels || 3 }}</span>
              </div>
              <div class="info-item">
                <span class="label">递归深度:</span>
                <span class="value">{{ task.slicingConfig?.divisions || 2 }}</span>
              </div>
              <div class="info-item" v-if="task.status === 'failed' && task.errorMessage">
                <span class="label error-label">失败原因:</span>
                <span class="value error-value">{{ task.errorMessage }}</span>
              </div>
            </div>

            <!-- 进度条 -->
            <div v-if="task.status === 'processing'" class="progress-section">
              <div class="progress-bar">
                <div class="progress-fill" :style="{ width: `${task.progress || 0}%` }"></div>
              </div>
              <span class="progress-text">{{ task.progress || 0 }}%</span>
            </div>

            <div class="task-footer">
              <span class="task-time">
                创建时间: {{ formatDateTime(task.createdAt) }}
              </span>
              <div class="task-actions" @click.stop>
                <button v-if="task.status === 'processing'" @click="cancelTask(task.id)" class="btn btn-sm btn-warning">
                  取消
                </button>
                <button v-if="task.status === 'completed'" @click="viewSlices(task.id)" class="btn btn-sm btn-primary">
                  查看切片
                </button>
                <button v-if="task.status === 'completed'" @click="previewSlices(task)" class="btn btn-sm btn-success">
                  预览
                </button>
                <button @click="deleteTask(task.id)" class="btn btn-sm btn-danger">
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
        <Pagination v-if="filteredTasks.length > 0" v-model:currentPage="currentPage" v-model:pageSize="pageSize"
          :total="filteredTasks.length" />
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

        <!-- LOD层级概览统计 -->
        <div v-if="selectedTaskId && lodLevelStats.size > 0" class="lod-stats-panel">
          <h3>📊 LOD层级统计</h3>
          <div class="lod-stats-grid">
            <div v-for="[level, stats] in Array.from(lodLevelStats.entries())" :key="level"
              :class="['lod-stat-card', { active: selectedLevel === level }]"
              @click="selectedLevel = level; loadSliceMetadata()">
              <div class="lod-level-badge">L{{ level }}</div>
              <div class="lod-stat-content">
                <div class="lod-stat-item">
                  <span class="lod-stat-label">切片数:</span>
                  <span class="lod-stat-value">{{ stats.count }}</span>
                </div>
                <div class="lod-stat-item">
                  <span class="lod-stat-label">总大小:</span>
                  <span class="lod-stat-value">{{ formatFileSize(stats.totalSize) }}</span>
                </div>
                <div class="lod-stat-item">
                  <span class="lod-stat-label">平均大小:</span>
                  <span class="lod-stat-value">{{ formatFileSize(stats.avgSize) }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- 切片元数据网格 -->
        <div v-if="sliceMetadata.length > 0" class="slice-grid-section">
          <div class="slice-grid-header">
            <h3>Level {{ selectedLevel }} - 切片详情 ({{ sliceMetadata.length }}个)</h3>
          </div>
          <div class="slice-grid">
            <div v-for="slice in sliceMetadata" :key="`${selectedLevel}_${slice.x}_${slice.y}_${slice.z}`"
              class="slice-card-enhanced">
              <div class="slice-card-header">
                <span class="slice-coord">
                  ({{ slice.x }}, {{ slice.y }}, {{ slice.z }})
                </span>
                <span class="slice-level-badge">L{{ selectedLevel }}</span>
              </div>

              <div class="slice-card-body">
                <div class="slice-info-row">
                  <span class="slice-info-label">文件大小:</span>
                  <span class="slice-info-value">{{ formatFileSize(slice.fileSize) }}</span>
                </div>

                <div v-if="slice.vertexCount" class="slice-info-row">
                  <span class="slice-info-label">顶点数:</span>
                  <span class="slice-info-value">{{ slice.vertexCount.toLocaleString() }}</span>
                </div>

                <div v-if="slice.boundingBox" class="slice-info-row">
                  <span class="slice-info-label">包围盒:</span>
                  <span class="slice-info-value bbox">{{ formatBoundingBox(slice.boundingBox) }}</span>
                </div>
              </div>

              <div class="slice-card-actions">
                <button @click="downloadSlice(selectedTaskId, selectedLevel, slice.x, slice.y, slice.z)"
                  class="btn-icon-small" title="下载切片">
                  📥
                </button>
              </div>
            </div>
          </div>
        </div>

        <div v-else-if="selectedTaskId" class="empty-state">
          <p>该任务在Level {{ selectedLevel }}暂无切片数据</p>
        </div>
      </div>
    </div>

    <!-- 切片策略视图 -->
    <div v-if="activeTab === 'strategies'" class="tab-content">
      <div class="strategies-section">
        <div class="strategy-header">
          <h2>切片策略说明</h2>
          <Badge variant="success" label="新架构" />
        </div>

        <div class="strategy-main-card">
          <div class="strategy-icon-large">🚀</div>
          <h2>瓦片生成流水线（Tile Generation Pipeline）</h2>
          <p class="strategy-description">
            采用三阶段切片处理流程，提供真正的网格分割和高质量的 LOD 生成。
          </p>

          <div class="pipeline-stages">
            <div class="stage">
              <div class="stage-number">1</div>
              <h4>网格简化（Decimation）</h4>
              <p>使用 Fast Quadric Mesh Simplification 算法</p>
              <ul>
                <li>二次误差度量（QEM）</li>
                <li>边折叠优化</li>
                <li>多 LOD 级别生成</li>
              </ul>
            </div>

            <div class="stage-arrow">→</div>

            <div class="stage">
              <div class="stage-number">2</div>
              <h4>空间分割（Splitting）</h4>
              <p>递归轴对齐空间分割（BSP）</p>
              <ul>
                <li>真正的网格分割</li>
                <li>三角形与平面相交计算</li>
                <li>自动处理跨越边界</li>
              </ul>
            </div>

            <div class="stage-arrow">→</div>

            <div class="stage">
              <div class="stage-number">3</div>
              <h4>格式转换（Conversion）</h4>
              <p>生成 3D Tiles 格式</p>
              <ul>
                <li>B3DM、GLTF 等格式</li>
                <li>自动生成 tileset.json</li>
                <li>优化渲染性能</li>
              </ul>
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
            <input v-model="taskForm.name" type="text" class="form-input" placeholder="输入任务名称" />
          </div>

          <div class="form-group">
            <label>描述</label>
            <textarea v-model="taskForm.description" class="form-textarea" placeholder="输入任务描述"></textarea>
          </div>

          <div class="form-group">
            <label>模型路径 *</label>
            <input v-model="taskForm.modelPath" type="text" class="form-input" placeholder="例如: F:/Data/3D/model.obj" />
            <small class="form-hint">模型文件绝对路径或者minio服务器上路径</small>
          </div>

          <div class="form-group">
            <label>输出路径</label>
            <input v-model="taskForm.outputPath" type="text" class="form-input" placeholder="例如: F:/Data/3D/Output" />
            <small class="form-hint">名称或绝对路径或空，名称或者为空则切片保存到minio</small>
          </div>

          <div class="form-group">
            <label>LOD层级数（网格简化级别）*</label>
            <input v-model.number="taskForm.lodLevels" type="number" min="1" max="5" class="form-input"
              placeholder="默认3" />
          </div>

          <div class="form-group">
            <label>输出格式 *</label>
            <select v-model="taskForm.outputFormat" class="form-select">
              <option value="b3dm">B3DM - Batched 3D Model（默认，推荐）✨</option>
              <option value="gltf">GLTF - GL Transmission Format</option>
              <option value="i3dm">I3DM - Instanced 3D Model</option>
              <option value="pnts">PNTS - Point Cloud</option>
              <option value="cmpt">CMPT - Composite</option>
            </select>
          </div>

          <div class="form-group">
            <label>纹理策略 *</label>
            <select v-model.number="taskForm.textureStrategy" class="form-select">
              <option :value="2">Repack - 重新打包纹理（PNG格式，推荐）✨</option>
              <option :value="3">RepackCompressed - 打包+压缩（JPEG质量75）</option>
              <option :value="1">Compress - 压缩纹理（保持原始分辨率）</option>
              <option :value="0">KeepOriginal - 保持原样（不推荐）</option>
            </select>
          </div>

          <div class="form-group" v-if="taskForm.enableMeshDecimation">
            <label>空间分割递归深度（Divisions）</label>
            <input v-model.number="taskForm.divisions" type="number" min="1" max="4" class="form-input"
              placeholder="默认2" />
            <small class="form-hint" style="color: #2196F3; display: block; margin-top: 4px;">
              📊 预估切片数：{{ estimateSliceCount(taskForm.lodLevels, taskForm.divisions) }} 个
              （{{ taskForm.lodLevels }} LOD × {{ Math.pow(2, taskForm.divisions) }}×{{ Math.pow(2, taskForm.divisions)
              }} 空间单元）
            </small>
          </div>

          <div class="form-group full-width">
            <label class="checkbox-label">
              <input v-model="taskForm.enableMeshDecimation" type="checkbox" />
              <span>启用网格简化（LOD生成）</span>
            </label>
            <small class="form-hint" v-if="taskForm.enableMeshDecimation">
              使用 Fast Quadric Mesh Simplification 算法生成多级 LOD
            </small>
          </div>

          <div class="form-group full-width">
            <label class="checkbox-label">
              <input v-model="taskForm.enableIncrementalUpdate" type="checkbox" />
              <span>启用增量更新</span>
            </label>
          </div>

          <div class="form-group full-width">
            <label class="checkbox-label">
              <input v-model="taskForm.enableCompression" type="checkbox" />
              <span>启用几何压缩</span>
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
                <Badge :variant="getStatusVariant(currentTask.status)" :label="getStatusText(currentTask.status)" />
              </div>
              <div class="detail-item">
                <span class="label">模型路径:</span>
                <span class="value">{{ currentTask.sourceModelPath }}</span>
              </div>
              <div class="detail-item">
                <span class="label">输出路径:</span>
                <span class="value">{{ currentTask.outputPath || '(MinIO存储)' }}</span>
              </div>
              <div class="detail-item">
                <span class="label">创建时间:</span>
                <span class="value">{{ formatDateTime(currentTask.createdAt) }}</span>
              </div>
              <div class="detail-item" v-if="currentTask.completedAt">
                <span class="label">完成时间:</span>
                <span class="value">{{ formatDateTime(currentTask.completedAt) }}</span>
              </div>
            </div>
          </div>

          <div class="detail-section">
            <h4>切片配置</h4>
            <div class="detail-grid">
              <div class="detail-item">
                <span class="label">输出格式:</span>
                <span class="value">{{ (currentTask.slicingConfig?.outputFormat || 'b3dm').toUpperCase() }}</span>
              </div>
              <div class="detail-item">
                <span class="label">纹理策略:</span>
                <span class="value">{{ getTextureStrategyName(currentTask.slicingConfig?.textureStrategy) }}</span>
              </div>
              <div class="detail-item">
                <span class="label">LOD层级:</span>
                <span class="value">{{ currentTask.slicingConfig?.lodLevels || 3 }}</span>
              </div>
              <div class="detail-item">
                <span class="label">递归深度:</span>
                <span class="value">{{ currentTask.slicingConfig?.divisions || 2 }}</span>
              </div>
              <div class="detail-item">
                <span class="label">网格简化:</span>
                <span class="value">{{ currentTask.slicingConfig?.enableMeshDecimation ? '✓ 已启用' : '✗ 未启用' }}</span>
              </div>
              <div class="detail-item">
                <span class="label">几何压缩:</span>
                <span class="value">{{ currentTask.slicingConfig?.compressOutput ? '✓ 已启用' : '✗ 未启用' }}</span>
              </div>
              <div class="detail-item">
                <span class="label">增量更新:</span>
                <span class="value">{{ currentTask.slicingConfig?.enableIncrementalUpdates ? '✓ 已启用' : '✗ 未启用' }}</span>
              </div>
              <div class="detail-item">
                <span class="label">坐标系统:</span>
                <span class="value">{{ currentTask.slicingConfig?.coordinateSystem || 'EPSG:4326' }}</span>
              </div>
            </div>
          </div>

          <div v-if="currentTask.status === 'failed' && currentTask.errorMessage" class="detail-section error-section">
            <h4>❌ 错误信息</h4>
            <div class="error-message-box">
              {{ currentTask.errorMessage }}
            </div>
          </div>

          <div v-if="taskProgress" class="detail-section">
            <h4>执行进度</h4>
            <div class="progress-detail">
              <div class="progress-bar large">
                <div class="progress-fill" :style="{ width: `${taskProgress.progress}%` }"></div>
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
                <div class="stat-value">{{ totalSliceCount || 0 }}</div>
                <div class="stat-label">总切片数</div>
              </div>
              <div class="stat-item">
                <div class="stat-value">{{ formatFileSize(totalDataSize || 0) }}</div>
                <div class="stat-label">数据大小</div>
              </div>
              <div class="stat-item">
                <div class="stat-value">{{ processedSliceCount || 0 }}</div>
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

    <!-- 切片预览对话框 -->
    <div v-if="showPreviewDialog" class="modal-overlay" @click="closePreviewDialog">
      <div class="modal-content fullscreen" @click.stop>
        <div class="modal-header">
          <h3>切片预览: {{ previewTask?.name }}</h3>
          <button @click="closePreviewDialog" class="btn-close">✕</button>
        </div>
        <div class="modal-body">
          <SlicePreview v-if="previewTask" :taskId="previewTask.id" :outputPath="previewTask.outputPath"
            :autoLoad="true" @loaded="onPreviewLoaded" @error="onPreviewError" />
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
import SlicePreview from '@/components/SlicePreview.vue'
import { useAuthStore } from '@/stores/auth'

const authStore = useAuthStore()
const userId = authStore.currentUser.value?.id || '00000000-0000-0000-0000-000000000001'

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
const lodLevelStats = ref<Map<number, { count: number; totalSize: number; avgSize: number }>>(new Map())

// 切片策略
const strategies = ref<any[]>([])

// 对话框状态
const showCreateTaskDialog = ref(false)
const showTaskDetailDialog = ref(false)
const showPreviewDialog = ref(false)

const currentTask = ref<any>(null)
const taskProgress = ref<any>(null)
const totalDataSize = ref<number>(0)
const totalSliceCount = ref<number>(0)
const processedSliceCount = ref<number>(0)
const previewTask = ref<any>(null)

// 任务表单
const taskForm = ref({
  name: '',
  description: '',
  modelPath: '',
  outputPath: '',
  slicingStrategy: 0,  // TileGenerationPipeline
  outputFormat: 'b3dm',  // 输出格式，默认b3dm
  textureStrategy: 2,  // Repack - 重新打包纹理（默认推荐）
  lodLevels: 3,
  divisions: 2,  // 空间分割递归深度
  enableCompression: true,
  enableIncrementalUpdate: false,
  enableMeshDecimation: true,  // 启用网格简化
  generateTileset: true  // 生成 tileset.json
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
      t.sourceModelPath.toLowerCase().includes(keyword)
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

const getTextureStrategyName = (strategy: number | string | undefined) => {
  // 如果是字符串（枚举名称）
  if (typeof strategy === 'string') {
    const nameMap: Record<string, string> = {
      'KeepOriginal': 'KeepOriginal - 保持原样',
      'Compress': 'Compress - 压缩纹理',
      'Repack': 'Repack - 重新打包（推荐）',
      'RepackCompressed': 'RepackCompressed - 打包+压缩'
    }
    return nameMap[strategy] || strategy
  }

  // 如果是数字（枚举值）
  if (typeof strategy === 'number') {
    const strategyMap: Record<number, string> = {
      0: 'KeepOriginal - 保持原样',
      1: 'Compress - 压缩纹理',
      2: 'Repack - 重新打包（推荐）',
      3: 'RepackCompressed - 打包+压缩'
    }
    return strategyMap[strategy] || '未知纹理策略'
  }

  return '未指定'
}

const formatBoundingBox = (bbox: any): string => {
  try {
    const box = typeof bbox === 'string' ? JSON.parse(bbox) : bbox
    if (box && box.minX !== undefined) {
      return `[${box.minX.toFixed(1)}, ${box.minY.toFixed(1)}, ${box.minZ.toFixed(1)}] - [${box.maxX.toFixed(1)}, ${box.maxY.toFixed(1)}, ${box.maxZ.toFixed(1)}]`
    }
    return String(bbox || 'N/A')
  } catch {
    return String(bbox || 'N/A')
  }
}

// 数据加载方法
const loadTasks = async () => {
  try {
    // 使用默认GUID作为用户ID
    tasks.value = await slicingService.getUserSlicingTasks(userId)
  } catch (error) {
    console.error('加载切片任务失败:', error)
  }
}

const refreshTasks = async () => {
  await loadTasks()
}

const loadSliceMetadata = async () => {
  // 清空旧数据，避免UI显示累积
  sliceMetadata.value = []

  if (!selectedTaskId.value) {
    lodLevelStats.value.clear()
    return
  }

  try {
    // 获取任务信息以确定最大LOD层级
    const taskInfo = await slicingService.getSlicingTask(selectedTaskId.value)
    const maxLevel = taskInfo?.slicingConfig?.lodLevels || 5

    // 更新可用层级列表
    availableLevels.value = Array.from({ length: maxLevel + 1 }, (_, i) => i)

    // 并行加载所有层级的统计信息
    const statsPromises = availableLevels.value.map(async (level) => {
      try {
        const slices = await slicingService.getSliceMetadata(selectedTaskId.value, level)
        if (slices && slices.length > 0) {
          const totalSize = slices.reduce((sum: number, s: any) => sum + (s.fileSize || 0), 0)
          return {
            level,
            count: slices.length,
            totalSize,
            avgSize: totalSize / slices.length
          }
        }
        return null
      } catch (error) {
        console.warn(`加载Level ${level}统计失败:`, error)
        return null
      }
    })

    const statsResults = await Promise.all(statsPromises)

    // 更新LOD统计Map
    lodLevelStats.value.clear()
    statsResults.forEach(stat => {
      if (stat) {
        lodLevelStats.value.set(stat.level, {
          count: stat.count,
          totalSize: stat.totalSize,
          avgSize: stat.avgSize
        })
      }
    })

    // 加载当前选中层级的详细数据
    const result = await slicingService.getSliceMetadata(
      selectedTaskId.value,
      selectedLevel.value
    )
    // 使用新数据替换
    sliceMetadata.value = result || []
  } catch (error) {
    console.error('加载切片元数据失败:', error)
    // 确保出错时也清空数据
    sliceMetadata.value = []
    lodLevelStats.value.clear()
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
    slicingStrategy: 0,  // TileGenerationPipeline
    outputFormat: 'b3dm',  // 输出格式，默认b3dm
    textureStrategy: 2,  // Repack - 重新打包纹理（默认推荐）
    lodLevels: 3,
    divisions: 2,
    enableCompression: true,
    enableIncrementalUpdate: false,
    enableMeshDecimation: true,  // 启用网格简化
    generateTileset: true  // 生成 tileset.json
  }
  showCreateTaskDialog.value = true
}

const closeCreateTaskDialog = () => {
  showCreateTaskDialog.value = false
}

// 估算切片数量
const estimateSliceCount = (lodLevels: number, divisions: number = 2): string => {
  // 计算空间单元数：(2^divisions)² 个网格单元（2D分割）
  const spatialCells = Math.pow(Math.pow(2, divisions), 2)
  // 总切片数 = LOD级别数 × 空间单元数
  const count = lodLevels * spatialCells

  if (count >= 1000000) {
    return `${(count / 1000000).toFixed(1)}百万`
  } else if (count >= 1000) {
    return `${(count / 1000).toFixed(1)}千`
  }
  return count.toString()
}

const createTask = async () => {
  try {
    if (!taskForm.value.name || !taskForm.value.modelPath) {
      alert('请填写必填字段')
      return
    }

    // 验证参数范围
    if (taskForm.value.lodLevels > 5) {
      alert('LOD级别建议不超过5，过高会导致生成时间过长。')
      return
    }

    if (taskForm.value.divisions > 4) {
      alert('空间分割深度建议不超过4（最多256个空间单元），过高会导致内存不足。')
      return
    }

    // 检查预估切片数量
    const estimatedCount = taskForm.value.lodLevels * Math.pow(Math.pow(2, taskForm.value.divisions), 2)
    if (estimatedCount > 1000) {
      const confirmed = confirm(
        `预估将生成 ${estimatedCount} 个切片，处理时间可能较长。是否继续？\n\n` +
        `建议：减少 LOD 级别或降低空间分割深度`
      )
      if (!confirmed) {
        return
      }
    }

    // 将前端表单数据映射到后端期望的格式
    const requestData = {
      name: taskForm.value.name,
      sourceModelPath: taskForm.value.modelPath,
      modelType: 'General3DModel', // 默认模型类型
      outputPath: taskForm.value.outputPath || '', // 添加输出路径
      slicingConfig: {
        outputFormat: taskForm.value.outputFormat,  // 使用用户选择的输出格式
        coordinateSystem: 'EPSG:4326',  // 后端必需字段
        customSettings: '{}',  // 后端必需字段
        divisions: taskForm.value.divisions,  // 空间分割递归深度
        lodLevels: taskForm.value.lodLevels,  // LOD级别数量
        enableMeshDecimation: taskForm.value.enableMeshDecimation,  // 启用网格简化
        generateTileset: taskForm.value.generateTileset,  // 生成tileset.json
        compressOutput: taskForm.value.enableCompression,  // 压缩输出
        enableIncrementalUpdates: taskForm.value.enableIncrementalUpdate,  // 启用增量更新
        textureStrategy: taskForm.value.textureStrategy  // 纹理策略枚举
      }
    }

    console.log('发送的请求数据:', JSON.stringify(requestData, null, 2))
    await slicingService.createSlicingTask(requestData, userId)
    await loadTasks()
    closeCreateTaskDialog()
  } catch (error: any) {
    console.error('创建切片任务失败:', error)
    console.error('错误详情:', error.response?.data)
    console.error('错误状态:', error.response?.status)
    const errorMessage = error.response?.data?.message || error.message || '创建任务失败'
    alert(`创建任务失败: ${errorMessage}`)
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

    // 计算总切片数
    await calculateTotalSliceCount(taskId, currentTask.value.status)

    // 计算已处理切片数
    await calculateProcessedSliceCount(currentTask.value.status)

    // 计算总数据大小
    await calculateTotalDataSize(taskId, currentTask.value.status)

    showTaskDetailDialog.value = true
  } catch (error) {
    console.error('加载任务详情失败:', error)
  }
}

/**
  * 计算已处理切片数量
  * 根据任务状态从不同来源获取已处理数量
  * @param status 任务状态
  */
const calculateProcessedSliceCount = (status: string) => {
  try {
    // 定义状态处理策略映射
    const statusStrategies = {
      processing: () => taskProgress.value?.processedTiles || 0,
      completed: () => totalSliceCount.value || 0,
      failed: () => taskProgress.value?.processedTiles || 0,
      pending: () => 0,
      cancelled: () => taskProgress.value?.processedTiles || 0
    }

    // 获取对应状态的处理策略，默认使用进度信息
    const strategy = statusStrategies[status as keyof typeof statusStrategies] || (() => taskProgress.value?.processedTiles || 0)

    processedSliceCount.value = strategy()

    // 验证计算结果的合理性
    if (processedSliceCount.value < 0) {
      console.warn(`计算得到负数已处理切片数: ${processedSliceCount.value}，重置为0`)
      processedSliceCount.value = 0
    }

    // 对于已完成状态，确保已处理数不超过总切片数
    if (status === 'completed' && totalSliceCount.value && processedSliceCount.value > totalSliceCount.value) {
      console.warn(`已完成任务的已处理数(${processedSliceCount.value})超过总切片数(${totalSliceCount.value})，使用总切片数`)
      processedSliceCount.value = totalSliceCount.value
    }

  } catch (error) {
    console.warn('计算已处理切片数失败:', error)
    // 错误情况下重置为安全默认值
    processedSliceCount.value = 0
  }
}

// 计算总切片数
const calculateTotalSliceCount = async (taskId: string, status: string) => {
  totalSliceCount.value = 0

  try {
    if (status === 'completed' || status === 'failed') {
      // 对于已完成或已失败的任务，通过获取各层级切片元数据来统计总数
      let totalCount = 0
      let level = 0
      let hasSlices = true

      while (hasSlices && level <= (currentTask.value?.slicingConfig?.lodLevels || 10)) {
        try {
          const slices = await slicingService.getSliceMetadata(taskId, level)
          if (slices && slices.length > 0) {
            totalCount += slices.length
          } else {
            // 如果当前层级没有切片，检查是否还有更多层级
            // 如果连续几个层级都没有切片，我们可以提前结束
            // 但为了安全，我们检查到最大层级
            hasSlices = level < (currentTask.value?.slicingConfig?.lodLevels || 10)
          }
          level++
        } catch (error) {
          // 如果获取特定层级失败，尝试下一个层级
          level++
          if (level > (currentTask.value?.slicingConfig?.lodLevels || 10)) {
            hasSlices = false
          }
        }
      }

      totalSliceCount.value = totalCount
    } else if (status === 'processing' && taskProgress.value) {
      // 对于处理中的任务，使用进度信息中的总瓦片数
      totalSliceCount.value = taskProgress.value.totalTiles || 0
    } else {
      // 对于其他状态，使用任务中的总切片数（如果存在）
      totalSliceCount.value = currentTask.value?.totalSlices || 0
    }
  } catch (error) {
    console.warn('计算总切片数失败:', error)
    // 如果计算失败，尝试使用任务中的数据
    totalSliceCount.value = currentTask.value?.totalSlices || 0
  }
}

// 计算总数据大小
const calculateTotalDataSize = async (taskId: string, status: string) => {
  totalDataSize.value = 0

  // 只对已完成或已失败的任务计算数据大小
  if (status === 'completed' || status === 'failed') {
    try {
      // 获取所有层级的切片元数据并计算总大小
      // 从第0层开始尝试获取数据
      let totalSize = 0
      let level = 0
      let hasSlices = true

      while (hasSlices && level <= (currentTask.value?.slicingConfig?.maxLevel || 10)) {
        try {
          const slices = await slicingService.getSliceMetadata(taskId, level)
          if (slices && slices.length > 0) {
            slices.forEach((slice: any) => {
              totalSize += slice.fileSize || 0
            })
          } else {
            // 如果当前层级没有切片，检查是否还有更多层级
            hasSlices = false
          }
          level++
        } catch (error) {
          // 如果获取特定层级失败，尝试下一个层级或停止
          hasSlices = false
          break
        }
      }

      totalDataSize.value = totalSize
    } catch (error) {
      console.warn('计算任务数据大小失败:', error)
      // 如果计算失败，尝试使用任务中的其他数据
      totalDataSize.value = 0
    }
  }
}

const closeTaskDetailDialog = () => {
  showTaskDetailDialog.value = false
  currentTask.value = null
  taskProgress.value = null
  totalDataSize.value = 0
  totalSliceCount.value = 0
  processedSliceCount.value = 0
}

const cancelTask = async (taskId: string) => {
  if (confirm('确定要取消此任务吗?')) {
    try {
      await slicingService.cancelSlicingTask(taskId, userId)
      await loadTasks()
    } catch (error) {
      console.error('取消任务失败:', error)
    }
  }
}

const deleteTask = async (taskId: string) => {
  if (confirm('确定要删除此任务吗? 这将删除所有切片数据。')) {
    try {
      await slicingService.deleteSlicingTask(taskId, userId)
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

// 预览切片
const previewSlices = (task: any) => {
  previewTask.value = task
  showPreviewDialog.value = true
}

const closePreviewDialog = () => {
  showPreviewDialog.value = false
  previewTask.value = null
}

const onPreviewLoaded = (sliceCount: number) => {
  console.log(`切片预览加载完成，共${sliceCount}个切片`)
}

const onPreviewError = (error: string) => {
  console.error('切片预览加载失败:', error)
  alert(`切片预览加载失败: ${error}`)
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

.info-item .error-label {
  color: #c62828;
  font-weight: 600;
}

.info-item .error-value {
  color: #c62828;
  background: #ffebee;
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
  font-size: 0.85rem;
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

/* LOD层级统计面板 */
.lod-stats-panel {
  margin: 2rem 0;
  padding: 1.5rem;
  background: #f8f9fa;
  border-radius: 8px;
  border: 1px solid #e1e5e9;
}

.lod-stats-panel h3 {
  margin: 0 0 1.5rem 0;
  font-size: 1.1rem;
  color: #333;
}

.lod-stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 1rem;
}

.lod-stat-card {
  padding: 1.25rem;
  background: white;
  border: 2px solid #e1e5e9;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.lod-stat-card:hover {
  border-color: #007acc;
  box-shadow: 0 4px 8px rgba(0, 122, 204, 0.2);
  transform: translateY(-2px);
}

.lod-stat-card.active {
  border-color: #007acc;
  background: #f0f8ff;
  box-shadow: 0 4px 12px rgba(0, 122, 204, 0.3);
}

.lod-level-badge {
  display: inline-block;
  padding: 0.5rem 1rem;
  background: #007acc;
  color: white;
  border-radius: 6px;
  font-weight: bold;
  font-size: 1.1rem;
  margin-bottom: 1rem;
}

.lod-stat-card.active .lod-level-badge {
  background: #005999;
}

.lod-stat-content {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.lod-stat-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 0.9rem;
}

.lod-stat-label {
  color: #666;
}

.lod-stat-value {
  font-weight: 600;
  color: #333;
}

/* 切片网格增强样式 */
.slice-grid-section {
  margin-top: 2rem;
}

.slice-grid-header {
  margin-bottom: 1.5rem;
  padding-bottom: 1rem;
  border-bottom: 2px solid #e1e5e9;
}

.slice-grid-header h3 {
  margin: 0;
  font-size: 1.1rem;
  color: #333;
}

.slice-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 1rem;
  max-height: 600px;
  overflow-y: auto;
  padding: 0.5rem;
}

.slice-card-enhanced {
  padding: 1.25rem;
  border: 2px solid #e1e5e9;
  border-radius: 8px;
  background: #fafafa;
  transition: all 0.2s ease;
}

.slice-card-enhanced:hover {
  border-color: #007acc;
  box-shadow: 0 4px 8px rgba(0, 122, 204, 0.2);
  transform: translateY(-2px);
}

.slice-card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
  padding-bottom: 0.75rem;
  border-bottom: 1px solid #e1e5e9;
}

.slice-coord {
  font-weight: bold;
  color: #333;
  font-family: 'Courier New', monospace;
  font-size: 0.95rem;
}

.slice-level-badge {
  padding: 0.25rem 0.6rem;
  background: #007acc;
  color: white;
  border-radius: 4px;
  font-size: 0.8rem;
  font-weight: 600;
}

.slice-card-body {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.slice-info-row {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  font-size: 0.9rem;
}

.slice-info-label {
  color: #666;
  font-weight: 500;
  min-width: 80px;
}

.slice-info-value {
  color: #333;
  font-weight: 600;
  text-align: right;
  flex: 1;
}

.slice-info-value.bbox {
  font-family: 'Courier New', monospace;
  font-size: 0.75rem;
  word-break: break-word;
}

.slice-card-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
  padding-top: 0.75rem;
  border-top: 1px solid #e1e5e9;
}

.btn-icon-small {
  padding: 0.5rem 0.75rem;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  background: white;
  cursor: pointer;
  font-size: 1.1rem;
  transition: all 0.2s ease;
}

.btn-icon-small:hover {
  background: #007acc;
  border-color: #007acc;
  transform: scale(1.1);
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

/* 错误信息样式 */
.error-section {
  background: #ffebee;
  border: 1px solid #ef5350;
  border-radius: 8px;
  padding: 1.5rem !important;
}

.error-section h4 {
  color: #c62828;
  margin-bottom: 1rem;
}

.error-message-box {
  background: white;
  border-left: 4px solid #c62828;
  padding: 1rem;
  border-radius: 4px;
  color: #c62828;
  font-family: 'Courier New', monospace;
  font-size: 0.9rem;
  line-height: 1.6;
  word-break: break-word;
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
.input-with-button {
  display: flex;
  gap: 0.5rem;
}

.input-with-button .form-input {
  flex: 1;
}

.input-with-button .btn {
  white-space: nowrap;
  padding: 0.5rem 1rem;
}

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

.modal-content.fullscreen {
  width: 95vw;
  height: 90vh;
  max-width: none;
  max-height: none;
  display: flex;
  flex-direction: column;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem 1.5rem;
  border-bottom: 1px solid #e1e5e9;
}

.modal-header h3 {
  margin: 0;
  font-size: 1.25rem;
}

.btn-close {
  padding: 0.5rem;
  border: none;
  background: transparent;
  font-size: 1.5rem;
  cursor: pointer;
  color: #666;
  transition: color 0.2s ease;
}

.btn-close:hover {
  color: #dc3545;
}

.modal-body {
  flex: 1;
  overflow: hidden;
  position: relative;
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

/* 策略页面新样式 */
.strategy-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
}

.strategy-main-card {
  background: white;
  border-radius: 12px;
  padding: 2.5rem;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.strategy-icon-large {
  font-size: 4rem;
  text-align: center;
  margin-bottom: 1rem;
}

.strategy-description {
  font-size: 1.1rem;
  color: #666;
  margin-bottom: 2rem;
  line-height: 1.6;
  text-align: center;
}

.strategy-description a {
  color: #007acc;
  text-decoration: none;
}

.strategy-description a:hover {
  text-decoration: underline;
}

.pipeline-stages {
  display: flex;
  align-items: stretch;
  gap: 1rem;
  margin: 2rem 0;
}

.stage {
  flex: 1;
  background: #f8f9fa;
  border-radius: 8px;
  padding: 1.5rem;
}

.stage-number {
  width: 40px;
  height: 40px;
  background: #007acc;
  color: white;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: bold;
  font-size: 1.2rem;
  margin-bottom: 1rem;
}

.stage h4 {
  margin: 0 0 0.5rem 0;
  color: #333;
}

.stage p {
  color: #666;
  margin-bottom: 1rem;
  font-size: 0.9rem;
}

.stage ul {
  margin: 0;
  padding-left: 1.2rem;
  font-size: 0.85rem;
  color: #666;
}

.stage ul li {
  margin: 0.3rem 0;
}

.stage-arrow {
  font-size: 2rem;
  color: #007acc;
  display: flex;
  align-items: center;
}

.strategy-advantages {
  margin: 2rem 0;
}

.strategy-advantages h4 {
  margin-bottom: 1rem;
  color: #333;
}

.advantages-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 1rem;
}

.advantage {
  background: #f0f7ff;
  border-left: 4px solid #007acc;
  padding: 1rem;
  border-radius: 4px;
}

.advantage-icon {
  font-size: 1.5rem;
  margin-right: 0.5rem;
}

.advantage strong {
  display: block;
  margin-bottom: 0.3rem;
  color: #333;
}

.advantage p {
  margin: 0;
  font-size: 0.85rem;
  color: #666;
}

.deprecation-notice {
  background: #fff3cd;
  border: 1px solid #ffc107;
  border-radius: 8px;
  padding: 1.5rem;
  margin-top: 2rem;
}

.deprecation-notice h4 {
  margin: 0 0 0.5rem 0;
  color: #856404;
}

.deprecation-notice p {
  margin: 0;
  color: #856404;
  line-height: 1.6;
}

/* 表单提示样式 */
.form-hint {
  display: block;
  font-size: 0.85rem;
  color: #666;
  margin-top: 0.25rem;
  line-height: 1.4;
}
</style>
