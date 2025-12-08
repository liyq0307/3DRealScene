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

    <!-- 格式兼容性提示 -->
    <div v-if="hasUnsupportedModels && showFormatNotice" class="format-notice">
      <div class="notice-icon">⚠️</div>
      <div class="notice-content">
        <strong>模型格式提示</strong>
        <p>
          本场景包含需要转换才能显示的模型格式。
          Cesium原生支持 glTF/GLB 和 3D Tiles 格式。
          其他格式需要通过切片服务转换为 3D Tiles 才能在地球上显示。
        </p>
        <div class="notice-actions">
          <button class="btn btn-primary btn-sm" @click="convertModelsToTiles">
            转换为 3D Tiles
          </button>
          <button class="btn btn-secondary btn-sm" @click="showFormatNotice = false">
            关闭提示
          </button>
        </div>
      </div>
    </div>

    <!-- Cesium 3D地球查看器 -->
    <div class="viewer-container">
      <CesiumViewer
        v-if="!loading && currentScene"
        :show-info="true"
        :scene-objects="sceneObjects"
        @ready="onCesiumReady"
        @error="onCesiumError"
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
import CesiumViewer from '@/components/CesiumViewer.vue'

// ==================== 组合式API ====================

const router = useRouter()
const route = useRoute()
const { success: showSuccess, error: showError } = useMessage()

// ==================== 响应式状态 ====================

const loading = ref(true)
const error = ref<string | null>(null)
const currentScene = ref<any>(null)
const sceneObjects = ref<any[]>([])
const showFormatNotice = ref(true)
const isFullscreen = ref(false)

// ==================== 计算属性 ====================

/**
 * 检查是否有不支持的模型格式
 */
const hasUnsupportedModels = computed(() => {
  if (!sceneObjects.value || sceneObjects.value.length === 0) return false

  const nativelySupportedFormats = ['gltf', 'glb', 'json']
  const convertibleFormats = ['obj', 'fbx', 'dae', 'stl', '3ds', 'blend', 'ply', 'las', 'laz', 'e57']

  return sceneObjects.value.some(obj => {
    if (!obj.displayPath) return false

    const fileExt = obj.displayPath.split('?')[0].split('.').pop()?.toLowerCase()

    // 如果是可转换格式，并且没有完成的切片任务，则认为是不支持的
    if (convertibleFormats.includes(fileExt || '')) {
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
 * Cesium就绪回调
 */
const onCesiumReady = (viewer: any) => {
  console.log('[ScenePreview] Cesium地球初始化成功', viewer)
  showSuccess('Cesium 3D地球加载成功')
}

/**
 * Cesium错误回调
 */
const onCesiumError = (err: Error) => {
  console.error('[ScenePreview] Cesium初始化失败:', err)
  showError('Cesium地球加载失败: ' + err.message)
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

/* 格式兼容性提示 */
.format-notice {
  display: flex;
  gap: 1rem;
  padding: 1rem 1.5rem;
  margin: 1rem 1.5rem;
  background: linear-gradient(135deg, rgba(255, 193, 7, 0.9) 0%, rgba(255, 235, 59, 0.9) 100%);
  border-radius: 8px;
  border-left: 4px solid #ff9800;
  box-shadow: 0 4px 12px rgba(255, 152, 0, 0.3);
  animation: slideInDown 0.4s ease;
  z-index: 5;
}

.notice-icon {
  font-size: 1.5rem;
  flex-shrink: 0;
  line-height: 1;
}

.notice-content {
  flex: 1;
}

.notice-content strong {
  display: block;
  margin-bottom: 0.5rem;
  color: #856404;
  font-size: 1rem;
  font-weight: 700;
}

.notice-content p {
  margin: 0 0 0.75rem 0;
  color: #664d03;
  font-size: 0.9rem;
  line-height: 1.6;
}

.notice-actions {
  display: flex;
  gap: 0.75rem;
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
