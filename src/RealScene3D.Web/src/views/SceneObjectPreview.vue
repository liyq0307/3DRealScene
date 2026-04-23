<template>
  <div class="scene-object-preview">
    <!-- 顶部控制栏 -->
    <div class="preview-header">
      <button @click="goBack" class="btn-back" title="返回场景对象列表">
        <span class="icon">←</span>
        <span class="text">返回</span>
      </button>

      <div class="object-info">
        <h1 class="object-title">{{ currentObject?.name || '加载中...' }}</h1>
        <p v-if="currentObject?.description" class="object-description">
          {{ currentObject.description }}
        </p>
        <p v-if="sceneName" class="scene-name">
          <span class="label">所属场景:</span>
          <span class="value">{{ sceneName }}</span>
        </p>
      </div>

      <div class="header-actions">
        <button v-if="hasUnsupportedFormat" @click="convertToTiles" class="btn-action" title="转换为3D Tiles">
          <span class="icon">🔄</span>
          <span class="text">转换模型</span>
        </button>
        <button
          v-if="renderEngine === 'cesium'"
          @click="toggleBasemap"
          class="btn-action"
          :title="showBasemap ? '隐藏底图' : '显示底图'"
        >
          <span class="icon">{{ showBasemap ? '🗺️' : '🌐' }}</span>
        </button>
        <button @click="toggleFullscreen" class="btn-action" title="全屏">
          <span class="icon">{{ isFullscreen ? '🗗' : '🗖' }}</span>
        </button>
      </div>
    </div>

    <!-- 动态渲染器容器 -->
    <div class="viewer-container">
      <!-- Three.js 查看器 (用于OBJ, FBX等格式) -->
      <ThreeViewer
        v-if="!loading && currentObject && renderEngine === 'threejs'"
        :scene-objects="[currentObject]"
        :show-info="true"
        :background-color="'#1a1a1a'"
        @ready="onThreeJSReady"
        @error="onThreeJSError"
      />

      <!-- Mars3D 3D地球查看器 (用于3D Tiles, GLTF等格式) -->
      <Mars3DViewer
        v-else-if="!loading && currentObject && renderEngine === 'cesium'"
        ref="mars3dViewerRef"
        :show-info="true"
        :scene-objects="[currentObject]"
        :show-basemap="showBasemap"
        @ready="onMars3DReady"
        @error="onMars3DError"
        @basemapChange="onBasemapChange"
      />
    </div>

    <!-- 加载状态 -->
    <div v-if="loading" class="loading-overlay">
      <div class="loading-content">
        <div class="spinner"></div>
        <p>加载场景对象中...</p>
      </div>
    </div>

    <!-- 错误状态 -->
    <div v-if="error" class="error-overlay">
      <div class="error-content">
        <div class="error-icon">⚠️</div>
        <h2>加载失败</h2>
        <p>{{ error }}</p>
        <button @click="goBack" class="btn btn-primary">
          返回对象列表
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * 场景对象预览页面组件
 *
 * 功能说明:
 * - 提供独立的全屏场景对象预览体验
 * - 基于Cesium的3D地球展示单个场景对象
 * - 支持模型格式转换
 * - 支持全屏模式
 * - 提供返回导航功能
 * - 复用场景预览的核心逻辑(DRY原则)
 *
 * 技术栈: Vue 3 + TypeScript + Cesium
 * 作者: liyq
 * 创建时间: 2025-12-08
 */
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { sceneService } from '@/services/api'
import { useMessage } from '@/composables/useMessage'
import { getBasemapPreference, setBasemapPreference } from '@/composables/useBasemapPreference'
import Mars3DViewer from '@/components/Mars3DViewer.vue'
import ThreeViewer from '@/components/ThreeViewer.vue'

// ==================== 组合式API ====================

const router = useRouter()
const route = useRoute()
const { success: showSuccess, error: showError } = useMessage()

// ==================== 响应式状态 ====================

const loading = ref(true)
const error = ref<string | null>(null)
const currentObject = ref<any>(null)
const currentScene = ref<any>(null)
const sceneName = ref<string>('')
const isFullscreen = ref(false)
const showBasemap = ref(getBasemapPreference())
const mars3dViewerRef = ref<InstanceType<typeof Mars3DViewer>>()

