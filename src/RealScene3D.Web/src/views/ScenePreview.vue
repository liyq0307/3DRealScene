<template>
  <div class="scene-preview">
    <!-- 顶部控制栏 -->
    <div class="preview-header">
      <button @click="goBack" class="btn-back" title="返回场景列表">
        <span class="icon">←</span>
        <span class="text">返回</span>
      </button>

      <div class="scene-info">
        <h1 class="scene-title">{{ currentScene?.name || '加载中...' }}</h1>
        <p v-if="currentScene?.description" class="scene-description">
          {{ currentScene.description }}
        </p>
      </div>

      <div class="header-actions">
        <button v-if="hasUnsupportedModels" @click="convertModelsToTiles" class="btn-action" title="转换为3D Tiles">
          <span class="icon">🔄</span>
          <span class="text">转换模型</span>
        </button>
        <button @click="toggleFullscreen" class="btn-action" title="全屏">
          <span class="icon">{{ isFullscreen ? '🗗' : '🗖' }}</span>
        </button>
      </div>
    </div>

    <!-- 动态渲染器容器 -->
    <div class="viewer-container">
      <!-- Three.js 查看器 (用于通用3D模型) -->
      <ThreeViewer
        v-if="!loading && sceneObjects.length > 0 && sceneRenderEngine === 'threejs'"
        :scene-objects="sceneObjects"
        :show-info="true"
        :background-color="'#1a1a1a'"
        :enable-shadows="true"
        :enable-anti-alias="true"
        @ready="onThreeJSReady"
        @error="onThreeJSError"
        @objectLoaded="onThreeJSObjectLoaded"
      />

      <!-- Mars3D 3D地球查看器 (用于地理空间数据) -->
      <Mars3DViewer
        v-else-if="!loading && sceneObjects.length > 0 && sceneRenderEngine === 'cesium'"
        :show-info="true"
        :scene-objects="sceneObjects"
        :show-basemap="true"
        @ready="onMars3DReady"
        @error="onMars3DError"
      />
    </div>

    <!-- 加载状态 -->
    <div v-if="loading" class="loading-overlay">
      <div class="loading-content">
        <div class="spinner"></div>
        <p>加载场景中...</p>
      </div>
    </div>

    <!-- 错误状态 -->
    <div v-if="error" class="error-overlay">
      <div class="error-content">
        <div class="error-icon">⚠️</div>
        <h2>加载失败</h2>
        <p>{{ error }}</p>
        <button @click="goBack" class="btn btn-primary">
          返回场景列表
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * 场景预览页面组件
 *
 * 功能说明：
 * - 提供独立的全屏场景预览体验
 * - 基于Cesium的3D地球展示场景对象
 * - 支持模型格式转换
 * - 支持全屏模式
 * - 提供返回导航功能
 *
 * 技术栈：Vue 3 + TypeScript + Cesium
 * 作者：liyq
 * 创建时间：2025-12-08
 */
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { sceneService } from '../services/api'
import { useMessage } from '@/composables/useMessage'
import Mars3DViewer from '@/components/Mars3DViewer.vue'
import ThreeViewer from '@/components/ThreeViewer.vue'

// ==================== 组合式API ====================

const router = useRouter()
const route = useRoute()
const { success: showSuccess, error: showError } = useMessage()

// ==================== 响应式状态 ====================

const loading = ref(true)
const error = ref<string | null>(null)
const currentScene = ref<any>(null)
const sceneObjects = ref<any[]>([])
const isFullscreen = ref(false)

// ==================== 计算属性 ====================

/**
 * 使用场景指定的渲染引擎
 * 优先使用场景的renderEngine字段，如果没有则回退到自动检测
 */
