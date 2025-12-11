<template>
  <div class="slice-preview-page">
    <!-- 顶部控制栏 -->
    <div class="preview-header">
      <button @click="goBack" class="btn-back" title="返回切片管理">
        <span class="icon">←</span>
        <span class="text">返回</span>
      </button>

      <div class="task-info">
        <h1 class="task-title">{{ taskInfo?.modelFileName || '切片预览' }}</h1>
        <div class="task-meta">
          <span v-if="taskInfo" class="status-badge" :class="getStatusClass(taskInfo.status)">
            {{ getStatusText(taskInfo.status) }}
          </span>
          <span v-if="taskInfo?.slicingConfig?.strategy" class="strategy-badge">
            {{ taskInfo.slicingConfig.strategy }}
          </span>
        </div>
      </div>

      <div class="header-actions">
        <button @click="toggleStats" class="btn-action" :title="showStats ? '隐藏统计' : '显示统计'">
          <span class="icon">📊</span>
        </button>
        <button @click="toggleFullscreen" class="btn-action" title="全屏">
          <span class="icon">{{ isFullscreen ? '🗗' : '🗖' }}</span>
        </button>
      </div>
    </div>

    <!-- 统计信息面板 -->
    <div v-if="showStats && taskInfo" class="stats-panel">
      <div class="stats-grid">
        <div class="stat-item">
          <span class="stat-icon">🧩</span>
          <div class="stat-content">
            <span class="stat-label">总切片数</span>
            <span class="stat-value">{{ taskInfo.totalSlices || 0 }}</span>
          </div>
        </div>
        <div class="stat-item">
          <span class="stat-icon">📐</span>
          <div class="stat-content">
            <span class="stat-label">LOD层级</span>
            <span class="stat-value">{{ (taskInfo.slicingConfig?.maxLevel || 0) + 1 }}</span>
          </div>
        </div>
        <div class="stat-item">
          <span class="stat-icon">💾</span>
          <div class="stat-content">
            <span class="stat-label">数据大小</span>
            <span class="stat-value">{{ formatFileSize(taskInfo.totalDataSize || 0) }}</span>
          </div>
        </div>
        <div class="stat-item">
          <span class="stat-icon">⏱️</span>
          <div class="stat-content">
            <span class="stat-label">处理耗时</span>
            <span class="stat-value">{{ formatDuration(taskInfo.processingTimeMs || 0) }}</span>
          </div>
        </div>
      </div>
    </div>

    <!-- 3D查看器容器 -->
    <div class="viewer-container">
      <CesiumViewer
        v-if="!loading && sceneObject"
        :sceneObjects="[sceneObject]"
        :showInfo="true"
        :initialPosition="initialCameraPosition"
        @objectLoaded="onObjectLoaded"
        @error="onViewerError"
      />

      <!-- 加载状态 -->
      <div v-if="loading" class="loading-overlay">
        <div class="loading-content">
          <div class="spinner"></div>
          <p>加载切片数据中...</p>
        </div>
      </div>

      <!-- 错误状态 -->
      <div v-if="error" class="error-overlay">
        <div class="error-content">
          <div class="error-icon">⚠️</div>
          <h2>加载失败</h2>
          <p>{{ error }}</p>
          <button @click="reload" class="btn btn-primary">
            重新加载
          </button>
        </div>
      </div>

      <!-- 无数据状态 -->
      <div v-if="!loading && !error && !sceneObject" class="no-data-overlay">
        <div class="no-data-content">
          <div class="no-data-icon">📂</div>
          <h2>无切片数据</h2>
          <p>当前任务没有可预览的切片数据</p>
          <button @click="goBack" class="btn btn-secondary">
            返回列表
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * 切片预览独立页面组件
 *
 * 功能说明:
 * - 提供独立的全屏切片预览体验
 * - 基于Cesium的3D地球展示切片数据
 * - 显示切片统计信息和元数据
 * - 支持全屏模式
 * - 提供返回导航功能
 *
 * 技术栈: Vue 3 + TypeScript + Cesium
 * 作者: liyq
 * 创建时间: 2025-12-10
 */
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { slicingService } from '@/services/api'
import { useMessage } from '@/composables/useMessage'
import CesiumViewer from '@/components/CesiumViewer.vue'

