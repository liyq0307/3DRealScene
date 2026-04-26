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

    <!-- 主内容区域 -->
    <div class="main-content">
      <!-- 左侧侧边栏 - 场景对象树 -->
      <div class="sidebar" :class="{ collapsed: !sidebarVisible }">
        <div class="sidebar-header" @click="toggleSidebar">
          <div class="header-left">
            <span class="toggle-icon">{{ sidebarVisible ? '◀' : '▶' }}</span>
            <h3>场景对象</h3>
          </div>
          <span class="object-count">{{ sceneObjects.length }}</span>
        </div>
        
        <div v-show="sidebarVisible" class="sidebar-content">
          <div class="sidebar-toolbar">
            <button @click="toggleAll" class="toolbar-btn" :title="allSelected ? '全不选' : '全选'">
              <span class="btn-icon">{{ allSelected ? '👁️' : '👁️‍🗨️' }}</span>
              <span class="btn-text">{{ allSelected ? '全部隐藏' : '全部显示' }}</span>
            </button>
          </div>

          <div class="object-list">
            <div 
              v-for="obj in sceneObjects" 
              :key="obj.id" 
              class="object-item"
              :class="{ hidden: !obj.isVisible, disabled: !canDirectDisplay(obj) }"
            >
              <label class="object-checkbox" :class="{ disabled: !canDirectDisplay(obj) }">
                <input 
                  type="checkbox" 
                  :checked="obj.isVisible !== false"
                  :disabled="!canDirectDisplay(obj)"
                  @change="toggleObjectVisibility(obj)"
                />
                <span class="checkbox-custom"></span>
              </label>
              <span class="object-name" :title="getObjectTooltip(obj)">{{ obj.name }}</span>
              <span v-if="obj.slicingTaskStatus === 'Completed'" class="slicing-badge" title="已切片">
                📦
              </span>
              <span v-else-if="!canDirectDisplay(obj)" class="need-slicing-badge" title="需要切片后才能显示">
                ⚠️
              </span>
            </div>
          </div>
        </div>
      </div>

      <!-- 动态渲染器容器 -->
      <div class="viewer-container">
        <!-- Three.js 查看器 (用于通用3D模型) -->
        <ThreeViewer
          v-if="!loading && visibleObjects.length > 0 && sceneRenderEngine === 'threejs'"
          :scene-objects="visibleObjects"
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
          v-else-if="!loading && sceneRenderEngine === 'cesium'"
          ref="mars3dViewerRef"
          :show-info="true"
          :scene-objects="visibleObjects"
          :show-basemap="true"
          @ready="onMars3DReady"
          @error="onMars3DError"
        />

        <!-- 无可见对象提示 (仅Three.js模式) -->
        <div v-if="!loading && visibleObjects.length === 0 && sceneRenderEngine === 'threejs'" class="no-objects-hint">
          <div class="hint-content">
            <span class="hint-icon">👁️</span>
            <p>所有对象已隐藏</p>
            <p class="hint-sub">请在左侧列表中勾选要显示的对象</p>
          </div>
        </div>
      </div>
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
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { sceneService } from '../services/api'
import { useMessage } from '@/composables/useMessage'
import Mars3DViewer from '@/components/Mars3DViewer.vue'
import ThreeViewer from '@/components/ThreeViewer.vue'

const router = useRouter()
const route = useRoute()
const { success: showSuccess, error: showError } = useMessage()

// ==================== 响应式状态 ====================

const loading = ref(true)
const error = ref<string | null>(null)
const currentScene = ref<any>(null)
const sceneObjects = ref<any[]>([])
const isFullscreen = ref(false)
const sidebarVisible = ref(true)
const mars3dViewerRef = ref<InstanceType<typeof Mars3DViewer>>()

// ==================== 计算属性 ====================

/**
 * 可见的场景对象
 */
const visibleObjects = computed(() => {
  return sceneObjects.value.filter(obj => obj.isVisible !== false)
})

/**
 * 是否全选
 */
const allSelected = computed(() => {
  return sceneObjects.value.length > 0 && sceneObjects.value.every(obj => obj.isVisible !== false)
})

/**
 * 使用场景指定的渲染引擎
 */