const sceneRenderEngine = computed(() => {
  // 优先使用场景指定的渲染引擎
  if (currentScene.value && currentScene.value.renderEngine) {
    const engine = currentScene.value.renderEngine
    return engine === 'ThreeJS' ? 'threejs' : 'cesium'
  }

  // 如果场景没有指定，回退到根据对象格式自动检测
  if (!sceneObjects.value || sceneObjects.value.length === 0) return 'cesium'

  // Three.js优先处理的格式
  const threeJSFormats = ['obj', 'fbx', 'dae', 'stl', '3ds', 'blend', 'ply']

  // Cesium专属格式
  const cesiumOnlyFormats = ['json', 'tiles', 'osgb', 'las', 'laz', 'e57']

  let hasThreeJSFormat = false
  let hasCesiumOnlyFormat = false

  for (const obj of sceneObjects.value) {
    if (!obj.displayPath) continue

    const fileExt = obj.displayPath.split('?')[0].split('.').pop()?.toLowerCase()

    if (threeJSFormats.includes(fileExt || '')) {
      hasThreeJSFormat = true
    }

    if (cesiumOnlyFormats.includes(fileExt || '')) {
      hasCesiumOnlyFormat = true
    }

    // 如果有完成的切片任务,优先使用Cesium
    if (obj.slicingTaskId && obj.slicingTaskStatus === 'Completed') {
      hasCesiumOnlyFormat = true
    }
  }

  // 如果只有Three.js格式,使用Three.js
  if (hasThreeJSFormat && !hasCesiumOnlyFormat) {
    return 'threejs'
  }

  // 其他情况使用Cesium
  return 'cesium'
})

/**
 * 检查是否有不支持的模型格式
 */
const hasUnsupportedModels = computed(() => {
  if (!sceneObjects.value || sceneObjects.value.length === 0) return false

  // 如果使用Three.js渲染,大部分格式都支持
  if (sceneRenderEngine.value === 'threejs') {
    return false
  }

  // Cesium场景下,检查需要切片的格式
  const needsSlicingFormats = ['osgb', 'las', 'laz', 'e57']

  return sceneObjects.value.some(obj => {
    if (!obj.displayPath) return false

    const fileExt = obj.displayPath.split('?')[0].split('.').pop()?.toLowerCase()

    // 如果是需要切片的格式，并且没有完成的切片任务，则认为是不支持的
    if (needsSlicingFormats.includes(fileExt || '')) {
      return !obj.slicingTaskId || obj.slicingTaskStatus !== 'Completed'
    }

    return false
  })
})

// ==================== 业务逻辑方法 ====================

/**
 * 加载场景详情
 */
const loadSceneDetails = async () => {
  const sceneId = route.params.id as string

  if (!sceneId) {
    error.value = '场景ID无效'
    loading.value = false
    return
  }

  try {
    console.log('[ScenePreview] 加载场景详情，ID:', sceneId)

    // 加载场景数据
    currentScene.value = await sceneService.getScene(sceneId)

    console.log('[ScenePreview] 场景数据加载成功:', currentScene.value)

    // 提取场景对象
    if (currentScene.value.sceneObjects && currentScene.value.sceneObjects.length > 0) {
      sceneObjects.value = currentScene.value.sceneObjects
      console.log('[ScenePreview] 场景对象数量:', sceneObjects.value.length)
    } else {
      sceneObjects.value = []
      console.warn('[ScenePreview] 场景没有对象数据')
    }

    loading.value = false
  } catch (err) {
    console.error('[ScenePreview] 加载场景详情失败:', err)
    error.value = err instanceof Error ? err.message : '加载场景失败'
    loading.value = false
    showError('加载场景详情失败')
  }
}

/**
 * 返回场景列表
 */
