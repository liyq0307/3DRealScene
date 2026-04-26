<template>
  <div class="three-viewer-wrapper">
    <div ref="threeContainer" class="three-container"></div>

    <!-- 控制面板 -->
    <div class="controls">
      <button class="btn" @click="resetView" title="重置视图">
        <span class="icon">🎥</span>
      </button>
      <button class="btn" @click="toggleWireframe" title="切换线框模式">
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
        <span class="info-label">三角面数:</span>
        <span class="info-value">{{ trianglesCount }}</span>
      </div>
      <div class="info-item">
        <span class="info-label">FPS:</span>
        <span class="info-value">{{ fps }}</span>
      </div>
      <div class="info-item">
        <span class="info-label">渲染引擎:</span>
        <span class="info-value">Three.js</span>
      </div>
      <div class="info-item">
        <span class="info-label">相机X:</span>
        <span class="info-value">{{ cameraPosition.x }}</span>
      </div>
      <div class="info-item">
        <span class="info-label">相机Y:</span>
        <span class="info-value">{{ cameraPosition.y }}</span>
      </div>
      <div class="info-item">
        <span class="info-label">相机Z:</span>
        <span class="info-value">{{ cameraPosition.z }}</span>
      </div>
    </div>

    <!-- 加载提示 -->
    <div v-if="loading" class="loading-overlay">
      <div class="loading-content">
        <div class="spinner"></div>
        <p>{{ loadingMessage }}</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * Three.js 3D查看器组件
 *
 * 功能特性:
 * - 基于Three.js的通用3D模型展示
 * - 支持多种3D模型格式（GLTF/GLB、OBJ、FBX、STL、PLY、DAE等）
 * - 实时性能监控（FPS、三角面数）
 * - 智能相机控制和轨道控制器
 * - 异步对象加载和错误处理
 * - 响应式设计,支持移动端
 *
 * 支持的文件格式:
 * - GLTF/GLB: PBR材质,骨骼动画
 * - OBJ: 静态网格,带MTL材质
 * - FBX: Autodesk格式,支持动画
 * - STL: 3D打印格式
 * - PLY: 点云数据
 * - DAE: Collada格式
 * - 3DS: 3D Studio格式
 *
 * 技术栈: Vue 3 Composition API + TypeScript + Three.js
 * 作者: liyq
 * 创建时间: 2025-12-08
 */
import { ref, onMounted, onUnmounted, watch, computed } from 'vue'
import { useMessage } from '@/composables/useMessage'
import * as THREE from 'three'
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js'
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js'
import { OBJLoader } from 'three/examples/jsm/loaders/OBJLoader.js'
import { MTLLoader } from 'three/examples/jsm/loaders/MTLLoader.js'
import { FBXLoader } from 'three/examples/jsm/loaders/FBXLoader.js'
import { STLLoader } from 'three/examples/jsm/loaders/STLLoader.js'
import { PLYLoader } from 'three/examples/jsm/loaders/PLYLoader.js'
import { ColladaLoader } from 'three/examples/jsm/loaders/ColladaLoader.js'
import { TilesRenderer } from '3d-tiles-renderer'
import { DebugTilesPlugin } from '3d-tiles-renderer/src/three/plugins/DebugTilesPlugin.js'
import fileHandleStore from '@/services/fileHandleStore'

// ==================== 类型定义 ====================

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
  position: [number, number, number] | TransformParams
  rotation: TransformParams | string
  scale: TransformParams | string
  modelPath?: string
  isVisible?: boolean
}

// ==================== Props 定义 ====================

interface Props {
  /** 是否显示信息面板 */
  showInfo?: boolean
  /** 初始相机位置 */
  initialCameraPosition?: { x: number; y: number; z: number }
  /** 场景对象列表 */
  sceneObjects?: SceneObject[]
  /** 背景颜色 */
  backgroundColor?: string
  /** 是否启用阴影 */
  enableShadows?: boolean
  /** 是否启用抗锯齿 */
  enableAntiAlias?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  showInfo: true,
  initialCameraPosition: () => ({ x: 5, y: 5, z: 5 }),
  sceneObjects: () => [],
  backgroundColor: '#1a1a1a',
  enableShadows: true,
  enableAntiAlias: true
})

// ==================== Emits 定义 ====================

const emit = defineEmits<{
  ready: [viewer: any]
  error: [error: Error]
  objectLoaded: [object: THREE.Object3D]
}>()

// ==================== 组合式API ====================

const { success: showSuccess, error: showError } = useMessage()

// ==================== 响应式状态 ====================

const threeContainer = ref<HTMLDivElement | null>(null)
const loading = ref(true)
const loadingMessage = ref('初始化Three.js场景...')
const wireframeMode = ref(false)
const boundingBoxVisible = ref(false) // 默认隐藏包围盒
const fps = ref(60)
const loadedObjectsCount = ref(0)
const trianglesCount = ref(0)
const cameraPosition = ref({ x: 0, y: 0, z: 0 })
const tilesCount = ref(0) // 追踪3D Tiles切片数量（响应式）

