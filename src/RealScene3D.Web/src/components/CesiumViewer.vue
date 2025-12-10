<template>
  <div class="cesium-viewer-wrapper">
    <div ref="cesiumContainer" class="cesium-container"></div>

    <!-- 控制面板 -->
    <div class="controls">
      <button class="btn" @click="resetView" title="重置视图">
        <span class="icon">🎥</span>
      </button>
      <button class="btn" @click="toggleWireframe" title="线框模式">
        <span class="icon">{{ wireframeMode ? '🔲' : '⬜' }}</span>
      </button>
      <button class="btn" @click="toggleAxes" title="切换坐标轴">
        <span class="icon">📐</span>
      </button>
      <button class="btn" @click="toggleGrid" title="切换网格">
        <span class="icon">＃</span>
      </button>
      <button class="btn" @click="takeScreenshot" title="截图">
        <span class="icon">📷</span>
      </button>
    </div>

    <!-- 信息面板 -->
    <div v-if="showInfo" class="info-panel">
      <div class="info-item">
        <span class="info-label">对象数量:</span>
        <span class="info-value">{{ loadedObjectsCount }}</span>
      </div>
      <div class="info-item">
        <span class="info-label">FPS:</span>
        <span class="info-value">{{ fps }}</span>
      </div>
      <div class="info-item">
        <span class="info-label">渲染引擎:</span>
        <span class="info-value">Cesium</span>
      </div>
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
 * 功能特性：
 * - 基于Cesium的专业级3D地球展示
 * - 支持多种3D模型格式（3D Tiles、GLTF/GLB）
 * - 实时性能监控（FPS、内存使用）
 * - 智能相机控制和视图操作
 * - 异步对象加载和错误处理
 * - 响应式设计，支持移动端
 *
 * 支持的文件格式：
 * - 本地支持：GLTF, GLB, JSON (3D Tiles)
 * - 可转换格式：OBJ, FBX, DAE, STL, 3DS, BLEND, PLY, LAS, LAZ, E57
 *
 * 技术栈：Vue 3 Composition API + TypeScript + Cesium
 * 作者：liyq
 * 创建时间：2025-10-22
 * 最后更新：2025-11-03
 */
import { ref, onMounted, onUnmounted, watch, computed, nextTick } from 'vue'
import { useMessage } from '@/composables/useMessage'
import * as Cesium from 'cesium'

// ==================== 类型定义 ====================

/**
 * Cesium渲染配置接口
 * 提供类型安全的渲染设置，支持性能优化和自适应渲染
 */
interface CesiumRenderConfig {
  /** 是否启用阴影渲染 */
  shadows: boolean
  /** 是否启用自动动画 */
  shouldAnimate: boolean
  /** 是否启用请求渲染模式（性能优化） */
  requestRenderMode: boolean
  /** 最大渲染时间变化阈值 */
  maximumRenderTimeChange: number
  /** 是否启用MSAA抗锯齿 */
  msaaSamples: number
  /** 是否启用FXAA后处理抗锯齿 */
  fxaa: boolean
  /** 阴影映射尺寸 */
  shadowMapSize: number
  /** 是否启用大气效果 */
  globe: {
    enableLighting: boolean
    dynamicAtmosphereLighting: boolean
    dynamicAtmosphereLightingFromSun: boolean
  }
}

/**
 * 性能监控配置
 */
interface PerformanceConfig {
  /** 目标FPS */
  targetFPS: number
  /** FPS监控间隔(ms) */
  fpsMonitorInterval: number
  /** 低性能阈值FPS */
  lowPerformanceThreshold: number
  /** 是否启用自适应质量调整 */
  enableAdaptiveQuality: boolean
}

/**
 * 变换参数接口
 */
interface TransformParams {
  x: number
  y: number
  z: number
}

/**
 * 场景对象接口
 */
interface SceneObject {
  id: string
  name: string
  displayPath: string
  position: [number, number, number] // 经度、纬度、高度
  rotation: TransformParams | string
  scale: TransformParams | string
  slicingTaskId?: string
  slicingTaskStatus?: string
  slicingOutputPath?: string
  modelPath?: string
}

/**
 * 相机信息接口
 */
interface CameraInfo {
  longitude: number
  latitude: number
  height: number
}

/**
 * 影像源配置接口
 */
interface ImageryProviderConfig {
  url: string
  subdomains?: readonly string[]
  credit: string
}

/**
 * 加载的模型信息接口
 */
interface LoadedModel {
  type: '3dtiles' | 'model'
  object: Cesium.Cesium3DTileset | Cesium.Model
  position: Cesium.Cartesian3
}


// ==================== 常量和配置 ====================

/**
 * 应用配置常量
 */
const APP_CONFIG = Object.freeze({
  /** 默认相机位置（北京天安门广场） */
  DEFAULT_POSITION: Object.freeze({
    longitude: 116.397128,
    latitude: 39.908802,
    height: 100
  }),

  /** 支持的文件格式 */
  SUPPORTED_FORMATS: Object.freeze({
    nativelySupported: Object.freeze(['gltf', 'glb', 'json']),
    convertible: Object.freeze(['obj', 'fbx', 'dae', 'stl', '3ds', 'blend', 'ply', 'las', 'laz', 'e57'])
  }),

  /** MinIO存储bucket名称 */
  MINIO_BUCKETS: Object.freeze(['models-3d', 'slices', 'textures', 'thumbnails', 'videos']),

  /** 影像源配置 */
  IMAGERY_SOURCES: Object.freeze({
    cartodb: Object.freeze({
      url: 'https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png',
      subdomains: Object.freeze(['a', 'b', 'c', 'd']),
      credit: 'Map tiles by CartoDB'
    }),
    esri: Object.freeze({
      url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
      subdomains: undefined,
      credit: 'Tiles © Esri — Source: Esri, i-cubed, USDA, USGS, AEX, GeoEye, Getmapping, Aerogrid, IGN, IGP, UPR-EGP, and the GIS User Community'
    })
  } as Record<string, ImageryProviderConfig>),

  /** 相机飞行配置 */
  CAMERA_FLIGHT_CONFIG: Object.freeze({
    duration: 2.0,
    heading: 0,
    pitch: -45,
    roll: 0
  }),

  /** 性能配置 */
  PERFORMANCE: Object.freeze({
    TILES_LOAD_TIMEOUT: 30000,
    MODEL_LOAD_TIMEOUT: 10000,
    BATCH_SIZE: 3,
    MIN_DISTANCE: 500,
    SCALE_FACTOR: 50
  }),

  /** UI配置 */
  UI: Object.freeze({
    FPS_MONITOR_INTERVAL: 1000,
    CAMERA_UPDATE_DEBOUNCE: 100
  })
})

// ==================== Props 定义 ====================

interface Props {
  /** 是否显示信息面板 */
  showInfo?: boolean
  /** 初始相机位置 */
  initialPosition?: {
    longitude: number
    latitude: number
    height: number
  }
  /** 地形数据源 */
  terrainProvider?: string
  /** 影像数据源 */
  imageryProvider?: string
  /** 场景对象列表 */
  sceneObjects?: SceneObject[]
  /** 自定义渲染配置 */
  renderConfig?: Partial<CesiumRenderConfig>
  /** 性能配置 */
  performanceConfig?: Partial<PerformanceConfig>
}