const sceneRenderEngine = computed(() => {
  if (currentScene.value && currentScene.value.renderEngine) {
    const engine = currentScene.value.renderEngine
    return engine === 'ThreeJS' ? 'threejs' : 'cesium'
  }

  if (!visibleObjects.value || visibleObjects.value.length === 0) return 'cesium'

  const threeJSFormats = ['obj', 'fbx', 'dae', 'stl', '3ds', 'blend', 'ply']
  const cesiumOnlyFormats = ['json', 'tiles', 'osgb', 'las', 'laz', 'e57']

  let hasThreeJSFormat = false
  let hasCesiumOnlyFormat = false

  for (const obj of visibleObjects.value) {
    if (!obj.displayPath) continue
    const fileExt = obj.displayPath.split('?')[0].split('.').pop()?.toLowerCase()

    if (threeJSFormats.includes(fileExt || '')) hasThreeJSFormat = true
    if (cesiumOnlyFormats.includes(fileExt || '')) hasCesiumOnlyFormat = true
    if (obj.slicingTaskId && obj.slicingTaskStatus === 'Completed') hasCesiumOnlyFormat = true
  }

  if (hasThreeJSFormat && !hasCesiumOnlyFormat) return 'threejs'
  return 'cesium'
})

/**
 * 检查是否有不支持的模型格式
 */
const hasUnsupportedModels = computed(() => {
  if (!visibleObjects.value || visibleObjects.value.length === 0) return false
  if (sceneRenderEngine.value === 'threejs') return false

  const needsSlicingFormats = ['osgb', 'las', 'laz', 'e57']

  return visibleObjects.value.some(obj => {
    if (!obj.displayPath) return false
    const fileExt = obj.displayPath.split('?')[0].split('.').pop()?.toLowerCase()
    if (needsSlicingFormats.includes(fileExt || '')) {
      return !obj.slicingTaskId || obj.slicingTaskStatus !== 'Completed'
    }
    return false
  })
})

// ==================== 业务逻辑方法 ====================

/**
 * 判断对象是否可以直接显示（不需要切片）
 */
const canDirectDisplay = (obj: any): boolean => {
  // 如果已完成切片，可以显示
  if (obj.slicingTaskStatus === 'Completed') {
    return true
  }
  
  // 获取文件扩展名
  const path = obj.displayPath || obj.modelPath || ''
  const fileExt = path.split('?')[0].split('.').pop()?.toLowerCase() || ''
  
  // Cesium 原生支持的格式
  const cesiumSupported = ['gltf', 'glb', 'json', 'tileset', 'tiles']
  // 需要切片的格式
  const needsSlicing = ['obj', 'fbx', 'dae', 'stl', '3ds', 'ply', 'osgb', 'las', 'laz', 'e57', 'blend']
  
  if (cesiumSupported.includes(fileExt)) {
    return true
  }
  
  if (needsSlicing.includes(fileExt)) {
    return false
  }
  
  // 其他格式默认可以显示
  return true
}

/**
 * 获取对象的提示文本
 */
const getObjectTooltip = (obj: any): string => {
  if (canDirectDisplay(obj)) {
    return obj.name
  }
  return `${obj.name}（需要切片后才能显示）`
}

/**
 * 切换侧边栏显示
 */
const toggleSidebar = () => {
  sidebarVisible.value = !sidebarVisible.value
}

/**
 * 切换对象可见性
 */
const toggleObjectVisibility = (obj: any) => {
  obj.isVisible = obj.isVisible === false ? true : false
}

/**
 * 全选/全不选
 */
const toggleAll = () => {
  if (allSelected.value) {
    sceneObjects.value.forEach(obj => obj.isVisible = false)
  } else {
    sceneObjects.value.forEach(obj => obj.isVisible = true)
  }
}

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
    currentScene.value = await sceneService.getScene(sceneId)
    console.log('[ScenePreview] 场景数据加载成功:', currentScene.value)

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

const goBack = () => router.push({ name: 'Scenes' })

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