// ==================== 计算属性 ====================

/** 是否有3D Tiles切片（用于控制包围盒按钮显示） */
const hasTilesModels = computed(() => {
  return tilesCount.value > 0
})

// ==================== Three.js核心对象 ====================
let scene: THREE.Scene | null = null
let camera: THREE.PerspectiveCamera | null = null
let renderer: THREE.WebGLRenderer | null = null
let controls: OrbitControls | null = null
let animationFrameId: number | null = null
let axesHelper: THREE.AxesHelper | null = null
let gridHelper: THREE.GridHelper | null = null

// 性能监控
let lastTime = performance.now()
let frameCount = 0
let fpsInterval: number | null = null

// 加载器集合
const loaders = {
  gltf: new GLTFLoader(),
  obj: new OBJLoader(),
  mtl: new MTLLoader(),
  fbx: new FBXLoader(),
  stl: new STLLoader(),
  ply: new PLYLoader(),
  dae: new ColladaLoader()
}

// 3D Tiles渲染器集合（每个tileset一个实例）
const tilesRenderers = new Map<string, TilesRenderer>()

// 调试插件集合（用于显示包围盒）
const debugPlugins = new Map<string, DebugTilesPlugin>()

// ==================== 业务逻辑方法 ====================

/**
 * 初始化Three.js场景
 */
const initThreeScene = () => {
  if (!threeContainer.value) return

  console.log('[ThreeViewer] 初始化Three.js场景')

  // 创建场景
  scene = new THREE.Scene()
  scene.background = new THREE.Color(props.backgroundColor)
  scene.fog = new THREE.Fog(props.backgroundColor, 10, 1000)

  // 创建相机
  const aspect = threeContainer.value.clientWidth / threeContainer.value.clientHeight
  camera = new THREE.PerspectiveCamera(60, aspect, 0.1, 10000)
  camera.position.set(
    props.initialCameraPosition.x,
    props.initialCameraPosition.y,
    props.initialCameraPosition.z
  )

  // 创建渲染器
  renderer = new THREE.WebGLRenderer({
    antialias: props.enableAntiAlias,
    alpha: true
  })
  renderer.setSize(threeContainer.value.clientWidth, threeContainer.value.clientHeight)
  renderer.setPixelRatio(window.devicePixelRatio)
  renderer.shadowMap.enabled = props.enableShadows
  renderer.shadowMap.type = THREE.PCFSoftShadowMap
  renderer.toneMapping = THREE.ACESFilmicToneMapping
  renderer.toneMappingExposure = 1.0
  renderer.outputColorSpace = THREE.SRGBColorSpace

  threeContainer.value.appendChild(renderer.domElement)

  // 创建轨道控制器
  controls = new OrbitControls(camera, renderer.domElement)
  controls.enableDamping = true
  controls.dampingFactor = 0.05
  controls.screenSpacePanning = false
  controls.minDistance = 1
  controls.maxDistance = 1000
  controls.maxPolarAngle = Math.PI

  // 添加环境光
  const ambientLight = new THREE.AmbientLight(0xffffff, 0.6)
  scene.add(ambientLight)

  // 添加方向光
  const directionalLight = new THREE.DirectionalLight(0xffffff, 0.8)
  directionalLight.position.set(10, 10, 5)
  directionalLight.castShadow = props.enableShadows
  directionalLight.shadow.mapSize.width = 2048
  directionalLight.shadow.mapSize.height = 2048
  scene.add(directionalLight)

  // 添加半球光
  const hemisphereLight = new THREE.HemisphereLight(0xffffff, 0x444444, 0.4)
  hemisphereLight.position.set(0, 20, 0)
  scene.add(hemisphereLight)

  // 添加坐标轴辅助器
  axesHelper = new THREE.AxesHelper(100)
  axesHelper.visible = true
  scene.add(axesHelper)

  // 添加网格辅助器
  gridHelper = new THREE.GridHelper(100, 100, 0x888888, 0x444444)
  gridHelper.visible = true
  scene.add(gridHelper)

  // 监听窗口大小变化
  window.addEventListener('resize', handleResize)

  // 开始渲染循环
  animate()

  // 开始FPS监控
  startFPSMonitoring()

  loading.value = false
  emit('ready', { scene, camera, renderer, controls })
}

/**
 * 渲染循环
 */
