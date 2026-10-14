import * as mars3d from "mars3d"
import { map, zxxzGraphicLayer } from "@mars/widgets/analysis/map"
const Cesium = mars3d.Cesium
// 事件对象，用于抛出事件给 Vue 组件
export const eventTarget = new mars3d.BaseClass()

// 已加载的在线选址模型管理 { graphicId: modelObject }
// primitive model → 存 graphic 实例; tileset → 存 tilesetLayer 实例
const loadedModels = new Map<string, any>()
// 全局已加载模型 URL 记录（同一模型场景中只允许添加一次）
const loadedModelUrls = new Map<string, any>()

/** 根据记录中的数据获取对应的模型对象 */
export const getZxxzModel = (recordData: any): any => {
  const innerData = recordData?.data || recordData
  if (!innerData) {
    return null
  }
  // primitive model: 通过 graphic.id 查找
  if (innerData.id) {
    return loadedModels.get(innerData.id) || zxxzGraphicLayer.getGraphicById(innerData.id)
  }
  // 3D tiles: 通过 layerId 查找
  if (innerData.layerId) {
    return loadedModels.get(String(innerData.layerId)) || map.getLayerById(innerData.layerId)
  }
  return null
}

/** 切换在线选址模型显示/隐藏 */
export const toggleZxxzModelVisibility = (recordData: any, visible: boolean) => {
  const model = getZxxzModel(recordData)
  if (model) {
    model.show = visible
  }
}

/** 删除在线选址模型 */
export const removeZxxzModel = (recordData: any) => {
  const innerData = recordData?.data || recordData
  if (!innerData) { return }

  // primitive model: 从 zxxzGraphicLayer 中移除
  if (innerData.id) {
    const graphic = zxxzGraphicLayer.getGraphicById(innerData.id)
    if (graphic) {
      zxxzGraphicLayer.removeGraphic(graphic)
      loadedModels.delete(innerData.id)
    }
    // 清理去重记录
    for (const [url, info] of loadedModelUrls.entries()) {
      if (info.graphicId === innerData.id) { loadedModelUrls.delete(url); break }
    }
    return
  }

  // 3D tiles: 从 map 中移除图层
  if (innerData.layerId) {
    const layer = map.getLayerById(innerData.layerId)
    if (layer) {
      layer.remove()
      loadedModels.delete(String(innerData.layerId))
    }
    // 清理去重记录
    for (const [url, info] of loadedModelUrls.entries()) {
      if (info.graphicId === String(innerData.layerId)) { loadedModelUrls.delete(url); break }
    }
  }
}

export const startDraw = async (height: string) => {
  const options: any = {
    type: "polygon",
    style: {
      color: "rgba(0, 255, 0, 1)",
      opacity: 1,
      diffHeight: parseFloat(height) || 0
    },
    success: (graphic: any) => {
      const cameraView = map.getCameraView()
      const shijiao = {
        lng: cameraView.lng,
        lat: cameraView.lat,
        alt: cameraView.alt,
        headingangle: cameraView.heading,
        pitchangle: cameraView.pitch
      }
      // 延迟触发属性面板打开，确保图形绘制状态已结束
      setTimeout(() => {
        if (graphic && !graphic.isDrawing) {
          eventTarget.fire("drawCreatedForPanel", { layerId: map.graphicLayer.id, graphicId: graphic.id })
        }
      }, 600)
      eventTarget.fire("drawEnd", {
        hztype: "sdhz",
        analysisType: "zxxz_online",
        data: {
          data: graphic.toJSON(),
          name: "在线选址",
          analysisType: "zxxz_online",
          shijiao
        },
        shijiao
      })
    }
  }
  await map.graphicLayer.startDraw(options)
}

