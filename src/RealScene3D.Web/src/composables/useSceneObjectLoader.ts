/**
 * 场景对象加载器组合式函数
 *
 * 从 legacy components/Mars3DViewer.vue 提取的纯加载逻辑,与具体 viewer 容器解耦。
 * 用于配合 components/mars-work/main-view.vue 在 mapLoaded 回调里加载 3D Tiles / GLTF / OSGB 等场景对象。
 *
 * 用法:
 *   const loader = useSceneObjectLoader()
 *   const onMars3DReady = (map) => {
 *     loader.setMap(map)
 *     loader.loadSceneObjects([sceneObject])
 *   }
 *   // 卸载: onUnmounted(() => loader.dispose())
 */
import { ref } from 'vue'
import * as mars3d from 'mars3d'
import * as Cesium from 'mars3d-cesium'
import authStore from '@/stores/auth'
import fileHandleStore from '@/services/fileHandleStore'
import { getSceneConfig, type SceneConfig } from '@/config/scene-config'

interface TransformParams {
  x: number
  y: number
  z: number
}

export interface SceneObjectInput {
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
  isVisible?: boolean
}

interface LoadedModel {
  type: '3dtiles' | 'model'
  object: any
  position: any
}

let _sceneConfig: SceneConfig | null = null
const cfg = (): SceneConfig => {
  if (!_sceneConfig) _sceneConfig = getSceneConfig()
  return _sceneConfig
}

const apiBaseUrl = (): string => {
  const baseUrl = import.meta.env.VITE_API_URL || '/api'
  return baseUrl.replace('/api', '')
}

const getFileExtension = (filePath: string): string => {
  if (!filePath || typeof filePath !== 'string') return ''
  const pathWithoutQuery = filePath.split('?')[0]
  const parts = pathWithoutQuery.split(/[\\\/]/)
  const lastPart = parts[parts.length - 1]
  const lastDotIndex = lastPart.lastIndexOf('.')
  if (lastDotIndex <= 0) return ''
  return lastPart.substring(lastDotIndex + 1).toLowerCase()
}

const isAbsoluteUrl = (url: string): boolean => {
  if (!url || typeof url !== 'string') return false
  return url.startsWith('http://') || url.startsWith('https://')
}