const animate = () => {
  animationFrameId = requestAnimationFrame(animate)

  if (controls) {
    controls.update()
  }

  // 更新所有3D Tiles渲染器
  if (camera && renderer) {
    tilesRenderers.forEach((tilesRenderer, key) => {
      tilesRenderer.setCamera(camera!)
      tilesRenderer.setResolutionFromRenderer(camera!, renderer!)
      tilesRenderer.update()

      // 每100帧打印一次调试信息
      if (frameCount % 100 === 0) {
        console.log(`[ThreeViewer] TilesRenderer[${key}] 状态:`)
        console.log(`[ThreeViewer]   - 已加载瓦片数: ${tilesRenderer.group.children.length}`)
        console.log(`[ThreeViewer]   - 下载中: ${(tilesRenderer.downloadQueue as any)?.items?.length || 0}`)
        console.log(`[ThreeViewer]   - 解析中: ${(tilesRenderer.parseQueue as any)?.items?.length || 0}`)
      }
    })
  }

  if (scene && camera && renderer) {
    renderer.render(scene, camera)

    // 更新相机位置信息
    cameraPosition.value = {
      x: Math.round(camera.position.x * 100) / 100,
      y: Math.round(camera.position.y * 100) / 100,
      z: Math.round(camera.position.z * 100) / 100
    }

    // 更新FPS计数
    frameCount++
    const currentTime = performance.now()
    if (currentTime >= lastTime + 1000) {
      fps.value = Math.round((frameCount * 1000) / (currentTime - lastTime))
      frameCount = 0
      lastTime = currentTime
    }
  }
}

/**
 * 窗口大小调整处理
 */
const handleResize = () => {
  if (!threeContainer.value || !camera || !renderer) return

  const width = threeContainer.value.clientWidth
  const height = threeContainer.value.clientHeight

  camera.aspect = width / height
  camera.updateProjectionMatrix()

  renderer.setSize(width, height)
}

/**
 * 加载场景对象
 */
const loadSceneObjects = async () => {
  if (!props.sceneObjects || props.sceneObjects.length === 0) {
    console.log('[ThreeViewer] 没有需要加载的对象')
    return
  }

  console.log('[ThreeViewer] ========================================')
  console.log('[ThreeViewer] 开始加载场景对象,数量:', props.sceneObjects.length)
  console.log('[ThreeViewer] 场景对象详情:')
  props.sceneObjects.forEach((obj, index) => {
    console.log(`[ThreeViewer] [${index}] ${obj.name}:`)
    console.log(`[ThreeViewer]     - id: ${obj.id}`)
    console.log(`[ThreeViewer]     - displayPath: ${obj.displayPath}`)
    console.log(`[ThreeViewer]     - modelPath: ${obj.modelPath || 'undefined'}`)
  })
  console.log('[ThreeViewer] ========================================')

  for (const obj of props.sceneObjects) {
    try {
      loadingMessage.value = `加载模型: ${obj.name}...`
      await loadSceneObject(obj)
      loadedObjectsCount.value++
    } catch (error) {
      console.error(`[ThreeViewer] 加载对象失败: ${obj.name}`, error)
      showError(`加载模型 ${obj.name} 失败`)
    }
  }

  // 调整相机以适应所有对象
  fitCameraToObjects()
}

/**
 * 加载OBJ+MTL模型的辅助函数
 */