export const updateModelHeight = (height) => {
  const newHeight = parseFloat(height) || 0

  map.graphicLayer.eachGraphic((graphic) => {
    if (graphic.style && "diffHeight" in graphic.style) {
      graphic.style.diffHeight = newHeight
      if (graphic.style.clampToGround) {
        graphic.style.clampToGround = false
      }
      graphic.reload()
      const cameraView = map.getCameraView()
      const shijiao = {
        lng: cameraView.lng,
        lat: cameraView.lat,
        alt: cameraView.alt,
        headingangle: cameraView.heading,
        pitchangle: cameraView.pitch
      }
      eventTarget.fire("drawEnd",
        {
          hztype: "sdhz",
          analysisType: "zxxz_online",
          data: {
            data: graphic.toJSON(),
            name: "在线选址",
            analysisType: "zxxz_online",
            shijiao
          },
          shijiao
        })
    }
  })
}


/** 根据url获取已加载的模型对象 */
const getModelByUrl = (url: string): any => {
  if (!loadedModelUrls.has(url)) { return null }
  const info = loadedModelUrls.get(url)
  if (!info?.graphicId) { return null }
  // primitive model
  let model = loadedModels.get(info.graphicId)
  if (model) { return model }
  // tileset layer
  model = map.getLayerById(info.graphicId)
  return model || null
}

/** 将已加载模型移动到目标位置并同步更新store */
const moveModelToPosition = async (model: any, url: string, targetPosition: any) => {
  // 区分 primitive model 和 3D tiles
  if (model.position !== undefined && model.setPosition) {
    // primitive model (gltf/glb)
    model.position = targetPosition
    model.reload?.()
    // 更新去重记录中的position
    loadedModelUrls.set(url, {
      ...loadedModelUrls.get(url),
      position: targetPosition,
      graphicId: model.id
    })
    // 触发store更新
    eventTarget.fire("zxxzGraphicEditorUpdate", { graphic: model })
  } else {
    // 3D tiles tilesetLayer
    if (model.setCenter) {
      const carto = Cesium.Cartographic.fromCartesian(targetPosition as any)
      model.setCenter({
        lng: Cesium.Math.toDegrees(carto.longitude),
        lat: Cesium.Math.toDegrees(carto.latitude),
        alt: carto.height
      })
    } else if (model.setPosition) {
      model.setPosition(targetPosition)
    } else {
      model.position = targetPosition
    }
    // 更新去重记录中的position
    loadedModelUrls.set(url, {
      ...loadedModelUrls.get(url),
      position: targetPosition,
      graphicId: String(model.id)
    })
    eventTarget.fire("zxxzTilesetUpdate", { layer: model })
  }
}

/** 检查模型是否已加载，返回模型对象或null */
const checkModelLoaded = (url: string): any | null => {
  return getModelByUrl(url)
}

export const startDrawModel = async(styleOptions: any) => {
  if (!zxxzGraphicLayer) {
    console.warn("zxxzGraphicLayer 未初始化")
    return
  }

  const existingModel = checkModelLoaded(styleOptions.url)
  if (existingModel) {
    // 模型已存在 → 进入重新拾取模式，拾取后移动位置
    const newPosition = await pickPosition()
    if (newPosition) {
      await moveModelToPosition(existingModel, styleOptions.url, newPosition)
      map.flyTo({ destination: newPosition, duration: 1.5 })
    }
    return
  }

  const is3DTiles = (styleOptions.url || "").endsWith(".json") || (styleOptions.url || "").includes("tileset")

  if (is3DTiles) {
    // 3D Tiles 模型：使用 TilesetLayer 加载后绑定到绘制位置
    await load3DTilesModel(styleOptions)
  } else {
    // 单体模型(gltf/glb)：使用 GraphicLayer 绘制
    await loadPrimitiveModel(styleOptions)
  }
}