// ==================== 计算属性 ====================

/**
 * 根据场景的渲染引擎和模型格式智能选择渲染引擎
 * 优先级:
 * 1. 使用场景指定的渲染引擎
 * 2. 如果场景没有指定，根据模型格式自动检测
 */
const renderEngine = computed(() => {
  // 优先使用场景指定的渲染引擎
  if (currentScene.value && currentScene.value.renderEngine) {
    const engine = currentScene.value.renderEngine
    return engine === 'ThreeJS' ? 'threejs' : 'cesium'
  }

  // 如果场景没有指定，回退到根据对象格式自动检测
  if (!currentObject.value || !currentObject.value.displayPath) return 'cesium'

  const fileExt = currentObject.value.displayPath.split('?')[0].split('.').pop()?.toLowerCase()

  // Three.js优先处理的格式 - 不需要地理坐标系统的通用3D模型
  const threeJSFormats = ['obj', 'fbx', 'dae', 'stl', '3ds', 'blend', 'ply']

  // Cesium优先处理的格式 - 需要地理坐标系统或已切片的数据
  const cesiumFormats = ['json', 'tiles'] // 3D Tiles用.json, tileset.json

  // 点云格式
  const pointCloudFormats = ['las', 'laz', 'e57']

  if (threeJSFormats.includes(fileExt || '')) {
    return 'threejs'
  }

  if (cesiumFormats.includes(fileExt || '')) {
    return 'cesium'
  }

  // GLTF/GLB可以被两者使用,如果有切片任务完成则用Cesium,否则用Three.js
  if (fileExt === 'gltf' || fileExt === 'glb') {
    // 如果有完成的切片任务,使用Cesium
    if (currentObject.value.slicingTaskId && currentObject.value.slicingTaskStatus === 'Completed') {
      return 'cesium'
    }
    // 否则使用Three.js (更快的本地加载)
    return 'threejs'
  }

  // OSGB格式,如果有切片任务完成则用Cesium
  if (fileExt === 'osgb') {
    if (currentObject.value.slicingTaskId && currentObject.value.slicingTaskStatus === 'Completed') {
      return 'cesium'
    }
    return 'cesium' // OSGB通常需要Cesium
  }

  // 点云格式,需要特殊处理
  if (pointCloudFormats.includes(fileExt || '')) {
    if (currentObject.value.slicingTaskId && currentObject.value.slicingTaskStatus === 'Completed') {
      return 'cesium'
    }
    // 未切片的点云暂时不支持
    return 'cesium'
  }

  // 默认使用Cesium
  return 'cesium'
})

/**
 * 检查对象的模型格式是否需要转换
 * 仅对Cesium不支持或需要切片的格式显示转换提示
 */
const hasUnsupportedFormat = computed(() => {
  if (!currentObject.value || !currentObject.value.displayPath) return false

  const fileExt = currentObject.value.displayPath.split('?')[0].split('.').pop()?.toLowerCase()

  // 如果使用Three.js渲染,则不需要转换
  if (renderEngine.value === 'threejs') {
    return false
  }

  // Cesium原生支持的格式
  const cesiumNativeFormats = ['gltf', 'glb', 'json']

  // 需要切片的格式
  const needsSlicingFormats = ['osgb', 'las', 'laz', 'e57']

  // 如果是需要切片的格式,并且没有完成的切片任务
  if (needsSlicingFormats.includes(fileExt || '')) {
    return !currentObject.value.slicingTaskId || currentObject.value.slicingTaskStatus !== 'Completed'
  }

  return false
})

// ==================== 业务逻辑方法 ====================

/**
 * 加载场景对象详情
 */