const loadOBJWithMTL = (
  modelPath: string,
  obj: SceneObject,
  resolve: (value?: void | PromiseLike<void>) => void,
  reject: (reason?: any) => void
) => {
  console.log('[ThreeViewer] 开始加载OBJ文件:', modelPath)

  // 将MinIO预签名URL转换为后端代理URL
  // 原始: http://localhost:9000/models-3d/folder/file.obj?X-Amz-...
  // 转换: /api/files/proxy/models-3d/folder/file.obj
  let proxyModelPath = modelPath
  let baseURL = ''
  let fileName = ''
  let folderPath = ''

  try {
    const urlObj = new URL(modelPath, window.location.origin)

    // 检查是否是MinIO URL（包含签名参数）
    if (urlObj.search.includes('X-Amz-Algorithm')) {
      console.log('[ThreeViewer] 检测到MinIO预签名URL，转换为代理路径')

      // 提取路径部分: /models-3d/folder/file.obj
      const pathname = urlObj.pathname

      // 转换为代理URL
      proxyModelPath = `/api/files/proxy${pathname}`

      console.log('[ThreeViewer] 转换后的代理路径:', proxyModelPath)
    }

    // 解析路径
    const urlForParsing = new URL(proxyModelPath, window.location.origin)
    const pathname = urlForParsing.pathname
    const pathParts = pathname.split('/')
    fileName = pathParts[pathParts.length - 1]  // file.obj
    const basePath = pathname.substring(0, pathname.lastIndexOf('/') + 1)  // /api/files/proxy/models-3d/folder/
    baseURL = urlForParsing.origin + basePath
    folderPath = basePath

    console.log('[ThreeViewer] 基础路径:', baseURL)
    console.log('[ThreeViewer] OBJ文件名:', fileName)
    console.log('[ThreeViewer] 文件夹路径:', folderPath)
  } catch (error) {
    console.error('[ThreeViewer] URL解析失败:', error)
    reject(error)
    return
  }

  // 构建MTL路径（不带签名参数）
  const mtlFileName = fileName.replace(/\.obj$/i, '.mtl')
  const mtlFullPath = baseURL + mtlFileName

  console.log('[ThreeViewer] MTL文件名:', mtlFileName)
  console.log('[ThreeViewer] MTL完整路径:', mtlFullPath)

  // 创建LoadingManager来正确处理纹理路径
  const loadingManager = new THREE.LoadingManager()

  // 设置URL修改器,确保纹理路径正确解析
  loadingManager.setURLModifier((url) => {
    // 如果是相对路径(纹理文件路径),添加基础路径
    if (!url.startsWith('http') && !url.startsWith('/') && !url.startsWith('data:')) {
      const resolved = baseURL + url
      console.log('[ThreeViewer] 纹理路径:', url, '->', resolved)
      return resolved
    }
    return url
  })

  // 创建MTL加载器
  const mtlLoader = new MTLLoader(loadingManager)
  // 不设置 setPath，直接使用完整路径避免路径重复拼接
  // mtlLoader.setPath(baseURL)

  // 加载MTL文件（使用代理路径，不带签名参数）
  mtlLoader.load(
    mtlFullPath,
    // MTL加载成功
    (materials) => {
      console.log('[ThreeViewer] ✅ MTL加载成功,材质数:', Object.keys(materials.materials).length)

      // 预加载材质和纹理
      materials.preload()

      // 打印材质详情
      Object.keys(materials.materials).forEach(key => {
        const mat = materials.materials[key] as any
        console.log('[ThreeViewer] 材质:', key)
        if (mat.map) console.log('  - 漫反射纹理:', mat.map)
        if (mat.normalMap) console.log('  - 法线贴图:', mat.normalMap)
        if (mat.bumpMap) console.log('  - 凹凸贴图:', mat.bumpMap)
      })

      // 创建OBJ加载器并应用材质
      const objLoader = new OBJLoader(loadingManager)
      objLoader.setMaterials(materials)

      // 加载OBJ（使用代理路径）
      objLoader.load(
        proxyModelPath,
        (object) => {
          console.log('[ThreeViewer] ✅ OBJ加载成功(带材质)')

          // 检查网格材质
          let meshCount = 0
          object.traverse((child) => {
            if (child instanceof THREE.Mesh) {
              meshCount++
              console.log(`[ThreeViewer] Mesh #${meshCount}:`, child.name || 'unnamed')
              if (Array.isArray(child.material)) {
                child.material.forEach((m: any, i) => {
                  console.log(`  材质[${i}]:`, m.name, m.map ? '有纹理' : '无纹理')
                })
              } else {
                const mat = child.material as any
                console.log('  材质:', mat.name, mat.map ? '有纹理' : '无纹理')
              }
            }
          })

          addObjectToScene(object, obj)
          resolve()
        },
        undefined,
        (error) => {
          console.error('[ThreeViewer] ❌ OBJ加载失败:', error)
          reject(error)
        }
      )
    },
    undefined,
    // MTL加载失败
    (mtlError) => {
      console.warn('[ThreeViewer] ⚠️ MTL加载失败,使用默认材质:', mtlError)

      // 不使用LoadingManager,直接加载OBJ（使用代理路径）
      const objLoader = new OBJLoader()
      objLoader.load(
        proxyModelPath,
        (object) => {
          console.log('[ThreeViewer] OBJ加载成功(无材质)')

          // 应用默认材质
          object.traverse((child) => {
            if (child instanceof THREE.Mesh) {
              child.material = new THREE.MeshPhongMaterial({
                color: 0xaaaaaa,
                side: THREE.DoubleSide
              })
            }
          })

          addObjectToScene(object, obj)
          resolve()
        },
        undefined,
        reject
      )
    }
  )
}

/**
 * 加载3D Tiles tileset
 */