const props = withDefaults(defineProps<Props>(), {
  showInfo: true,
  initialPosition: () => ({
    longitude: 116.39, // 北京
    latitude: 39.91,
    height: 15000000 // 15000km高度
  }),
  sceneObjects: () => [],
  renderConfig: () => ({
    shadows: false,
    shouldAnimate: true,
    requestRenderMode: false,
    maximumRenderTimeChange: Infinity,
    msaaSamples: 4,
    fxaa: true,
    shadowMapSize: 2048,
    globe: {
      enableLighting: true,
      dynamicAtmosphereLighting: true,
      dynamicAtmosphereLightingFromSun: true
    }
  }),
  performanceConfig: () => ({
    targetFPS: 60,
    fpsMonitorInterval: 1000,
    lowPerformanceThreshold: 30,
    enableAdaptiveQuality: true
  })
})

// ==================== Emits 定义 ====================

const emit = defineEmits<{
  /** 查看器初始化完成 */
  ready: [viewer: Cesium.Viewer]
  /** 初始化错误 */
  error: [error: Error]
  /** 对象加载完成 */
  objectLoaded: [object: SceneObject, success: boolean]
}>()

// ==================== DOM引用 ====================

const cesiumContainer = ref<HTMLDivElement>()

// ==================== 组合式API ====================

const { error: showError, success: showSuccess } = useMessage()

// ==================== 响应式状态 ====================

const loading = ref(true)
const fps = ref(60)
const loadedObjectsCount = ref(0)
const wireframeMode = ref(false)
const axesVisible = ref(true)
const gridVisible = ref(true)
const cameraInfo = ref<CameraInfo>({
  longitude: 0,
  latitude: 0,
  height: 0
})

// ==================== 计算属性 ====================

/** API基础URL */
const apiBaseUrl = computed(() => {
  const baseUrl = import.meta.env.VITE_API_URL || 'http://localhost:5000/api'
  return baseUrl.replace('/api', '')
})

// ==================== Cesium对象 ====================

let viewer: Cesium.Viewer | null = null
let frameCount = 0
let lastTime = performance.now()
const loadedModels = new Map<string, LoadedModel>()
// 存储模型的相机监听器清理函数
const modelCameraListeners = new WeakMap<Cesium.Model, () => void>()

// ==================== 工具函数 ====================

/**
 * 获取文件扩展名
 * @param filePath 文件路径
 * @returns 小写的文件扩展名，如果没有则返回空字符串
 */
const getFileExtension = (filePath: string): string => {
  if (!filePath || typeof filePath !== 'string') {
    console.warn('[getFileExtension] 无效的文件路径:', filePath)
    return ''
  }

  const pathWithoutQuery = filePath.split('?')[0]
  return pathWithoutQuery.split('.').pop()?.toLowerCase() || ''
}

/**
 * 判断是否为绝对URL
 * @param url URL字符串
 * @returns 是否为绝对URL
 */
const isAbsoluteUrl = (url: string): boolean => {
  if (!url || typeof url !== 'string') {
    return false
  }
  return url.startsWith('http://') || url.startsWith('https://')
}

/**
 * 解析旋转和缩放参数
 * @param rotation 旋转参数
 * @param scale 缩放参数
 * @returns 解析后的参数对象
 */
const parseTransformParams = (rotation: SceneObject['rotation'], scale: SceneObject['scale']) => {
  try {
    const parsedRotation = typeof rotation === 'string' ? JSON.parse(rotation) : rotation || { x: 0, y: 0, z: 0 }
    const parsedScale = typeof scale === 'string' ? JSON.parse(scale) : scale || { x: 1, y: 1, z: 1 }
    return { parsedRotation, parsedScale }
  } catch (error) {
    console.error('[parseTransformParams] 解析变换参数失败:', error)
    // 返回默认值
    return {
      parsedRotation: { x: 0, y: 0, z: 0 },
      parsedScale: { x: 1, y: 1, z: 1 }
    }
  }
}

/**
 * 创建模型矩阵
 */
const createModelMatrix = (position: number[], rotation: { x: number; y: number; z: number }, scale: { x: number; y: number; z: number }): Cesium.Matrix4 => {
  const cartesian = Cesium.Cartesian3.fromDegrees(position[0], position[1], position[2])
  const heading = Cesium.Math.toRadians(rotation.y)
  const pitch = Cesium.Math.toRadians(rotation.x)
  const roll = Cesium.Math.toRadians(rotation.z)
  const hpr = new Cesium.HeadingPitchRoll(heading, pitch, roll)
  const orientation = Cesium.Transforms.headingPitchRollQuaternion(cartesian, hpr)

  return Cesium.Matrix4.fromTranslationQuaternionRotationScale(
    cartesian,
    orientation,
    new Cesium.Cartesian3(scale.x, scale.y, scale.z)
  )
}

/**
 * 飞行到指定位置
 */
const flyToPosition = async (position: number[], duration: number = APP_CONFIG.CAMERA_FLIGHT_CONFIG.duration): Promise<void> => {
  if (!viewer) return

  const cartesian = Cesium.Cartesian3.fromDegrees(position[0], position[1], position[2])

  await viewer.camera.flyTo({
    destination: cartesian,
    orientation: {
      heading: Cesium.Math.toRadians(APP_CONFIG.CAMERA_FLIGHT_CONFIG.heading),
      pitch: Cesium.Math.toRadians(APP_CONFIG.CAMERA_FLIGHT_CONFIG.pitch),
      roll: APP_CONFIG.CAMERA_FLIGHT_CONFIG.roll
    },
    duration
  })
}

// ==================== 监听 Props 变化 ====================

// 使用防抖处理场景对象变化，避免频繁加载
let sceneObjectsDebounceTimer: number | null = null

watch(
  () => props.sceneObjects,
  (newObjects) => {
    if (!viewer) {
      console.warn('查看器尚未准备就绪，将在准备就绪时加载对象')
      return
    }

    // 清除之前的定时器
    if (sceneObjectsDebounceTimer) {
      clearTimeout(sceneObjectsDebounceTimer)
    }

    // 延迟执行，避免频繁调用
    sceneObjectsDebounceTimer = window.setTimeout(async () => {
      try {
        await loadSceneObjects(newObjects || [])
      } catch (error) {
        console.error('[watch sceneObjects] 加载场景对象失败:', error)
        showError('加载场景对象失败')
      }
      sceneObjectsDebounceTimer = null
    }, 300) // 300ms防抖延迟
  },
  { deep: true, immediate: false }
)

// ==================== 场景对象加载 ====================

/**
 * 处理对象URL路径
 */
