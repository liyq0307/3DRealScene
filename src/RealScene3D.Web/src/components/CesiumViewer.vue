<template>
  <div class="cesium-viewer-wrapper">
    <div ref="cesiumContainer" class="cesium-container"></div>

    <!-- 控制面板 -->
    <div class="controls">
      <button class="btn" @click="resetView" title="重置视图">
        <span class="icon">🎥</span>
      </button>
      <button class="btn" @click="toggleTerrain" title="切换地形">
        <span class="icon">{{ terrainEnabled ? '🌋' : '🌍' }}</span>
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
const currentImagerySource = ref('cartodb') // 'cartodb' 或 'esri'
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
  (newVal) => {
    console.log('[CesiumViewer] sceneObjects prop changed, newVal:', newVal)
    console.log('[CesiumViewer] Viewer initialized:', !!viewer)

    if (!viewer) {
      console.warn('[CesiumViewer] Viewer not ready yet, will load objects when ready')
      return
    }

    loadSceneObjects(newVal || [])
  },
  { deep: true, immediate: false }
)

// ==================== 场景对象加载 ====================

const loadSceneObjects = async (objects: any[]) => {
  if (!viewer) {
    console.error('[CesiumViewer] Viewer not initialized yet!')
    return
  }

  // 清除之前加载的所有模型
  clearLoadedObjects()

  console.log(`[CesiumViewer] ========== Loading Scene Objects ==========`)
  console.log(`[CesiumViewer] Received ${objects?.length || 0} scene objects`)
  console.log(`[CesiumViewer] Objects data:`, JSON.stringify(objects, null, 2))

  // 如果没有场景对象，直接返回
  if (!objects || objects.length === 0) {
    console.warn('[CesiumViewer] No scene objects to load - viewer will show default earth')
    return
  }

  // API基础URL - 用于非MinIO资源
  const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000/api'
  const BASE_URL = API_BASE_URL.replace('/api', '')

  for (const obj of objects) {
    try {
      if (!obj.displayPath) {
        console.warn(`[CesiumViewer] Object ${obj.name} has no displayPath, skipping.`)
        continue
      }

      // 构建完整的URL路径
      let fullPath = obj.displayPath

      // 如果是后端代理路径（以 /api/ 开头），需要添加完整的API基础URL
      if (fullPath.startsWith('/api/')) {
        fullPath = `${BASE_URL}${fullPath}`
        console.log(`[CesiumViewer] Using backend proxy path for ${obj.name}: ${fullPath}`)
      }
      // 判断是否为MinIO存储的路径（相对路径且不包含协议）
      else if (!fullPath.startsWith('http://') && !fullPath.startsWith('https://')) {
        // 如果路径中包含MinIO已知的bucket名称，说明是MinIO路径
        const minioBuckets = ['models-3d', 'textures', 'thumbnails', 'videos']
        const pathParts = fullPath.split('/').filter((p: string) => p)
        const firstPart = pathParts[0]

        if (minioBuckets.includes(firstPart)) {
          // 格式：models-3d/object-name/file
          // 使用后端代理路径而不是直接访问MinIO
          fullPath = `${BASE_URL}/api/files/proxy/${fullPath}`
          console.log(`[CesiumViewer] Using backend proxy path for MinIO object: ${fullPath}`)
        } else {
          // 假设是MinIO对象名称（没有bucket前缀）
          // 默认使用models-3d bucket，通过后端代理访问
          fullPath = `${BASE_URL}/api/files/proxy/models-3d/${fullPath.replace(/^\//, '')}`
          console.log(`[CesiumViewer] Using backend proxy path for MinIO object (default bucket): ${fullPath}`)
        }
      }

      // 如果displayPath已经是完整的MinIO签名URL，直接使用
      // MinIO签名URL包含X-Amz参数
      if (fullPath.includes('X-Amz-Algorithm')) {
        console.log(`[CesiumViewer] Using presigned MinIO URL for ${obj.name}`)
      }

      // 检查文件扩展名
      const fileExt = fullPath.split('?')[0].split('.').pop()?.toLowerCase()

      // Cesium原生支持的格式
      const nativelySupportedFormats = ['gltf', 'glb', 'json'] // json for tileset.json

      // 需要转换的格式（通过切片服务转换为3D Tiles）
      const convertibleFormats = ['obj', 'fbx', 'dae', 'stl', '3ds', 'blend', 'ply', 'las', 'laz', 'e57']

      // 检查是否为不支持的格式
      if (!nativelySupportedFormats.includes(fileExt || '') && convertibleFormats.includes(fileExt || '')) {
        console.warn(`[CesiumViewer] ⚠ Format .${fileExt} requires conversion for Cesium display`)

        // 优先使用已完成的切片输出
        if (obj.slicingTaskId && obj.slicingTaskStatus === 'Completed' && obj.slicingOutputPath) {
          console.info(`[CesiumViewer] ✓ Using completed slicing output: ${obj.slicingOutputPath}`)
          // 使用切片输出路径
          fullPath = obj.slicingOutputPath

          // 构建切片代理URL
          if (!fullPath.startsWith('http://') && !fullPath.startsWith('https://')) {
            fullPath = `${BASE_URL}/api/files/proxy/${fullPath}`
          }

          // 确保路径指向tileset.json
          if (!fullPath.endsWith('tileset.json')) {
            if (fullPath.endsWith('/') || fullPath.endsWith('\\')) {
              fullPath = fullPath + 'tileset.json'
            } else {
              fullPath = fullPath + '/tileset.json'
            }
          }

          console.log(`[CesiumViewer] Using 3D Tiles path: ${fullPath}`)
        } else {
          // 如果没有完成的切片，显示提示信息
          const statusMsg = obj.slicingTaskId
            ? `Slicing task status: ${obj.slicingTaskStatus || 'Unknown'}`
            : 'No slicing task found'

          console.warn(`[CesiumViewer] Cannot display .${fileExt} file directly. ${statusMsg}`)
          console.info(`[CesiumViewer] Tip: Create a slicing task to convert this model to 3D Tiles format for viewing`)

          // 在地图上显示占位符标记，但不加载模型
          const position = obj.position || [116.397128, 39.908802, 100]
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
              text: `${obj.name}\n(${fileExt?.toUpperCase()} - 需要切片转换)`,
              font: '14px sans-serif',
              fillColor: Cesium.Color.WHITE,
              outlineColor: Cesium.Color.BLACK,
              outlineWidth: 2,
              style: Cesium.LabelStyle.FILL_AND_OUTLINE,
              verticalOrigin: Cesium.VerticalOrigin.BOTTOM,
              pixelOffset: new Cesium.Cartesian2(0, -20)
            }
          })

          // 飞向该位置
          viewer.camera.flyTo({
            destination: cartesian,
            orientation: {
              heading: Cesium.Math.toRadians(0),
              pitch: Cesium.Math.toRadians(-45),
              roll: 0.0
            },
            duration: 2.0
          })

          continue
        }
      } else if (!nativelySupportedFormats.includes(fileExt || '') && !convertibleFormats.includes(fileExt || '')) {
        console.error(`[CesiumViewer] ✗ Unsupported file format: .${fileExt}`)
        continue
      }

      console.log(`[CesiumViewer] Loading object ${obj.name} from ${fullPath}`)

      // 解析位置、旋转、缩放
      let position = obj.position || [0, 0, 0]
      const rotation = typeof obj.rotation === 'string' ? JSON.parse(obj.rotation) : obj.rotation || { x: 0, y: 0, z: 0 }
      const scale = typeof obj.scale === 'string' ? JSON.parse(obj.scale) : obj.scale || { x: 1, y: 1, z: 1 }

      // 检查位置是否有效，如果是[0,0,0]则自动使用默认位置（北京天安门广场）
      if (position[0] === 0 && position[1] === 0 && position[2] === 0) {
        const DEFAULT_POSITION = [116.397128, 39.908802, 100]  // 北京天安门广场
        console.warn(`[CesiumViewer] ⚠ Object ${obj.name} has invalid position [0,0,0]!`)
        console.warn(`[CesiumViewer] Auto-fixing: Using default position (Beijing Tiananmen Square): [${DEFAULT_POSITION.join(', ')}]`)
        console.warn(`[CesiumViewer] Tip: Update the object's position in the database to set a permanent custom location.`)
        position = DEFAULT_POSITION
      }

      // 输出位置信息，帮助调试
      console.log(`[CesiumViewer] Object ${obj.name} position:`, position)
      console.log(`[CesiumViewer]   - Longitude: ${position[0]}°`)
      console.log(`[CesiumViewer]   - Latitude: ${position[1]}°`)
      console.log(`[CesiumViewer]   - Height: ${position[2]}m`)
      console.log(`[CesiumViewer] Object ${obj.name} rotation:`, rotation)
      console.log(`[CesiumViewer] Object ${obj.name} scale:`, scale)

      // 创建模型矩阵（位置 + 旋转 + 缩放）
      const cartesian = Cesium.Cartesian3.fromDegrees(position[0], position[1], position[2])
      const heading = Cesium.Math.toRadians(rotation.y) // Y轴旋转对应偏航角
      const pitch = Cesium.Math.toRadians(rotation.x)   // X轴旋转对应俯仰角
      const roll = Cesium.Math.toRadians(rotation.z)    // Z轴旋转对应翻滚角
      const hpr = new Cesium.HeadingPitchRoll(heading, pitch, roll)
      const orientation = Cesium.Transforms.headingPitchRollQuaternion(cartesian, hpr)

      const modelMatrix = Cesium.Matrix4.fromTranslationQuaternionRotationScale(
        cartesian,
        orientation,
        new Cesium.Cartesian3(scale.x, scale.y, scale.z)
      )

      // 添加调试标记点（Entity）- 无论模型是否加载成功都会显示
      viewer.entities.add({
        position: cartesian,
        point: {
          pixelSize: 20,
          color: Cesium.Color.RED,
          outlineColor: Cesium.Color.WHITE,
          outlineWidth: 2
        },
        label: {
          text: obj.name,
          font: '14px sans-serif',
          fillColor: Cesium.Color.WHITE,
          outlineColor: Cesium.Color.BLACK,
          outlineWidth: 2,
          style: Cesium.LabelStyle.FILL_AND_OUTLINE,
          verticalOrigin: Cesium.VerticalOrigin.BOTTOM,
          pixelOffset: new Cesium.Cartesian2(0, -20)
        }
      })
      console.log(`[CesiumViewer] ✓ Added debug marker for ${obj.name}`)

      // 首先飞向标记点位置，确保用户能看到正确的位置
      console.log(`[CesiumViewer] Flying to position [${position.join(', ')}]...`)
      viewer.camera.flyTo({
        destination: cartesian,
        orientation: {
          heading: Cesium.Math.toRadians(0),
          pitch: Cesium.Math.toRadians(-45),
          roll: 0.0
        },
        duration: 2.0
      })

      // 根据文件类型选择加载方式
      const isGltfModel = ['gltf', 'glb'].includes(fileExt || '')
      const is3DTiles = fullPath.endsWith('tileset.json') || fullPath.includes('/tileset.json')

      if (is3DTiles) {
        // 加载3D Tiles (包括从其他格式切片转换的)
        console.log(`[CesiumViewer] Loading 3D Tileset from: ${fullPath}`)
        try {
          const tileset = await Cesium.Cesium3DTileset.fromUrl(fullPath)

          // 应用变换矩阵
          tileset.modelMatrix = modelMatrix

          // 添加到场景
          viewer.scene.primitives.add(tileset)
          loadedModels.set(obj.id, { type: '3dtiles', object: tileset, position: cartesian })

          console.log(`[CesiumViewer] ✓ Loaded 3D Tileset for ${obj.name} at position [${position.join(', ')}]`)

          // 飞向切片集
          console.log(`[CesiumViewer] Flying to tileset ${obj.name}...`)
          try {
            await viewer.flyTo(tileset, {
              duration: 2.0,
              offset: new Cesium.HeadingPitchRange(0, -0.5, tileset.boundingSphere.radius * 2.0)
            })
          } catch (flyError) {
            console.warn(`[CesiumViewer] Failed to fly to tileset, flying to position instead:`, flyError)
            // 如果flyTo失败，使用相机直接飞向位置
            viewer.camera.flyTo({
              destination: cartesian,
              orientation: {
                heading: Cesium.Math.toRadians(0),
                pitch: Cesium.Math.toRadians(-45),
                roll: 0.0
              },
              duration: 2.0
            })
          }
        } catch (tilesetError) {
          console.error(`[CesiumViewer] ✗ Failed to load 3D Tileset:`, tilesetError)
          console.error(`[CesiumViewer] Tileset URL was: ${fullPath}`)
          if (tilesetError instanceof Error) {
            console.error(`[CesiumViewer] Error message: ${tilesetError.message}`)
            console.error(`[CesiumViewer] Error stack:`, tilesetError.stack)
          }
        }
      } else if (isGltfModel) {
        // 加载glTF/GLB模型
        console.log(`[CesiumViewer] Loading glTF/GLB model from: ${fullPath}`)
        try {
          const model = await Cesium.Model.fromGltfAsync({
            url: fullPath,
            modelMatrix: modelMatrix,
            // 启用颜色混合和透明度
            colorBlendMode: Cesium.ColorBlendMode.MIX,
            // 增加最大纹理大小
            maximumScale: 20000
          })

          viewer.scene.primitives.add(model)
          loadedModels.set(obj.id, { type: 'model', object: model, position: cartesian })

          console.log(`[CesiumViewer] ✓ Loaded glTF/GLB model for ${obj.name} at position [${position.join(', ')}]`)

          // 飞向模型位置
          console.log(`[CesiumViewer] Flying to model ${obj.name}...`)
          viewer.camera.flyTo({
            destination: cartesian,
            orientation: {
              heading: Cesium.Math.toRadians(0),
              pitch: Cesium.Math.toRadians(-45),
              roll: 0.0
            },
            duration: 2.0
          })
        } catch (modelError) {
          console.error(`[CesiumViewer] ✗ Failed to load glTF/GLB model:`, modelError)
          console.error(`[CesiumViewer] Model URL was: ${fullPath}`)
          if (modelError instanceof Error) {
            console.error(`[CesiumViewer] Error message: ${modelError.message}`)
            console.error(`[CesiumViewer] Error stack:`, modelError.stack)
          }
        }
      } else {
        console.warn(`[CesiumViewer] Unknown format for ${obj.name}, skipping`)
        continue
      }
    } catch (error) {
      console.error(`[CesiumViewer] ✗ Failed to load object ${obj.name} (${obj.id}):`, error)
    }
  }

  console.log(`[CesiumViewer] Successfully loaded ${loadedModels.size} objects`)

  // 如果加载了模型，调整相机视图以包含所有模型
  if (loadedModels.size > 0) {
    console.log(`[CesiumViewer] Adjusting camera to show all loaded objects...`)

    // 如果只有一个模型，已经在上面飞向了
    if (loadedModels.size === 1) {
      console.log(`[CesiumViewer] Single object loaded, camera already adjusted`)
    } else {
      // 多个模型时，计算边界并调整视图
      try {
        // 收集所有模型的位置，计算中心点
        const positions: Cesium.Cartesian3[] = []
        loadedModels.forEach((item) => {
          positions.push(item.position)
        })

        // 计算边界球体
        const boundingSphere = Cesium.BoundingSphere.fromPoints(positions)

        // 飞向边界球体中心
        viewer.camera.flyToBoundingSphere(boundingSphere, {
          duration: 2.0,
          offset: new Cesium.HeadingPitchRange(0, -0.5, boundingSphere.radius * 3.0)
        })

        console.log(`[CesiumViewer] Camera adjusted to show all ${loadedModels.size} objects`)
      } catch (error) {
        console.warn(`[CesiumViewer] Failed to adjust camera for multiple objects:`, error)
      }
    }
  }
}