const loadTileset = (
  modelPath: string,
  obj: SceneObject,
  resolve: (value?: void | PromiseLike<void>) => void,
  reject: (reason?: any) => void
) => {
  console.log('[ThreeViewer] ======================================')
  console.log('[ThreeViewer] 开始加载3D Tiles')
  console.log('[ThreeViewer] 原始路径:', modelPath)

  if (!scene || !camera || !renderer) {
    reject(new Error('Three.js场景未初始化'))
    return
  }

  try {
    // 将MinIO预签名URL转换为后端代理URL
    let proxyTilesetPath = modelPath

    const urlObj = new URL(modelPath, window.location.origin)
    if (urlObj.search.includes('X-Amz-Algorithm')) {
      proxyTilesetPath = `/api/files/proxy${urlObj.pathname}`
    } else if (urlObj.pathname.startsWith('/api/files/')) {
      proxyTilesetPath = urlObj.pathname
    }

    console.log('[ThreeViewer] 最终tileset路径:', proxyTilesetPath)

    // 创建TilesRenderer实例
    const tilesRenderer = new TilesRenderer(proxyTilesetPath)

    // 创建调试插件（用于显示包围盒）
    const debugPlugin = new DebugTilesPlugin({
      displayBoxBounds: boundingBoxVisible.value,
      displaySphereBounds: false,
      enabled: true
    })

    // 将插件添加到TilesRenderer（registerPlugin只接受一个参数：plugin对象）
    tilesRenderer.registerPlugin(debugPlugin)

    console.log('[ThreeViewer] DebugTilesPlugin已创建并注册，初始状态:', boundingBoxVisible.value)

    // 先添加到场景
    scene.add(tilesRenderer.group)

    // 应用场景对象的变换到group
    const position = Array.isArray(obj.position)
      ? obj.position
      : [obj.position.x, obj.position.y, obj.position.z]

    tilesRenderer.group.position.set(position[0], position[1], position[2])

    const rotation = typeof obj.rotation === 'string'
      ? JSON.parse(obj.rotation)
      : obj.rotation

    tilesRenderer.group.rotation.set(
      (rotation.x * Math.PI) / 180,
      (rotation.y * Math.PI) / 180,
      (rotation.z * Math.PI) / 180
    )

    const scale = typeof obj.scale === 'string' ? JSON.parse(obj.scale) : obj.scale
    tilesRenderer.group.scale.set(scale.x, scale.y, scale.z)

    console.log('[ThreeViewer] Group变换:', {
      position,
      rotation: [rotation.x, rotation.y, rotation.z],
      scale: [scale.x, scale.y, scale.z]
    })

    // 监听tileset加载完成
    tilesRenderer.addEventListener('load-tile-set', () => {
      console.log('[ThreeViewer] ✅ Tileset加载成功')

      // 同步调试插件显示状态
      debugPlugin.displayBoxBounds = boundingBoxVisible.value
      debugPlugin.displaySphereBounds = false
      console.log('[ThreeViewer] 包围盒显示状态已同步:', boundingBoxVisible.value)

      // 获取包围盒并调整相机
      const box = new THREE.Box3()
      const sphere = new THREE.Sphere()
      tilesRenderer.getBoundingBox(box)
      tilesRenderer.getBoundingSphere(sphere)

      console.log('[ThreeViewer] 包围盒信息:', {
        boxMin: box.min.toArray(),
        boxMax: box.max.toArray(),
        sphereCenter: sphere.center.toArray(),
        sphereRadius: sphere.radius
      })

      if (!box.isEmpty() && sphere.radius > 0) {
        // 使用包围球来定位相机
        const center = sphere.center
        const radius = sphere.radius

        // 设置相机位置：从中心沿(1,1,1)方向移动到合适距离
        const distance = radius * 2.5
        const direction = new THREE.Vector3(1, 1, 1).normalize()
        camera!.position.copy(center).add(direction.multiplyScalar(distance))
        camera!.lookAt(center)

        // 设置轨道控制器目标
        if (controls) {
          controls.target.copy(center)
          controls.update()
        }

        // 调整near/far平面
        camera!.near = radius / 100
        camera!.far = radius * 10
        camera!.updateProjectionMatrix()

        console.log('[ThreeViewer] 相机已调整:', {
          position: camera!.position.toArray(),
          target: center.toArray(),
          near: camera!.near,
          far: camera!.far,
          distance
        })

        // 强制更新一次以触发瓦片加载
        tilesRenderer.setCamera(camera!)
        tilesRenderer.setResolutionFromRenderer(camera!, renderer!)
        tilesRenderer.update()

        console.log('[ThreeViewer] 首次update已调用')
      } else {
        console.warn('[ThreeViewer] 包围盒为空或包围球半径为0')
      }
    })

    // 监听瓦片加载
    tilesRenderer.addEventListener('load-model', () => {
      console.log('[ThreeViewer] ✅ 加载了一个瓦片')
    })

    // 监听错误
    tilesRenderer.addEventListener('load-tile-set-error', (event: any) => {
      console.error('[ThreeViewer] ❌ Tileset加载失败:', event)
    })

    tilesRenderer.addEventListener('load-tile-error', (event: any) => {
      console.error('[ThreeViewer] ❌ 瓦片加载失败:', event)
    })

    // 立即设置相机和分辨率
    tilesRenderer.setCamera(camera)
    tilesRenderer.setResolutionFromRenderer(camera, renderer)

    // 存储渲染器实例和调试插件
    tilesRenderers.set(obj.id, tilesRenderer)
    debugPlugins.set(obj.id, debugPlugin)

    // 更新对象计数和切片计数
    loadedObjectsCount.value++
    tilesCount.value = tilesRenderers.size // 更新切片数量（触发hasTilesModels计算属性）

    console.log('[ThreeViewer] ✅ TilesRenderer已创建并添加到场景')
    console.log('[ThreeViewer] 当前切片数量:', tilesCount.value)
    console.log('[ThreeViewer] ======================================')

    resolve()
  } catch (error) {
    console.error('[ThreeViewer] ❌ 创建TilesRenderer失败:', error)
    reject(error)
  }
}