// ==================== 组合式API ====================

const router = useRouter()
const route = useRoute()
const { success: showSuccess, error: showError } = useMessage()

// ==================== 响应式状态 ====================

const loading = ref(true)
const error = ref<string | null>(null)
const taskInfo = ref<any>(null)
const showStats = ref(true)
const isFullscreen = ref(false)

// ==================== 计算属性 ====================

/**
 * 计算场景对象配置
 */
const sceneObject = computed(() => {
  if (!taskInfo.value || !taskInfo.value.outputPath) {
    return null
  }

  // 确保路径格式正确
  let tilesetPath = taskInfo.value.outputPath.trim()
  tilesetPath = tilesetPath.replace(/[\/\\]+$/, '')

  if (!tilesetPath.endsWith('tileset.json')) {
    tilesetPath = `${tilesetPath}/tileset.json`
  }

  return {
    id: `slice_preview_${taskInfo.value.id}`,
    name: taskInfo.value.modelFileName || '切片预览',
    displayPath: tilesetPath,
    position: [116.39, 39.91, 100], // 默认位置：北京，高度100米
    rotation: { x: 0, y: 0, z: 0 },
    scale: { x: 1, y: 1, z: 1 },
    slicingTaskId: taskInfo.value.id,
    slicingTaskStatus: taskInfo.value.status,
    slicingOutputPath: taskInfo.value.outputPath
  }
})

/**
 * 初始相机位置
 */
const initialCameraPosition = computed(() => {
  return {
    longitude: 116.39,
    latitude: 39.91,
    height: 15000000 // 15000km 高度
  }
})

// ==================== 业务逻辑方法 ====================

/**
 * 加载切片任务详情
 */
const loadTaskDetails = async () => {
  const taskId = route.params.taskId as string

  if (!taskId) {
    error.value = '切片任务ID无效'
    loading.value = false
    return
  }

  try {
    console.log('[SlicePreviewPage] 加载切片任务详情, 任务ID:', taskId)
    taskInfo.value = await slicingService.getSlicingTask(taskId)
    console.log('[SlicePreviewPage] 任务信息:', taskInfo.value)

    // 检查任务状态
    if (taskInfo.value.status !== 'Completed') {
      showError('切片任务尚未完成，无法预览')
    }

    loading.value = false
  } catch (err) {
    console.error('[SlicePreviewPage] 加载切片任务失败:', err)
    error.value = err instanceof Error ? err.message : '加载切片任务失败'
    loading.value = false
    showError('加载切片任务失败')
  }
}

/**
 * 重新加载
 */
const reload = () => {
  error.value = null
  loading.value = true
  loadTaskDetails()
}

/**
 * 返回切片管理页面
 */
const goBack = () => {
  router.push({ name: 'Slicing' })
}

/**
 * 切换统计面板
 */
const toggleStats = () => {
  showStats.value = !showStats.value
}

/**
 * 切换全屏模式
 */
const toggleFullscreen = () => {
  if (!document.fullscreenElement) {
    document.documentElement.requestFullscreen()
    isFullscreen.value = true
  } else {
    if (document.exitFullscreen) {
      document.exitFullscreen()
      isFullscreen.value = false
    }
  }
}

/**
 * 监听全屏状态变化
 */
const handleFullscreenChange = () => {
  isFullscreen.value = !!document.fullscreenElement
}

/**
 * 对象加载回调
 */
const onObjectLoaded = (obj: any, success: boolean) => {
  console.log('[SlicePreviewPage] 切片加载结果:', { obj, success })
  if (!success) {
    showError('切片数据加载失败')
  } else {
    showSuccess('切片数据加载成功')
  }
}

/**
 * 查看器错误回调
 */
