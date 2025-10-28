<template>
  <div class="cesium-viewer-wrapper">
    <div ref="cesiumContainer" class="cesium-container"></div>

    <!-- 控制面板 -->
    <div class="controls">
      <button class="btn" @click="resetView" title="重置视图">
        <span class="icon">🎥</span>
      </button>
      <button class="btn" @click="toggleTerrain" title="切换地形">
        <span class="icon">{{ terrainEnabled ? '🗻' : '🌍' }}</span>
      </button>
      <button class="btn" @click="toggleImagery" title="切换影像">
        <span class="icon">🗺️</span>
      </button>
      <button class="btn" @click="takeScreenshot" title="截图">
        <span class="icon">📷</span>
      </button>
    </div>

    <!-- 信息面板 -->
    <div v-if="showInfo" class="info-panel">
      <div class="info-item">
        <span class="info-label">经度:</span>
        <span class="info-value">{{ cameraInfo.longitude }}°</span>
      </div>
      <div class="info-item">
        <span class="info-label">纬度:</span>
        <span class="info-value">{{ cameraInfo.latitude }}°</span>
      </div>
      <div class="info-item">
        <span class="info-label">高度:</span>
        <span class="info-value">{{ cameraInfo.height }}m</span>
      </div>
      <div class="info-item">
        <span class="info-label">FPS:</span>
        <span class="info-value">{{ fps }}</span>
      </div>
    </div>

    <!-- 加载提示 -->
    <div v-if="loading" class="loading-overlay">
      <div class="loading-content">
        <div class="spinner"></div>
        <p>加载Cesium地球中...</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * Cesium 3D地球查看器组件
 *
 * 功能说明：
 * - 基于Cesium的专业级3D地球展示
 * - 支持真实地形和卫星影像
 * - 相机控制和视图操作
 * - 性能监控和信息显示
 *
 * 技术栈：Vue 3 + TypeScript + Cesium
 * 作者：liyq
 * 创建时间：2025-10-22
 */
import { ref, onMounted, onUnmounted, watch } from 'vue'
import * as Cesium from 'cesium'

// ==================== Props 定义 ====================

interface Props {
  showInfo?: boolean          // 是否显示信息面板
  initialPosition?: {         // 初始相机位置
    longitude: number
    latitude: number
    height: number
  }
  terrainProvider?: string    // 地形数据源
  imageryProvider?: string    // 影像数据源
  sceneObjects?: any[]        // 场景对象列表
}

const props = withDefaults(defineProps<Props>(), {
  showInfo: true,
  initialPosition: () => ({
    longitude: 116.39,  // 北京
    latitude: 39.91,
    height: 15000000    // 15000km高度
  }),
  sceneObjects: () => []
})

// ==================== Emits 定义 ====================

const emit = defineEmits<{
  ready: [viewer: Cesium.Viewer]
  error: [error: Error]
}>()

// ==================== DOM引用 ====================

const cesiumContainer = ref<HTMLDivElement>()

// ==================== 响应式状态 ====================

const loading = ref(true)
const terrainEnabled = ref(true)
const fps = ref(60)
const cameraInfo = ref({
  longitude: 0,
  latitude: 0,
  height: 0
})

// ==================== Cesium对象 ====================

let viewer: Cesium.Viewer | null = null
let frameCount = 0
let lastTime = performance.now()
const loadedModels = new Map<string, any>() // Store references to loaded models/tilesets

// ==================== 监听 Props 变化 ====================

watch(
  () => props.sceneObjects,
  (newVal, oldVal) => {
    if (viewer && newVal !== oldVal) {
      loadSceneObjects(newVal || [])
    }
  },
  { deep: true }
)

// ==================== 场景对象加载 ====================