/**
 * 加载单个场景对象
 */
const loadSceneObject = async (obj: SceneObject): Promise<void> => {
  return new Promise(async (resolve, reject) => {
    let modelPath = obj.displayPath || obj.modelPath
    if (!modelPath) {
      reject(new Error('模型路径为空'))
      return
    }

    console.log(`[ThreeViewer] ====== 开始加载场景对象 ======`)
    console.log(`[ThreeViewer] 对象名称: ${obj.name}`)
    console.log(`[ThreeViewer] displayPath: ${obj.displayPath}`)
    console.log(`[ThreeViewer] modelPath: ${obj.modelPath}`)
    console.log(`[ThreeViewer] 使用路径: ${modelPath}`)

    // 检查是否为文件句柄路径
    if (modelPath.startsWith('local-file-handle://')) {
      console.log('[ThreeViewer] 检测到文件句柄路径，尝试获取文件:', modelPath)
      
      try {
        const uuid = modelPath.replace('local-file-handle://', '')
        const handle = await fileHandleStore.getHandle<any>(uuid)
        
        if (handle) {
          // 检查权限
          const permission = await handle.queryPermission({ mode: 'read' })
          if (permission === 'granted') {
            const file = await handle.getFile()
            // 创建临时URL用于预览
            modelPath = URL.createObjectURL(file)
            console.log('[ThreeViewer] 文件句柄转换成功:', { uuid, fileName: file.name, blobUrl: modelPath })
          } else {
            // 请求权限
            const requestResult = await handle.requestPermission({ mode: 'read' })
            if (requestResult === 'granted') {
              const file = await handle.getFile()
              modelPath = URL.createObjectURL(file)
              console.log('[ThreeViewer] 文件句柄权限获取成功:', { uuid, fileName: file.name, blobUrl: modelPath })
            } else {
              reject(new Error('无法访问文件句柄，请重新选择文件'))
              return
            }
          }
        } else {
          reject(new Error('文件句柄不存在，请重新选择文件'))
          return
        }
      } catch (err) {
        console.error('[ThreeViewer] 文件句柄处理失败:', err)
        reject(new Error('文件句柄访问失败，请重新选择文件或使用MinIO上传'))
        return
      }
    }

    // 获取文件扩展名
    const fileExt = modelPath.split('?')[0].split('.').pop()?.toLowerCase()

    console.log(`[ThreeViewer] 检测到文件扩展名: ${fileExt}`)
    console.log(`[ThreeViewer] ============================`)

    // 根据文件类型选择加载器
    switch (fileExt) {
      case 'gltf':
      case 'glb':
        loaders.gltf.load(
          modelPath,
          (gltf) => {
            addObjectToScene(gltf.scene, obj)
            resolve()
          },
          undefined,
          reject
        )
        break

      case 'obj':
        // 使用专门的OBJ+MTL加载函数
        loadOBJWithMTL(modelPath, obj, resolve, reject)
        break

      case 'fbx':
        loaders.fbx.load(
          modelPath,
          (object) => {
            addObjectToScene(object, obj)
            resolve()
          },
          undefined,
          reject
        )
        break

      case 'stl':
        loaders.stl.load(
          modelPath,
          (geometry) => {
            const material = new THREE.MeshPhongMaterial({ color: 0x888888 })
            const mesh = new THREE.Mesh(geometry, material)
            mesh.castShadow = true
            mesh.receiveShadow = true
            addObjectToScene(mesh, obj)
            resolve()
          },
          undefined,
          reject
        )
        break

      case 'ply':
        loaders.ply.load(
          modelPath,
          (geometry) => {
            const material = new THREE.MeshPhongMaterial({ vertexColors: true })
            const mesh = new THREE.Mesh(geometry, material)
            mesh.castShadow = true
            mesh.receiveShadow = true
            addObjectToScene(mesh, obj)
            resolve()
          },
          undefined,
          reject
        )
        break

      case 'dae':
        loaders.dae.load(
          modelPath,
          (collada) => {
            addObjectToScene(collada.scene, obj)
            resolve()
          },
          undefined,
          reject
        )
        break

      case 'json':
        // 检查是否为tileset.json
        if (modelPath.includes('tileset')) {
          loadTileset(modelPath, obj, resolve, reject)
        } else {
          reject(new Error(`不支持的JSON文件: ${modelPath}`))
        }
        break

      default:
        reject(new Error(`不支持的文件格式: ${fileExt}`))
    }
  })
}

