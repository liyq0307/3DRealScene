<template>
  <div class="slice-preview">
    <div v-if="loading" class="loading-state">
      <div class="spinner"></div>
      <p>加载切片预览中...</p>
    </div>

    <div v-else-if="error" class="error-state">
      <span class="error-icon">⚠️</span>
      <p>{{ error }}</p>
      <button @click="reload" class="btn btn-primary">重新加载</button>
    </div>

    <div v-else class="preview-container">
      <!-- 切片统计信息 -->
      <div class="stats-panel">
        <h3>切片统计</h3>
        <div class="stats-grid">
          <div class="stat-item">
            <span class="stat-label">总切片数</span>
            <span class="stat-value">{{ totalSlices }}</span>
          </div>
          <div class="stat-item">
            <span class="stat-label">LOD层级数</span>
            <span class="stat-value">{{ maxLevel + 1 }}</span>
          </div>
          <div class="stat-item">
            <span class="stat-label">总数据大小</span>
            <span class="stat-value">{{ formatFileSize(totalSize) }}</span>
          </div>
          <div class="stat-item">
            <span class="stat-label">切片策略</span>
            <span class="stat-value">{{ strategy }}</span>
          </div>
        </div>
      </div>

      <!-- 3D预览区域 -->
      <div class="viewer-panel">
        <div class="viewer-header">
          <h3>🌐 3D切片预览</h3>
          <div class="viewer-info">
            <span>输出路径: {{ outputPath }}</span>
            <span v-if="sceneObject" class="debug-info">| Tileset路径: {{ sceneObject.displayPath }}</span>
          </div>
        </div>
        <div class="viewer-container">
          <Mars3DViewer
            v-if="sceneObject"
            :sceneObjects="[sceneObject]"
            :showInfo="true"
            :initialPosition="initialCameraPosition"
            @objectLoaded="onObjectLoaded"
            @error="onViewerError"
          />
          <div v-else class="no-data">
            <p>⚠️ 无法加载切片数据，请检查输出路径是否正确</p>
            <p class="path-info">当前路径: {{ outputPath }}</p>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { slicingService } from '@/services/api'
import Mars3DViewer from './Mars3DViewer.vue'

// Props
interface Props {
  taskId: string          // 切片任务ID
  outputPath?: string     // 切片输出路径
  autoLoad?: boolean      // 是否自动加载
}

const props = withDefaults(defineProps<Props>(), {
  autoLoad: true
})

// Emits
const emit = defineEmits<{
  loaded: [sliceCount: number]
  error: [error: string]
}>()

// 状态
const loading = ref(false)
const error = ref('')
const taskInfo = ref<any>(null)

// 计算属性
const totalSlices = computed(() => {
  return taskInfo.value?.totalSlices || 0
})

const totalSize = computed(() => {
  return taskInfo.value?.totalDataSize || 0
})

const maxLevel = computed(() => {
  return taskInfo.value?.slicingConfig?.maxLevel || 0
})

const strategy = computed(() => {
  return taskInfo.value?.slicingConfig?.strategy || 'Unknown'
})

// 计算场景对象配置
const sceneObject = computed(() => {
  if (!props.outputPath) {
    console.warn('[SlicePreview] 没有提供输出路径')
    return null
  }

  // 确保路径格式正确
  let tilesetPath = props.outputPath.trim()

  // 移除末尾的斜杠或反斜杠
  tilesetPath = tilesetPath.replace(/[\/\\]+$/, '')

  // 如果路径不以 tileset.json 结尾，添加它
  if (!tilesetPath.endsWith('tileset.json')) {
    tilesetPath = `${tilesetPath}/tileset.json`
  }

  console.log('[SlicePreview] 构建场景对象:', {
    taskId: props.taskId,
    outputPath: props.outputPath,
    tilesetPath: tilesetPath
  })

  return {
    id: `slice_preview_${props.taskId}`,
    name: `切片预览`,
    displayPath: tilesetPath,
    position: [116.39, 39.91, 100] as [number, number, number], // 默认位置：北京，高度100米（避免在地面以下）
    rotation: { x: 0, y: 0, z: 0 },
    scale: { x: 1, y: 1, z: 1 },
    slicingTaskId: props.taskId,
    slicingTaskStatus: 'Completed',
    slicingOutputPath: props.outputPath
  }
})