const loadSceneObjects = async (objects: any[]) => {
  if (!viewer) return

  // 清除之前加载的所有模型
  clearLoadedObjects()

  for (const obj of objects) {
    try {
      if (!obj.displayPath) {
        console.warn(`Object ${obj.name} has no displayPath, skipping.`)
        continue
      }

      // 检查displayPath是否指向3D Tileset (以tileset.json结尾)
      if (obj.displayPath.endsWith('tileset.json')) {
        const tileset = await Cesium.Cesium3DTileset.fromUrl(obj.displayPath)
        viewer.scene.primitives.add(tileset)
        loadedModels.set(obj.id, tileset)
        console.log(`Loaded 3D Tileset for object ${obj.name}: ${obj.displayPath}`)
      } else {
        // 否则，假定为GLTF/GLB模型
        const model = await Cesium.Model.fromGltfAsync({
          url: obj.displayPath,
          modelMatrix: Cesium.Matrix4.IDENTITY // 根据需要调整位置、旋转、缩放
        });
        viewer.scene.primitives.add(model);
        loadedModels.set(obj.id, model)
        console.log(`Loaded original model for object ${obj.name}: ${obj.displayPath}`)
      }
    } catch (error) {
      console.error(`Failed to load object ${obj.name} (${obj.id}):`, error)
    }
  }
}

const clearLoadedObjects = () => {
  if (!viewer) return
  loadedModels.forEach((model) => {
    viewer?.scene.primitives.remove(model)
  })
  loadedModels.clear()
}

// ==================== 初始化Cesium ====================

/**
 * 初始化Cesium查看器
 */
const initCesium = async () => {
  if (!cesiumContainer.value) return

  try {
    // 注意：不再使用Cesium Ion，完全使用开源免费数据源
    // 如需使用Cesium Ion，请到 https://ion.cesium.com/ 注册获取令牌

    // 创建Cesium查看器
    viewer = new Cesium.Viewer(cesiumContainer.value, {
      // 使用椭球地形（平面地球，无需令牌）
      terrainProvider: new Cesium.EllipsoidTerrainProvider(),

      // 时间轴和动画控件
      animation: false,
      timeline: false,

      // 其他UI控件
      baseLayerPicker: false,    // 基础图层选择器
      fullscreenButton: false,   // 全屏按钮
      geocoder: false,           // 地理编码搜索
      homeButton: false,         // 主页按钮
      infoBox: false,            // 信息框
      sceneModePicker: false,    // 场景模式选择器
      selectionIndicator: false, // 选择指示器
      navigationHelpButton: false, // 导航帮助按钮

      // 渲染设置
      shadows: false,             // 阴��（性能考虑）
      shouldAnimate: true,        // 自动动画

      // 请求渲染模式（优化性能）
      requestRenderMode: false,   // 设为false以持续渲染
      maximumRenderTimeChange: Infinity
    })

    // 移除默认影像图层并添加自定义影像（使用免费的Bing Maps）
    viewer.imageryLayers.removeAll()

    // 使用OpenStreetMap作为备用（完全免费，无需令牌）
    const imageryProvider = new Cesium.OpenStreetMapImageryProvider({
      url: 'https://a.tile.openstreetmap.org/'
    })
    viewer.imageryLayers.addImageryProvider(imageryProvider)

    // 移除Cesium Logo（可选）
    const creditContainer = viewer.cesiumWidget.creditContainer as HTMLElement
    if (creditContainer) {
      creditContainer.style.display = 'none'
    }

    // 设置初始相机位置
    viewer.camera.setView({
      destination: Cesium.Cartesian3.fromDegrees(
        props.initialPosition.longitude,
        props.initialPosition.latitude,
        props.initialPosition.height
      ),
      orientation: {
        heading: Cesium.Math.toRadians(0),
        pitch: Cesium.Math.toRadians(-90),
        roll: 0.0
      }
    })

    // 设置相机运动事件监听
    viewer.camera.moveEnd.addEventListener(updateCameraInfo)

    // 初始化相机信息
    updateCameraInfo()

    // 启动FPS监控
    startFPSMonitor()

    // 初始加载场景对象
    if (props.sceneObjects && props.sceneObjects.length > 0) {
      loadSceneObjects(props.sceneObjects)
    }

    loading.value = false
    emit('ready', viewer)

    console.log('Cesium地球初始化成功')
  } catch (error: any) {
    console.error('Cesium初始化失败:', error)
    loading.value = false
    emit('error', error)
  }
}

// ==================== 相机控制 ====================

/**
 * 更新相机信息
 */
const updateCameraInfo = () => {
  if (!viewer) return

  const cameraPosition = viewer.camera.positionCartographic

  cameraInfo.value = {
    longitude: parseFloat(Cesium.Math.toDegrees(cameraPosition.longitude).toFixed(4)),
    latitude: parseFloat(Cesium.Math.toDegrees(cameraPosition.latitude).toFixed(4)),
    height: Math.round(cameraPosition.height)
  }
}

