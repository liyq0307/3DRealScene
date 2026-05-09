/**
 * 场景配置加载工具
 *
 * 借鉴 cityplan 项目的 config.json 思路，将场景参数外置到配置文件中，
 * 部署时只需修改 public/config/scene-config.json 即可适配不同项目，
 * 无需修改代码重新构建。
 *
 * 使用方式：
 *   import { loadSceneConfig, getSceneConfig } from '@/config/scene-config'
 *   await loadSceneConfig()          // 在应用启动或组件挂载时加载
 *   const config = getSceneConfig()  // 获取已加载的配置
 */

// ==================== 类型定义 ====================

export interface Mars3DSceneConfig {
  scene: {
    center: {
      lng: number
      lat: number
      alt: number
      heading: number
      pitch: number
    }
    showSun: boolean
    showMoon: boolean
    showSkyAtmosphere: boolean
    fog: boolean
    cameraController: {
      minimumZoomDistance: number
      maximumZoomDistance: number
    }
    globe: {
      depthTestAgainstTerrain: boolean
      enableLighting: boolean
    }
    clock: {
      currentTime: string
    }
  }
  control: {
    toolbar: { position: string }
    homeButton: boolean
    fullscreenButton: boolean
    navigationHelpButton: boolean
    baseLayerPicker: boolean
    sceneModePicker: boolean
    vrButton: boolean
    animation: boolean
    timeline: boolean
    infoBox: boolean
    selectionIndicator: boolean
    compass: { bottom: string; right: string }
  }
  terrain: {
    show: boolean
    url: string
  }
  basemaps: Array<{
    name: string
    type: string
    url: string
    show: boolean
    [key: string]: any
  }>
  performance: {
    tileCacheSize: number
    maximumScreenSpaceError: number
  }
  defaultPosition: {
    longitude: number
    latitude: number
    height: number
  }
  cameraFlight: {
    duration: number
    heading: number
    pitch: number
    roll: number
  }
  helper: {
    axesLength: number
    gridSpacing: number
    gridColor: string
  }
  tileset: {
    maximumScreenSpaceError: number
    maximumMemoryUsage: number
  }
}

export interface ThreeJSSceneConfig {
  camera: {
    fov: number
    near: number
    far: number
    initialPosition: { x: number; y: number; z: number }
  }
  controls: {
    dampingFactor: number
    minDistance: number
    maxDistance: number
    maxPolarAngle: number
  }
  lights: {
    ambient: { color: string; intensity: number }
    directional: { color: string; intensity: number; position: number[]; shadowMapSize: number }
    hemisphere: { skyColor: string; groundColor: string; intensity: number; position: number[] }
  }
  fog: {
    near: number
    far: number
  }
  helpers: {
    axes: { size: number }
    grid: { size: number; divisions: number; colorCenterLine: string; colorGrid: string }
  }
  renderer: {
    toneMapping: string
    toneMappingExposure: number
  }
}

export interface FormatsConfig {
  nativelySupported: string[]
  threejsSupported: string[]
  convertible: string[]
  cesiumSupported: string[]
  needsSlicing: string[]
  threejsEngineFormats: string[]
  cesiumOnlyFormats: string[]
}

export interface PerformanceConfig {
  tilesLoadTimeout: number
  modelLoadTimeout: number
  batchSize: number
  minDistance: number
  scaleFactor: number
}

export interface UIConfig {
  fpsMonitorInterval: number
  cameraUpdateDebounce: number
}

export interface MinIOConfig {
  buckets: string[]
}

export interface SceneConfig {
  mars3d: Mars3DSceneConfig
  threejs: ThreeJSSceneConfig
  formats: FormatsConfig
  performance: PerformanceConfig
  ui: UIConfig
  minio: MinIOConfig
}

// ==================== 默认值（内联回退，确保配置加载失败时系统仍可运行） ====================