const goBack = () => {
  router.push({ name: 'Scenes' })
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
 * 转换模型为3D Tiles格式
 */
const convertModelsToTiles = async () => {
  if (!sceneObjects.value || sceneObjects.value.length === 0) {
    showError('没有可转换的模型')
    return
  }

  const convertibleFormats = ['obj', 'fbx', 'dae', 'stl', '3ds', 'blend', 'ply', 'las', 'laz', 'e57']

  // 找出需要转换的模型
  const modelsToConvert = sceneObjects.value.filter(obj => {
    if (!obj.displayPath) return false
    const fileExt = obj.displayPath.split('?')[0].split('.').pop()?.toLowerCase()
    return convertibleFormats.includes(fileExt || '') &&
           (!obj.slicingTaskId || obj.slicingTaskStatus !== 'Completed')
  })

  if (modelsToConvert.length === 0) {
    showError('没有需要转换的模型')
    return
  }

  if (confirm(`确定要为 ${modelsToConvert.length} 个模型创建切片任务吗？`)) {
    showSuccess(`准备为 ${modelsToConvert.length} 个模型创建切片任务（功能待实现）`)
    // TODO: 调用切片服务API创建切片任务
  }
}

/**
 * Mars3D就绪回调
 */
const onMars3DReady = (map: any) => {
  console.log('[ScenePreview] Mars3D地球初始化成功', map)
  showSuccess('Mars3D 3D地球加载成功')
}

/**
 * Mars3D错误回调
 */
const onMars3DError = (err: Error) => {
  console.error('[ScenePreview] Mars3D初始化失败:', err)
  showError('Mars3D地球加载失败: ' + err.message)
}

/**
 * Three.js就绪回调
 */
const onThreeJSReady = (viewerData: any) => {
  console.log('[ScenePreview] Three.js场景初始化成功', viewerData)
  showSuccess('Three.js 场景加载成功')
}

/**
 * Three.js对象加载回调
 */
const onThreeJSObjectLoaded = (object: any) => {
  console.log('[ScenePreview] Three.js对象加载成功', object)
}

/**
 * Three.js错误回调
 */
const onThreeJSError = (err: Error) => {
  console.error('[ScenePreview] Three.js初始化失败:', err)
  showError('Three.js场景加载失败: ' + err.message)
}

/**
 * 监听全屏状态变化
 */
const handleFullscreenChange = () => {
  isFullscreen.value = !!document.fullscreenElement
}

// ==================== 生命周期钩子 ====================

onMounted(() => {
  console.log('[ScenePreview] 组件挂载，开始加载场景')
  loadSceneDetails()

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
 * 场景预览页面样式
 * 采用全屏布局，提供沉浸式的3D场景浏览体验
 */

.scene-preview {
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

.scene-info {
  flex: 1;
  min-width: 0;
}

.scene-title {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 700;
  color: white;
  line-height: 1.2;
}

.scene-description {
  margin: 0.25rem 0 0 0;
  font-size: 0.9rem;
  color: rgba(255, 255, 255, 0.7);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.header-actions {
  display: flex;
  gap: 0.75rem;
}

.btn-action {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.6rem 1rem;
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 8px;
  color: white;
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 0.9rem;
}

.btn-action:hover {
  background: rgba(255, 255, 255, 0.2);
  border-color: rgba(255, 255, 255, 0.3);
  transform: translateY(-2px);
}

.btn-action .icon {
  font-size: 1.2rem;
}



/* Cesium查看器容器 */
.viewer-container {
  flex: 1;
  position: relative;
  overflow: hidden;
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
  background: rgba(255, 255, 255, 0.9);
  color: #666;
  border-color: #e1e5e9;
}

.btn-secondary:hover {
  background: white;
  border-color: #cbd5e0;
}

.btn-sm {
  padding: 0.4rem 0.8rem;
  font-size: 0.85rem;
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

/* 动画 */
@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

@keyframes slideInDown {
  from {
    transform: translateY(-100%);
    opacity: 0;
  }
  to {
    transform: translateY(0);
    opacity: 1;
  }
}

/* 响应式设计 */
@media (max-width: 768px) {
  .preview-header {
    flex-wrap: wrap;
    gap: 1rem;
  }

  .scene-info {
    flex-basis: 100%;
    order: 2;
  }

  .header-actions {
    order: 1;
  }

  .scene-title {
    font-size: 1.25rem;
  }

  .btn-action .text {
    display: none;
  }
}
</style>