/** 加载单体模型 */
const loadPrimitiveModel = async(styleOptions: any) => {
  const style = mars3d.Util.clone(styleOptions)
  const graphic = await zxxzGraphicLayer.startDraw({
    type: "model",
    drawShow: true,
    style
  })

  // 注册到模型管理器
  loadedModels.set(graphic.id, graphic)
  // 全局去重记录
  loadedModelUrls.set(styleOptions.url, { graphicId: graphic.id, position: graphic.position })

  // 监听单体模型(gltf/glb)的所有编辑相关事件，确保属性修改能同步回 store
  const eventNames = [
    "editStart", "editStop", "editStyle",
    "editMovePoint", "editRemovePoint", "editAddPoint",
    "positionChanged", "changed"
  ]
  eventNames.forEach((name) => {
    graphic.on(name as any, (e: any) => {
      console.log(`[zxxz-model] 事件触发: ${name}`, e, graphic.toJSON())
      eventTarget.fire("zxxzGraphicEditorUpdate", { graphic })
    })
  })

  const cameraView = map.getCameraView()
  const shijiao = {
    lng: cameraView.lng,
    lat: cameraView.lat,
    alt: cameraView.alt,
    headingangle: cameraView.heading,
    pitchangle: cameraView.pitch
  }
  eventTarget.fire("drawEnd", {
    hztype: "mox",
    analysisType: "zxxz_online",
    data: {
      data: graphic.toJSON(),
      name: "在线选址",
      analysisType: "zxxz_online",
      shijiao
    },
    shijiao
  })
}

/** 加载3D Tiles模型 */
const load3DTilesModel = async(styleOptions: any) => {
  const position = await pickPosition()
  if (!position) { return }

  console.log("开始加载3D Tiles, url:", styleOptions.url)

  // Cartesian3 → 经纬度
  const carto = Cesium.Cartographic.fromCartesian(position as any)
  const pos = {
    lng: Cesium.Math.toDegrees(carto.longitude),
    lat: Cesium.Math.toDegrees(carto.latitude),
    alt: carto.height
  }

  const tilesetLayer = new mars3d.layer.TilesetLayer({
    name: "在线选址",
    url: styleOptions.url,
    position: pos,
    maximumScreenSpaceError: 1,
    maximumMemoryUsage: 1024,
    hasEdit: true,
    hasEditContextMenu: true
  })

  map.addLayer(tilesetLayer)

  loadedModels.set(String(tilesetLayer.id), tilesetLayer)
  loadedModelUrls.set(styleOptions.url, { graphicId: String(tilesetLayer.id), position: Cesium.Cartesian3.fromDegrees(pos.lng, pos.lat, pos.alt) })

  tilesetLayer.readyPromise
    .then(() => {
      console.log("模型加载完成", tilesetLayer)

      tilesetLayer.on(mars3d.EventType.updatePosition, () => {
        eventTarget.fire("zxxzTilesetUpdate", { layer: tilesetLayer })
      })

      tilesetLayer.bindContextMenu([
        {
          text: "开始编辑",
          icon: "fa fa-edit",
          show: () => tilesetLayer.hasEdit && !tilesetLayer.isEditing,
          callback: () => {
            tilesetLayer.startEditing()
          }
        },
        {
          text: "停止编辑",
          icon: "fa fa-edit",
          show: () => tilesetLayer.isEditing,
          callback: () => {
            tilesetLayer.stopEditing()
            eventTarget.fire("zxxzTilesetUpdate", { layer: tilesetLayer })
          }
        }
      ])

      const json = tilesetLayer.toJSON({ full: true })
      eventTarget.fire("tiles3dLayerLoad", { layer: json })
    })
    .catch((e) => {
      console.log("模型加载失败", e)
    })


  console.log("3D Tiles 已添加, position:", pos)

  const cameraView = map.getCameraView()
  const shijiao = {
    lng: cameraView.lng,
    lat: cameraView.lat,
    alt: cameraView.alt,
    headingangle: cameraView.heading,
    pitchangle: cameraView.pitch
  }
  eventTarget.fire("drawEnd", {
    analysisType: "zxxz_online",
    data: {
      data: { position: pos, url: styleOptions.url, layerId: tilesetLayer.id },
      name: "在线选址",
      analysisType: "zxxz_online",
      shijiao
    },
    shijiao
  })
}