const resolveObjectUrl = (displayPath: string): string => {
  let fullPath = displayPath

  // 检查是否为Windows本地文件路径 (例如 F:/Data/3D/test/tileset.json 或 F:\Data\3D\test\tileset.json)
  const isWindowsPath = /^[A-Za-z]:[\\/]/.test(fullPath)

  if (isWindowsPath) {
    // 本地文件路径需要通过API代理访问
    // 将路径转换为相对于数据根目录的路径
    // 例如: F:/Data/3D/test/tileset.json -> test/tileset.json
    const match = fullPath.match(/^[A-Za-z]:[\\/]Data[\\/]3D[\\/](.+)$/i)
    if (match) {
      const relativePath = match[1].replace(/\\/g, '/')
      fullPath = `${apiBaseUrl.value}/api/files/local/${relativePath}`
      console.log('[CesiumViewer] 本地文件路径转换:', { original: displayPath, converted: fullPath })
      return fullPath
    } else {
      console.warn('[CesiumViewer] 无法识别的本地文件路径格式:', displayPath)
      // 尝试提取文件名部分
      const pathParts = fullPath.replace(/\\/g, '/').split('/')
      const fileName = pathParts[pathParts.length - 1]
      fullPath = `${apiBaseUrl.value}/api/files/local/${fileName}`
    }
    return fullPath
  }

  // 如果是后端代理路径（以 /api/ 开头），添加完整的API基础URL
  if (fullPath.startsWith('/api/')) {
    fullPath = `${apiBaseUrl.value}${fullPath}`
  }
  // 处理MinIO存储路径
  else if (!isAbsoluteUrl(fullPath)) {
    const pathParts = fullPath.split('/').filter((p: string) => p)
    const firstPart = pathParts[0]

    if (APP_CONFIG.MINIO_BUCKETS.includes(firstPart)) {
      fullPath = `${apiBaseUrl.value}/api/files/proxy/${fullPath}`
    } else {
      fullPath = `${apiBaseUrl.value}/api/files/proxy/slices/${fullPath.replace(/^\//, '')}`
    }
  }

  return fullPath
}

/**
 * 处理需要转换的文件格式
 */
const handleConvertibleFormat = async (obj: SceneObject): Promise<boolean> => {
  const fileExt = getFileExtension(obj.displayPath)

  // 检查是否有完成的切片输出
  if (obj.slicingTaskId && obj.slicingTaskStatus === 'Completed' && obj.slicingOutputPath) {
    // 使用切片输出
    let tilesetPath = obj.slicingOutputPath

    // 确保路径指向tileset.json
    if (!tilesetPath.endsWith('tileset.json')) {
      tilesetPath = tilesetPath.replace(/\/$|\\$/, '') + '/tileset.json'
    }

    // 使用resolveObjectUrl处理路径（支持本地路径和MinIO路径）
    const fullTilesetPath = resolveObjectUrl(tilesetPath)

    console.log('[CesiumViewer] 使用切片输出:', {
      original: obj.slicingOutputPath,
      tilesetPath,
      fullTilesetPath
    })

    // 加载3D Tiles
    return await loadTileset(obj, fullTilesetPath)
  } else {
    // 显示占位符标记
    const position = obj.position.every(coord => coord === 0) ? [APP_CONFIG.DEFAULT_POSITION.longitude, APP_CONFIG.DEFAULT_POSITION.latitude, APP_CONFIG.DEFAULT_POSITION.height] : obj.position

    if (!viewer) return false

    const cartesian = Cesium.Cartesian3.fromDegrees(position[0], position[1], position[2])

    viewer.entities.add({
      position: cartesian,
      point: {
        pixelSize: 20,
        color: Cesium.Color.ORANGE,
        outlineColor: Cesium.Color.WHITE,
        outlineWidth: 2
      },
      label: {
        text: `${obj.name}\n(${fileExt.toUpperCase()} - 需要切片转换)`,
        font: '14px sans-serif',
        fillColor: Cesium.Color.WHITE,
        outlineColor: Cesium.Color.BLACK,
        outlineWidth: 2,
        style: Cesium.LabelStyle.FILL_AND_OUTLINE,
        verticalOrigin: Cesium.VerticalOrigin.BOTTOM,
        pixelOffset: new Cesium.Cartesian2(0, -20)
      }
    })

    await flyToPosition(position)
    emit('objectLoaded', obj, false)
    return false
  }
}

/**
 * 创建3D Tiles数据集的配置选项
 */
const createTilesetOptions = () => ({
  // 设置最大屏幕空间误差，优化加载性能（降低值以强制加载更多瓦片）
  maximumScreenSpaceError: 2,
  // 禁用一些可能导致问题的特性
  skipLevelOfDetail: false,
  // 确保模型始终可见
  show: true,
  // 动态屏幕空间错误，根据相机距离自动调整细节
  dynamicScreenSpaceError: false,
  // 预加载当视野
  preloadWhenHidden: true,
  // 预加载兄弟节点
  preloadFlightDestinations: true,
  // 立即加载期望的细节级别
  immediatelyLoadDesiredLevelOfDetail: true,
  // 加载兄弟节点
  loadSiblings: true,
  // 调试选项
  debugShowBoundingVolume: import.meta.env.DEV,
  debugShowContentBoundingVolume: import.meta.env.DEV
})

/**
 * 检查是否需要应用自定义变换
 */
const hasCustomTransform = (obj: SceneObject, parsedRotation: any, parsedScale: any): boolean => {
  return (obj.position && (obj.position[0] !== 116.39 || obj.position[1] !== 39.91 || obj.position[2] !== 100)) ||
         (parsedRotation.x !== 0 || parsedRotation.y !== 0 || parsedRotation.z !== 0) ||
         (parsedScale.x !== 1 || parsedScale.y !== 1 || parsedScale.z !== 1)
}

/**
 * 处理本地坐标系的tileset
 */
const handleLocalCoordinateTileset = async (
  tileset: Cesium.Cesium3DTileset,
  obj: SceneObject,
  parsedRotation: any,
  parsedScale: any
): Promise<number[]> => {
  const originalRadius = tileset.boundingSphere?.radius ?? 0
  console.log('[loadTileset] 模型原始包围球半径:', originalRadius)

  // 如果模型太小（半径小于10米），自动放大
  let adjustedScale = { ...parsedScale }
  if (originalRadius > 0 && originalRadius < 10) {
    const scaleFactor = APP_CONFIG.PERFORMANCE.SCALE_FACTOR / originalRadius
    adjustedScale = {
      x: parsedScale.x * scaleFactor,
      y: parsedScale.y * scaleFactor,
      z: parsedScale.z * scaleFactor
    }
    console.log('[loadTileset] 模型太小，自动放大scale:', scaleFactor, '倍，新scale:', adjustedScale)
  }

  // 对于本地坐标系的模型，强制应用modelMatrix
  const modelMatrix = createModelMatrix(obj.position, parsedRotation, adjustedScale)
  tileset.modelMatrix = modelMatrix
  console.log('[loadTileset] 已强制应用modelMatrix')

  // 确保tileset可见性设置
  tileset.show = true
  tileset.style = new Cesium.Cesium3DTileStyle({ show: true })
  console.log('[loadTileset] 已设置tileset可见性')

  // 等待一下让boundingSphere更新
  await new Promise(resolve => setTimeout(resolve, 200))

  return obj.position
}


/**
 * 加载3D Tiles数据集
 */