const convertModelsToTiles = async () => {
  if (!visibleObjects.value || visibleObjects.value.length === 0) {
    showError('没有可转换的模型')
    return
  }

  const convertibleFormats = ['obj', 'fbx', 'dae', 'stl', '3ds', 'blend', 'ply', 'las', 'laz', 'e57']
  const modelsToConvert = visibleObjects.value.filter(obj => {
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
  }
}

const onMars3DReady = (map: any) => {
  console.log('[ScenePreview] Mars3D地球初始化成功', map)
  showSuccess('Mars3D 3D地球加载成功')
}

const onMars3DError = (err: Error) => {
  console.error('[ScenePreview] Mars3D初始化失败:', err)
  showError('Mars3D地球加载失败: ' + err.message)
}

const onThreeJSReady = (viewerData: any) => {
  console.log('[ScenePreview] Three.js场景初始化成功', viewerData)
  showSuccess('Three.js 场景加载成功')
}

const onThreeJSObjectLoaded = (object: any) => {
  console.log('[ScenePreview] Three.js对象加载成功', object)
}

const onThreeJSError = (err: Error) => {
  console.error('[ScenePreview] Three.js初始化失败:', err)
  showError('Three.js场景加载失败: ' + err.message)
}

const handleFullscreenChange = () => {
  isFullscreen.value = !!document.fullscreenElement
}

// ==================== 生命周期钩子 ====================

onMounted(() => {
  console.log('[ScenePreview] 组件挂载，开始加载场景')
  loadSceneDetails()
  document.addEventListener('fullscreenchange', handleFullscreenChange)
})

onUnmounted(() => {
  document.removeEventListener('fullscreenchange', handleFullscreenChange)
})
</script>

<style scoped>
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
  gap: 0.8rem;
  padding: 0.35rem 0.8rem;
  background: rgba(0, 0, 0, 0.8);
  backdrop-filter: blur(10px);
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  z-index: 10;
}

.btn-back {
  display: inline-flex;
  align-items: center;
  gap: 0.3rem;
  padding: 0.3rem 0.6rem;
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 5px;
  color: white;
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 0.8rem;
  font-weight: 500;
}

.btn-back:hover {
  background: rgba(255, 255, 255, 0.2);
  border-color: rgba(255, 255, 255, 0.3);
  transform: translateX(-3px);
}

.btn-back .icon { font-size: 0.9rem; }

.scene-info {
  flex: 1;
  min-width: 0;
}

.scene-title {
  margin: 0;
  font-size: 1rem;
  font-weight: 600;
  color: white;
  line-height: 1.2;
}

.scene-description {
  margin: 0.1rem 0 0 0;
  font-size: 0.75rem;
  color: rgba(255, 255, 255, 0.7);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.header-actions {
  display: flex;
  gap: 0.4rem;
}

.btn-action {
  display: inline-flex;
  align-items: center;
  gap: 0.3rem;
  padding: 0.3rem 0.6rem;
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 5px;
  color: white;
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 0.8rem;
}

.btn-action:hover {
  background: rgba(255, 255, 255, 0.2);
  border-color: rgba(255, 255, 255, 0.3);
  transform: translateY(-2px);
}

.btn-action .icon { font-size: 1.2rem; }

/* 主内容区域 */
.main-content {
  flex: 1;
  display: flex;
  overflow: hidden;
}

/* 左侧侧边栏 */
.sidebar {
  width: 220px;
  min-width: 220px;
  background: linear-gradient(180deg, rgba(15, 15, 20, 0.98) 0%, rgba(20, 20, 28, 0.98) 100%);
  border-right: 1px solid rgba(255, 255, 255, 0.08);
  display: flex;
  flex-direction: column;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  overflow: hidden;
}

.sidebar.collapsed {
  width: 36px;
  min-width: 36px;
}

.sidebar-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.4rem 0.6rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.06);
  cursor: pointer;
  user-select: none;
  transition: background 0.2s ease;
  background: rgba(255, 255, 255, 0.02);
}

.sidebar-header:hover {
  background: rgba(255, 255, 255, 0.04);
}

.header-left {
  display: flex;
  align-items: center;
  gap: 0.4rem;
}

.toggle-icon {
  color: rgba(255, 255, 255, 0.4);
  font-size: 0.65rem;
  transition: all 0.2s ease;
  width: 12px;
  text-align: center;
}

.sidebar-header:hover .toggle-icon {
  color: rgba(255, 255, 255, 0.7);
}

.sidebar-header h3 {
  margin: 0;
  font-size: 0.8rem;
  font-weight: 600;
  color: rgba(255, 255, 255, 0.9);
  letter-spacing: 0.02em;
}

.sidebar.collapsed .sidebar-header {
  justify-content: center;
  padding: 0.4rem 0;
}

.sidebar.collapsed .sidebar-header h3 {
  display: none;
}

.sidebar.collapsed .object-count {
  display: none;
}