/**
 * 添加对象到场景
 */
const addObjectToScene = (object: THREE.Object3D, sceneObj: SceneObject) => {
  if (!scene) return

  // 应用变换
  const position = Array.isArray(sceneObj.position)
    ? sceneObj.position
    : [sceneObj.position.x, sceneObj.position.y, sceneObj.position.z]

  object.position.set(position[0], position[1], position[2])

  // 处理旋转
  const rotation = typeof sceneObj.rotation === 'string'
    ? JSON.parse(sceneObj.rotation)
    : sceneObj.rotation

  object.rotation.set(
    (rotation.x * Math.PI) / 180,
    (rotation.y * Math.PI) / 180,
    (rotation.z * Math.PI) / 180
  )

  // 处理缩放
  const scale = typeof sceneObj.scale === 'string' ? JSON.parse(sceneObj.scale) : sceneObj.scale

  object.scale.set(scale.x, scale.y, scale.z)

  // 添加到场景
  scene.add(object)

  // 更新三角面数
  updateTrianglesCount()

  emit('objectLoaded', object)

  console.log(`[ThreeViewer] 对象已添加到场景: ${sceneObj.name}`)
}

/**
 * 更新三角面数统计
 */
const updateTrianglesCount = () => {
  if (!scene) return

  let count = 0
  scene.traverse((object) => {
    if (object instanceof THREE.Mesh) {
      const geometry = object.geometry
      if (geometry.index) {
        count += geometry.index.count / 3
      } else {
        count += geometry.attributes.position.count / 3
      }
    }
  })

  trianglesCount.value = Math.floor(count)
}

/**
 * 调整相机以适应所有对象
 */
const fitCameraToObjects = () => {
  if (!scene || !camera || !controls) return

  const box = new THREE.Box3()
  let hasObjects = false

  // 1. 统计普通网格对象
  scene.traverse((object) => {
    if (object instanceof THREE.Mesh) {
      box.expandByObject(object)
      hasObjects = true
    }
  })

  // 2. 统计3D Tiles渲染器的包围盒
  if (tilesRenderers.size > 0) {
    tilesRenderers.forEach((tilesRenderer) => {
      // 使用TilesRenderer的group包围盒
      const tilesBox = new THREE.Box3().setFromObject(tilesRenderer.group)
      if (!tilesBox.isEmpty()) {
        box.union(tilesBox)
        hasObjects = true
      }
    })
  }

  if (!hasObjects || box.isEmpty()) {
    console.warn('[ThreeViewer] 场景中没有可见对象（可能正在加载中）')
    return
  }

  const center = box.getCenter(new THREE.Vector3())
  const size = box.getSize(new THREE.Vector3())

  const maxDim = Math.max(size.x, size.y, size.z)
  const fov = camera.fov * (Math.PI / 180)
  let cameraZ = Math.abs(maxDim / 2 / Math.tan(fov / 2))
  cameraZ *= 1.5 // 增加一些边距

  camera.position.set(center.x + cameraZ, center.y + cameraZ, center.z + cameraZ)
  camera.lookAt(center)
  controls.target.copy(center)
  controls.update()

  console.log('[ThreeViewer] 相机已调整以适应对象')
}

/**
 * 重置视图
 */
const resetView = () => {
  fitCameraToObjects()
  showSuccess('视图已重置')
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

  showSuccess(wireframeMode.value ? '线框模式已启用' : '线框模式已禁用')
}

/**
 * 切换坐标轴
 */
const toggleAxes = () => {
  if (axesHelper) {
    axesHelper.visible = !axesHelper.visible
    showSuccess(axesHelper.visible ? '坐标轴已显示' : '坐标轴已隐藏')
  }
}

/**
 * 切换网格
 */
const toggleGrid = () => {
  if (gridHelper) {
    gridHelper.visible = !gridHelper.visible
    showSuccess(gridHelper.visible ? '网格已显示' : '网格已隐藏')
  }
}

/**
 * 切换包围盒显示（显示每个瓦片的包围盒）
 */
const toggleBoundingBox = () => {
  if (!scene) return

  boundingBoxVisible.value = !boundingBoxVisible.value

  console.log('[ThreeViewer] 切换包围盒显示状态:', boundingBoxVisible.value)
  console.log('[ThreeViewer] DebugPlugin数量:', debugPlugins.size)

  // 遍历所有调试插件，更新每个瓦片的包围盒显示状态
  debugPlugins.forEach((debugPlugin, key) => {
    console.log(`[ThreeViewer] 更新DebugPlugin[${key}]:`)
    console.log('  - displayBoxBounds 设置前:', debugPlugin.displayBoxBounds)

    debugPlugin.displayBoxBounds = boundingBoxVisible.value
    debugPlugin.displaySphereBounds = false // 只显示盒状包围体

    console.log('  - displayBoxBounds 设置后:', debugPlugin.displayBoxBounds)
    console.log('  - enabled:', debugPlugin.enabled)
  })

  showSuccess(boundingBoxVisible.value ? '包围盒已显示' : '包围盒已隐藏')
}