const loadTileset = async (obj: SceneObject, url: string): Promise<boolean> => {
  if (!viewer) return false

  console.log('[loadTileset] 开始加载 tileset，URL:', url)

  try {
    // 创建带超时的Promise来避免异步响应消息通道关闭问题
    const loadTilesetPromise = Cesium.Cesium3DTileset.fromUrl(url, createTilesetOptions())
    console.log('[loadTileset] 等待 tileset 加载...')

    // 添加超时控制
    const timeoutPromise = new Promise<never>((_, reject) => {
      setTimeout(() => reject(new Error('Tileset加载超时')), APP_CONFIG.PERFORMANCE.TILES_LOAD_TIMEOUT)
    })

    const tileset = await Promise.race([loadTilesetPromise, timeoutPromise])
    console.log('[loadTileset] Tileset 加载成功:', tileset)

    // 在开发环境下获取tileset.json的元数据用于调试
    if (import.meta.env.DEV) {
      try {
        const response = await fetch(url)
        const tilesetJson = await response.json()
        console.log('[loadTileset] Tileset.json内容:', tilesetJson)
        console.log('[loadTileset] Root content URI:', tilesetJson.root?.content?.uri)
        console.log('[loadTileset] Root geometricError:', tilesetJson.root?.geometricError)
      } catch (e) {
        console.warn('[loadTileset] 无法获取tileset.json内容:', e)
      }
    }

    // 解析旋转和缩放参数
    const { parsedRotation, parsedScale } = parseTransformParams(obj.rotation, obj.scale)
    const customTransform = hasCustomTransform(obj, parsedRotation, parsedScale)

    console.log('[loadTileset] 是否有自定义变换:', customTransform)

    // 检查boundingBox是否全为0（无效的包围盒）
    const hasBoundingSphere = tileset.boundingSphere && tileset.boundingSphere.radius > 0
    const boundingSphereIsValid = hasBoundingSphere &&
                                  !isNaN(tileset.boundingSphere.center.x) &&
                                  !isNaN(tileset.boundingSphere.center.y) &&
                                  !isNaN(tileset.boundingSphere.center.z) &&
                                  (tileset.boundingSphere.center.x !== 0 ||
                                   tileset.boundingSphere.center.y !== 0 ||
                                   tileset.boundingSphere.center.z !== 0)

    console.log('[loadTileset] BoundingSphere有效性:', boundingSphereIsValid)

    // 如果有自定义变换或包围盒无效，强制应用modelMatrix
    if (customTransform || !boundingSphereIsValid) {
      const modelMatrix = createModelMatrix(obj.position, parsedRotation, parsedScale)
      tileset.modelMatrix = modelMatrix
      console.log('[loadTileset] 强制应用modelMatrix（自定义变换或包围盒无效）')
    } else {
      console.log('[loadTileset] 使用tileset自带的transform')
    }

    viewer.scene.primitives.add(tileset)
    console.log('[loadTileset] Tileset 已添加到场景')

    // 强制设置tileset可见
    tileset.show = true
    tileset.style = new Cesium.Cesium3DTileStyle({ show: true })
    console.log('[loadTileset] 强制设置tileset可见性')

    // 等待tileset初始加载完成，确保boundingSphere已经计算
    let attempts = 0
    const maxAttempts = 20
    while (attempts < maxAttempts && (!tileset.boundingSphere || tileset.boundingSphere.radius === 0)) {
      await new Promise(resolve => setTimeout(resolve, 100))
      attempts++
    }

    console.log('[loadTileset] BoundingSphere状态:', {
      exists: !!tileset.boundingSphere,
      radius: tileset.boundingSphere?.radius ?? 'undefined',
      center: tileset.boundingSphere?.center ?? 'undefined'
    })

    // 确定最终使用的位置和包围球中心
    let actualPosition: number[]
    let center: Cesium.Cartesian3

    // 如果包围盒无效或者强制应用了modelMatrix，直接使用对象位置
    if (!boundingSphereIsValid || customTransform) {
      actualPosition = obj.position
      center = Cesium.Cartesian3.fromDegrees(obj.position[0], obj.position[1], obj.position[2])
      console.log('[loadTileset] 使用对象位置作为中心:', actualPosition)
    } else {
      // 使用tileset的boundingSphere中心
      const bsCenter = tileset.boundingSphere.center
      const cartographic = Cesium.Cartographic.fromCartesian(bsCenter)
      if (cartographic) {
        actualPosition = [
          Cesium.Math.toDegrees(cartographic.longitude),
          Cesium.Math.toDegrees(cartographic.latitude),
          cartographic.height
        ]
        center = bsCenter
        console.log('[loadTileset] 使用tileset包围球中心:', actualPosition)
      } else {
        actualPosition = obj.position
        center = Cesium.Cartesian3.fromDegrees(obj.position[0], obj.position[1], obj.position[2])
      }
    }

    const cartesian = center
    loadedModels.set(obj.id, { type: '3dtiles', object: tileset, position: cartesian })

    // 更新对象计数
    loadedObjectsCount.value = loadedModels.size

    // 强制加载所有瓦片（设置为0以绕过LOD检查）
    tileset.maximumScreenSpaceError = 0

    // 强制更新以立即加载瓦片
    viewer.scene.requestRender()

    // 添加延迟避免异步响应问题
    await new Promise(resolve => setTimeout(resolve, 100))
    await flyToTileset(tileset, cartesian)

    console.log('[loadTileset] Tileset 加载完成')
    emit('objectLoaded', obj, true)
    return true
  } catch (error) {
    const errorMessage = `加载 ${obj.name} 的 3D 瓦片数据集失败: ${error instanceof Error ? error.message : String(error)}`
    console.error('[loadTileset] 错误:', errorMessage)
    console.error('[loadTileset] 错误详情:', error)
    showError(errorMessage)
    emit('objectLoaded', obj, false)
    return false
  }
}

/**
 * 创建GLTF模型的加载选项
 */
const createModelOptions = (url: string, modelMatrix: Cesium.Matrix4) => ({
  url,
  modelMatrix,
  // 混合模式
  colorBlendMode: Cesium.ColorBlendMode.MIX,
  // 大幅增加最大缩放以确保模型可见
  maximumScale: Number.MAX_VALUE,
  // 设置最小像素大小，确保即使很远也能看到
  minimumPixelSize: 128,
  // 允许选取（用于交互）
  allowPicking: true,
  // 模型始终可见，不受视距影响
  show: true,
  // 禁用距离显示条件，确保始终渲染
  distanceDisplayCondition: undefined,
  // 启用深度测试，确保正确的遮挡关系
  scene: viewer!.scene
})

/**
 * 等待模型就绪
 */
const waitForModelReady = (model: Cesium.Model): Promise<void> => {
  return new Promise<void>((resolve, reject) => {
    if (model.ready) {
      console.log('[loadGltfModel] 模型已经就绪')
      resolve()
    } else {
      const removeListener = model.readyEvent.addEventListener(() => {
        console.log('[loadGltfModel] 模型就绪事件触发')
        removeListener()
        resolve()
      })

      // 设置超时
      setTimeout(() => {
        console.warn('[loadGltfModel] 等待模型就绪超时')
        removeListener()
        reject(new Error('Model ready timeout'))
      }, APP_CONFIG.PERFORMANCE.MODEL_LOAD_TIMEOUT)
    }
  })
}

/**
 * 处理模型的相机飞行
 */
const flyToModel = async (model: Cesium.Model, obj: SceneObject): Promise<void> => {
  try {
    const boundingSphere = model.boundingSphere
    if (boundingSphere && boundingSphere.radius > 0) {
      // 计算相机距离：包围球半径的3倍
      const distance = boundingSphere.radius * 3.0
      console.log('[loadGltfModel] 计算的相机距离:', distance)

      await viewer!.camera.flyToBoundingSphere(boundingSphere, {
        duration: APP_CONFIG.CAMERA_FLIGHT_CONFIG.duration,
        offset: new Cesium.HeadingPitchRange(0, -0.5, distance)
      })
      console.log('[loadGltfModel] 已飞向模型')
    } else {
      console.warn('[loadGltfModel] 包围球无效，使用备用方案')
      await flyToPosition(obj.position)
    }
  } catch (flyError) {
    console.warn('[loadGltfModel] 飞向模型失败，使用备用方案:', flyError)
    await flyToPosition(obj.position)
  }
}

/**
 * 添加模型相机监听器
 */
