<template>
  <div class="scene-viewer-wrapper">
    <div ref="containerRef" class="scene-viewer"></div>

    <!-- 主控制面板 -->
    <div class="controls">
      <button class="btn" @click="resetCamera" title="重置相机">
        <span v-if="mode === 'enhanced'" class="icon">🎥</span>
        <span v-else>重置相机</span>
      </button>
      <button class="btn" @click="toggleGrid" title="切换网格">
        <span v-if="mode === 'enhanced'" class="icon">{{ gridHelper?.visible ? '📏' : '📐' }}</span>
        <span v-else>切换网格</span>
      </button>
      <button v-if="showAxesControl" class="btn" @click="toggleAxes" title="切换坐标轴">
        <span class="icon">{{ axesHelper?.visible ? '📊' : '📈' }}</span>
      </button>
      <button v-if="showWireframe" class="btn" @click="toggleWireframe" title="切换线框">
        <span class="icon">{{ wireframeMode ? '🔲' : '🔳' }}</span>
      </button>
      <button v-if="showScreenshot" class="btn" @click="takeScreenshot" title="截图">
        <span class="icon">📷</span>
      </button>
    </div>

    <!-- 光照控制面板 -->
    <div v-if="showLightingPanel" class="lighting-panel" :class="{ collapsed: lightingCollapsed }">
      <div class="panel-header" @click="lightingCollapsed = !lightingCollapsed">
        <h4>光照控制</h4>
        <span class="toggle-icon">{{ lightingCollapsed ? '▶' : '▼' }}</span>
      </div>
      <div v-if="!lightingCollapsed" class="panel-content">
        <div class="control-group">
          <label>环境光强度</label>
          <input
            v-model.number="ambientIntensity"
            type="range"
            min="0"
            max="1"
            step="0.1"
            @input="updateLighting"
          />
          <span class="value">{{ ambientIntensity.toFixed(1) }}</span>
        </div>
        <div class="control-group">
          <label>方向光强度</label>
          <input
            v-model.number="directionalIntensity"
            type="range"
            min="0"
            max="2"
            step="0.1"
            @input="updateLighting"
          />
          <span class="value">{{ directionalIntensity.toFixed(1) }}</span>
        </div>
        <div class="control-group">
          <label>背景颜色</label>
          <input
            v-model="backgroundColor"
            type="color"
            @input="updateBackground"
            class="color-input"
          />
        </div>
      </div>
    </div>

    <!-- 模型加载器 -->
    <div v-if="showModelLoader" class="loader-panel" :class="{ collapsed: loaderCollapsed }">
      <div class="panel-header" @click="loaderCollapsed = !loaderCollapsed">
        <h4>模型加载</h4>
        <span class="toggle-icon">{{ loaderCollapsed ? '▶' : '▼' }}</span>
      </div>
      <div v-if="!loaderCollapsed" class="panel-content">
        <div class="control-group">
          <label>模型路径</label>
          <input
            v-model="modelPath"
            type="text"
            placeholder="输入模型路径或URL"
            class="model-input"
          />
        </div>
        <div class="control-group">
          <label>模型类型</label>
          <select v-model="modelType" class="model-select">
            <option value="gltf">GLTF/GLB</option>
            <option value="obj">OBJ</option>
            <option value="fbx">FBX</option>
          </select>
        </div>
        <button @click="loadModel" class="btn-load" :disabled="!modelPath || loading">
          {{ loading ? '加载中...' : '加载模型' }}
        </button>
        <div v-if="loadedModels.length > 0" class="loaded-models">
          <h5>已加载模型 ({{ loadedModels.length }})</h5>
          <div
            v-for="(model, index) in loadedModels"
            :key="index"
            class="model-item"
          >
            <span class="model-name">{{ model.name }}</span>
            <button @click="removeModel(index)" class="btn-remove">✕</button>
          </div>
        </div>
      </div>
    </div>

    <!-- 统计信息 -->
    <div v-if="showStats" class="stats-panel">
      <div class="stat-item">
        <span class="stat-label">FPS:</span>
        <span class="stat-value">{{ fps }}</span>
      </div>
      <div class="stat-item">
        <span class="stat-label">对象:</span>
        <span class="stat-value">{{ objectCount }}</span>
      </div>
      <div class="stat-item">
        <span class="stat-label">三角形:</span>
        <span class="stat-value">{{ triangles.toLocaleString() }}</span>
      </div>
    </div>

    <!-- 加载进度条 -->
    <div v-if="loading" class="loading-overlay">
      <div class="loading-content">
        <div class="spinner"></div>
        <p>加载中...</p>
        <div class="progress-bar">
          <div class="progress-fill" :style="{ width: `${loadingProgress}%` }"></div>
        </div>
        <span class="progress-text">{{ loadingProgress }}%</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * 3D场景查看器组件 - 可配置版本
 *
 * 功能说明：
 * - 支持简单模式(simple)和增强模式(enhanced)
 * - 可通过props配置各项功能的显示与隐藏
 * - 基于Three.js的WebGL 3D渲染
 * - 相机轨道控制（OrbitControls）
 * - 光照控制、模型加载、性能统计等增强功能
 *
 * 技术栈：Vue 3 + TypeScript + Three.js
 * 作者：liyq
 * 创建时间：2025-10-13
 * 更新时间：2025-10-17（合并 EnhancedSceneViewer 功能）
 */
