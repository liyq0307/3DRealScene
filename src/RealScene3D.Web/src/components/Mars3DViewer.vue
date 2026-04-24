<template>
  <div class="mars3d-viewer-wrapper">
    <div ref="mars3dContainer" class="mars3d-container"></div>

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
      <button v-if="hasTilesModels" class="btn" @click="toggleBoundingBox" title="切换包围盒">
        <span class="icon">{{ boundingBoxVisible ? '📦' : '⬛' }}</span>
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
        <span class="info-value">Mars3D</span>
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
        <p>加载Mars3D地球中...</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * Mars3D 3D地球查看器组件
 *
 * 基于Mars3D框架（Cesium二次封装）的专业级3D地球展示
 * 集成 Three.js 支持 OBJ、FBX、DAE、STL、PLY 等格式
 *
 * 技术栈：Vue 3 Composition API + TypeScript + Mars3D + Three.js
 */
import { ref, onMounted, onUnmounted, watch, computed, nextTick } from 'vue'
import { useMessage } from '@/composables/useMessage'
import * as mars3d from 'mars3d'
import * as Cesium from 'mars3d-cesium'
import authStore from '@/stores/auth'
import fileHandleStore from '@/services/fileHandleStore'


// ==================== 类型定义 ====================

interface TransformParams {
  x: number
  y: number
  z: number
}

interface SceneObject {
  id: string
  name: string
  displayPath: string
  position: [number, number, number]
  rotation: TransformParams | string
  scale: TransformParams | string
  slicingTaskId?: string
  slicingTaskStatus?: string
  slicingOutputPath?: string
  modelPath?: string
}

interface CameraInfo {
  longitude: number
  latitude: number
  height: number
}

interface LoadedModel {
  type: '3dtiles' | 'model'
  object: any
  position: any
}

// ==================== 常量和配置 ====================

const APP_CONFIG = Object.freeze({
  DEFAULT_POSITION: Object.freeze({
    longitude: 116.397128,
    latitude: 39.908802,
    height: 100
  }),
  SUPPORTED_FORMATS: Object.freeze({
    nativelySupported: Object.freeze(['gltf', 'glb', 'json']),
    threejsSupported: Object.freeze(['obj', 'fbx', 'dae', 'stl', 'ply']), // Three.js 支持的格式
    convertible: Object.freeze(['blend', 'las', 'laz', 'e57']) // 需要切片转换的格式
  }),
  MINIO_BUCKETS: Object.freeze(['models-3d', 'slices', 'textures', 'thumbnails', 'videos']),
  CAMERA_FLIGHT_CONFIG: Object.freeze({
    duration: 2.0,
    heading: 0,
    pitch: -45,
    roll: 0
  }),
  PERFORMANCE: Object.freeze({
    TILES_LOAD_TIMEOUT: 30000,
    MODEL_LOAD_TIMEOUT: 10000,
    BATCH_SIZE: 3,
    MIN_DISTANCE: 500,
    SCALE_FACTOR: 50
  }),
  UI: Object.freeze({
    FPS_MONITOR_INTERVAL: 1000,
    CAMERA_UPDATE_DEBOUNCE: 100
  })
})

// ==================== Props 定义 ====================

interface Props {
  showInfo?: boolean
  initialPosition?: {
    longitude: number
    latitude: number
    height: number
  }
  terrainProvider?: string
  imageryProvider?: string
  sceneObjects?: SceneObject[]
  showBasemap?: boolean
  basemapToggle?: boolean
  backgroundColor?: string
}

const props = withDefaults(defineProps<Props>(), {
  showInfo: true,
  initialPosition: () => ({
    longitude: 116.39,
    latitude: 39.91,
    height: 15000000
  }),
  sceneObjects: () => [],
  showBasemap: false,
  basemapToggle: true,
  backgroundColor: '#1a1a1a'
})

// ==================== Emits 定义 ====================

const emit = defineEmits<{
  ready: [map: mars3d.Map]
  error: [error: Error]
  objectLoaded: [object: SceneObject, success: boolean]
  basemapChange: [visible: boolean]
}>()

// ==================== DOM引用 ====================

const mars3dContainer = ref<HTMLDivElement>()

// ==================== 组合式API ====================

const { error: showError, success: showSuccess } = useMessage()

// ==================== 响应式状态 ====================

const loading = ref(true)
const fps = ref(60)
const loadedObjectsCount = ref(0)
const wireframeMode = ref(false)
const axesVisible = ref(true)
const gridVisible = ref(true)
const boundingBoxVisible = ref(false)
const tilesCount = ref(0)
const cameraInfo = ref<CameraInfo>({
  longitude: 0,
  latitude: 0,
  height: 0
})
const basemapVisible = ref(props.showBasemap)

// ==================== 计算属性 ====================

const apiBaseUrl = computed(() => {
  const baseUrl = import.meta.env.VITE_API_URL || '/api'
  return baseUrl.replace('/api', '')
})

const hasTilesModels = computed(() => {
  return tilesCount.value > 0
})

// ==================== Mars3D对象 ====================

let map: mars3d.Map | null = null
let graphicLayer: mars3d.layer.GraphicLayer | null = null
let viewer: any = null
let frameCount = 0
let lastTime = performance.now()
const loadedModels = new Map<string, LoadedModel>()

// ==================== 工具函数 ====================

const getFileExtension = (filePath: string): string => {
  if (!filePath || typeof filePath !== 'string') {
    return ''
  }
  // 移除查询参数
  const pathWithoutQuery = filePath.split('?')[0]
  // 获取路径的最后一部分（文件名）
  const parts = pathWithoutQuery.split(/[\\\/]/)
  const lastPart = parts[parts.length - 1]
  
  // 如果没有点，或者点是第一个字符（隐藏文件），则没有扩展名
  const lastDotIndex = lastPart.lastIndexOf('.')
  if (lastDotIndex <= 0) {
    return ''
  }
  
  return lastPart.substring(lastDotIndex + 1).toLowerCase()
}

const isAbsoluteUrl = (url: string): boolean => {
  if (!url || typeof url !== 'string') {
    return false
  }
  return url.startsWith('http://') || url.startsWith('https://')
}

const parseTransformParams = (rotation: SceneObject['rotation'], scale: SceneObject['scale']) => {
  try {
    const parsedRotation = typeof rotation === 'string' ? JSON.parse(rotation) : rotation || { x: 0, y: 0, z: 0 }
    const parsedScale = typeof scale === 'string' ? JSON.parse(scale) : scale || { x: 1, y: 1, z: 1 }
    return { parsedRotation, parsedScale }
  } catch (error) {
    console.error('[parseTransformParams] 解析变换参数失败:', error)
    return {
      parsedRotation: { x: 0, y: 0, z: 0 },
      parsedScale: { x: 1, y: 1, z: 1 }
    }
  }
}