const addModelCameraListener = (model: Cesium.Model): void => {
  const cameraListener = viewer!.camera.moveEnd.addEventListener(() => {
    if (model && !model.isDestroyed()) {
      model.show = true
    }
  })
  modelCameraListeners.set(model, cameraListener)
}

/**
 * 加载glTF/GLB模型
 */
const loadGltfModel = async (obj: SceneObject, url: string): Promise<boolean> => {
  if (!viewer) return false

  console.log('[loadGltfModel] 开始加载 GLB/GLTF 模型，URL:', url)

  try {
    console.log('[loadGltfModel] 解析变换参数...')
    const { parsedRotation, parsedScale } = parseTransformParams(obj.rotation, obj.scale)
    console.log('[loadGltfModel] 位置:', obj.position)
    console.log('[loadGltfModel] 旋转:', parsedRotation)
    console.log('[loadGltfModel] 缩放:', parsedScale)

    const modelMatrix = createModelMatrix(obj.position, parsedRotation, parsedScale)
    console.log('[loadGltfModel] ModelMatrix 创建完成')

    console.log('[loadGltfModel] 开始从 URL 加载模型...')
    const model = await Cesium.Model.fromGltfAsync(createModelOptions(url, modelMatrix))

    console.log('[loadGltfModel] 模型加载成功:', model)

    viewer.scene.primitives.add(model)
    console.log('[loadGltfModel] 模型已添加到场景')

    const cartesian = Cesium.Cartesian3.fromDegrees(obj.position[0], obj.position[1], obj.position[2])
    loadedModels.set(obj.id, { type: 'model', object: model, position: cartesian })

    // 更新对象计数
    loadedObjectsCount.value = loadedModels.size

    console.log('[loadGltfModel] 准备飞向模型位置...')

    try {
      await waitForModelReady(model)
      console.log('[loadGltfModel] 模型已就绪')

      // 确保模型始终可见
      model.show = true
      console.log('[loadGltfModel] 设置模型可见性: true')

      // 添加相机移动监听，确保模型始终可见
      addModelCameraListener(model)

      // 飞向模型位置
      await flyToModel(model, obj)
    } catch (readyError) {
      console.error('[loadGltfModel] 模型就绪等待失败:', readyError)
      // 备用：直接飞向位置
      await flyToPosition(obj.position)
    }

    console.log('[loadGltfModel] GLB/GLTF 模型加载完成')
    emit('objectLoaded', obj, true)
    return true
  } catch (error) {
    const errorMessage = `加载 ${obj.name} 的 glTF/GLB 模型失败: ${error instanceof Error ? error.message : String(error)}`
    console.error('[loadGltfModel] 错误:', errorMessage)
    console.error('[loadGltfModel] 错误详情:', error)
    showError(errorMessage)
    emit('objectLoaded', obj, false)
    return false
  }
}

/**
 * 飞向3D Tiles数据集
 */
const flyToTileset = async (tileset: Cesium.Cesium3DTileset, fallbackPosition: Cesium.Cartesian3): Promise<void> => {
  if (!viewer) return

  try {
    const radius = tileset.boundingSphere.radius
    console.log('[flyToTileset] BoundingSphere半径:', radius)

    // 对于小模型或本地坐标系模型，使用更大的距离
    const minDistance = APP_CONFIG.PERFORMANCE.MIN_DISTANCE
    const distance = Math.max(radius * 3.0, minDistance)
    console.log('[flyToTileset] 计算的相机距离:', distance)

    await viewer.flyTo(tileset, {
      duration: APP_CONFIG.CAMERA_FLIGHT_CONFIG.duration,
      offset: new Cesium.HeadingPitchRange(0, -0.5, distance)
    })
    console.log('[flyToTileset] 飞行完成')
  } catch (error) {
    console.warn('[flyToTileset] 飞向瓦片数据集失败，使用备用位置:', error)
    try {
      viewer.camera.flyTo({
        destination: fallbackPosition,
        orientation: {
          heading: Cesium.Math.toRadians(APP_CONFIG.CAMERA_FLIGHT_CONFIG.heading),
          pitch: Cesium.Math.toRadians(APP_CONFIG.CAMERA_FLIGHT_CONFIG.pitch),
          roll: APP_CONFIG.CAMERA_FLIGHT_CONFIG.roll
        },
        duration: APP_CONFIG.CAMERA_FLIGHT_CONFIG.duration
      })
    } catch (fallbackError) {
      console.error('备用相机飞行也失败:', fallbackError)
    }
  }
}

/**
 * 验证场景对象的基本属性
 * @param obj 场景对象
 * @returns 验证结果
 */
const validateSceneObject = (obj: SceneObject): { isValid: boolean; error?: string } => {
  if (!obj) {
    return { isValid: false, error: '对象为空' }
  }

  if (!obj.id || typeof obj.id !== 'string') {
    return { isValid: false, error: '对象ID无效' }
  }

  if (!obj.name || typeof obj.name !== 'string') {
    return { isValid: false, error: '对象名称无效' }
  }

  if (!obj.displayPath || typeof obj.displayPath !== 'string') {
    return { isValid: false, error: '显示路径无效' }
  }

  if (!Array.isArray(obj.position) || obj.position.length !== 3) {
    return { isValid: false, error: '位置信息无效' }
  }

  return { isValid: true }
}

/**
 * 规范化对象位置
 * @param obj 场景对象
 * @returns 规范化后的位置
 */
const normalizeObjectPosition = (obj: SceneObject): [number, number, number] => {
  const position = obj.position

  // 检查位置是否有效
  if (position.every(coord => coord === 0)) {
    console.warn(`对象 ${obj.name} 的位置无效 [0,0,0]，使用默认位置`)
    return [
      APP_CONFIG.DEFAULT_POSITION.longitude,
      APP_CONFIG.DEFAULT_POSITION.latitude,
      APP_CONFIG.DEFAULT_POSITION.height
    ]
  }

  // 检查位置坐标是否在合理范围内
  const [longitude, latitude, height] = position
  if (longitude < -180 || longitude > 180 || latitude < -90 || latitude > 90 || height < -1000) {
    console.warn(`对象 ${obj.name} 的位置超出合理范围，使用默认位置`, position)
    return [
      APP_CONFIG.DEFAULT_POSITION.longitude,
      APP_CONFIG.DEFAULT_POSITION.latitude,
      APP_CONFIG.DEFAULT_POSITION.height
    ]
  }

  return position
}

/**
 * 确定对象加载策略
 * @param fullPath 完整路径
 * @param fileExt 文件扩展名
 * @returns 加载策略
 */
const determineLoadStrategy = (fullPath: string, fileExt: string): 'tileset' | 'gltf' | 'convertible' | null => {
  if (APP_CONFIG.SUPPORTED_FORMATS.convertible.includes(fileExt)) {
    return 'convertible'
  }

  if (!APP_CONFIG.SUPPORTED_FORMATS.nativelySupported.includes(fileExt)) {
    return null
  }

  if (fullPath.endsWith('tileset.json') || fullPath.includes('/tileset.json')) {
    return 'tileset'
  }

  if (['gltf', 'glb'].includes(fileExt)) {
    return 'gltf'
  }

  return null
}

/**
 * 加载单个场景对象
 * @param obj 场景对象
 */