const parseTransformParams = (rotation: SceneObjectInput['rotation'], scale: SceneObjectInput['scale']) => {
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

const validateSceneObject = (obj: SceneObjectInput): { isValid: boolean; error?: string } => {
  if (!obj) return { isValid: false, error: '对象为空' }
  if (!obj.id || typeof obj.id !== 'string') return { isValid: false, error: '对象ID无效' }
  if (!obj.name || typeof obj.name !== 'string') return { isValid: false, error: '对象名称无效' }
  if (!obj.displayPath || typeof obj.displayPath !== 'string') return { isValid: false, error: '显示路径无效' }
  if (!Array.isArray(obj.position) || obj.position.length !== 3) return { isValid: false, error: '位置信息无效' }
  return { isValid: true }
}

const normalizeObjectPosition = (obj: SceneObjectInput): [number, number, number] => {
  const position = obj.position
  if (obj.slicingTaskId && obj.slicingTaskStatus === 'Completed' && obj.slicingOutputPath) return position
  if (position.every((coord) => coord === 0)) {
    return [cfg().mars3d.defaultPosition.longitude, cfg().mars3d.defaultPosition.latitude, cfg().mars3d.defaultPosition.height]
  }
  const [longitude, latitude, height] = position
  if (longitude < -180 || longitude > 180 || latitude < -90 || latitude > 90 || height < -1000) {
    return [cfg().mars3d.defaultPosition.longitude, cfg().mars3d.defaultPosition.latitude, cfg().mars3d.defaultPosition.height]
  }
  return position
}

const determineLoadStrategy = (fullPath: string, fileExt: string): 'tileset' | 'gltf' | 'threejs' | 'convertible' | null => {
  if (fullPath.toLowerCase().endsWith('tileset.json') || fullPath.toLowerCase().includes('/tileset.json')) return 'tileset'
  if (fileExt) {
    if (cfg().formats.convertible.includes(fileExt)) return 'convertible'
    if (cfg().formats.threejsSupported.includes(fileExt)) return 'threejs'
    if (['gltf', 'glb'].includes(fileExt)) return 'gltf'
    if (fileExt === 'json') return 'tileset'
    if (!cfg().formats.nativelySupported.includes(fileExt)) return null
  }
  if (!fileExt) {
    const lowerPath = fullPath.toLowerCase()
    if (lowerPath.includes('obj') || lowerPath.includes('fbx') || lowerPath.includes('dae') ||
        lowerPath.includes('las') || lowerPath.includes('laz') || lowerPath.includes('e57')) {
      return 'convertible'
    }
    return 'tileset'
  }
  return null
}

const resolveObjectUrl = async (displayPath: string): Promise<{ url: string; originalFileName?: string }> => {
  let fullPath = displayPath
  const base = apiBaseUrl()

  if (fullPath.includes('/api/files/local/')) {
    const encodedPathMatch = fullPath.match(/\/api\/files\/local\/(.+)$/)
    if (encodedPathMatch) {
      const encodedPath = encodedPathMatch[1]
      const decodedPath = decodeURIComponent(encodedPath)
      const normalizedPath = decodedPath.replace(/\\/g, '/')
      const driveMatch = normalizedPath.match(/^([A-Za-z]:)(.*)/)
      if (driveMatch) {
        const drive = driveMatch[1].replace(':', '%3A')
        const rest = driveMatch[2]
        const encodedRest = rest.replace(/\s/g, '%20').replace(/#/g, '%23').replace(/\?/g, '%3F')
        fullPath = `${base}/api/files/local/${drive}${encodedRest}`
      } else {
        fullPath = `${base}/api/files/local/${normalizedPath.replace(/:/g, '%3A')}`
      }
      return { url: fullPath }
    }
  }

  const isWindowsPath = /^[A-Za-z]:[\\/]/.test(fullPath)
  if (isWindowsPath) {
    const normalizedPath = fullPath.replace(/\\/g, '/')
    const driveMatch = normalizedPath.match(/^([A-Za-z]:)(.*)/)
    if (driveMatch) {
      const drive = driveMatch[1].replace(':', '%3A')
      const rest = driveMatch[2]
      const encodedRest = rest.replace(/\s/g, '%20').replace(/#/g, '%23').replace(/\?/g, '%3F')
      fullPath = `${base}/api/files/local/${drive}${encodedRest}`
    } else {
      fullPath = `${base}/api/files/local/${normalizedPath.replace(/:/g, '%3A')}`
    }
    return { url: fullPath }
  }

  if (fullPath.startsWith('/api/')) {
    fullPath = `${base}${fullPath}`
  } else if (!isAbsoluteUrl(fullPath)) {
    const pathParts = fullPath.split('/').filter((p: string) => p)
    const firstPart = pathParts[0]
    if (cfg().minio.buckets.includes(firstPart)) {
      fullPath = `${base}/api/files/proxy/${fullPath.replace(/^\//, '')}`
    } else {
      fullPath = `${base}/api/files/proxy/slices/${fullPath.replace(/^\//, '')}`
    }
  }

  return { url: fullPath }
}

export function useSceneObjectLoader() {
  let map: mars3d.Map | null = null
  let viewer: any = null
  let graphicLayer: mars3d.layer.GraphicLayer | null = null
  const loadedModels = new Map<string, LoadedModel>()
  const loadedCount = ref(0)
  const tilesCount = ref(0)

  const setMap = (m: mars3d.Map): void => {
    map = m
    viewer = m.viewer
    graphicLayer = new mars3d.layer.GraphicLayer()
    m.addLayer(graphicLayer)
  }

  const handleConvertibleFormat = async (obj: SceneObjectInput): Promise<boolean> => {
    let fileExt = getFileExtension(obj.displayPath)
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
      const position = obj.position.every((coord) => coord === 0)
        ? [cfg().mars3d.defaultPosition.longitude, cfg().mars3d.defaultPosition.latitude, cfg().mars3d.defaultPosition.height]
        : obj.position

      if (!map || !graphicLayer) return false

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
      return false
    }
  }

  const loadTileset = async (obj: SceneObjectInput, url: string): Promise<boolean> => {
    if (!map) return false
    try {
      const token = authStore.token.value
      const isSignedUrl = url.includes('X-Amz-Signature')

      let resource: any = url
      if (url.includes('/api/files/local/')) {
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

      const tilesetCfg = cfg().mars3d.tileset
      const layerConfig: any = {
        url: resource,
        maximumScreenSpaceError: tilesetCfg.maximumScreenSpaceError,
        maximumMemoryUsage: tilesetCfg.maximumMemoryUsage,
        show: true
      }

      const { parsedRotation, parsedScale } = parseTransformParams(obj.rotation, obj.scale)
      const isLocalCoordinates = obj.position.every((coord) => coord === 0)
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

      const tilesetLayer = new mars3d.layer.TilesetLayer(layerConfig)
      map.addLayer(tilesetLayer)

      const tileset = tilesetLayer.tileset
      if (!tileset) {
        await new Promise<void>((resolve, reject) => {
          const timeout = setTimeout(() => reject(new Error('Tileset加载超时')), cfg().performance.tilesLoadTimeout)
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
      let attempts = 0
      const maxAttempts = 20
      while (attempts < maxAttempts && (!actualTileset.boundingSphere || actualTileset.boundingSphere.radius === 0)) {
        await new Promise((resolve) => setTimeout(resolve, 100))
        attempts++
      }

      let center: any
      if (isLocalCoordinates && actualTileset.boundingSphere && actualTileset.boundingSphere.radius > 0) {
        center = actualTileset.boundingSphere.center
      } else {
        center = Cesium.Cartesian3.fromDegrees(obj.position[0], obj.position[1], obj.position[2])
      }

      loadedModels.set(obj.id, { type: '3dtiles', object: tilesetLayer, position: center })
      if (obj.isVisible === false) tilesetLayer.show = false

      loadedCount.value = loadedModels.size
      tilesCount.value = Array.from(loadedModels.values()).filter((item) => item.type === '3dtiles').length
      return true
    } catch (error) {
      console.error('[useSceneObjectLoader.loadTileset] 错误:', error)
      return false
    }
  }

  const loadGltfModel = async (obj: SceneObjectInput, url: string): Promise<boolean> => {
    if (!map || !viewer) return false
    try {
      const { parsedRotation, parsedScale } = parseTransformParams(obj.rotation, obj.scale)
      const token = authStore.token.value
      const isSignedUrl = url.includes('X-Amz-Signature')

      const resource = (token && !isSignedUrl)
        ? new Cesium.Resource({ url, headers: { Authorization: `Bearer ${token}` } })
        : url

      const position = obj.position.every((coord) => coord === 0)
        ? [cfg().mars3d.defaultPosition.longitude, cfg().mars3d.defaultPosition.latitude, cfg().mars3d.defaultPosition.height]
        : obj.position

      const cartesian = Cesium.Cartesian3.fromDegrees(position[0], position[1], position[2])
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
        url: resource as any,
        modelMatrix,
        colorBlendMode: Cesium.ColorBlendMode.MIX,
        maximumScale: Number.MAX_VALUE,
        minimumPixelSize: 128,
        allowPicking: true,
        show: true,
        scene: viewer.scene
      })

      viewer.scene.primitives.add(model)

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
          }, cfg().performance.modelLoadTimeout)
        }
      })

      model.show = obj.isVisible !== false
      loadedModels.set(obj.id, { type: 'model', object: model, position: cartesian })
      loadedCount.value = loadedModels.size
      return true
    } catch (error) {
      console.error('[useSceneObjectLoader.loadGltfModel] 错误:', error)
      return false
    }
  }

  const loadThreeJSModel = async (obj: SceneObjectInput, _url: string): Promise<boolean> => {
    return await handleConvertibleFormat(obj)
  }

  const loadSceneObject = async (obj: SceneObjectInput): Promise<void> => {
    try {
      if (obj.isVisible === false) return

      const validation = validateSceneObject(obj)
      if (!validation.isValid) {
        console.error(`[useSceneObjectLoader] 对象验证失败: ${validation.error}`)
        return
      }

      obj.position = normalizeObjectPosition(obj)

      if (obj.slicingTaskId && obj.slicingTaskStatus === 'Completed' && obj.slicingOutputPath) {
        let tilesetPath = obj.slicingOutputPath
        if (!tilesetPath.endsWith('tileset.json')) {
          tilesetPath = tilesetPath.replace(/\/$|\\$/, '') + '/tileset.json'
        }
        const fullTilesetPath = await resolveObjectUrl(tilesetPath)
        await loadTileset(obj, fullTilesetPath.url)
        return
      }

      const modelPath = obj.modelPath || obj.displayPath
      if (modelPath && modelPath.startsWith('local-file-handle://')) {
        try {
          const uuid = modelPath.replace('local-file-handle://', '')
          const handle = await fileHandleStore.getHandle<any>(uuid)
          if (handle) {
            let permission = await handle.queryPermission({ mode: 'read' })
            if (permission !== 'granted') {
              permission = await handle.requestPermission({ mode: 'read' })
            }
            if (permission === 'granted') {
              const file = await handle.getFile()
              const blobUrl = URL.createObjectURL(file)
              const fileName = file.name
              const fileExt = getFileExtension(fileName)
              const strategy = determineLoadStrategy(blobUrl, fileExt)
              switch (strategy) {
                case 'convertible': await handleConvertibleFormat(obj); break
                case 'tileset': await loadTileset(obj, blobUrl); break
                case 'gltf': await loadGltfModel(obj, blobUrl); break
                case 'threejs': await loadThreeJSModel(obj, blobUrl); break
                default: console.error(`不支持的文件格式: .${fileExt}`)
              }
              return
            }
          }
          throw new Error('无法访问文件句柄')
        } catch (err) {
          console.error('[useSceneObjectLoader] 文件句柄处理失败:', err)
          return
        }
      }

      const resolved = await resolveObjectUrl(obj.displayPath)
      let fullPath = resolved.url
      let fileExt = resolved.originalFileName
        ? getFileExtension(resolved.originalFileName)
        : getFileExtension(fullPath)

      if (!fileExt && !fullPath.toLowerCase().includes('tileset.json')) {
        const strategy = determineLoadStrategy(fullPath, fileExt)
        if (strategy === 'tileset') {
          fullPath = fullPath.replace(/\/?$/, '/tileset.json')
          fileExt = 'json'
        }
      }

      const strategy = determineLoadStrategy(fullPath, fileExt)
      switch (strategy) {
        case 'convertible': await handleConvertibleFormat(obj); break
        case 'tileset': await loadTileset(obj, fullPath); break
        case 'gltf': await loadGltfModel(obj, fullPath); break
        case 'threejs': await loadThreeJSModel(obj, fullPath); break
        default: console.error(`不支持的文件格式: .${fileExt}`)
      }
    } catch (error) {
      console.error('[useSceneObjectLoader.loadSceneObject] 错误:', error)
    }
  }

  const flyToAllObjects = async (): Promise<void> => {
    if (!map) return
    if (loadedModels.size === 0) {
      map.setCameraView({
        lng: cfg().mars3d.defaultPosition.longitude,
        lat: cfg().mars3d.defaultPosition.latitude,
        alt: 10000,
        heading: cfg().mars3d.cameraFlight.heading,
        pitch: cfg().mars3d.cameraFlight.pitch
      }, { duration: cfg().mars3d.cameraFlight.duration })
      return
    }
    try {
      if (loadedModels.size === 1) {
        const modelInfo = Array.from(loadedModels.values())[0]
        if (modelInfo.type === '3dtiles') {
          const layer = modelInfo.object as mars3d.layer.TilesetLayer
          const tileset = layer.tileset
          if (tileset && tileset.boundingSphere && tileset.boundingSphere.radius > 0) {
            const radius = tileset.boundingSphere.radius
            const distance = Math.max(radius * 3.0, cfg().performance.minDistance)
            layer.flyTo({ duration: cfg().mars3d.cameraFlight.duration, radius: distance })
          } else {
            layer.flyTo({ duration: cfg().mars3d.cameraFlight.duration })
          }
        } else {
          const model = modelInfo.object as Cesium.Model
          if (model.boundingSphere && model.boundingSphere.radius > 0) {
            viewer.camera.flyToBoundingSphere(model.boundingSphere, {
              duration: cfg().mars3d.cameraFlight.duration,
              offset: new Cesium.HeadingPitchRange(0, -0.5, Math.max(model.boundingSphere.radius * 3.0, 500))
            })
          } else {
            const cartographic = Cesium.Cartographic.fromCartesian(modelInfo.position)
            if (cartographic) {
              map.setCameraView({
                lng: Cesium.Math.toDegrees(cartographic.longitude),
                lat: Cesium.Math.toDegrees(cartographic.latitude),
                alt: cartographic.height + 1000,
                heading: cfg().mars3d.cameraFlight.heading,
                pitch: cfg().mars3d.cameraFlight.pitch
              }, { duration: cfg().mars3d.cameraFlight.duration })
            }
          }
        }
      } else {
        const positions = Array.from(loadedModels.values()).map((item) => item.position)
        const boundingSphere = Cesium.BoundingSphere.fromPoints(positions)
        if (boundingSphere && boundingSphere.radius > 0) {
          const center = boundingSphere.center
          const cartographic = Cesium.Cartographic.fromCartesian(center)
          if (cartographic) {
            map.setCameraView({
              lng: Cesium.Math.toDegrees(cartographic.longitude),
              lat: Cesium.Math.toDegrees(cartographic.latitude),
              alt: cartographic.height + boundingSphere.radius * 3.0,
              heading: cfg().mars3d.cameraFlight.heading,
              pitch: cfg().mars3d.cameraFlight.pitch
            }, { duration: cfg().mars3d.cameraFlight.duration })
          }
        }
      }
    } catch (error) {
      console.warn('[useSceneObjectLoader.flyToAllObjects] 飞向对象失败:', error)
    }
  }

  const loadSceneObjects = async (objects: SceneObjectInput[]): Promise<void> => {
    if (!map) {
      console.warn('[useSceneObjectLoader] map 未设置,跳过加载')
      return
    }
    clearLoadedObjects()
    if (!objects || objects.length === 0) return
    const validObjects = objects.filter((obj) => validateSceneObject(obj).isValid)
    if (validObjects.length === 0) return

    const batchSize = cfg().performance.batchSize
    const batches = []
    for (let i = 0; i < validObjects.length; i += batchSize) {
      batches.push(validObjects.slice(i, i + batchSize))
    }
    for (const batch of batches) {
      try {
        await Promise.all(batch.map(loadSceneObject))
        if (batches.length > 1) await new Promise((resolve) => setTimeout(resolve, 50))
      } catch (error) {
        console.error('[useSceneObjectLoader.loadSceneObjects] 批次加载失败:', error)
      }
    }
    await flyToAllObjects()
  }

  const setObjectVisibility = (objectId: string, visible: boolean): void => {
    const item = loadedModels.get(objectId)
    if (!item) return
    if (item.type === '3dtiles') {
      (item.object as mars3d.layer.TilesetLayer).show = visible
    } else {
      (item.object as Cesium.Model).show = visible
    }
    if (viewer) viewer.scene.requestRender()
  }

  function clearLoadedObjects(): void {
    if (!map) return
    loadedModels.forEach((item) => {
      try {
        if (!item || !item.object) return
        if (item.type === '3dtiles') {
          const layer = item.object as mars3d.layer.TilesetLayer
          if ((layer as any).isDestroy) return
          map!.removeLayer(layer, true)
        } else {
          if (viewer && !viewer.isDestroyed?.()) {
            viewer.scene.primitives.remove(item.object as Cesium.Model)
          }
        }
      } catch (e) {
        console.warn('[useSceneObjectLoader.clearLoadedObjects] 移除图层失败:', e)
      }
    })
    loadedModels.clear()
    loadedCount.value = 0
    tilesCount.value = 0
  }

  const dispose = (): void => {
    clearLoadedObjects()
    if (graphicLayer && map) {
      try {
        map.removeLayer(graphicLayer, true)
      } catch (e) {
        console.warn('[useSceneObjectLoader.dispose] 移除 graphicLayer 失败:', e)
      }
      graphicLayer = null
    }
    map = null
    viewer = null
  }

  return {
    setMap,
    loadSceneObjects,
    loadSceneObject,
    clearLoadedObjects,
    setObjectVisibility,
    dispose,
    loadedCount,
    tilesCount
  }
}