/**
 * 截图
 */
const takeScreenshot = () => {
  if (!renderer) return

  try {
    const dataURL = renderer.domElement.toDataURL('image/png')
    const link = document.createElement('a')
    link.download = `threejs-screenshot-${Date.now()}.png`
    link.href = dataURL
    link.click()
    showSuccess('截图已保存')
  } catch (error) {
    console.error('[ThreeViewer] 截图失败:', error)
    showError('截图失败')
  }
}

/**
 * 开始FPS监控
 */
const startFPSMonitoring = () => {
  fpsInterval = window.setInterval(() => {
    // FPS已在animate中更新
  }, 1000)
}

/**
 * 清理资源
 */
const cleanup = () => {
  console.log('[ThreeViewer] 清理资源')

  // 停止动画循环
  if (animationFrameId !== null) {
    cancelAnimationFrame(animationFrameId)
    animationFrameId = null
  }

  // 停止FPS监控
  if (fpsInterval !== null) {
    clearInterval(fpsInterval)
    fpsInterval = null
  }

  // 清理事件监听
  window.removeEventListener('resize', handleResize)

  // 释放控制器
  if (controls) {
    controls.dispose()
    controls = null
  }

  // 释放3D Tiles渲染器和调试插件
  tilesRenderers.forEach((tilesRenderer, key) => {
    tilesRenderer.dispose()
    console.log(`[ThreeViewer] 释放TilesRenderer: ${key}`)
  })
  tilesRenderers.clear()
  debugPlugins.clear()
  tilesCount.value = 0 // 重置切片计数

  // 释放场景中的资源
  if (scene) {
    scene.traverse((object) => {
      if (object instanceof THREE.Mesh) {
        object.geometry.dispose()
        if (Array.isArray(object.material)) {
          object.material.forEach((material) => material.dispose())
        } else {
          object.material.dispose()
        }
      }
    })
    scene = null
  }

  // 释放渲染器
  if (renderer) {
    renderer.dispose()
    renderer = null
  }

  camera = null
}

// ==================== 监听器 ====================

watch(
  () => props.sceneObjects,
  async () => {
    if (scene && props.sceneObjects.length > 0) {
      await loadSceneObjects()
    }
  },
  { deep: true }
)

// ==================== 生命周期钩子 ====================

onMounted(async () => {
  console.log('[ThreeViewer] 组件挂载')
  initThreeScene()
  await loadSceneObjects()
})

onUnmounted(() => {
  console.log('[ThreeViewer] 组件卸载')
  cleanup()
})
</script>

<style scoped>
/**
 * Three.js查看器样式
 * 全屏布局,提供沉浸式的3D查看体验
 */

.three-viewer-wrapper {
  position: relative;
  width: 100%;
  height: 100%;
  overflow: hidden;
  background: #1a1a1a;
}

.three-container {
  width: 100%;
  height: 100%;
}

/* 控制面板 */
.controls {
  position: absolute;
  top: 20px;
  right: 20px;
  display: flex;
  flex-direction: column;
  gap: 10px;
  z-index: 10;
}

.btn {
  width: 48px;
  height: 48px;
  background: rgba(255, 255, 255, 0.9);
  border: none;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
}

.btn:hover {
  background: white;
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
}

.btn .icon {
  font-size: 1.4rem;
}

/* 信息面板 */
.info-panel {
  position: absolute;
  top: 20px;
  left: 20px;
  background: rgba(0, 0, 0, 0.8);
  backdrop-filter: blur(10px);
  padding: 1rem;
  border-radius: 8px;
  color: white;
  font-family: 'Courier New', monospace;
  min-width: 200px;
  z-index: 10;
}

.info-item {
  display: flex;
  justify-content: space-between;
  margin-bottom: 0.5rem;
  font-size: 0.9rem;
}

.info-item:last-child {
  margin-bottom: 0;
}

.info-label {
  color: rgba(255, 255, 255, 0.7);
  margin-right: 1rem;
}

.info-value {
  color: #4ade80;
  font-weight: bold;
}

/* 加载覆盖层 */
.loading-overlay {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.9);
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

/* 动画 */
@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

/* 响应式设计 */
@media (max-width: 768px) {
  .controls {
    top: 10px;
    right: 10px;
    gap: 8px;
  }

  .btn {
    width: 40px;
    height: 40px;
  }

  .info-panel {
    top: 10px;
    left: 10px;
    padding: 0.75rem;
    min-width: 180px;
  }

  .info-item {
    font-size: 0.85rem;
  }
}
</style>