const onViewerError = (err: Error) => {
  console.error('[SlicePreviewPage] Cesium查看器错误:', err)
  showError('查看器错误: ' + err.message)
}

/**
 * 格式化文件大小
 */
const formatFileSize = (bytes: number): string => {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i]
}

/**
 * 格式化持续时间
 */
const formatDuration = (ms: number): string => {
  if (ms < 1000) return `${ms}ms`
  const seconds = Math.floor(ms / 1000)
  if (seconds < 60) return `${seconds}秒`
  const minutes = Math.floor(seconds / 60)
  const remainingSeconds = seconds % 60
  return `${minutes}分${remainingSeconds}秒`
}

/**
 * 获取状态样式类
 */
const getStatusClass = (status: string): string => {
  const statusMap: Record<string, string> = {
    'Pending': 'status-pending',
    'Processing': 'status-processing',
    'Completed': 'status-completed',
    'Failed': 'status-failed'
  }
  return statusMap[status] || ''
}

/**
 * 获取状态文本
 */
const getStatusText = (status: string): string => {
  const textMap: Record<string, string> = {
    'Pending': '等待中',
    'Processing': '处理中',
    'Completed': '已完成',
    'Failed': '失败'
  }
  return textMap[status] || status
}

// ==================== 生命周期钩子 ====================

onMounted(() => {
  console.log('[SlicePreviewPage] 组件挂载, 开始加载切片任务')
  loadTaskDetails()

  // 监听全屏状态变化
  document.addEventListener('fullscreenchange', handleFullscreenChange)
})

onUnmounted(() => {
  // 清理全屏事件监听
  document.removeEventListener('fullscreenchange', handleFullscreenChange)
})
</script>

<style scoped>
/**
 * 切片预览页面样式
 * 采用全屏布局，提供沉浸式的3D切片浏览体验
 */

.slice-preview-page {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  display: flex;
  flex-direction: column;
  background: #000;
  z-index: 1000;
}

/* 顶部控制栏 */
.preview-header {
  display: flex;
  align-items: center;
  gap: 1.5rem;
  padding: 1rem 1.5rem;
  background: rgba(0, 0, 0, 0.8);
  backdrop-filter: blur(10px);
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  z-index: 10;
}

.btn-back {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.6rem 1.2rem;
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 8px;
  color: white;
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 0.95rem;
  font-weight: 500;
}

.btn-back:hover {
  background: rgba(255, 255, 255, 0.2);
  border-color: rgba(255, 255, 255, 0.3);
  transform: translateX(-3px);
}

.btn-back .icon {
  font-size: 1.2rem;
}

.task-info {
  flex: 1;
  min-width: 0;
}

.task-title {
  margin: 0 0 0.5rem 0;
  font-size: 1.5rem;
  font-weight: 700;
  color: white;
  line-height: 1.2;
}

.task-meta {
  display: flex;
  gap: 0.75rem;
  align-items: center;
}