const loadSceneObject = async (obj: SceneObject): Promise<void> => {
  try {
    console.log('[CesiumViewer] 开始加载场景对象:', obj.name)

    // 验证对象
    const validation = validateSceneObject(obj)
    if (!validation.isValid) {
      console.error(`[CesiumViewer] 对象验证失败: ${validation.error}`)
      emit('objectLoaded', obj, false)
      return
    }

    // 规范化位置
    obj.position = normalizeObjectPosition(obj)

    // 解析URL
    const fullPath = resolveObjectUrl(obj.displayPath)
    console.log('[CesiumViewer] fullPath:', fullPath)

    // 检查文件格式
    const fileExt = getFileExtension(fullPath)
    console.log('[CesiumViewer] fileExt:', fileExt)

    // 确定加载策略
    const strategy = determineLoadStrategy(fullPath, fileExt)

    switch (strategy) {
      case 'convertible':
        console.log('[CesiumViewer] 格式需要转换，调用 handleConvertibleFormat')
        await handleConvertibleFormat(obj)
        break
      case 'tileset':
        console.log('[CesiumViewer] 检测到 tileset.json，开始加载 3D Tiles')
        await loadTileset(obj, fullPath)
        break
      case 'gltf':
        console.log('[CesiumViewer] 检测到 GLTF/GLB 模型，开始加载')
        await loadGltfModel(obj, fullPath)
        break
      default:
        console.error(`不支持的文件格式: .${fileExt}`)
        emit('objectLoaded', obj, false)
    }
  } catch (error) {
    const errorMessage = `加载对象 ${obj.name} 失败: ${error instanceof Error ? error.message : String(error)}`
    console.error('[loadSceneObject] 错误:', errorMessage, error)
    showError(errorMessage)

    emit('objectLoaded', obj, false)
  }
}

/**
 * 加载场景对象列表（优化版本）
 */
const loadSceneObjects = async (objects: SceneObject[]): Promise<void> => {
  if (!viewer) {
    throw new Error('查看器未初始化')
  }

  // 清除之前加载的对象
  clearLoadedObjects()

  if (!objects || objects.length === 0) {
    console.log('没有场景对象需要加载')
    return
  }

  console.log(`[loadSceneObjects] 开始加载 ${objects.length} 个场景对象`)

  // 过滤和验证对象
  const validObjects = objects.filter(obj => {
    const validation = validateSceneObject(obj)
    if (!validation.isValid) {
      console.warn(`[loadSceneObjects] 跳过无效对象 ${obj.name}: ${validation.error}`)
      return false
    }
    return true
  })

  if (validObjects.length === 0) {
    console.warn('[loadSceneObjects] 没有有效的场景对象')
    return
  }

  // 批量并发加载对象（限制并发数量避免性能问题）
  const batchSize = APP_CONFIG.PERFORMANCE.BATCH_SIZE
  const batches = []

  for (let i = 0; i < validObjects.length; i += batchSize) {
    batches.push(validObjects.slice(i, i + batchSize))
  }

  // 串行处理批次，避免过多的并发请求
  for (const batch of batches) {
    try {
      await Promise.all(batch.map(loadSceneObject))
      // 在批次间添加小延迟，避免资源竞争
      if (batches.length > 1) {
        await new Promise(resolve => setTimeout(resolve, 50))
      }
    } catch (error) {
      console.error('[loadSceneObjects] 批次加载失败:', error)
      // 继续处理下一批次，不中断整个加载过程
    }
  }

  // 调整相机以显示所有对象
  try {
    await adjustCameraForLoadedObjects()
  } catch (error) {
    console.warn('[loadSceneObjects] 调整相机失败:', error)
  }

  console.log(`[loadSceneObjects] 成功加载 ${loadedModels.size} 个对象`)
}

/**
 * 为已加载的对象调整相机视角
 */
const adjustCameraForLoadedObjects = async (): Promise<void> => {
  if (!viewer || loadedModels.size === 0) return

  if (loadedModels.size === 1) {
    // 单个对象已经通过flyTo处理过了
    return
  }

  try {
    const positions = Array.from(loadedModels.values()).map(item => item.position)
    const boundingSphere = Cesium.BoundingSphere.fromPoints(positions)

    await viewer.camera.flyToBoundingSphere(boundingSphere, {
      duration: APP_CONFIG.CAMERA_FLIGHT_CONFIG.duration,
      offset: new Cesium.HeadingPitchRange(0, -0.5, boundingSphere.radius * 3.0)
    })
  } catch (error) {
    const errorMessage = `为多个对象调整相机失败: ${error instanceof Error ? error.message : String(error)}`
    console.warn(errorMessage)
    showError(errorMessage)
  }
}

/**
 * 清除已加载的对象
 */
const clearLoadedObjects = (): void => {
  if (!viewer) return

  loadedModels.forEach((item) => {
    // 清除相机监听器（如果是Model）
    if (item.type === 'model') {
      const model = item.object as Cesium.Model
      const listener = modelCameraListeners.get(model)
      if (listener) {
        listener() // 调用清理函数
        modelCameraListeners.delete(model)
      }
    }
    // 从场景中移除对象
    viewer!.scene.primitives.remove(item.object)
  })
  loadedModels.clear()

  // 重置计数器
  loadedObjectsCount.value = 0

  // 清除所有Entity标记
  viewer!.entities.removeAll()
}

// ==================== 初始化Cesium ====================

/**
 * 创建Cesium查看器的配置选项
 */
const createViewerOptions = () => ({
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

  // 渲染设置（优化性能）
  shadows: false,             // 禁用阴影以提升性能
  shouldAnimate: true,        // 自动动画

  // 请求渲染模式（优化性能）- 仅在场景变化时渲染
  requestRenderMode: true,    // 启用请求渲染模式
  maximumRenderTimeChange: 0.0,  // 设为0以提高响应性

  // 场景配置
  scene3DOnly: false,         // 允许2D/3D/Columbus视图

  // MSAA抗锯齿（性能优化）
  msaaSamples: 2              // 降低MSAA采样数（4 -> 2）
})

/**
 * 设置初始相机位置
 */
const setupInitialCamera = (viewer: Cesium.Viewer): void => {
  try {
    const position = props.initialPosition
    if (!position || typeof position.longitude !== 'number' || typeof position.latitude !== 'number' || typeof position.height !== 'number') {
      throw new Error('初始相机位置无效')
    }

    viewer.camera.setView({
      destination: Cesium.Cartesian3.fromDegrees(
        position.longitude,
        position.latitude,
        position.height
      ),
      orientation: {
        heading: Cesium.Math.toRadians(APP_CONFIG.CAMERA_FLIGHT_CONFIG.heading),
        pitch: Cesium.Math.toRadians(APP_CONFIG.CAMERA_FLIGHT_CONFIG.pitch),
        roll: APP_CONFIG.CAMERA_FLIGHT_CONFIG.roll
      }
    })
  } catch (error) {
    console.warn('[setupInitialCamera] 设置初始相机位置失败，使用默认位置:', error)
    // 使用默认位置
    viewer.camera.setView({
      destination: Cesium.Cartesian3.fromDegrees(
        APP_CONFIG.DEFAULT_POSITION.longitude,
        APP_CONFIG.DEFAULT_POSITION.latitude,
        APP_CONFIG.DEFAULT_POSITION.height
      ),
      orientation: {
        heading: Cesium.Math.toRadians(APP_CONFIG.CAMERA_FLIGHT_CONFIG.heading),
        pitch: Cesium.Math.toRadians(APP_CONFIG.CAMERA_FLIGHT_CONFIG.pitch),
        roll: APP_CONFIG.CAMERA_FLIGHT_CONFIG.roll
      }
    })
  }
}