/** 拾取地图点位 */
const pickPosition = () => {
  return new Promise((resolve) => {
    map.graphicLayer.startDraw({
      type: "point",
      success: (_graphic: any) => {
        const pos = _graphic.positionShow || _graphic.position
        // 清除用于拾取位置的临时点
        map.graphicLayer.removeGraphic(_graphic)
        // 如果是 CallbackProperty，尝试获取实际值
        if (pos && typeof pos.getValue === "function") {
          const val = pos.getValue(Cesium.JulianDate.now())
          console.log("拾取点位:", val)
          resolve(val)
          return
        }
        console.log("拾取点位:", pos)
        resolve(pos)
      },
      cancel: () => {
        resolve(undefined)
      }
    })
  })
}


export const removeModel = () => {
  // 清空 zxxzGraphicLayer 中的所有 primitive model（gltf/glb）
  if (zxxzGraphicLayer) {
    zxxzGraphicLayer.clear()
  }

  // 移除 loadedModels 中的所有 3D tiles 模型
  loadedModels.forEach((model) => {
    try {
      if (model.remove) {
        model.remove() // tilesetLayer.remove()
      } else if (map && map.getLayerById(model.id)) {
        map.removeLayer(model)
      }
    } catch (e) {
      console.warn("移除模型失败:", e)
    }
  })
  loadedModels.clear()
  loadedModelUrls.clear()

  console.log("[zxxz] 已移除所有在线选址模型")
}

/** 取消当前正在进行的模型绘制/加载 */
export const cancelCurrentDraw = () => {
  if (zxxzGraphicLayer) {
    zxxzGraphicLayer.clearDrawing()
  }
  if (map.graphicLayer) {
    map.graphicLayer.clearDrawing()
  }
}

/** 根据默认视角参数定位加载/移动模型并 flyTo */
export const loadModelWithDefaultView = async (options: {
  url: string
  latitude: number
  longitude: number
  elevation?: number
  pitchangle?: number
  direction?: number
  sightheight?: number
}) => {
  if (!zxxzGraphicLayer) {
    console.warn("zxxzGraphicLayer 未初始化")
    return
  }

  const { url, latitude, longitude, elevation = 0, pitchangle = -45, direction = 0, sightheight } = options
  const targetPos = Cesium.Cartesian3.fromDegrees(longitude, latitude, elevation)

  cancelCurrentDraw()

  const existingModel = checkModelLoaded(url)
  if (existingModel) {
    // 模型已存在 → 移动到默认视角位置 + flyTo
    await moveModelToPosition(existingModel, url, targetPos)
    map.flyToPoint([longitude, latitude, elevation], {
      heading: sightheight,
      pitch: pitchangle,
      roll: 0,
      radius: 500
    }, 2000)
    return
  }

  // 模型不存在 → 新加载
  const is3DTiles = (url || "").endsWith(".json") || (url || "").includes("tileset")

  if (is3DTiles) {
    const layer = await load3DTilesModelAtPosition({ url, position: targetPos, pitchangle, direction, latitude, longitude, elevation, sightheight })
    if (layer) {
      map.flyToPoint([longitude, latitude, elevation], {
        heading: sightheight,
        pitch: pitchangle,
        roll: 0,
        radius: 500
      }, 2000)
    }
  } else {
    await loadPrimitiveModelAtPosition({ url, position: targetPos, pitchangle, direction, latitude, longitude, elevation, sightheight })
    map.flyToPoint([longitude, latitude, elevation], {
      heading: sightheight,
      pitch: pitchangle,
      roll: 0,
      radius: 500
    }, 2000)
  }
}