import { ref, onMounted, onUnmounted, computed } from 'vue'
import * as THREE from 'three'
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js'
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js'
import { OBJLoader } from 'three/examples/jsm/loaders/OBJLoader.js'
import { FBXLoader } from 'three/examples/jsm/loaders/FBXLoader.js'

// ==================== Props 定义 ====================

interface Props {
  mode?: 'simple' | 'enhanced'      // 模式：简单模式或增强模式
  showLightingPanel?: boolean        // 是否显示光照控制面板
  showModelLoader?: boolean          // 是否显示模型加载器
  showStats?: boolean                // 是否显示性能统计
  showScreenshot?: boolean           // 是否显示截图功能
  showAxesControl?: boolean          // 是否显示坐标轴控制
  showWireframe?: boolean            // 是否显示线框切换
}

const props = withDefaults(defineProps<Props>(), {
  mode: 'simple',
  showLightingPanel: false,
  showModelLoader: false,
  showStats: false,
  showScreenshot: false,
  showAxesControl: false,
  showWireframe: false
})

// 当 mode 为 'enhanced' 时，自动启用所有增强功能
const isEnhancedMode = computed(() => props.mode === 'enhanced')
const showLightingPanel = computed(() => isEnhancedMode.value || props.showLightingPanel)
const showModelLoader = computed(() => isEnhancedMode.value || props.showModelLoader)
const showStats = computed(() => isEnhancedMode.value || props.showStats)
const showScreenshot = computed(() => isEnhancedMode.value || props.showScreenshot)
const showAxesControl = computed(() => isEnhancedMode.value || props.showAxesControl)
const showWireframe = computed(() => isEnhancedMode.value || props.showWireframe)

// ==================== DOM引用 ====================

const containerRef = ref<HTMLDivElement>()

// ==================== Three.js核心对象 ====================

let scene: THREE.Scene | null = null
let camera: THREE.PerspectiveCamera | null = null
let renderer: THREE.WebGLRenderer | null = null
let controls: OrbitControls | null = null
let animationId: number | null = null

// 光照对象
let ambientLight: THREE.AmbientLight | null = null
let directionalLight: THREE.DirectionalLight | null = null

// 辅助对象
const gridHelper = ref<THREE.GridHelper | null>(null)
const axesHelper = ref<THREE.AxesHelper | null>(null)

// ==================== 响应式状态 ====================

const lightingCollapsed = ref(false)
const loaderCollapsed = ref(false)
const wireframeMode = ref(false)
const loading = ref(false)
const loadingProgress = ref(0)

// 光照参数
const ambientIntensity = ref(0.6)
const directionalIntensity = ref(0.8)
const backgroundColor = ref('#1a1a1a')

// 模型加载
const modelPath = ref('')
const modelType = ref('gltf')
const loadedModels = ref<Array<{ name: string; object: THREE.Object3D }>>([])

// 性能统计
const fps = ref(60)
const objectCount = ref(0)
const triangles = ref(0)

// 性能监控
let frameCount = 0
let lastTime = performance.now()

// 用于清理的引用
let handleResizeFunc: (() => void) | null = null

// ==================== 初始化场景 ====================

/**
 * 初始化3D场景
 */