.status-badge,
.strategy-badge {
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.status-badge.status-pending {
  background: rgba(255, 193, 7, 0.2);
  color: #ffc107;
  border: 1px solid rgba(255, 193, 7, 0.4);
}

.status-badge.status-processing {
  background: rgba(33, 150, 243, 0.2);
  color: #2196f3;
  border: 1px solid rgba(33, 150, 243, 0.4);
}

.status-badge.status-completed {
  background: rgba(76, 175, 80, 0.2);
  color: #4caf50;
  border: 1px solid rgba(76, 175, 80, 0.4);
}

.status-badge.status-failed {
  background: rgba(244, 67, 54, 0.2);
  color: #f44336;
  border: 1px solid rgba(244, 67, 54, 0.4);
}

.strategy-badge {
  background: rgba(156, 39, 176, 0.2);
  color: #ce93d8;
  border: 1px solid rgba(156, 39, 176, 0.4);
}

.header-actions {
  display: flex;
  gap: 0.75rem;
}

.btn-action {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  padding: 0;
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 8px;
  color: white;
  cursor: pointer;
  transition: all 0.2s ease;
}

.btn-action:hover {
  background: rgba(255, 255, 255, 0.2);
  border-color: rgba(255, 255, 255, 0.3);
  transform: translateY(-2px);
}

.btn-action .icon {
  font-size: 1.2rem;
}

/* 统计信息面板 */
.stats-panel {
  padding: 1rem 1.5rem;
  background: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(10px);
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  z-index: 9;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
}

.stat-item {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1rem;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  transition: all 0.2s ease;
}

.stat-item:hover {
  background: rgba(255, 255, 255, 0.08);
  border-color: rgba(255, 255, 255, 0.2);
}

.stat-icon {
  font-size: 2rem;
  flex-shrink: 0;
}

.stat-content {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.stat-label {
  font-size: 0.75rem;
  color: rgba(255, 255, 255, 0.6);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.stat-value {
  font-size: 1.25rem;
  font-weight: 700;
  color: white;
  font-family: monospace;
}

/* 查看器容器 */
.viewer-container {
  flex: 1;
  position: relative;
  overflow: hidden;
}

/* 加载状态 */
.loading-overlay {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.95);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 100;
}

.loading-content {
  text-align: center;
  color: white;
}

.spinner {
  width: 60px;
  height: 60px;
  border: 4px solid rgba(255, 255, 255, 0.3);
  border-top-color: white;
  border-radius: 50%;
  animation: spin 1s linear infinite;
  margin: 0 auto 1.5rem;
}

.loading-content p {
  margin: 0;
  font-size: 1.1rem;
  color: rgba(255, 255, 255, 0.9);
}

/* 错误状态 */
.error-overlay {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.95);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 100;
}

.error-content {
  text-align: center;
  color: white;
  max-width: 500px;
  padding: 2rem;
}

.error-icon {
  font-size: 4rem;
  margin-bottom: 1rem;
}

.error-content h2 {
  margin: 0 0 1rem 0;
  font-size: 1.5rem;
  color: #f87171;
}

.error-content p {
  margin: 0 0 2rem 0;
  font-size: 1rem;
  color: rgba(255, 255, 255, 0.8);
  line-height: 1.6;
}

/* 无数据状态 */
.no-data-overlay {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.95);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 100;
}

.no-data-content {
  text-align: center;
  color: white;
  max-width: 500px;
  padding: 2rem;
}

.no-data-icon {
  font-size: 4rem;
  margin-bottom: 1rem;
}

.no-data-content h2 {
  margin: 0 0 1rem 0;
  font-size: 1.5rem;
  color: white;
}

.no-data-content p {
  margin: 0 0 2rem 0;
  font-size: 1rem;
  color: rgba(255, 255, 255, 0.7);
  line-height: 1.6;
}

/* 按钮样式 */
.btn {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.6rem 1.2rem;
  border: 1px solid transparent;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 0.9rem;
  font-weight: 600;
  text-decoration: none;
}

.btn-primary {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  border: none;
  box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4);
}

.btn-primary:hover {
  transform: translateY(-2px);
  box-shadow: 0 6px 20px rgba(102, 126, 234, 0.6);
}

.btn-secondary {
  background: rgba(255, 255, 255, 0.1);
  color: white;
  border-color: rgba(255, 255, 255, 0.2);
}

.btn-secondary:hover {
  background: rgba(255, 255, 255, 0.2);
  border-color: rgba(255, 255, 255, 0.3);
}

/* 动画 */
@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

/* 响应式设计 */
@media (max-width: 768px) {
  .preview-header {
    flex-wrap: wrap;
    gap: 1rem;
  }

  .task-info {
    flex-basis: 100%;
    order: 2;
  }

  .header-actions {
    order: 1;
  }

  .task-title {
    font-size: 1.25rem;
  }

  .stats-grid {
    grid-template-columns: 1fr;
  }

  .btn-back .text {
    display: none;
  }
}
</style>
