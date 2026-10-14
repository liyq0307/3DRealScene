import { $message } from "@mars/components/mars-ui"
import { map, eventTarget as fxEvent, graphicLayer } from "@mars/widgets/analysis/map"
import * as mars3d from "mars3d"
const Cesium = mars3d.Cesium

// 导出 map 对象供外部使用
export { map }

/** 单条绘制数据的结构 */
export interface DrawItem {
  shijiao: any
  graphic: any
}

// 存储所有创建的绘制图形对象
let drawGraphics: any[] = []

// 存储已保存的图形ID集合（图上标绘保留的依据）
const savedGraphicIds: Set<string> = new Set()

// 监听外部清除事件（history-list删除 / toolBox全局清除 时触发）
fxEvent.on("clearBasicDrawings", () => {
  destroyAll()
})

// 点绘制
export async function drawPoint() {
  const graphic = await map.graphicLayer.startDraw({
    type: "point",
    style: {
      color: "rgba(51, 136, 255, 1)",
      pixelSize: 12,
      visibleDepth: false,
      clampToGround: true
    }
  })

  if (graphic) {
    addGraphicToDrawList(graphic)
    fxEvent.fire("drawEnd", {
      value: JSON.stringify({
        shijiao: map.getCameraView(),
        graphic: graphic.toJSON()
      })
    })
  }
}

// 线绘制
export async function drawLine() {
  const graphic = await map.graphicLayer.startDraw({
    type: "polyline",
    style: {
      color: "rgba(51, 136, 255, 1)",
      width: 3,
      visibleDepth: false,
      clampToGround: true
    }
  })

  if (graphic) {
    addGraphicToDrawList(graphic)
    fxEvent.fire("drawEnd", {
      value: JSON.stringify({
        shijiao: map.getCameraView(),
        graphic: graphic.toJSON()
      })
    })
  }
}

// 多边形绘制
export async function drawPolygon() {
  const graphic = await map.graphicLayer.startDraw({
    type: "polygon",
    style: {
      color: "rgba(51, 136, 255, 1)",
      opacity: 0.5,
      outline: true,
      outlineColor: "rgba(255, 255, 255, 1)",
      outlineWidth: 2.0,
      clampToGround: true,
      classificationType: Cesium.ClassificationType.BOTH
    }
  })

  if (graphic) {
    addGraphicToDrawList(graphic)

    fxEvent.fire("drawEnd", {
      value: JSON.stringify({
        shijiao: map.getCameraView(),
        graphic: graphic.toJSON()
      })
    })
  }
}

// 圆形绘制
export async function drawCircle() {
  const graphic = await map.graphicLayer.startDraw({
    type: "circle",
    style: {
      color: "rgba(51, 136, 255, 1)",
      opacity: 0.6,
      outline: true,
      outlineColor: "rgba(255, 255, 255, 1)",
      outlineWidth: 2.0,
      clampToGround: true,
      classificationType: Cesium.ClassificationType.BOTH
    }
  })

  if (graphic) {
    addGraphicToDrawList(graphic)

    fxEvent.fire("drawEnd", {
      value: JSON.stringify({
        shijiao: map.getCameraView(),
        graphic: graphic.toJSON()
      })
    })
  }
}

// 添加图形到地图图层
function addGraphicToDrawList(graphic: any) {
  map?.graphicLayer.addGraphic(graphic)
  drawGraphics.push(graphic)
}

/**
 * 清除所有未保存的图形（保留已保存在图上的标绘）
 * 点击"清除"按钮 or 离开页面时调用
 */
export function clearAll() {
  drawGraphics = drawGraphics.filter((graphic) => {
    if (!savedGraphicIds.has(graphic.id)) {
      map?.graphicLayer.removeGraphic(graphic)
      return false
    }
    return true
  })
}

/**
 * 销毁全部（包括已保存的图形）—— 用于彻底清理场景
 */
export function destroyAll() {
  drawGraphics.forEach((graphic) => {
    map?.graphicLayer.removeGraphic(graphic)
  })
  drawGraphics = []
  savedGraphicIds.clear()
}

/**
 * 标记当前所有图形为已保存（点击保存按钮时调用）
 * 保存后图上标绘仍然存在，且不会被 clearAll 清除
 */
export function markGraphicsAsSaved() {
  drawGraphics.forEach((graphic) => {
    savedGraphicIds.add(graphic.id)
  })
}

// 取消标绘绘制（组件销毁时清除鼠标绑定事件）
export function cancelDrawing() {
  if (map?.graphicLayer) {
    map.graphicLayer.clearDrawing()
  }
}