const initScene = () => {
  if (!containerRef.value) return

  const width = containerRef.value.clientWidth
  const height = containerRef.value.clientHeight

  // 创建场景
  scene = new THREE.Scene()
  scene.background = new THREE.Color(backgroundColor.value)

  // 创建相机
  camera = new THREE.PerspectiveCamera(75, width / height, 0.1, 1000)
  camera.position.set(10, 10, 10)
  camera.lookAt(0, 0, 0)

  // 创建渲染器
  renderer = new THREE.WebGLRenderer({
    antialias: true,
    preserveDrawingBuffer: true // 支持截图
  })
  renderer.setSize(width, height)
  renderer.setPixelRatio(window.devicePixelRatio)
  renderer.shadowMap.enabled = true
  renderer.shadowMap.type = THREE.PCFSoftShadowMap
  containerRef.value.appendChild(renderer.domElement)

  // 创建轨道控制器
  controls = new OrbitControls(camera, renderer.domElement)
  controls.enableDamping = true
  controls.dampingFactor = 0.05
  controls.minDistance = 1
  controls.maxDistance = 500

  // 添加光照
  ambientLight = new THREE.AmbientLight(0xffffff, ambientIntensity.value)
  scene.add(ambientLight)

  directionalLight = new THREE.DirectionalLight(0xffffff, directionalIntensity.value)
  directionalLight.position.set(10, 20, 10)
  directionalLight.castShadow = true
  directionalLight.shadow.mapSize.width = 2048
  directionalLight.shadow.mapSize.height = 2048
  scene.add(directionalLight)

  // 添加辅助对象
  gridHelper.value = new THREE.GridHelper(20, 20, 0x444444, 0x222222)
  scene.add(gridHelper.value)

  axesHelper.value = new THREE.AxesHelper(5)
  scene.add(axesHelper.value)

  // 添加示例几何体
  const geometry = new THREE.BoxGeometry(2, 2, 2)
  const material = new THREE.MeshStandardMaterial({
    color: 0x667eea,
    metalness: 0.3,
    roughness: 0.4
  })
  const cube = new THREE.Mesh(geometry, material)
  cube.position.y = 1
  cube.castShadow = true
  cube.receiveShadow = true
  scene.add(cube)

  // 添加地面
  const groundGeometry = new THREE.PlaneGeometry(50, 50)
  const groundMaterial = new THREE.MeshStandardMaterial({
    color: 0x333333,
    roughness: 0.8
  })
  const ground = new THREE.Mesh(groundGeometry, groundMaterial)
  ground.rotation.x = -Math.PI / 2
  ground.receiveShadow = true
  scene.add(ground)

  // 窗口大小调整
  handleResizeFunc = handleResize
  window.addEventListener('resize', handleResizeFunc)

  // 启动动画循环
  animate()

  // 如果显示统计信息，启动统计更新
  if (showStats.value) {
    updateStats()
  }
}

// ==================== 动画循环 ====================

/**
 * 动画循环
 */
const animate = () => {
  animationId = requestAnimationFrame(animate)

  if (controls) {
    controls.update()
  }

  if (renderer && scene && camera) {
    renderer.render(scene, camera)
  }

  // 计算FPS
  if (showStats.value) {
    frameCount++
    const currentTime = performance.now()
    if (currentTime >= lastTime + 1000) {
      fps.value = Math.round((frameCount * 1000) / (currentTime - lastTime))
      frameCount = 0
      lastTime = currentTime
    }
  }
}

// ==================== 统计信息 ====================

/**
 * 更新统计信息
 */
const updateStats = () => {
  if (!scene) return

  let totalTriangles = 0
  let totalObjects = 0

  scene.traverse((object) => {
    if (object instanceof THREE.Mesh) {
      totalObjects++
      const geometry = object.geometry
      if (geometry.index) {
        totalTriangles += geometry.index.count / 3
      } else if (geometry.attributes.position) {
        totalTriangles += geometry.attributes.position.count / 3
      }
    }
  })

  objectCount.value = totalObjects
  triangles.value = Math.round(totalTriangles)

  setTimeout(updateStats, 1000)
}

// ==================== 事件处理 ====================

/**
 * 窗口大小调整
 */
const handleResize = () => {
  if (!containerRef.value || !camera || !renderer) return

  const width = containerRef.value.clientWidth
  const height = containerRef.value.clientHeight

  camera.aspect = width / height
  camera.updateProjectionMatrix()
  renderer.setSize(width, height)
}

/**
 * 重置相机
 */
const resetCamera = () => {
  if (camera && controls) {
    camera.position.set(10, 10, 10)
    camera.lookAt(0, 0, 0)
    controls.target.set(0, 0, 0)
    controls.update()
  }
}