const loadObjectDetails = async () => {
  const sceneId = route.params.sceneId as string
  const objectId = route.params.objectId as string

  if (!sceneId || !objectId) {
    error.value = '场景ID或对象ID无效'
    loading.value = false
    return
  }

  try {
    console.log('[SceneObjectPreview] 加载场景对象详情, 场景ID:', sceneId, '对象ID:', objectId)

    // 加载场景数据以获取对象列表
    const scene = await sceneService.getScene(sceneId)
    currentScene.value = scene
    sceneName.value = scene.name

    console.log('[SceneObjectPreview] 场景数据加载成功:', scene)
    console.log('[SceneObjectPreview] 场景渲染引擎:', scene.renderEngine)

    // 从场景对象列表中找到目标对象
    if (scene.sceneObjects && scene.sceneObjects.length > 0) {
      currentObject.value = scene.sceneObjects.find((obj: any) => obj.id === objectId)

      if (!currentObject.value) {
        error.value = '未找到指定的场景对象'
        loading.value = false
        showError('未找到指定的场景对象')
        return
      }

      console.log('[SceneObjectPreview] 场景对象加载成功:', currentObject.value)
    } else {
      error.value = '场景中没有对象数据'
      loading.value = false
      showError('场景中没有对象数据')
      return
    }

    loading.value = false
  } catch (err) {
    console.error('[SceneObjectPreview] 加载场景对象失败:', err)
    error.value = err instanceof Error ? err.message : '加载场景对象失败'
    loading.value = false
    showError('加载场景对象失败')
  }
}

/**
 * 返回场景对象列表
 */
const goBack = () => {
  router.push({ name: 'SceneObjects' })
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
const convertToTiles = async () => {
  if (!currentObject.value) {
    showError('没有可转换的模型')
    return
  }

  if (confirm('确定要为此对象创建切片任务吗?')) {
    showSuccess('准备创建切片任务(功能待实现)')
    // TODO: 调用切片服务API创建切片任务
  }
}

/**
 * Mars3D就绪回调
 */
const onMars3DReady = (map: any) => {
  console.log('[SceneObjectPreview] Mars3D地球初始化成功', map)
  showSuccess('Mars3D 3D地球加载成功')
}

/**
 * Mars3D错误回调
 */
const onMars3DError = (err: Error) => {
  console.error('[SceneObjectPreview] Mars3D初始化失败:', err)
  showError('Mars3D地球加载失败: ' + err.message)
}

/**
 * Three.js就绪回调
 */
const onThreeJSReady = (model: any) => {
  console.log('[SceneObjectPreview] Three.js模型加载成功', model)
  showSuccess('Three.js 模型加载成功')
}

/**
 * Three.js错误回调
 */
const onThreeJSError = (err: Error) => {
  console.error('[SceneObjectPreview] Three.js加载失败:', err)
  showError('Three.js模型加载失败: ' + err.message)
}

/**
 * 切换底图显示状态
 */
const toggleBasemap = async () => {
  if (mars3dViewerRef.value) {
    await mars3dViewerRef.value.toggleBasemap()
  }
}

/**
 * 底图状态变化处理
 */
const onBasemapChange = (visible: boolean) => {
  showBasemap.value = visible
  setBasemapPreference(visible)
}

/**
 * 监听全屏状态变化
 */
const handleFullscreenChange = () => {
  isFullscreen.value = !!document.fullscreenElement
}

// ==================== 生命周期钩子 ====================

onMounted(() => {
  console.log('[SceneObjectPreview] 组件挂载,开始加载场景对象')
  loadObjectDetails()

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
 * 场景对象预览页面样式
 * 采用全屏布局,提供沉浸式的3D对象浏览体验
 * 复用场景预览的样式设计(DRY原则)
 */

.scene-object-preview {
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

.object-info {
  flex: 1;
  min-width: 0;
}

.object-title {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 700;
  color: white;
  line-height: 1.2;
}

.object-description {
  margin: 0.25rem 0 0 0;
  font-size: 0.9rem;
  color: rgba(255, 255, 255, 0.7);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.scene-name {
  margin: 0.5rem 0 0 0;
  font-size: 0.85rem;
  color: rgba(255, 255, 255, 0.6);
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.scene-name .label {
  color: rgba(255, 255, 255, 0.5);
}

.scene-name .value {
  color: rgba(255, 255, 255, 0.8);
  font-weight: 500;
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

  .object-info {
    flex-basis: 100%;
    order: 2;
  }

  .header-actions {
    order: 1;
  }

  .object-title {
    font-size: 1.25rem;
  }

  .btn-action .text {
    display: none;
  }
}
</style>