/** 在指定位置加载单体模型 */
const loadPrimitiveModelAtPosition = async (options: {
  url: string
  position: any
  pitchangle?: number
  direction?: number
  latitude?: number
  longitude?: number
  elevation?: number
  sightheight?: number
}) => {
  const graphic = new mars3d.graphic.ModelPrimitive({
    position: options.position,
    style: {
      url: options.url,
      scale: 1,
      minimumPixelSize: 64
    },
    flyTo: true
  })

  zxxzGraphicLayer.addGraphic(graphic)
  loadedModels.set(graphic.id as string, graphic)
  // 全局去重记录
  loadedModelUrls.set(options.url, { graphicId: graphic.id as string, position: options.position })

  // 注册编辑事件监听
  const eventNames = [
    "editStart", "editStop", "editStyle",
    "editMovePoint", "editRemovePoint", "editAddPoint",
    "positionChanged", "changed"
  ]
  eventNames.forEach((name) => {
    graphic.on(name as any, (e: any) => {
      eventTarget.fire("zxxzGraphicEditorUpdate", { graphic })
    })
  })

  eventTarget.fire("drawEnd", {
    hztype: "sdhz",
    analysisType: "zxxz_online",
    data: {
      data: graphic.toJSON(),
      name: "在线选址",
      analysisType: "zxxz_online",
      shijiao: {
        alt: options.elevation,
        lng: options.longitude,
        lat: options.latitude,
        headingangle: options.sightheight,
        pitchangle: options.pitchangle
      }
    }
  })
}

/** 在指定位置加载3D Tiles模型，返回 readyPromise 供调用方等待加载完成 */
const load3DTilesModelAtPosition = async (styleOptions: any): Promise<any> => {
  console.log("默认视角加载3D Tiles, url:", styleOptions.url)

  const pos = {
    lng: styleOptions.longitude,
    lat: styleOptions.latitude,
    alt: styleOptions.elevation || 0
  }

  const tilesetLayer = new mars3d.layer.TilesetLayer({
    name: "在线选址",
    url: styleOptions.url,
    position: pos,
    maximumScreenSpaceError: 1,
    maximumMemoryUsage: 1024,
    hasEdit: true,
    hasEditContextMenu: true,
    flyTo: true
  })

  map.addLayer(tilesetLayer)
  loadedModels.set(String(tilesetLayer.id), tilesetLayer)
  loadedModelUrls.set(styleOptions.url, {
    graphicId: String(tilesetLayer.id),
    position: Cesium.Cartesian3.fromDegrees(pos.lng, pos.lat, pos.alt)
  })

  eventTarget.fire("drawEnd", {
    analysisType: "zxxz_online",
    data: {
      data: { position: pos, url: styleOptions.url, layerId: tilesetLayer.id },
      name: "在线选址",
      analysisType: "zxxz_online",
      shijiao: {
        alt: styleOptions.elevation,
        lng: styleOptions.longitude,
        lat: styleOptions.latitude,
        headingangle: styleOptions.sightheight,
        pitchangle: styleOptions.pitchangle
      }
    }
  })

  try {
    await tilesetLayer.readyPromise
    console.log("默认视角3D Tiles模型加载完成", tilesetLayer)

    tilesetLayer.on(mars3d.EventType.updatePosition, () => {
      eventTarget.fire("zxxzTilesetUpdate", { layer: tilesetLayer })
    })

    tilesetLayer.bindContextMenu([
      {
        text: "开始编辑",
        icon: "fa fa-edit",
        show: () => tilesetLayer.hasEdit && !tilesetLayer.isEditing,
        callback: () => { tilesetLayer.startEditing() }
      },
      {
        text: "停止编辑",
        icon: "fa fa-edit",
        show: () => tilesetLayer.isEditing,
        callback: () => {
          tilesetLayer.stopEditing()
          eventTarget.fire("zxxzTilesetUpdate", { layer: tilesetLayer })
        }
      }

    ])

    const json = tilesetLayer.toJSON({ full: true })
    eventTarget.fire("tiles3dLayerLoad", { layer: json })
    return tilesetLayer
  } catch (e) {
    console.log("默认视角3D Tiles模型加载失败", e)
    return null
  }
}

// 取消绘制（组件销毁时清除鼠标绑定事件）
export function cancelDraw() {
  if (map?.graphicLayer) {
    map.graphicLayer.clearDrawing()
  }
}