/**
 * 切换网格
 */
const toggleGrid = () => {
  if (gridHelper.value) {
    gridHelper.value.visible = !gridHelper.value.visible
  }
}

/**
 * 切换坐标轴
 */
const toggleAxes = () => {
  if (axesHelper.value) {
    axesHelper.value.visible = !axesHelper.value.visible
  }
}

/**
 * 切换线框模式
 */
const toggleWireframe = () => {
  if (!scene) return

  wireframeMode.value = !wireframeMode.value

  scene.traverse((object) => {
    if (object instanceof THREE.Mesh) {
      const material = object.material as THREE.Material
      if ('wireframe' in material) {
        (material as any).wireframe = wireframeMode.value
      }
    }
  })
}

/**
 * 更新光照
 */
const updateLighting = () => {
  if (ambientLight) {
    ambientLight.intensity = ambientIntensity.value
  }
  if (directionalLight) {
    directionalLight.intensity = directionalIntensity.value
  }
}

/**
 * 更新背景色
 */
const updateBackground = () => {
  if (scene) {
    scene.background = new THREE.Color(backgroundColor.value)
  }
}

/**
 * 加载模型
 */
const loadModel = async () => {
  if (!modelPath.value || !scene) return

  loading.value = true
  loadingProgress.value = 0

  try {
    let loader: any
    const path = modelPath.value

    switch (modelType.value) {
      case 'gltf':
        loader = new GLTFLoader()
        break
      case 'obj':
        loader = new OBJLoader()
        break
      case 'fbx':
        loader = new FBXLoader()
        break
      default:
        throw new Error('不支持的模型类型')
    }

    loader.load(
      path,
      (object: any) => {
        const model = modelType.value === 'gltf' ? object.scene : object

        // 设置模型属性
        model.traverse((child: any) => {
          if (child instanceof THREE.Mesh) {
            child.castShadow = true
            child.receiveShadow = true
          }
        })

        // 计算模型边界并居中
        const box = new THREE.Box3().setFromObject(model)
        const center = box.getCenter(new THREE.Vector3())
        model.position.sub(center)
        model.position.y = 0

        scene!.add(model)

        const name = path.split('/').pop() || `Model ${loadedModels.value.length + 1}`
        loadedModels.value.push({ name, object: model })

        loading.value = false
        loadingProgress.value = 100
        modelPath.value = ''
      },
      (progress: any) => {
        if (progress.lengthComputable) {
          loadingProgress.value = Math.round((progress.loaded / progress.total) * 100)
        }
      },
      (error: any) => {
        console.error('模型加载失败:', error)
        alert('模型加载失败: ' + error.message)
        loading.value = false
      }
    )
  } catch (error: any) {
    console.error('模型加载错误:', error)
    alert('模型加载错误: ' + error.message)
    loading.value = false
  }
}

/**
 * 移除模型
 */
const removeModel = (index: number) => {
  const model = loadedModels.value[index]
  if (model && scene) {
    scene.remove(model.object)

    // 释放资源
    model.object.traverse((child) => {
      if (child instanceof THREE.Mesh) {
        child.geometry.dispose()
        if (Array.isArray(child.material)) {
          child.material.forEach(material => material.dispose())
        } else {
          child.material.dispose()
        }
      }
    })

    loadedModels.value.splice(index, 1)
  }
}

/**
 * 截图
 */
const takeScreenshot = () => {
  if (!renderer) return

  const link = document.createElement('a')
  link.download = `screenshot-${Date.now()}.png`
  link.href = renderer.domElement.toDataURL('image/png')
  link.click()
}

// ==================== 生命周期 ====================

onMounted(() => {
  initScene()
})

onUnmounted(() => {
  if (animationId) {
    cancelAnimationFrame(animationId)
  }
  if (handleResizeFunc) {
    window.removeEventListener('resize', handleResizeFunc)
  }
  if (controls) {
    controls.dispose()
  }
  if (scene) {
    scene.traverse((object) => {
      if (object instanceof THREE.Mesh) {
        if (object.geometry) {
          object.geometry.dispose()
        }
        if (object.material) {
          if (Array.isArray(object.material)) {
            object.material.forEach(material => material.dispose())
          } else {
            object.material.dispose()
          }
        }
      }
    })
    scene.clear()
  }
  if (renderer) {
    renderer.dispose()
  }
})
</script>