const DEFAULT_CONFIG: SceneConfig = {
  mars3d: {
    scene: {
      center: { lng: 116.39, lat: 39.91, alt: 15000000, heading: 0, pitch: -45 },
      showSun: false,
      showMoon: false,
      showSkyAtmosphere: true,
      fog: false,
      cameraController: { minimumZoomDistance: 1, maximumZoomDistance: 50000000 },
      globe: { depthTestAgainstTerrain: false, enableLighting: false },
      clock: { currentTime: '2025-01-01' }
    },
    control: {
      toolbar: { position: 'right-bottom' },
      homeButton: true,
      fullscreenButton: false,
      navigationHelpButton: false,
      baseLayerPicker: false,
      sceneModePicker: true,
      vrButton: false,
      animation: false,
      timeline: false,
      infoBox: false,
      selectionIndicator: false,
      compass: { bottom: 'toolbar', right: '5px' }
    },
    terrain: { show: false, url: '' },
    basemaps: [{
      name: 'ArcGIS影像底图',
      type: 'xyz',
      url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
      show: true
    }],
    performance: { tileCacheSize: 100, maximumScreenSpaceError: 2 },
    defaultPosition: { longitude: 116.397128, latitude: 39.908802, height: 100 },
    cameraFlight: { duration: 2.0, heading: 0, pitch: -45, roll: 0 },
    helper: { axesLength: 500000, gridSpacing: 30, gridColor: 'rgba(255,255,255,0.15)' },
    tileset: { maximumScreenSpaceError: 16, maximumMemoryUsage: 512 }
  },
  threejs: {
    camera: { fov: 60, near: 0.1, far: 10000, initialPosition: { x: 5, y: 5, z: 5 } },
    controls: { dampingFactor: 0.05, minDistance: 1, maxDistance: 1000, maxPolarAngle: 180 },
    lights: {
      ambient: { color: '#ffffff', intensity: 0.6 },
      directional: { color: '#ffffff', intensity: 0.8, position: [10, 10, 5], shadowMapSize: 2048 },
      hemisphere: { skyColor: '#ffffff', groundColor: '#444444', intensity: 0.4, position: [0, 20, 0] }
    },
    fog: { near: 10, far: 1000 },
    helpers: {
      axes: { size: 100 },
      grid: { size: 100, divisions: 100, colorCenterLine: '#888888', colorGrid: '#444444' }
    },
    renderer: { toneMapping: 'ACESFilmic', toneMappingExposure: 1.0 }
  },
  formats: {
    nativelySupported: ['gltf', 'glb', 'json'],
    threejsSupported: ['obj', 'fbx', 'dae', 'stl', 'ply'],
    convertible: ['blend', 'las', 'laz', 'e57'],
    cesiumSupported: ['gltf', 'glb', 'json', 'tileset', 'tiles'],
    needsSlicing: ['obj', 'fbx', 'dae', 'stl', '3ds', 'ply', 'osgb', 'las', 'laz', 'e57', 'blend'],
    threejsEngineFormats: ['obj', 'fbx', 'dae', 'stl', '3ds', 'blend', 'ply'],
    cesiumOnlyFormats: ['json', 'tiles', 'osgb', 'las', 'laz', 'e57']
  },
  performance: {
    tilesLoadTimeout: 30000,
    modelLoadTimeout: 10000,
    batchSize: 3,
    minDistance: 500,
    scaleFactor: 50
  },
  ui: {
    fpsMonitorInterval: 1000,
    cameraUpdateDebounce: 100
  },
  minio: {
    buckets: ['models-3d', 'slices', 'textures', 'thumbnails', 'videos']
  }
}

// ==================== 配置加载与缓存 ====================

let _config: SceneConfig | null = null
let _loadingPromise: Promise<SceneConfig> | null = null

/**
 * 异步加载场景配置文件
 * 多次调用只会加载一次（单例模式）
 * 加载失败时回退到默认值
 */
export async function loadSceneConfig(): Promise<SceneConfig> {
  if (_config) return _config

  if (_loadingPromise) return _loadingPromise

  _loadingPromise = (async () => {
    try {
      const baseUrl = import.meta.env.BASE_URL || '/'
      const url = `${baseUrl}config/scene-config.json`
      const response = await fetch(url)
      if (!response.ok) {
        console.warn(`[scene-config] 加载配置文件失败 (${response.status})，使用默认配置`)
        _config = DEFAULT_CONFIG
        return _config
      }
      const data = await response.json()
      // 深度合并：用户配置覆盖默认值，确保新增字段不会缺失
      _config = deepMerge(DEFAULT_CONFIG, data) as SceneConfig
      console.log('[scene-config] 场景配置加载成功')
      return _config
    } catch (error) {
      console.warn('[scene-config] 加载配置文件异常，使用默认配置:', error)
      _config = DEFAULT_CONFIG
      return _config
    }
  })()

  return _loadingPromise
}

/**
 * 获取已加载的场景配置（同步）
 * 必须在 loadSceneConfig() 完成后调用
 */
export function getSceneConfig(): SceneConfig {
  if (!_config) {
    console.warn('[scene-config] 配置尚未加载，使用默认配置。建议先调用 loadSceneConfig()')
    return DEFAULT_CONFIG
  }
  return _config
}

/**
 * 重置配置状态（主要用于测试）
 */
export function resetSceneConfig(): void {
  _config = null
  _loadingPromise = null
}

// ==================== 工具函数 ====================

/**
 * 深度合并对象，source 的值覆盖 target
 */
function deepMerge(target: any, source: any): any {
  if (!source || typeof source !== 'object') return target
  if (!target || typeof target !== 'object') return source

  const result = { ...target }
  for (const key of Object.keys(source)) {
    if (
      source[key] !== null &&
      typeof source[key] === 'object' &&
      !Array.isArray(source[key]) &&
      target[key] !== null &&
      typeof target[key] === 'object' &&
      !Array.isArray(target[key])
    ) {
      result[key] = deepMerge(target[key], source[key])
    } else {
      result[key] = source[key]
    }
  }
  return result
}