// ==================== 监听 Props 变化 ====================

let sceneObjectsDebounceTimer: number | null = null
let isInitialLoad = true

watch(
  () => props.sceneObjects,
  (newObjects) => {
    if (!map) {
      console.warn('查看器尚未准备就绪，将在准备就绪时加载对象')
      return
    }

    if (isInitialLoad) {
      isInitialLoad = false
      return
    }

    if (sceneObjectsDebounceTimer) {
      clearTimeout(sceneObjectsDebounceTimer)
    }

    sceneObjectsDebounceTimer = window.setTimeout(async () => {
      try {
        await loadSceneObjects(newObjects || [])
      } catch (error) {
        console.error('[watch sceneObjects] 加载场景对象失败:', error)
        showError('加载场景对象失败')
      }
      sceneObjectsDebounceTimer = null
    }, 300)
  },
  { deep: true, immediate: false }
)

// ==================== 场景对象加载 ====================

const resolveObjectUrl = async (displayPath: string): Promise<{ url: string; originalFileName?: string }> => {
  let fullPath = displayPath

  // ✅ 检查是否为已编码的 API 路径（数据库中可能存储了编码后的路径）
  if (fullPath.includes('/api/files/local/')) {
    // 提取编码后的路径部分
    const encodedPathMatch = fullPath.match(/\/api\/files\/local\/(.+)$/)
    if (encodedPathMatch) {
      const encodedPath = encodedPathMatch[1]
      // 解码路径
      const decodedPath = decodeURIComponent(encodedPath)
      // 标准化路径：将反斜杠转换为正斜杠
      const normalizedPath = decodedPath.replace(/\\/g, '/')

      // 分离盘符和后续路径
      const driveMatch = normalizedPath.match(/^([A-Za-z]:)(.*)/)
      if (driveMatch) {
        const drive = driveMatch[1].replace(':', '%3A')
        const rest = driveMatch[2]
        // 对剩余部分进行编码，但保留正斜杠 /
        const encodedRest = rest
          .replace(/\s/g, '%20')
          .replace(/#/g, '%23')
          .replace(/\?/g, '%3F')

        fullPath = `${apiBaseUrl.value}/api/files/local/${drive}${encodedRest}`
      } else {
        // 降级方案：常规全量替换
        fullPath = `${apiBaseUrl.value}/api/files/local/${normalizedPath.replace(/:/g, '%3A')}`
      }

      console.log('[Mars3DViewer] 修复已编码的 API 路径:', {
        original: displayPath,
        decoded: decodedPath,
        normalized: normalizedPath,
        converted: fullPath
      })
      return { url: fullPath }
    }
  }

  // 检查是否为Windows本地文件路径 (例如 F:/Data/3D/test/tileset.json 或 F:\Data\3D\test\tileset.json)
  const isWindowsPath = /^[A-Za-z]:[\\/]/.test(fullPath)

  if (isWindowsPath) {
    // 本地文件路径需要通过API代理访问
    // 传递完整的绝对路径，让后端处理
    // 例如: E:\Data\3D\test\tileset.json -> /api/files/local/E:/Data/3D/test/tileset.json

    // 标准化路径：将所有反斜杠转换为正斜杠
    const normalizedPath = fullPath.replace(/\\/g, '/')

    // ✅ 分离盘符和后续路径
    // 例如: E:/Data/3D/tileset.json -> drive='E%3A', rest='/Data/3D/tileset.json'
    const driveMatch = normalizedPath.match(/^([A-Za-z]:)(.*)/)
    if (driveMatch) {
      const drive = driveMatch[1].replace(':', '%3A')
      const rest = driveMatch[2]
      // 对剩余部分进行编码，但保留正斜杠 /
      const encodedRest = rest
        .replace(/\s/g, '%20')
        .replace(/#/g, '%23')
        .replace(/\?/g, '%3F')
      
      fullPath = `${apiBaseUrl.value}/api/files/local/${drive}${encodedRest}`
    } else {
      // 降级方案：常规全量替换
      fullPath = `${apiBaseUrl.value}/api/files/local/${normalizedPath.replace(/:/g, '%3A')}`
    }
    console.log('[Mars3DViewer] 本地文件路径转换:', {
      original: displayPath,
      normalized: normalizedPath,
      converted: fullPath
    })
    return { url: fullPath }
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
      // MinIO bucket路径，如 models-3d/xxx/xxx.b3dm
      fullPath = `${apiBaseUrl.value}/api/files/proxy/${fullPath}`
      console.log('[Mars3DViewer] MinIO bucket路径转换:', {
        original: displayPath,
        bucket: firstPart,
        converted: fullPath
      })
    } else {
      // 切片输出路径，默认放在 slices bucket
      fullPath = `${apiBaseUrl.value}/api/files/proxy/slices/${fullPath.replace(/^\//, '')}`
      console.log('[Mars3DViewer] 切片路径转换:', {
        original: displayPath,
        converted: fullPath
      })
    }
  }

  return { url: fullPath }
}

const handleConvertibleFormat = async (obj: SceneObject): Promise<boolean> => {
  let fileExt = getFileExtension(obj.displayPath)
  
  // ✅ 如果是文件夹，尝试从路径名中识别格式
  if (!fileExt) {
    const lowerPath = obj.displayPath.toLowerCase()
    if (lowerPath.includes('obj')) fileExt = 'obj'
    else if (lowerPath.includes('fbx')) fileExt = 'fbx'
    else if (lowerPath.includes('las') || lowerPath.includes('laz')) fileExt = 'las'
    else fileExt = '文件夹'
  }

  if (obj.slicingTaskId && obj.slicingTaskStatus === 'Completed' && obj.slicingOutputPath) {
    let tilesetPath = obj.slicingOutputPath
    if (!tilesetPath.endsWith('tileset.json')) {
      tilesetPath = tilesetPath.replace(/\/$|\\$/, '') + '/tileset.json'
    }
    const fullTilesetPath = await resolveObjectUrl(tilesetPath)
    return await loadTileset(obj, fullTilesetPath.url)
  } else {
    const position = obj.position.every(coord => coord === 0)
      ? [APP_CONFIG.DEFAULT_POSITION.longitude, APP_CONFIG.DEFAULT_POSITION.latitude, APP_CONFIG.DEFAULT_POSITION.height]
      : obj.position

    if (!map || !graphicLayer) return false

    // 使用Mars3D的PointEntity添加占位标记
    // label是style内的子属性
    const graphic = new mars3d.graphic.PointEntity({
      position: position,
      style: {
        pixelSize: 20,
        color: '#FFA500',
        outline: true,
        outlineColor: '#ffffff',
        outlineWidth: 2,
        label: {
          text: `${obj.name}\n(${fileExt.toUpperCase()} - 需要切片转换)`,
          font_size: 14,
          color: '#ffffff',
          outline: true,
          outlineColor: '#000000',
          outlineWidth: 2,
          pixelOffsetY: -20
        }
      }
    })
    graphicLayer.addGraphic(graphic)

    emit('objectLoaded', obj, false)
    return false
  }
}

/**
 * 加载3D Tiles数据集 - 使用Mars3D的TilesetLayer
 */
const loadTileset = async (obj: SceneObject, url: string): Promise<boolean> => {
  if (!map) return false

  console.log('[loadTileset] 开始加载 tileset，URL:', url)

  try {
    const token = authStore.token.value
    const isSignedUrl = url.includes('X-Amz-Signature')

    // ✅ 创建 Cesium.Resource 对象，正确处理相对路径
    // 对于本地文件路径，需要提取基础目录作为 baseUrl
    let resource: any = url
    if (url.includes('/api/files/local/')) {
      // 提取 tileset.json 所在的目录作为 baseUrl
      const baseUrl = url.substring(0, url.lastIndexOf('/') + 1)
      console.log('[loadTileset] 设置 baseUrl:', baseUrl)

      resource = new Cesium.Resource({
        url: url,
        headers: token && !isSignedUrl ? { Authorization: `Bearer ${token}` } : undefined
      })
    } else if (token && !isSignedUrl) {
      resource = new Cesium.Resource({
        url: url,
        headers: { Authorization: `Bearer ${token}` }
      })
    }

    // 构建Mars3D TilesetLayer配置
    const layerConfig: any = {
      url: resource,  // ✅ 使用 Resource 对象而不是字符串
      maximumScreenSpaceError: 16, // 默认值，平衡性能和质量
      maximumMemoryUsage: 512, // 限制内存使用 (MB)
      show: true,
      debugShowBoundingVolume: boundingBoxVisible.value,
      debugShowContentBoundingVolume: boundingBoxVisible.value
    }

    // 解析旋转和缩放参数
    const { parsedRotation, parsedScale } = parseTransformParams(obj.rotation, obj.scale)

    // 检查是否为本地坐标系模型
    const isLocalCoordinates = obj.position.every(coord => coord === 0)

    // 对于非本地坐标系的模型，在构造时传入position和scale
    if (!isLocalCoordinates) {
      layerConfig.position = {
        lng: obj.position[0],
        lat: obj.position[1],
        alt: obj.position[2],
        heading: parsedRotation.y,
        pitch: parsedRotation.x + 90,
        roll: parsedRotation.z
      }
      layerConfig.scale = parsedScale.x
    }

    // 使用Mars3D的TilesetLayer加载3D Tiles
    const tilesetLayer = new mars3d.layer.TilesetLayer(layerConfig)
    map.addLayer(tilesetLayer)

    // 等待tileset加载完成
    const tileset = tilesetLayer.tileset
    if (!tileset) {
      await new Promise<void>((resolve, reject) => {
        const timeout = setTimeout(() => reject(new Error('Tileset加载超时')), APP_CONFIG.PERFORMANCE.TILES_LOAD_TIMEOUT)
        const checkReady = () => {
          if (tilesetLayer.tileset) {
            clearTimeout(timeout)
            resolve()
          } else {
            setTimeout(checkReady, 100)
          }
        }
        checkReady()
      })
    }

    const actualTileset = tilesetLayer.tileset
    console.log('[loadTileset] Tileset 加载成功')

    // 等待boundingSphere计算完成
    let attempts = 0
    const maxAttempts = 20
    while (attempts < maxAttempts && (!actualTileset.boundingSphere || actualTileset.boundingSphere.radius === 0)) {
      await new Promise(resolve => setTimeout(resolve, 100))
      attempts++
    }

    // 计算中心位置
    let center: any

    if (isLocalCoordinates && actualTileset.boundingSphere && actualTileset.boundingSphere.radius > 0) {
      center = actualTileset.boundingSphere.center
    } else {
      center = Cesium.Cartesian3.fromDegrees(obj.position[0], obj.position[1], obj.position[2])
    }

    loadedModels.set(obj.id, { type: '3dtiles', object: tilesetLayer, position: center })

    loadedObjectsCount.value = loadedModels.size
    tilesCount.value = Array.from(loadedModels.values()).filter(item => item.type === '3dtiles').length

    console.log('[loadTileset] Tileset 加载完成')
    emit('objectLoaded', obj, true)
    return true
  } catch (error) {
    const errorMessage = `加载 ${obj.name} 的 3D 瓦片数据集失败: ${error instanceof Error ? error.message : String(error)}`
    console.error('[loadTileset] 错误:', errorMessage)
    showError(errorMessage)
    emit('objectLoaded', obj, false)
    return false
  }
}

/**
 * 加载glTF/GLB模型 - 使用Cesium原生Model API
 * Mars3D的ModelEntity对远程URL和签名URL支持有限，回退到Cesium原生API
 */
const loadGltfModel = async (obj: SceneObject, url: string): Promise<boolean> => {
  if (!map || !viewer) return false

  console.log('[loadGltfModel] 开始加载 GLB/GLTF 模型，URL:', url)

  try {
    const { parsedRotation, parsedScale } = parseTransformParams(obj.rotation, obj.scale)

    const token = authStore.token.value
    const isSignedUrl = url.includes('X-Amz-Signature')

    // 创建Cesium.Resource对象（处理Authorization header）
    const resource = (token && !isSignedUrl)
      ? new Cesium.Resource({
          url: url,
          headers: { Authorization: `Bearer ${token}` }
        })
      : url

    // 使用Cesium原生Model API加载GLB
    const position = obj.position.every(coord => coord === 0)
      ? [APP_CONFIG.DEFAULT_POSITION.longitude, APP_CONFIG.DEFAULT_POSITION.latitude, APP_CONFIG.DEFAULT_POSITION.height]
      : obj.position

    const cartesian = Cesium.Cartesian3.fromDegrees(position[0], position[1], position[2])

    // 创建模型矩阵（添加默认+90度X轴旋转，与切片模型保持一致）
    const pitch = Cesium.Math.toRadians(parsedRotation.x + 90)
    const heading = Cesium.Math.toRadians(parsedRotation.y)
    const roll = Cesium.Math.toRadians(parsedRotation.z)

    const hpr = new Cesium.HeadingPitchRoll(heading, pitch, roll)
    const orientation = Cesium.Transforms.headingPitchRollQuaternion(cartesian, hpr)

    const modelMatrix = Cesium.Matrix4.fromTranslationQuaternionRotationScale(
      cartesian,
      orientation,
      new Cesium.Cartesian3(parsedScale.x, parsedScale.y, parsedScale.z)
    )

    const model = await Cesium.Model.fromGltfAsync({
      url: resource,
      modelMatrix,
      colorBlendMode: Cesium.ColorBlendMode.MIX,
      maximumScale: Number.MAX_VALUE,
      minimumPixelSize: 128,
      allowPicking: true,
      show: true,
      distanceDisplayCondition: undefined,
      scene: viewer.scene
    })

    viewer.scene.primitives.add(model)

    // 等待模型就绪
    await new Promise<void>((resolve, reject) => {
      if (model.ready) {
        resolve()
      } else {
        const removeListener = model.readyEvent.addEventListener(() => {
          removeListener()
          resolve()
        })
        setTimeout(() => {
          removeListener()
          reject(new Error('Model ready timeout'))
        }, APP_CONFIG.PERFORMANCE.MODEL_LOAD_TIMEOUT)
      }
    })

    model.show = true

    loadedModels.set(obj.id, { type: 'model', object: model, position: cartesian })
    loadedObjectsCount.value = loadedModels.size

    console.log('[loadGltfModel] GLB/GLTF 模型加载完成')
    emit('objectLoaded', obj, true)
    return true
  } catch (error) {
    const errorMessage = `加载 ${obj.name} 的 glTF/GLB 模型失败: ${error instanceof Error ? error.message : String(error)}`
    console.error('[loadGltfModel] 错误:', errorMessage)
    showError(errorMessage)
    emit('objectLoaded', obj, false)
    return false
  }
}

/**
 * 使用 Three.js 叠加渲染模型
 * 目前统一使用 handleConvertibleFormat 显示占位点，提示用户切片
 */
const loadThreeJSModel = async (obj: SceneObject, url: string): Promise<boolean> => {
  console.log('[loadThreeJSModel] 原始模型格式暂不支持直接预览，请先进行切片:', url)
  return await handleConvertibleFormat(obj)
}

/**
 * 飞行到指定位置 - 使用Mars3D的setCameraView
 */
const flyToPosition = async (position: number[], duration: number = APP_CONFIG.CAMERA_FLIGHT_CONFIG.duration): Promise<void> => {
  if (!map) return

  map.setCameraView({
    lng: position[0],
    lat: position[1],
    alt: position[2],
    heading: APP_CONFIG.CAMERA_FLIGHT_CONFIG.heading,
    pitch: APP_CONFIG.CAMERA_FLIGHT_CONFIG.pitch
  }, { duration })
}

/**
 * 飞向3D Tiles数据集
 */
const flyToTileset = async (tilesetLayer: mars3d.layer.TilesetLayer, fallbackPosition: any): Promise<void> => {
  if (!map) return

  try {
    const tileset = tilesetLayer.tileset
    if (!tileset || !tileset.boundingSphere) {
      await flyToPosition([APP_CONFIG.DEFAULT_POSITION.longitude, APP_CONFIG.DEFAULT_POSITION.latitude, 10000])
      return
    }

    const radius = tileset.boundingSphere.radius
    const distance = Math.max(radius * 3.0, APP_CONFIG.PERFORMANCE.MIN_DISTANCE)

    // 使用TilesetLayer的flyTo方法
    tilesetLayer.flyTo({
      duration: APP_CONFIG.CAMERA_FLIGHT_CONFIG.duration,
      radius: distance
    })
  } catch (error) {
    console.warn('[flyToTileset] 飞向瓦片数据集失败:', error)
    // 备用方案
    if (fallbackPosition) {
      try {
        const cartographic = map.viewer.scene.globe.ellipsoid.cartesianToCartographic(fallbackPosition)
        if (cartographic) {
          map.setCameraView({
            lng: Cesium.Math.toDegrees(cartographic.longitude),
            lat: Cesium.Math.toDegrees(cartographic.latitude),
            alt: cartographic.height + 1000,
            heading: APP_CONFIG.CAMERA_FLIGHT_CONFIG.heading,
            pitch: APP_CONFIG.CAMERA_FLIGHT_CONFIG.pitch
          }, { duration: APP_CONFIG.CAMERA_FLIGHT_CONFIG.duration })
        }
      } catch (e) {
        // 忽略
      }
    }
  }
}

const validateSceneObject = (obj: SceneObject): { isValid: boolean; error?: string } => {
  if (!obj) return { isValid: false, error: '对象为空' }
  if (!obj.id || typeof obj.id !== 'string') return { isValid: false, error: '对象ID无效' }
  if (!obj.name || typeof obj.name !== 'string') return { isValid: false, error: '对象名称无效' }
  if (!obj.displayPath || typeof obj.displayPath !== 'string') return { isValid: false, error: '显示路径无效' }
  if (!Array.isArray(obj.position) || obj.position.length !== 3) return { isValid: false, error: '位置信息无效' }
  return { isValid: true }
}

const normalizeObjectPosition = (obj: SceneObject): [number, number, number] => {
  const position = obj.position

  if (obj.slicingTaskId && obj.slicingTaskStatus === 'Completed' && obj.slicingOutputPath) {
    return position
  }

  if (position.every(coord => coord === 0)) {
    return [APP_CONFIG.DEFAULT_POSITION.longitude, APP_CONFIG.DEFAULT_POSITION.latitude, APP_CONFIG.DEFAULT_POSITION.height]
  }

  const [longitude, latitude, height] = position
  if (longitude < -180 || longitude > 180 || latitude < -90 || latitude > 90 || height < -1000) {
    return [APP_CONFIG.DEFAULT_POSITION.longitude, APP_CONFIG.DEFAULT_POSITION.latitude, APP_CONFIG.DEFAULT_POSITION.height]
  }

  return position
}

const determineLoadStrategy = (fullPath: string, fileExt: string): 'tileset' | 'gltf' | 'threejs' | 'convertible' | null => {
  // 1. 优先检查是否明确是 tileset.json
  if (fullPath.toLowerCase().endsWith('tileset.json') || fullPath.toLowerCase().includes('/tileset.json')) {
    return 'tileset'
  }

  // 2. 根据扩展名判定
  if (fileExt) {
    if (APP_CONFIG.SUPPORTED_FORMATS.convertible.includes(fileExt)) return 'convertible'
    if (APP_CONFIG.SUPPORTED_FORMATS.threejsSupported.includes(fileExt)) return 'threejs'
    if (['gltf', 'glb'].includes(fileExt)) return 'gltf'
    if (fileExt === 'json') return 'tileset'
    if (!APP_CONFIG.SUPPORTED_FORMATS.nativelySupported.includes(fileExt)) return null
  }

  // 3. 处理文件夹（无扩展名）
  if (!fileExt) {
    const lowerPath = fullPath.toLowerCase()
    // 包含模型关键字的文件夹判定为需转换
    if (lowerPath.includes('obj') || lowerPath.includes('fbx') || lowerPath.includes('dae') || 
        lowerPath.includes('las') || lowerPath.includes('laz') || lowerPath.includes('e57')) {
      return 'convertible'
    }
    // 默认假设是瓦片集文件夹
    return 'tileset'
  }
  
  return null
}

const loadSceneObject = async (obj: SceneObject): Promise<void> => {
  try {
    console.log('[Mars3DViewer] 开始加载场景对象:', obj.name, obj)

    // 验证对象
    const validation = validateSceneObject(obj)
    if (!validation.isValid) {
      console.error(`[Mars3DViewer] 对象验证失败: ${validation.error}`)
      emit('objectLoaded', obj, false)
      return
    }

    // 规范化位置
    obj.position = normalizeObjectPosition(obj)

    // ✅ 优先检查是否有完成的切片输出（适用于所有格式，包括GLB/GLTF）
    if (obj.slicingTaskId && obj.slicingTaskStatus === 'Completed' && obj.slicingOutputPath) {
      console.log('[Mars3DViewer] 检测到已完成的切片任务，使用切片输出')
      let tilesetPath = obj.slicingOutputPath

      // 确保路径指向tileset.json
      if (!tilesetPath.endsWith('tileset.json')) {
        tilesetPath = tilesetPath.replace(/\/$|\\$/, '') + '/tileset.json'
      }

      // 使用resolveObjectUrl处理路径（支持本地路径和MinIO路径）
      const fullTilesetPath = await resolveObjectUrl(tilesetPath)

      console.log('[Mars3DViewer] 使用切片输出:', {
        original: obj.slicingOutputPath,
        tilesetPath,
        fullTilesetPath
      })

      // 加载3D Tiles
      await loadTileset(obj, fullTilesetPath.url)
      return
    }

    // ✅ 检查是否为文件句柄路径（使用modelPath而不是displayPath）
    const modelPath = obj.modelPath || obj.displayPath
    if (modelPath && modelPath.startsWith('local-file-handle://')) {
      console.log('[Mars3DViewer] 检测到文件句柄路径，尝试获取文件:', modelPath)
      
      try {
        const uuid = modelPath.replace('local-file-handle://', '')
        const handle = await fileHandleStore.getHandle<any>(uuid)
        
        if (handle) {
          // 检查权限
          let permission = await handle.queryPermission({ mode: 'read' })
          if (permission !== 'granted') {
            // 请求权限
            permission = await handle.requestPermission({ mode: 'read' })
          }
          
          if (permission === 'granted') {
            const file = await handle.getFile()
            // 创建临时URL用于预览
            const blobUrl = URL.createObjectURL(file)
            const fileName = file.name
            const fileExt = getFileExtension(fileName)
            
            console.log('[Mars3DViewer] 文件句柄转换成功:', { uuid, fileName, blobUrl, fileExt })
            
            // 确定加载策略
            const strategy = determineLoadStrategy(blobUrl, fileExt)
            
            switch (strategy) {
              case 'convertible':
                console.log('[Mars3DViewer] 格式需要转换，调用 handleConvertibleFormat')
                await handleConvertibleFormat(obj)
                break
              case 'tileset':
                console.log('[Mars3DViewer] 检测到 tileset.json，开始加载 3D Tiles')
                await loadTileset(obj, blobUrl)
                break
              case 'gltf':
                console.log('[Mars3DViewer] 检测到 GLTF/GLB 模型，开始加载')
                await loadGltfModel(obj, blobUrl)
                break
              case 'threejs':
                console.log('[Mars3DViewer] 检测到 Three.js 支持的格式，开始加载')
                await loadThreeJSModel(obj, blobUrl)
                break
              default:
                console.error(`不支持的文件格式: .${fileExt}`)
                emit('objectLoaded', obj, false)
            }
            return
          }
        }
        
        throw new Error('无法访问文件句柄')
      } catch (err) {
        console.error('[Mars3DViewer] 文件句柄处理失败:', err)
        showError('文件句柄访问失败，请重新选择文件或使用MinIO上传')
        emit('objectLoaded', obj, false)
        return
      }
    }

    // 解析URL（非文件句柄路径）
    const resolved = await resolveObjectUrl(obj.displayPath)
    let fullPath = resolved.url
    console.log('[Mars3DViewer] fullPath:', fullPath)

    // 检查文件格式
    let fileExt = resolved.originalFileName 
      ? getFileExtension(resolved.originalFileName)
      : getFileExtension(fullPath)
      
    // ✅ 修复文件夹逻辑：根据策略决定是否追加 tileset.json
    if (!fileExt && !fullPath.toLowerCase().includes('tileset.json')) {
      const strategy = determineLoadStrategy(fullPath, fileExt)
      if (strategy === 'tileset') {
        console.log('[Mars3DViewer] 判定为 3D Tiles 文件夹，尝试追加 tileset.json')
        fullPath = fullPath.replace(/\/?$/, '/tileset.json')
        fileExt = 'json'
      } else {
        console.log(`[Mars3DViewer] 判定文件夹策略为: ${strategy}`)
      }
    }
    
    console.log('[Mars3DViewer] fileExt:', fileExt)

    // 确定加载策略
    const strategy = determineLoadStrategy(fullPath, fileExt)

    switch (strategy) {
      case 'convertible':
        console.log('[Mars3DViewer] 格式需要转换，调用 handleConvertibleFormat')
        await handleConvertibleFormat(obj)
        break
      case 'tileset':
        console.log('[Mars3DViewer] 检测到 tileset.json，开始加载 3D Tiles')
        await loadTileset(obj, fullPath)
        break
      case 'gltf':
        console.log('[Mars3DViewer] 检测到 GLTF/GLB 模型，开始加载')
        await loadGltfModel(obj, fullPath)
        break
      case 'threejs':
        console.log('[Mars3DViewer] 检测到 Three.js 支持的格式，开始加载')
        await loadThreeJSModel(obj, fullPath)
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

const loadSceneObjects = async (objects: SceneObject[]): Promise<void> => {
  if (!map) throw new Error('查看器未初始化')

  clearLoadedObjects()

  if (!objects || objects.length === 0) return

  const validObjects = objects.filter(obj => {
    const validation = validateSceneObject(obj)
    return validation.isValid
  })

  if (validObjects.length === 0) return

  const batchSize = APP_CONFIG.PERFORMANCE.BATCH_SIZE
  const batches = []
  for (let i = 0; i < validObjects.length; i += batchSize) {
    batches.push(validObjects.slice(i, i + batchSize))
  }

  for (const batch of batches) {
    try {
      await Promise.all(batch.map(loadSceneObject))
      if (batches.length > 1) {
        await new Promise(resolve => setTimeout(resolve, 50))
      }
    } catch (error) {
      console.error('[loadSceneObjects] 批次加载失败:', error)
    }
  }

  // 所有对象加载完成后，统一飞向所有对象
  await flyToAllObjects()
}

/**
 * 飞向所有已加载的对象（统一视角定位）
 */
const flyToAllObjects = async (): Promise<void> => {
  if (!map || loadedModels.size === 0) return

  try {
    if (loadedModels.size === 1) {
      // 单个对象 - 直接飞向该对象
      const modelInfo = Array.from(loadedModels.values())[0]
      if (modelInfo.type === '3dtiles') {
        const layer = modelInfo.object as mars3d.layer.TilesetLayer
        // 等待tileset就绪后flyTo
        const tileset = layer.tileset
        if (tileset && tileset.boundingSphere && tileset.boundingSphere.radius > 0) {
          const radius = tileset.boundingSphere.radius
          const distance = Math.max(radius * 3.0, APP_CONFIG.PERFORMANCE.MIN_DISTANCE)
          layer.flyTo({ duration: APP_CONFIG.CAMERA_FLIGHT_CONFIG.duration, radius: distance })
        } else {
          layer.flyTo({ duration: APP_CONFIG.CAMERA_FLIGHT_CONFIG.duration })
        }
      } else {
        // Cesium.Model
        const model = modelInfo.object as Cesium.Model
        if (model.boundingSphere && model.boundingSphere.radius > 0) {
          viewer.camera.flyToBoundingSphere(model.boundingSphere, {
            duration: APP_CONFIG.CAMERA_FLIGHT_CONFIG.duration,
            offset: new Cesium.HeadingPitchRange(0, -0.5, Math.max(model.boundingSphere.radius * 3.0, 500))
          })
        } else {
          // 使用位置飞向
          const cartographic = Cesium.Cartographic.fromCartesian(modelInfo.position)
          if (cartographic) {
            map.setCameraView({
              lng: Cesium.Math.toDegrees(cartographic.longitude),
              lat: Cesium.Math.toDegrees(cartographic.latitude),
              alt: cartographic.height + 1000,
              heading: APP_CONFIG.CAMERA_FLIGHT_CONFIG.heading,
              pitch: APP_CONFIG.CAMERA_FLIGHT_CONFIG.pitch
            }, { duration: APP_CONFIG.CAMERA_FLIGHT_CONFIG.duration })
          }
        }
      }
    } else {
      // 多个对象 - 计算所有位置的包围球，飞向中心
      const positions = Array.from(loadedModels.values()).map(item => item.position)
      const boundingSphere = Cesium.BoundingSphere.fromPoints(positions)

      if (boundingSphere && boundingSphere.radius > 0) {
        const center = boundingSphere.center
        const cartographic = Cesium.Cartographic.fromCartesian(center)
        if (cartographic) {
          map.setCameraView({
            lng: Cesium.Math.toDegrees(cartographic.longitude),
            lat: Cesium.Math.toDegrees(cartographic.latitude),
            alt: cartographic.height + boundingSphere.radius * 3.0,
            heading: APP_CONFIG.CAMERA_FLIGHT_CONFIG.heading,
            pitch: APP_CONFIG.CAMERA_FLIGHT_CONFIG.pitch
          }, { duration: APP_CONFIG.CAMERA_FLIGHT_CONFIG.duration })
        }
      }
    }
  } catch (error) {
    console.warn('[flyToAllObjects] 飞向对象失败:', error)
  }
}

const clearLoadedObjects = (): void => {
  if (!map) return

  loadedModels.forEach((item) => {
    if (item.type === '3dtiles') {
      const layer = item.object as mars3d.layer.TilesetLayer
      map!.removeLayer(layer, true)
    } else {
      // Cesium.Model - 从primitives中移除
      const model = item.object as Cesium.Model
      if (viewer) {
        viewer.scene.primitives.remove(model)
      }
    }
  })
  loadedModels.clear()
  loadedObjectsCount.value = 0
  tilesCount.value = 0
}

// ==================== 初始化Mars3D ====================

const initMars3D = async (): Promise<void> => {
  if (!mars3dContainer.value) {
    throw new Error('Mars3D容器未找到')
  }

  try {
    console.log('[initMars3D] 开始初始化Mars3D地图')

    // Mars3D Map构造函数第一个参数需要是DOM元素的id字符串
    // 给容器设置一个唯一id
    const containerId = 'mars3d-container-' + Date.now()
    mars3dContainer.value.id = containerId

    // Mars3D Map配置
    map = new mars3d.Map(containerId, {
      scene: {
        center: {
          lng: props.initialPosition.longitude,
          lat: props.initialPosition.latitude,
          alt: props.initialPosition.height,
          heading: APP_CONFIG.CAMERA_FLIGHT_CONFIG.heading,
          pitch: APP_CONFIG.CAMERA_FLIGHT_CONFIG.pitch
        },
        showSun: false,
        showMoon: false,
        showSkyAtmosphere: true,
        fog: false,
        cameraController: {
          minimumZoomDistance: 1,
          maximumZoomDistance: 50000000
        },
        globe: {
          depthTestAgainstTerrain: false,
          enableLighting: false
        },
        clock: {
          currentTime: '2025-01-01'
        }
      },
      control: {
        baseLayerPicker: false,
        homeButton: false,
        sceneModePicker: false,
        navigationHelpButton: false,
        geocoder: false,
        fullscreenButton: false,
        animation: false,
        timeline: false,
        infoBox: false,
        selectionIndicator: false
      },
      terrain: {
        show: false,
        url: ''
      },
      basemaps: [
        {
          name: 'ArcGIS影像底图',
          type: 'xyz',
          url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
          show: true // 始终加载底图，确保后续可以切换显示/隐藏
        }
      ]
    })

    // 获取Cesium Viewer引用
    viewer = map.viewer

    // 创建矢量数据图层并添加到Map（必须在Map创建后添加，否则会有警告）
    graphicLayer = new mars3d.layer.GraphicLayer()
    map.addLayer(graphicLayer)

    // 性能优化设置
    if (viewer.scene.globe) {
      viewer.scene.globe.tileCacheSize = 100
      viewer.scene.globe.maximumScreenSpaceError = 2
    }

    // 隐藏Cesium Logo
    try {
      const creditContainer = viewer.cesiumWidget.creditContainer as HTMLElement
      if (creditContainer) {
        creditContainer.style.display = 'none'
      }
    } catch (e) {
      // 忽略
    }

    // 根据props.showBasemap设置底图初始可见性（初始化时已加载底图，这里控制显示状态）
    if (!props.showBasemap) {
      updateBasemaps(false)
      configureBackground()
    }

    // 添加坐标轴辅助器
    initAxesHelper()

    // 添加网格辅助器
    initGridHelper()

    // 设置相机运动事件监听
    viewer.camera.moveEnd.addEventListener(updateCameraInfo)

    // 初始化相机信息
    updateCameraInfo()

    // 启动性能监控
    startPerformanceMonitoring()

    // 初始加载场景对象
    if (props.sceneObjects && Array.isArray(props.sceneObjects) && props.sceneObjects.length > 0) {
      console.log(`[initMars3D] 开始加载 ${props.sceneObjects.length} 个场景对象`)
      await loadSceneObjects(props.sceneObjects)
    }

    loading.value = false
    console.log('[initMars3D] Mars3D地图初始化完成')
    emit('ready', map)
  } catch (error) {
    const errorMessage = `Mars3D 初始化失败: ${error instanceof Error ? error.message : String(error)}`
    console.error('[initMars3D] 初始化失败:', error)

    loading.value = false

    if (map) {
      try {
        map.destroy()
        map = null
      } catch (destroyError) {
        console.error('[initMars3D] 清理失败:', destroyError)
      }
    }

    showError(errorMessage)
    emit('error', error instanceof Error ? error : new Error(String(error)))
  }
}

// ==================== 辅助器 ====================

/**
 * 初始化坐标轴辅助器 - 使用Mars3D的PolylineEntity
 * PolylineEntity的style中直接使用color属性，不需要MaterialUtil
 */
const initAxesHelper = (): void => {
  if (!map || !graphicLayer) return
  try {
    const axisLength = 500000

    // X轴 (红色 - 东)
    graphicLayer.addGraphic(new mars3d.graphic.PolylineEntity({
      id: 'axis-x',
      positions: [[0, 0, 0], [axisLength / 111320, 0, 0]],
      style: {
        width: 2,
        color: '#ff0000'
      },
      show: axesVisible.value
    }))

    // Y轴 (绿色 - 北)
    graphicLayer.addGraphic(new mars3d.graphic.PolylineEntity({
      id: 'axis-y',
      positions: [[0, 0, 0], [0, axisLength / 111320, 0]],
      style: {
        width: 2,
        color: '#00ff00'
      },
      show: axesVisible.value
    }))

    // Z轴 (蓝色 - 上)
    graphicLayer.addGraphic(new mars3d.graphic.PolylineEntity({
      id: 'axis-z',
      positions: [[0, 0, 0], [0, 0, axisLength]],
      style: {
        width: 2,
        color: '#0000ff'
      },
      show: axesVisible.value
    }))

    console.log('[initAxesHelper] 坐标轴创建成功')
  } catch (error) {
    console.warn('[initAxesHelper] 创建坐标轴失败:', error)
  }
}

/**
 * 初始化网格辅助器 - 使用Mars3D的PolylineEntity
 */
const initGridHelper = (): void => {
  if (!map || !graphicLayer) return
  try {
    // 经度线（南北向）- 每30度一条
    for (let lon = -180; lon <= 180; lon += 30) {
      const positions: [number, number, number][] = []
      for (let lat = -85; lat <= 85; lat += 10) {
        positions.push([lon, lat, 0])
      }
      graphicLayer.addGraphic(new mars3d.graphic.PolylineEntity({
        id: `grid-lon-${lon}`,
        positions,
        style: {
          width: 1,
          color: 'rgba(255,255,255,0.15)',
          clampToGround: true
        },
        show: gridVisible.value
      }))
    }

    // 纬度线（东西向）- 每30度一条
    for (let lat = -60; lat <= 60; lat += 30) {
      const positions: [number, number, number][] = []
      for (let lon = -180; lon <= 180; lon += 10) {
        positions.push([lon, lat, 0])
      }
      graphicLayer.addGraphic(new mars3d.graphic.PolylineEntity({
        id: `grid-lat-${lat}`,
        positions,
        style: {
          width: 1,
          color: 'rgba(255,255,255,0.15)',
          clampToGround: true
        },
        show: gridVisible.value
      }))
    }

    console.log('[initGridHelper] 网格创建成功')
  } catch (error) {
    console.warn('[initGridHelper] 创建网格失败:', error)
  }
}

// ==================== 相机控制 ====================

let cameraUpdateTimer: number | null = null

const updateCameraInfo = () => {
  if (!map) return

  if (cameraUpdateTimer) clearTimeout(cameraUpdateTimer)

  cameraUpdateTimer = window.setTimeout(() => {
    try {
      if (!map) return

      // 使用map.getCenter()获取当前视角中心
      const center = map.getCenter()
      if (center) {
        cameraInfo.value = {
          longitude: parseFloat(center.lng.toFixed(4)),
          latitude: parseFloat(center.lat.toFixed(4)),
          height: Math.round(center.alt || 0)
        }
      }
    } catch (error) {
      console.warn('[updateCameraInfo] 更新相机信息失败:', error)
    }
    cameraUpdateTimer = null
  }, APP_CONFIG.UI.CAMERA_UPDATE_DEBOUNCE)
}

/**
 * 重置视图 - 使用Mars3D的setCameraView
 */
const resetView = async (): Promise<void> => {
  if (!map) {
    showError('Mars3D 查看器未初始化')
    return
  }

  try {
    if (loadedModels.size > 0) {
      if (loadedModels.size === 1) {
        const modelInfo = Array.from(loadedModels.values())[0]
        if (modelInfo.type === '3dtiles') {
          const layer = modelInfo.object as mars3d.layer.TilesetLayer
          layer.flyTo({ duration: APP_CONFIG.CAMERA_FLIGHT_CONFIG.duration })
        } else {
          // Cesium.Model - 飞向模型位置
          const model = modelInfo.object as Cesium.Model
          if (model.boundingSphere && model.boundingSphere.radius > 0) {
            viewer.camera.flyToBoundingSphere(model.boundingSphere, {
              duration: APP_CONFIG.CAMERA_FLIGHT_CONFIG.duration,
              offset: new Cesium.HeadingPitchRange(0, -0.5, Math.max(model.boundingSphere.radius * 3.0, 500))
            })
          }
        }
      } else {
        // 多个对象 - 飞向全局视角
        map.setCameraView({
          lng: APP_CONFIG.DEFAULT_POSITION.longitude,
          lat: APP_CONFIG.DEFAULT_POSITION.latitude,
          alt: 10000,
          heading: APP_CONFIG.CAMERA_FLIGHT_CONFIG.heading,
          pitch: APP_CONFIG.CAMERA_FLIGHT_CONFIG.pitch
        }, { duration: APP_CONFIG.CAMERA_FLIGHT_CONFIG.duration })
      }
    } else {
      map.setCameraView({
        lng: props.initialPosition.longitude,
        lat: props.initialPosition.latitude,
        alt: 10000,
        heading: APP_CONFIG.CAMERA_FLIGHT_CONFIG.heading,
        pitch: APP_CONFIG.CAMERA_FLIGHT_CONFIG.pitch
      }, { duration: APP_CONFIG.CAMERA_FLIGHT_CONFIG.duration })
    }
  } catch (error) {
    const errorMessage = `重置视图失败: ${error instanceof Error ? error.message : String(error)}`
    console.error(errorMessage)
    showError(errorMessage)
  }
}

/**
 * 截图 - 使用Mars3D的expImage方法
 */
const takeScreenshot = (): void => {
  if (!map) {
    showError('Mars3D 查看器未初始化')
    return
  }

  try {
    // 使用Mars3D内置的导出图片方法
    map.expImage({
      download: true,
      filename: `mars3d-screenshot-${Date.now()}`
    })
    showSuccess('截图已保存')
  } catch (error) {
    const errorMessage = `截图失败: ${error instanceof Error ? error.message : String(error)}`
    console.error(errorMessage)
    showError(errorMessage)
  }
}

const toggleWireframe = (): void => {
  wireframeMode.value = !wireframeMode.value
  showSuccess(wireframeMode.value ? '线框模式（Mars3D不支持）' : '线框模式已禁用')
}

const toggleAxes = (): void => {
  if (!graphicLayer) return

  axesVisible.value = !axesVisible.value

  const axisX = graphicLayer.getGraphicById('axis-x')
  const axisY = graphicLayer.getGraphicById('axis-y')
  const axisZ = graphicLayer.getGraphicById('axis-z')

  if (axisX) axisX.show = axesVisible.value
  if (axisY) axisY.show = axesVisible.value
  if (axisZ) axisZ.show = axesVisible.value

  showSuccess(axesVisible.value ? '坐标轴已显示' : '坐标轴已隐藏')
}

const toggleGrid = (): void => {
  if (!graphicLayer) return

  gridVisible.value = !gridVisible.value

  for (let lon = -180; lon <= 180; lon += 30) {
    const graphic = graphicLayer.getGraphicById(`grid-lon-${lon}`)
    if (graphic) graphic.show = gridVisible.value
  }

  for (let lat = -60; lat <= 60; lat += 30) {
    const graphic = graphicLayer.getGraphicById(`grid-lat-${lat}`)
    if (graphic) graphic.show = gridVisible.value
  }

  showSuccess(gridVisible.value ? '网格已显示' : '网格已隐藏')
}

const toggleBoundingBox = (): void => {
  if (!map) return

  boundingBoxVisible.value = !boundingBoxVisible.value

  loadedModels.forEach((item) => {
    if (item.type === '3dtiles') {
      const layer = item.object as mars3d.layer.TilesetLayer
      if (layer.tileset) {
        layer.tileset.debugShowBoundingVolume = boundingBoxVisible.value
        layer.tileset.debugShowContentBoundingVolume = boundingBoxVisible.value
      }
    }
  })

  viewer.scene.requestRender()
  showSuccess(boundingBoxVisible.value ? '包围盒已显示' : '包围盒已隐藏')
}

// ==================== FPS监控 ====================

let fpsAnimationId: number | null = null

const startPerformanceMonitoring = (): void => {
  const updatePerformance = () => {
    frameCount++
    const currentTime = performance.now()

    if (currentTime >= lastTime + APP_CONFIG.UI.FPS_MONITOR_INTERVAL) {
      fps.value = Math.round((frameCount * 1000) / (currentTime - lastTime))
      frameCount = 0
      lastTime = currentTime
    }

    if (map) {
      fpsAnimationId = requestAnimationFrame(updatePerformance)
    }
  }
  updatePerformance()
}

const stopPerformanceMonitoring = (): void => {
  if (fpsAnimationId !== null) {
    cancelAnimationFrame(fpsAnimationId)
    fpsAnimationId = null
  }
}

// ==================== 底图控制 ====================

/**
 * 更新底图配置
 */
const updateBasemaps = (show: boolean): void => {
  if (!map) return

  try {
    console.log('[updateBasemaps] 设置底图可见性:', show)

    // Mars3D通过basemap属性控制底图配置
    if (map.basemap && typeof map.basemap === 'object' && 'show' in map.basemap) {
      (map.basemap as any).show = show
    }

    // 直接操作 Cesium 的底图图层，确保立即生效
    if (viewer && viewer.imageryLayers && viewer.imageryLayers.length > 0) {
      const baseImageryLayer = viewer.imageryLayers.get(0)
      if (baseImageryLayer) {
        baseImageryLayer.show = show
        console.log('[updateBasemaps] Cesium底图图层show属性已设置为:', baseImageryLayer.show)
      }
    }

    // 强制刷新地图渲染
    if (viewer) {
      viewer.scene.requestRender()
    }
  } catch (error) {
    console.error('[updateBasemaps] 底图配置失败:', error)
  }
}

/**
 * 配置背景色
 */
const configureBackground = (): void => {
  if (!viewer) return

  try {
    viewer.scene.backgroundColor = Cesium.Color.fromCssColorString(props.backgroundColor)
  } catch (error) {
    console.error('[configureBackground] 背景配置失败:', error)
  }
}

/**
 * 切换底图显示状态
 */
const toggleBasemap = async (): Promise<void> => {
  await setBasemapVisible(!basemapVisible.value)
}

/**
 * 设置底图显示状态
 */
const setBasemapVisible = async (visible: boolean): Promise<void> => {
  basemapVisible.value = visible
  updateBasemaps(visible)
  if (!visible) {
    configureBackground()
  }
  emit('basemapChange', visible)
}

/**
 * 获取底图显示状态
 */
const getBasemapVisible = (): boolean => {
  return basemapVisible.value
}

// ==================== 监听Props变化 ====================

watch(() => props.showBasemap, (newVal) => {
  basemapVisible.value = newVal
  updateBasemaps(newVal)
  if (!newVal) {
    configureBackground()
  }
})

// ==================== 生命周期 ====================

onMounted(async () => {
  await nextTick()
  await initMars3D()
})

onUnmounted(() => {
  if (map) {
    stopPerformanceMonitoring()
    clearLoadedObjects()
    map.destroy()
    map = null
    viewer = null
    graphicLayer = null
  }
})

// ==================== 暴露方法 ====================

defineExpose({
  toggleBasemap,
  setBasemapVisible,
  getBasemapVisible
})
</script>

<style scoped>
.mars3d-viewer-wrapper {
  position: relative;
  width: 100%;
  height: 100%;
  overflow: hidden;
}

.mars3d-container {
  position: relative;
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