// 导出GeoJSON（基于当前所有图形）
export function exportGeoJson() {
  const graphics = drawGraphics

  if (graphics.length === 0) {
    $message("图层中没有图形数据")
    return
  }

  // 过滤出有效且仍在图层上的图形
  const validGraphics = graphics.filter((graphic) => {
    // 跳过已从图层移除的失效图形（通过 id 查找判断是否仍存在）
    if (!map?.graphicLayer || !map.graphicLayer.getGraphicById(graphic.id)) {
      return false
    }
    const type = graphic.type
    return (
      type === "point" ||
      type === "polyline" ||
      type === "polygon" ||
      type === "circle" ||
      type === "point-entity" ||
      type === "polyline-entity" ||
      type === "polygon-entity"
    )
  })

  if (validGraphics.length === 0) {
    $message("没有可导出的点、线、面图形")
    return
  }

  // 导出为GeoJSON格式
  const geoJSON = exportToGeoJSON(validGraphics)

  return geoJSON
}

// 导出为GeoJSON
function exportToGeoJSON(graphics: any[]) {
  const features = graphics.map((graphic) => {
    const type = graphic.type
    let geometry: any = null
    const properties: any = {
      id: graphic.id,
      name: graphic.name || "",
      type
    }

    if (type === "point" || type === "point-entity") {
      const position = Array.isArray(graphic.positions) ? graphic.positions[0] : graphic.position
      // 检查位置有效性
      if (!position) {
        console.warn(`图形 ${graphic.id} 缺少位置数据，跳过导出`)
        return null
      }
      const carto = Cesium.Cartographic.fromCartesian(position)
      geometry = {
        type: "Point",
        coordinates: [Cesium.Math.toDegrees(carto.longitude), Cesium.Math.toDegrees(carto.latitude), carto.height]
      }
    } else if (type === "polyline" || type === "polyline-entity") {
      const positions = (Array.isArray(graphic.positions) && graphic.positions.length > 0) || graphic.positionsShow
      // 检查位置有效性
      if (!positions || positions.length === 0) {
        console.warn(`图形 ${graphic.id} 缺少位置数据，跳过导出`)
        return null
      }
      const coordinates = positions.map((pos) => {
        if (!pos) { return null }
        const carto = Cesium.Cartographic.fromCartesian(pos)
        return [Cesium.Math.toDegrees(carto.longitude), Cesium.Math.toDegrees(carto.latitude), carto.height]
      }).filter(Boolean)
      if (coordinates.length === 0) {
        console.warn(`图形 ${graphic.id} 位置数据无效，跳过导出`)
        return null
      }
      geometry = {
        type: "LineString",
        coordinates
      }
    } else if (type === "polygon" || type === "polygon-entity") {
      const positions = (Array.isArray(graphic.positions) && graphic.positions.length > 0) || graphic.positionsShow
      // 检查位置有效性
      if (!positions || positions.length === 0) {
        console.warn(`图形 ${graphic.id} 缺少位置数据，跳过导出`)
        return null
      }
      const coordinates = positions.map((pos) => {
        if (!pos) { return null }
        const carto = Cesium.Cartographic.fromCartesian(pos)
        return [Cesium.Math.toDegrees(carto.longitude), Cesium.Math.toDegrees(carto.latitude), carto.height]
      }).filter(Boolean)
      if (coordinates.length < 3) {
        console.warn(`图形 ${graphic.id} 位置数据不足，跳过导出`)
        return null
      }
      // 闭合多边形
      coordinates.push(coordinates[0])
      geometry = {
        type: "Polygon",
        coordinates: [coordinates]
      }
    } else if (type === "circle") {
      const center = graphic.positions ? graphic.positions[0] : graphic.position
      // 检查位置有效性
      if (!center) {
        console.warn(`图形 ${graphic.id} 缺少位置数据，跳过导出`)
        return null
      }
      const radius = graphic.radius
      const carto = Cesium.Cartographic.fromCartesian(center)

      // 简化：将圆转换为多点近似
      const numPoints = 64
      const coordinates: number[][] = []
      for (let i = 0; i <= numPoints; i++) {
        const angle = (i / numPoints) * 2 * Math.PI
        const lon = Cesium.Math.toDegrees(carto.longitude) + (radius * Math.cos(angle)) / 111320
        const lat = Cesium.Math.toDegrees(carto.latitude) + (radius * Math.sin(angle)) / 110540
        coordinates.push([lon, lat, carto.height])
      }
      geometry = {
        type: "Polygon",
        coordinates: [coordinates]
      }
    }

    if (!geometry) {
      return null
    }

    return {
      type: "Feature",
      geometry,
      properties
    }
  }).filter(Boolean)

  return {
    type: "FeatureCollection",
    features
  }
}