const outputPath = computed(() => {
  return props.outputPath || '未设置'
})

const initialCameraPosition = computed(() => {
  // 可以根据模型的包围盒计算合适的相机位置
  return {
    longitude: 116.39,
    latitude: 39.91,
    height: 10000 // 10km 高度，适合查看切片数据
  }
})

// 方法
const formatFileSize = (bytes: number): string => {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i]
}


const onObjectLoaded = (obj: any, success: boolean) => {
  console.log('[SlicePreview] 对象加载结果:', { obj, success })
  if (!success) {
    error.value = '3D模型加载失败，请检查输出路径是否正确'
  }
}

const onViewerError = (err: Error) => {
  console.error('[SlicePreview] Cesium查看器错误:', err)
  error.value = `Cesium查看器错误: ${err.message}`
}

const loadSliceData = async () => {
  if (!props.taskId) {
    error.value = '无效的任务ID'
    return
  }

  loading.value = true
  error.value = ''

  try {
    // 仅加载任务详情
    taskInfo.value = await slicingService.getSlicingTask(props.taskId)
    console.log('[SlicePreview] 任务信息:', taskInfo.value)

    emit('loaded', totalSlices.value)
  } catch (err: any) {
    const errorMsg = err.response?.data?.message || err.message || '加载切片数据失败'
    error.value = errorMsg
    console.error('[SlicePreview] 加载失败:', err)
    emit('error', errorMsg)
  } finally {
    loading.value = false
  }
}

const reload = () => {
  loadSliceData()
}

// 监听taskId变化
watch(() => props.taskId, () => {
  if (props.taskId && props.autoLoad) {
    loadSliceData()
  }
}, { immediate: true })

// 生命周期
onMounted(() => {
  if (props.autoLoad && props.taskId) {
    loadSliceData()
  }
})
</script>

<style scoped>
.slice-preview {
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
}

.loading-state,
.error-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 4rem;
  text-align: center;
}

.spinner {
  width: 50px;
  height: 50px;
  border: 4px solid #f3f3f3;
  border-top: 4px solid #007acc;
  border-radius: 50%;
  animation: spin 1s linear infinite;
  margin-bottom: 1rem;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

.error-icon {
  font-size: 3rem;
  margin-bottom: 1rem;
}

.error-state p {
  color: #dc3545;
  margin-bottom: 1rem;
}

.preview-container {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  overflow-y: auto;
  padding: 1rem;
}

/* 统计面板 */
.stats-panel {
  background: white;
  padding: 1.5rem;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.stats-panel h3 {
  margin: 0 0 1rem 0;
  font-size: 1.1rem;
  color: #333;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
}

.stat-item {
  display: flex;
  flex-direction: column;
  padding: 1rem;
  background: #f8f9fa;
  border-radius: 6px;
}

.stat-label {
  font-size: 0.85rem;
  color: #666;
  margin-bottom: 0.5rem;
}

.stat-value {
  font-size: 1.5rem;
  font-weight: bold;
  color: #007acc;
}

/* 3D查看器面板 */
.viewer-panel {
  background: white;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
  overflow: hidden;
  display: flex;
  flex-direction: column;
  min-height: 500px;
}

.viewer-header {
  padding: 1rem 1.5rem;
  border-bottom: 1px solid #e1e5e9;
  background: #f8f9fa;
}

.viewer-header h3 {
  margin: 0 0 0.5rem 0;
  font-size: 1.1rem;
  color: #333;
}

.viewer-info {
  font-size: 0.85rem;
  color: #666;
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.debug-info {
  color: #999;
  font-family: monospace;
  font-size: 0.75rem;
}

.viewer-container {
  flex: 1;
  position: relative;
  min-height: 450px;
}

.no-data {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  padding: 2rem;
  text-align: center;
}

.no-data p {
  color: #666;
  margin: 0.5rem 0;
}

.path-info {
  font-family: monospace;
  font-size: 0.85rem;
  color: #999;
  word-break: break-all;
}
</style>