.object-count {
  background: rgba(99, 102, 241, 0.25);
  color: #a5b4fc;
  padding: 0.15rem 0.5rem;
  border-radius: 10px;
  font-size: 0.75rem;
  font-weight: 600;
}

.sidebar-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.sidebar-toolbar {
  display: flex;
  gap: 0.3rem;
  padding: 0.3rem 0.5rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.04);
}

.toolbar-btn {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.25rem;
  padding: 0.25rem 0.4rem;
  background: rgba(99, 102, 241, 0.15);
  border: 1px solid rgba(99, 102, 241, 0.25);
  border-radius: 4px;
  color: #a5b4fc;
  font-size: 0.7rem;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
}

.toolbar-btn:hover {
  background: rgba(99, 102, 241, 0.3);
  border-color: rgba(99, 102, 241, 0.4);
  color: #c7d2fe;
}

.toolbar-btn .btn-icon {
  font-size: 0.75rem;
}

.toolbar-btn .btn-text {
  font-weight: 500;
}

.object-list {
  flex: 1;
  overflow-y: auto;
  padding: 0.25rem 0;
}

.object-item {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  padding: 0.5rem 0.8rem;
  cursor: pointer;
  transition: background 0.15s ease;
  border-left: 2px solid transparent;
}

.object-item:hover {
  background: rgba(255, 255, 255, 0.03);
}

.object-item.hidden {
  opacity: 0.45;
}

.object-item.disabled {
  opacity: 0.35;
  cursor: not-allowed;
}

.object-item.disabled:hover {
  background: transparent;
}

.object-checkbox {
  position: relative;
  display: flex;
  align-items: center;
  cursor: pointer;
  flex-shrink: 0;
}

.object-checkbox.disabled {
  cursor: not-allowed;
}

.object-checkbox input {
  position: absolute;
  opacity: 0;
  width: 0;
  height: 0;
}

.checkbox-custom {
  width: 16px;
  height: 16px;
  border: 1.5px solid rgba(255, 255, 255, 0.25);
  border-radius: 3px;
  transition: all 0.15s ease;
  display: flex;
  align-items: center;
  justify-content: center;
}

.object-checkbox input:checked + .checkbox-custom {
  background: #6366f1;
  border-color: #6366f1;
}

.object-checkbox input:checked + .checkbox-custom::after {
  content: '✓';
  color: white;
  font-size: 10px;
  font-weight: bold;
}

.object-checkbox input:disabled + .checkbox-custom {
  background: rgba(255, 255, 255, 0.05);
  border-color: rgba(255, 255, 255, 0.15);
  cursor: not-allowed;
}

.object-name {
  flex: 1;
  color: rgba(255, 255, 255, 0.85);
  font-size: 0.85rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.slicing-badge {
  font-size: 0.7rem;
  opacity: 0.6;
  flex-shrink: 0;
}

.need-slicing-badge {
  font-size: 0.7rem;
  color: #fbbf24;
  flex-shrink: 0;
}

/* 查看器容器 */
.viewer-container {
  flex: 1;
  position: relative;
  overflow: hidden;
}

/* 无对象提示 */
.no-objects-hint {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(0, 0, 0, 0.5);
}

.hint-content {
  text-align: center;
  color: white;
}

.hint-icon {
  font-size: 4rem;
  display: block;
  margin-bottom: 1rem;
  opacity: 0.5;
}

.hint-content p {
  margin: 0;
  font-size: 1.2rem;
}

.hint-sub {
  margin-top: 0.5rem !important;
  font-size: 0.9rem !important;
  opacity: 0.7;
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
  to { transform: rotate(360deg); }
}

/* 滚动条样式 */
.object-list::-webkit-scrollbar {
  width: 6px;
}

.object-list::-webkit-scrollbar-track {
  background: transparent;
}

.object-list::-webkit-scrollbar-thumb {
  background: rgba(255, 255, 255, 0.2);
  border-radius: 3px;
}

.object-list::-webkit-scrollbar-thumb:hover {
  background: rgba(255, 255, 255, 0.3);
}

/* 响应式设计 */
@media (max-width: 768px) {
  .preview-header {
    flex-wrap: wrap;
    padding: 0.75rem 1rem;
    gap: 1rem;
  }

  .sidebar {
    position: absolute;
    left: 0;
    top: 60px;
    bottom: 0;
    z-index: 20;
    width: 260px;
  }

  .sidebar.collapsed {
    left: -260px;
  }
}
</style>