const clearLoadedObjects = () => {
  if (!viewer) return
  loadedModels.forEach((item) => {
    viewer?.scene.primitives.remove(item.object)
  })
  loadedModels.clear()
  // 清除所有Entity标记
  viewer.entities.removeAll()
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

     // 注意：不再移除默认影像图层，直接使用构造函数中设置的自定义影像提供者

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
     console.log('[CesiumViewer] Checking for initial scene objects...')
     console.log('[CesiumViewer] props.sceneObjects:', props.sceneObjects)
     console.log('[CesiumViewer] props.sceneObjects length:', props.sceneObjects?.length || 0)

     if (props.sceneObjects && props.sceneObjects.length > 0) {
       console.log('[CesiumViewer] Loading initial scene objects...')
       await loadSceneObjects(props.sceneObjects)
     } else {
       console.warn('[CesiumViewer] No initial scene objects to load')
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
      // 启用真实地形（需要Cesium Ion令牌）
      // 暂时使用椭球地形作为替代
      viewer.terrainProvider = new Cesium.EllipsoidTerrainProvider()
      console.log('地形已启用（椭球模式）- 如需真实地形请配置Cesium Ion令牌')
    } else {
      // 禁用地形（使用椭球地形提供者 - 平坦表面）
      viewer.terrainProvider = new Cesium.EllipsoidTerrainProvider()
      console.log('地形已禁用（平面模式）')
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

    // 移除所有现有图层
    layers.removeAll()

    // 在CartoDB和ESRI之间切换
    let newProvider

    if (currentImagerySource.value === 'cartodb') {
      // 切换到ESRI World Imagery (卫星影像)
      newProvider = new Cesium.UrlTemplateImageryProvider({
        url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
        credit: 'Tiles © Esri — Source: Esri, i-cubed, USDA, USGS, AEX, GeoEye, Getmapping, Aerogrid, IGN, IGP, UPR-EGP, and the GIS User Community'
      })
      currentImagerySource.value = 'esri'
      console.log('切换到ESRI卫星影像')
    } else {
      // 切换回CartoDB Voyager
      newProvider = new Cesium.UrlTemplateImageryProvider({
        url: 'https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png',
        subdomains: ['a', 'b', 'c', 'd'],
        credit: 'Map tiles by CartoDB'
      })
      currentImagerySource.value = 'cartodb'
      console.log('切换到CartoDB地图')
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