/**
 * 隐藏Cesium Logo
 */
const hideCesiumLogo = (viewer: Cesium.Viewer): void => {
  try {
    const creditContainer = viewer.cesiumWidget.creditContainer as HTMLElement
    if (creditContainer) {
      creditContainer.style.display = 'none'
    }
  } catch (error) {
    console.warn('[hideCesiumLogo] 隐藏Cesium Logo失败:', error)
  }
}

/**
 * 初始化坐标轴辅助器
 */
const initAxesHelper = (viewer: Cesium.Viewer): void => {
  try {
    // 缩短坐标轴长度以提升性能
    const axisLength = 500000 // 500km，原来是1000km

    // 创建原点
    const origin = Cesium.Cartesian3.fromDegrees(0, 0, 0)

    // X轴 (红色 - 东)
    const xEnd = Cesium.Cartesian3.fromDegrees(axisLength / 111320, 0, 0)
    viewer.entities.add({
      id: 'axis-x',
      polyline: {
        positions: [origin, xEnd],
        width: 2,
        material: Cesium.Color.RED,
        clampToGround: false
      },
      show: axesVisible.value
    })

    // Y轴 (绿色 - 北)
    const yEnd = Cesium.Cartesian3.fromDegrees(0, axisLength / 111320, 0)
    viewer.entities.add({
      id: 'axis-y',
      polyline: {
        positions: [origin, yEnd],
        width: 2,
        material: Cesium.Color.GREEN,
        clampToGround: false
      },
      show: axesVisible.value
    })

    // Z轴 (蓝色 - 上)
    const zEnd = Cesium.Cartesian3.fromDegrees(0, 0, axisLength)
    viewer.entities.add({
      id: 'axis-z',
      polyline: {
        positions: [origin, zEnd],
        width: 2,
        material: Cesium.Color.BLUE,
        clampToGround: false
      },
      show: axesVisible.value
    })

    console.log('[initAxesHelper] 坐标轴创建成功')
  } catch (error) {
    console.warn('[initAxesHelper] 创建坐标轴失败:', error)
  }
}

/**
 * 初始化网格辅助器
 */
const initGridHelper = (viewer: Cesium.Viewer): void => {
  try {
    // 经度线（南北向）- 每30度一条，减少线条数量提升性能
    for (let lon = -180; lon <= 180; lon += 30) {
      const positions = []
      // 减少点的密度，每10度采样一次
      for (let lat = -85; lat <= 85; lat += 10) {
        positions.push(Cesium.Cartesian3.fromDegrees(lon, lat, 0))
      }
      viewer.entities.add({
        id: `grid-lon-${lon}`,
        polyline: {
          positions: positions,
          width: 1,
          material: Cesium.Color.WHITE.withAlpha(0.15),
          clampToGround: true
        },
        show: gridVisible.value
      })
    }

    // 纬度线（东西向）- 每30度一条，减少线条数量提升性能
    for (let lat = -60; lat <= 60; lat += 30) {
      const positions = []
      // 减少点的密度，每10度采样一次
      for (let lon = -180; lon <= 180; lon += 10) {
        positions.push(Cesium.Cartesian3.fromDegrees(lon, lat, 0))
      }
      viewer.entities.add({
        id: `grid-lat-${lat}`,
        polyline: {
          positions: positions,
          width: 1,
          material: Cesium.Color.WHITE.withAlpha(0.15),
          clampToGround: true
        },
        show: gridVisible.value
      })
    }

    console.log('[initGridHelper] 网格创建成功')
  } catch (error) {
    console.warn('[initGridHelper] 创建网格失败:', error)
  }
}

/**
 * 初始化Cesium查看器
 */
const initCesium = async (): Promise<void> => {
  if (!cesiumContainer.value) {
    throw new Error('Cesium容器未找到')
  }

  try {
    console.log('[initCesium] 开始初始化Cesium查看器')

    // 创建Cesium查看器
    viewer = new Cesium.Viewer(cesiumContainer.value, createViewerOptions())

    // 配置globe设置（必须在viewer创建后设置）
    if (viewer.scene.globe) {
      viewer.scene.globe.depthTestAgainstTerrain = false  // 禁用地形深度测试，避免模型被遮挡
      viewer.scene.globe.enableLighting = false  // 禁用全局光照以提升性能
    }

    // 场景性能优化设置
    if (viewer.scene.fog) {
      viewer.scene.fog.enabled = false  // 禁用雾效
    }

    if (viewer.scene.skyAtmosphere) {
      viewer.scene.skyAtmosphere.show = true  // 保持大气层显示（视觉效果）
    }

    if (viewer.scene.sun) {
      viewer.scene.sun.show = false  // 隐藏太阳
    }

    if (viewer.scene.moon) {
      viewer.scene.moon.show = false  // 隐藏月亮
    }

    // 优化地球渲染
    if (viewer.scene.globe) {
      viewer.scene.globe.tileCacheSize = 100  // 减少瓦片缓存大小（默认100）
      viewer.scene.globe.maximumScreenSpaceError = 2  // 适当降低屏幕空间误差以提升性能
    }

    // 隐藏Cesium Logo
    hideCesiumLogo(viewer)

    // 设置初始相机位置
    setupInitialCamera(viewer)

    // 添加坐标轴辅助器
    initAxesHelper(viewer)

    // 添加网格辅助器
    initGridHelper(viewer)

    // 设置相机运动事件监听
    viewer.camera.moveEnd.addEventListener(updateCameraInfo)

    // 初始化相机信息
    updateCameraInfo()

    // 启动性能监控
    startPerformanceMonitoring()

    // 初始加载场景对象
    if (props.sceneObjects && Array.isArray(props.sceneObjects) && props.sceneObjects.length > 0) {
      console.log(`[initCesium] 开始加载 ${props.sceneObjects.length} 个场景对象`)
      await loadSceneObjects(props.sceneObjects)
    }

    loading.value = false
    console.log('[initCesium] Cesium查看器初始化完成')
    emit('ready', viewer)
  } catch (error) {
    const errorMessage = `Cesium 初始化失败: ${error instanceof Error ? error.message : String(error)}`
    console.error('[initCesium] 初始化失败:', error)

    loading.value = false

    // 清理失败的查看器实例
    if (viewer) {
      try {
        viewer.destroy()
        viewer = null
      } catch (destroyError) {
        console.error('[initCesium] 清理失败的查看器时出错:', destroyError)
      }
    }

    showError(errorMessage)
    emit('error', error instanceof Error ? error : new Error(String(error)))
  }
}

// ==================== 相机控制 ====================

/**
 * 更新相机信息（带防抖优化）
 */
let cameraUpdateTimer: number | null = null

const updateCameraInfo = () => {
  if (!viewer) return

  // 清除之前的定时器
  if (cameraUpdateTimer) {
    clearTimeout(cameraUpdateTimer)
  }

  // 延迟更新，避免频繁计算
  cameraUpdateTimer = window.setTimeout(() => {
    try {
      const cameraPosition = viewer!.camera.positionCartographic

      if (cameraPosition) {
        cameraInfo.value = {
          longitude: parseFloat(Cesium.Math.toDegrees(cameraPosition.longitude).toFixed(4)),
          latitude: parseFloat(Cesium.Math.toDegrees(cameraPosition.latitude).toFixed(4)),
          height: Math.round(cameraPosition.height)
        }
      }
    } catch (error) {
      console.warn('[updateCameraInfo] 更新相机信息失败:', error)
    }
    cameraUpdateTimer = null
  }, APP_CONFIG.UI.CAMERA_UPDATE_DEBOUNCE)
}