/**
 * 重置视图到初始位置
 */
const resetView = () => {
  if (!viewer) return

  viewer.camera.flyTo({
    destination: Cesium.Cartesian3.fromDegrees(
      props.initialPosition.longitude,
      props.initialPosition.latitude,
      props.initialPosition.height
    ),
    orientation: {
      heading: Cesium.Math.toRadians(0),
      pitch: Cesium.Math.toRadians(-90),
      roll: 0.0
    },
    duration: 2
  })
}

/**
 * 切换地形
 */
const toggleTerrain = async () => {
  if (!viewer) return

  terrainEnabled.value = !terrainEnabled.value

  try {
    if (terrainEnabled.value) {
      // 暂时使用椭球地形
      // 如需真实地形，请配置Cesium Ion令牌并使用 createWorldTerrainAsync()
      viewer.terrainProvider = new Cesium.EllipsoidTerrainProvider()
      console.log('地形已启用（椭球模式）')
    } else {
      viewer.terrainProvider = new Cesium.EllipsoidTerrainProvider()
      console.log('地形已禁用')
    }
  } catch (error) {
    console.error('切换地形失败:', error)
  }
}

/**
 * 切换影像图层
 */
const toggleImagery = async () => {
  if (!viewer) return

  try {
    const layers = viewer.imageryLayers
    const currentLayer = layers.get(0)

    // 移除当前图层
    if (currentLayer) {
      layers.remove(currentLayer)
    }

    // 在OpenStreetMap和其他免费影像源之间切换
    let newProvider

    // 简单的切换逻辑：在OpenStreetMap和CartoDB之间切换
    if (!currentLayer || currentLayer.imageryProvider instanceof Cesium.OpenStreetMapImageryProvider) {
      // 切换到CartoDB Voyager
      newProvider = new Cesium.UrlTemplateImageryProvider({
        url: 'https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png',
        subdomains: ['a', 'b', 'c', 'd'],
        credit: 'Map tiles by CartoDB'
      })
    } else {
      // 切换回OpenStreetMap
      newProvider = new Cesium.OpenStreetMapImageryProvider({
        url: 'https://a.tile.openstreetmap.org/'
      })
    }

    layers.addImageryProvider(newProvider)
  } catch (error) {
    console.error('切换影像失败:', error)
  }
}

/**
 * 截图
 */
const takeScreenshot = () => {
  if (!viewer) return

  viewer.render()

  const canvas = viewer.scene.canvas
  const image = canvas.toDataURL('image/png')

  const link = document.createElement('a')
  link.download = `cesium-screenshot-${Date.now()}.png`
  link.href = image
  link.click()
}

// ==================== FPS监控 ====================

/**
 * 启动FPS监控
 */
const startFPSMonitor = () => {
  const updateFPS = () => {
    frameCount++
    const currentTime = performance.now()

    if (currentTime >= lastTime + 1000) {
      fps.value = Math.round((frameCount * 1000) / (currentTime - lastTime))
      frameCount = 0
      lastTime = currentTime
    }

    if (viewer) {
      requestAnimationFrame(updateFPS)
    }
  }

  updateFPS()
}

// ==================== 生命周期 ====================

onMounted(() => {
  initCesium()
})

onUnmounted(() => {
  if (viewer) {
    viewer.destroy()
    viewer = null
  }
})
</script>

<style scoped>
.cesium-viewer-wrapper {
  position: relative;
  width: 100%;
  height: 100%;
  overflow: hidden;
}

.cesium-container {
  width: 100%;
  height: 100%;
}

/* 控制面板 */
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

/* 信息面板 */
.info-panel {
  position: absolute;
  top: 1rem;
  left: 1rem;
  background: rgba(0, 0, 0, 0.7);
  padding: 0.75rem 1rem;
  border-radius: 4px;
  color: white;
  font-size: 0.85rem;
  backdrop-filter: blur(10px);
  z-index: 10;
  min-width: 200px;
}

.info-item {
  display: flex;
  justify-content: space-between;
  margin-bottom: 0.5rem;
}

.info-item:last-child {
  margin-bottom: 0;
}

.info-label {
  color: #999;
  margin-right: 1rem;
}

.info-value {
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
  background: rgba(0, 0, 0, 0.8);
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
</style>