<style scoped>
.scene-viewer-wrapper {
  position: relative;
  width: 100%;
  height: 100%;
  overflow: hidden;
}

.scene-viewer {
  width: 100%;
  height: 100%;
}

/* 主控制面板 */
.controls {
  position: absolute;
  top: 1rem;
  right: 1rem;
  display: flex;
  gap: 0.5rem;
  z-index: 10;
}

.btn {
  padding: 0.5rem;
  background: rgba(255, 255, 255, 0.9);
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s ease;
  backdrop-filter: blur(10px);
}

.btn:hover {
  background: white;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
}

.icon {
  font-size: 1.2rem;
}

/* 侧边面板 */
.lighting-panel,
.loader-panel {
  position: absolute;
  top: 1rem;
  left: 1rem;
  background: rgba(255, 255, 255, 0.95);
  border-radius: 8px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  backdrop-filter: blur(10px);
  max-width: 300px;
  z-index: 10;
  transition: all 0.3s ease;
}

.loader-panel {
  top: auto;
  bottom: 80px;
}

.lighting-panel.collapsed,
.loader-panel.collapsed {
  max-height: 48px;
  overflow: hidden;
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem 1rem;
  cursor: pointer;
  border-bottom: 1px solid #e1e5e9;
  user-select: none;
}

.panel-header:hover {
  background: #f8f9fa;
}

.panel-header h4 {
  margin: 0;
  font-size: 0.9rem;
  font-weight: 600;
  color: #333;
}

.toggle-icon {
  color: #666;
  font-size: 0.8rem;
}

.panel-content {
  padding: 1rem;
}

.control-group {
  margin-bottom: 1rem;
}

.control-group:last-child {
  margin-bottom: 0;
}

.control-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-size: 0.85rem;
  font-weight: 500;
  color: #666;
}

.control-group input[type="range"] {
  width: 100%;
  margin-bottom: 0.25rem;
}

.control-group .value {
  float: right;
  font-size: 0.85rem;
  color: #007acc;
  font-weight: 500;
}

.color-input {
  width: 100%;
  height: 36px;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  cursor: pointer;
}

.model-input,
.model-select {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  font-size: 0.85rem;
}

.btn-load {
  width: 100%;
  padding: 0.5rem;
  background: #007acc;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-weight: 500;
  transition: all 0.2s ease;
}

.btn-load:hover:not(:disabled) {
  background: #005999;
}

.btn-load:disabled {
  background: #ccc;
  cursor: not-allowed;
}

.loaded-models {
  margin-top: 1rem;
  padding-top: 1rem;
  border-top: 1px solid #e1e5e9;
}

.loaded-models h5 {
  margin: 0 0 0.5rem 0;
  font-size: 0.85rem;
  color: #666;
}

.model-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.5rem;
  background: #f8f9fa;
  border-radius: 4px;
  margin-bottom: 0.5rem;
}

.model-name {
  font-size: 0.85rem;
  color: #333;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.btn-remove {
  padding: 0.25rem 0.5rem;
  background: #dc3545;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.75rem;
}

.btn-remove:hover {
  background: #c82333;
}

/* 统计信息 */
.stats-panel {
  position: absolute;
  bottom: 1rem;
  right: 1rem;
  display: flex;
  gap: 1rem;
  background: rgba(0, 0, 0, 0.7);
  padding: 0.5rem 1rem;
  border-radius: 4px;
  color: white;
  font-size: 0.85rem;
  backdrop-filter: blur(10px);
  z-index: 10;
}

.stat-item {
  display: flex;
  gap: 0.25rem;
}

.stat-label {
  color: #999;
}

.stat-value {
  color: #0f0;
  font-weight: 600;
  font-family: monospace;
}

/* 加载覆盖层 */
.loading-overlay {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.7);
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
  width: 50px;
  height: 50px;
  border: 4px solid rgba(255, 255, 255, 0.3);
  border-top-color: white;
  border-radius: 50%;
  animation: spin 1s linear infinite;
  margin: 0 auto 1rem;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.progress-bar {
  width: 200px;
  height: 4px;
  background: rgba(255, 255, 255, 0.3);
  border-radius: 2px;
  margin: 1rem auto 0.5rem;
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  background: #007acc;
  transition: width 0.3s ease;
}

.progress-text {
  font-size: 0.85rem;
  color: #999;
}
</style>