/**
 * 重置视图 - 智能飞向场景对象或初始位置
 */
const resetView = async (): Promise<void> => {
  if (!viewer) {
    showError('Cesium 查看器未初始化')
    return
  }

  try {
    // 如果有加载的模型，飞向所有模型
    if (loadedModels.size > 0) {
      console.log('[resetView] 飞向已加载的场景对象')

      if (loadedModels.size === 1) {
        // 单个对象，飞向该对象
        const modelInfo = Array.from(loadedModels.values())[0]
        const position = modelInfo.position

        if (modelInfo.type === '3dtiles') {
          const tileset = modelInfo.object as Cesium.Cesium3DTileset
          await viewer.flyTo(tileset, {
            duration: APP_CONFIG.CAMERA_FLIGHT_CONFIG.duration,
            offset: new Cesium.HeadingPitchRange(0, -0.5, Math.max(tileset.boundingSphere.radius * 3.0, 500))
          })
        } else {
          // Model类型 - 飞向模型的包围球
          const model = modelInfo.object as Cesium.Model
          if (model.boundingSphere && model.boundingSphere.radius > 0) {
            await viewer.camera.flyToBoundingSphere(model.boundingSphere, {
              duration: APP_CONFIG.CAMERA_FLIGHT_CONFIG.duration,
              offset: new Cesium.HeadingPitchRange(0, -0.5, Math.max(model.boundingSphere.radius * 3.0, 500))
            })
          } else {
            // 备用方案：使用固定偏移量
            const offset = new Cesium.Cartesian3(500, 500, 500)
            const cameraPosition = Cesium.Cartesian3.add(position, offset, new Cesium.Cartesian3())

            await viewer.camera.flyTo({
              destination: cameraPosition,
              orientation: {
                heading: Cesium.Math.toRadians(APP_CONFIG.CAMERA_FLIGHT_CONFIG.heading),
                pitch: Cesium.Math.toRadians(APP_CONFIG.CAMERA_FLIGHT_CONFIG.pitch),
                roll: APP_CONFIG.CAMERA_FLIGHT_CONFIG.roll
              },
              duration: APP_CONFIG.CAMERA_FLIGHT_CONFIG.duration
            })
          }
        }
      } else {
        // 多个对象，飞向包围盒中心
        const positions = Array.from(loadedModels.values()).map(item => item.position)
        const boundingSphere = Cesium.BoundingSphere.fromPoints(positions)

        await viewer.camera.flyToBoundingSphere(boundingSphere, {
          duration: APP_CONFIG.CAMERA_FLIGHT_CONFIG.duration,
          offset: new Cesium.HeadingPitchRange(0, -0.5, boundingSphere.radius * 3.0)
        })
      }
    } else {
      // 没有模型，飞向初始位置（使用合理的高度）
      console.log('[resetView] 没有场景对象，飞向初始位置')
      const reasonableHeight = 10000 // 10km，而不是15000km

      await viewer.camera.flyTo({
        destination: Cesium.Cartesian3.fromDegrees(
          props.initialPosition.longitude,
          props.initialPosition.latitude,
          reasonableHeight
        ),
        orientation: {
          heading: Cesium.Math.toRadians(APP_CONFIG.CAMERA_FLIGHT_CONFIG.heading),
          pitch: Cesium.Math.toRadians(APP_CONFIG.CAMERA_FLIGHT_CONFIG.pitch),
          roll: APP_CONFIG.CAMERA_FLIGHT_CONFIG.roll
        },
        duration: APP_CONFIG.CAMERA_FLIGHT_CONFIG.duration
      })
    }
  } catch (error) {
    const errorMessage = `重置视图失败: ${error instanceof Error ? error.message : String(error)}`
    console.error(errorMessage)
    showError(errorMessage)
  }
}

/**
 * 截图
 */
const takeScreenshot = (): void => {
  if (!viewer) {
    showError('Cesium 查看器未初始化')
    return
  }

  try {
    viewer.render()

    const canvas = viewer.scene.canvas
    const image = canvas.toDataURL('image/png')

    const link = document.createElement('a')
    link.download = `cesium-screenshot-${Date.now()}.png`
    link.href = image
    link.click()

    showSuccess('Screenshot saved successfully')
  } catch (error) {
    const errorMessage = `截图失败: ${error instanceof Error ? error.message : String(error)}`
    console.error(errorMessage)
    showError(errorMessage)
  }
}

/**
 * 切换线框模式
 * 注意：Cesium的3D Tiles不直接支持线框模式，此功能主要用于界面一致性
 */
const toggleWireframe = (): void => {
  wireframeMode.value = !wireframeMode.value

  // Cesium的3D Tiles和Model不直接支持线框模式
  // 这里仅切换状态，保持UI一致性
  showSuccess(wireframeMode.value ? '线框模式（Cesium不支持）' : '线框模式已禁用')
}

/**
 * 切换坐标轴
 */
const toggleAxes = (): void => {
  if (!viewer) return

  axesVisible.value = !axesVisible.value

  // 通过ID获取坐标轴实体并设置显示状态
  const axisX = viewer.entities.getById('axis-x')
  const axisY = viewer.entities.getById('axis-y')
  const axisZ = viewer.entities.getById('axis-z')

  if (axisX) axisX.show = axesVisible.value
  if (axisY) axisY.show = axesVisible.value
  if (axisZ) axisZ.show = axesVisible.value

  showSuccess(axesVisible.value ? '坐标轴已显示' : '坐标轴已隐藏')
}

/**
 * 切换网格
 */
const toggleGrid = (): void => {
  if (!viewer) return

  gridVisible.value = !gridVisible.value

  // 控制所有网格线的显示状态（匹配30度间隔）
  for (let lon = -180; lon <= 180; lon += 30) {
    const entity = viewer.entities.getById(`grid-lon-${lon}`)
    if (entity) entity.show = gridVisible.value
  }

  for (let lat = -60; lat <= 60; lat += 30) {
    const entity = viewer.entities.getById(`grid-lat-${lat}`)
    if (entity) entity.show = gridVisible.value
  }

  showSuccess(gridVisible.value ? '网格已显示' : '网格已隐藏')
}

// ==================== FPS监控 ====================

let fpsAnimationId: number | null = null

/**
 * 启动性能监控
 */
const startPerformanceMonitoring = (): void => {
  const updatePerformance = () => {
    frameCount++
    const currentTime = performance.now()

    if (currentTime >= lastTime + APP_CONFIG.UI.FPS_MONITOR_INTERVAL) {
      fps.value = Math.round((frameCount * 1000) / (currentTime - lastTime))
      frameCount = 0
      lastTime = currentTime
    }

    if (viewer) {
      fpsAnimationId = requestAnimationFrame(updatePerformance)
    }
  }

  updatePerformance()
}

/**
 * 停止性能监控
 */
const stopPerformanceMonitoring = (): void => {
  if (fpsAnimationId !== null) {
    cancelAnimationFrame(fpsAnimationId)
    fpsAnimationId = null
  }
}

// ==================== 生命周期 ====================

onMounted(async () => {
  await nextTick()
  await initCesium()
})

onUnmounted(() => {
  if (viewer) {
    // 停止性能监控
    stopPerformanceMonitoring()

    // 清理加载的模型
    clearLoadedObjects()

    // 销毁查看器
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
  flex-direction: column;
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
