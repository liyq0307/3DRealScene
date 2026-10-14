import { map, eventTarget as fxEvent, sightline } from "@mars/widgets/analysis/map"
import * as mars3d from "mars3d"
const Cesium = mars3d.Cesium

// 用于存储所有创建的通视线数据（包含位置、样式等信息）
const sightlineData = new Map<string, any>()

// 控制指定通视线的显示隐藏
export function toggleSightlineVisibility(id: string, visible?: boolean) {
  // console.log("切换通视线显示状态, id:", id, "visible:", visible)

  const data = sightlineData.get(id)
  if (!data) {
    console.warn("未找到通视线数据, id:", id)
    return
  }

  if (visible) {
    // 显示：重新添加通视线和点
    console.log("重新添加通视线:", id)
    map.scene.globe.depthTestAgainstTerrain = true
    if (data.type === "line") {
      // 添加通视线
      sightline.add(data.center, data.targetPoint, { offsetHeight: data.offsetHeight, id: data.id })
      // 显示观察位置点和目标点
      if (data.centerPoint) {
        data.centerPoint.show = true
      }
      if (data.targetPointGraphic) {
        data.targetPointGraphic.show = true
      }
    } else if (data.type === "circle") {
      const targetPoints = data.targetPoints
      for (let i = 0; i < targetPoints.length; i++) {
        sightline.add(data.center, targetPoints[i], { offsetHeight: data.offsetHeight, id: data.id })
      }
      // 显示观察位置点
      if (data.centerPoint) {
        data.centerPoint.show = true
      }
    }
    map.scene.globe.depthTestAgainstTerrain = false
    data.visible = true
  } else {
    // 隐藏：清除所有通视线，然后重新添加其他可见的通视线
    console.log("隐藏通视线:", id)
    // 隐藏观察位置点和目标点
    if (data.centerPoint) {
      data.centerPoint.show = false
    }
    if (data.targetPointGraphic) {
      data.targetPointGraphic.show = false
    }
    data.visible = false
    // 清除所有通视线
    sightline.clear()
  }
}

// 移除指定的通视线
export function removeSightline(id: string) {
  const data = sightlineData.get(id)
  // // console.log("尝试移除通视线, id:", id, "数据存在:", data)
  if (data) {
    // 移除观察位置和目标点的 graphic
    if (data.centerPoint) {
      map.graphicLayer.removeGraphic(data.centerPoint)
      // // console.log("已移除观察位置点")
    }
    if (data.targetPointsGraphics && data.targetPointsGraphics.length > 0) {
      data.targetPointsGraphics.forEach((graphic) => {
        map.graphicLayer.removeGraphic(graphic)
      })
      // // console.log("已移除目标点1")
    } else if (data.targetPointGraphic) {
      map.graphicLayer.removeGraphic(data.targetPointGraphic)
      // // console.log("已移除目标点2")
    }

    sightlineData.delete(id)
    // console.log("已移除通视线数据:", id)

    // 清除所有通视线
    sightline.clear()
  }
}

// 统一清除之前的通视线（线、圆）和可视域分析数据，保证唯一存在
export function clearAllPreviousAnalysis() {
  const idsToRemove = Array.from(sightlineData.keys())
  idsToRemove.forEach((id) => removeSightline(id))

  // 确保彻底移除所有 viewShed 类型的 graphic，防止残留
  const allGraphics = map.graphicLayer.getGraphics()
  allGraphics.forEach((g) => {
    if (g.type === "viewShed" || g.attr?.type === "ts_analysis_viewshed") {
      map.graphicLayer.removeGraphic(g)
    }
  })
}

// 绘制线通视
export async function drawLine() {
  const sightlineId = "sightline_" + new Date().getTime()
  // 清空之前所有分析数据（线、圆、可视域）
  clearAllPreviousAnalysis()

  const graphic = await map.graphicLayer.startDraw({
    type: "polyline",
    maxPointNum: 2,
    style: {
      color: "#55ff33",
      width: 5
    },
    attr: {
      type: "tsfx_polyline"
    },
    success: function (graphic) {
      fxEvent.fire("drawLineEnd", {
        extent: JSON.stringify({
          type: "ts_analysis",
          shijiao: map.getCameraView(),
          graphic,
          sightlineId
        })
      }) // 抛出事件，可以vue中去监听事件
    }
  })

  // 绘制成功后创建点
  const positions = graphic.positionsShow
  map.graphicLayer.getGraphicById(graphic.id).remove() // 移除刚刚画的线
  map.scene.globe.depthTestAgainstTerrain = true
  const center = positions[0]
  const targetPoint = positions[1]

  // 添加通视线
  sightline.add(center, targetPoint, { offsetHeight: 1.5, id: sightlineId })

  // 创建观察位置和目标点
  const centerPoint = createPoint(center, true)
  const targetPointGraphic = createPoint(targetPoint, false)

  // 存储通视线数据（包含点的 graphic 对象）
  sightlineData.set(sightlineId, {
    id: sightlineId,
    type: "line",
    center,
    targetPoint,
    offsetHeight: 1.5,
    visible: true,
    centerPoint, // 观察位置点
    targetPointGraphic // 目标点
  })
  // console.log("已存储线通视数据, id:", sightlineId, "当前通视线数量:", sightlineData.size)

  map.scene.globe.depthTestAgainstTerrain = false
}

/**
 * 绘制成功后创建点
 *
 * @param {Array} position 坐标点
 * @param {boolean} isFirst 点文字
 * @return {object} 返回像素点Entity对象
 */
function createPoint(position, isFirst) {
  const graphic = new mars3d.graphic.PointEntity({
    position,
    style: {
      color: Cesium.Color.fromCssColorString("rgba(51, 136, 255, 1)"),
      pixelSize: 6,
      outlineColor: Cesium.Color.fromCssColorString("rgba(255, 255, 255, 1)"),
      outlineWidth: 2,
      scaleByDistance: new Cesium.NearFarScalar(1.5e2, 1.0, 8.0e6, 0.2),
      label: {
        text: isFirst ? "观察位置" : "目标点",
        font_size: 17,
        font_family: "楷体",
        color: Cesium.Color.AZURE,
        outline: true,
        outlineColor: Cesium.Color.BLACK,
        outlineWidth: 2,
        horizontalOrigin: Cesium.HorizontalOrigin.CENTER,
        verticalOrigin: Cesium.VerticalOrigin.BOTTOM,
        pixelOffset: new Cesium.Cartesian2(0, -20), // 偏移量
        distanceDisplayCondition: new Cesium.DistanceDisplayCondition(0.0, 2000000)
      }
    },
    attr: {
      type: "tsfx_point"
    }
  })
  map.graphicLayer.addGraphic(graphic)
  return graphic
}
// 绘制圆通视
export async function drawCircle() {
  const sightlineId = "sightline_" + new Date().getTime()
  // 清空之前所有分析数据（线、圆、可视域）
  clearAllPreviousAnalysis()

  const graphic = await map.graphicLayer.startDraw({
    type: "circle",
    style: {
      color: "rgba(255, 255, 0, 1)",
      opacity: 0.2,
      clampToGround: true
    },
    attr: {
      type: "tsfx_circle"
    },
    success: function (graphic) {
      fxEvent.fire("drawLineEnd", {
        extent: JSON.stringify({
          type: "ts_analysis",
          shijiao: map.getCameraView(),
          graphic,
          sightlineId
        })
      }) // 抛出事件，可以vue中去监听事件
    }
  })

  let center = graphic.positionShow
  center = mars3d.PointUtil.addPositionsHeight(center, 1.5) // 加人的身高等因素，略微抬高一些

  const targetPoints = graphic.getOutlinePositions(false, 45)

  map.graphicLayer.getGraphicById(graphic.id).remove()
  map.scene.globe.depthTestAgainstTerrain = true

  for (let i = 0; i < targetPoints.length; i++) {
    let targetPoint = targetPoints[i]
    targetPoint = mars3d.PointUtil.getSurfacePosition(map.scene, targetPoint)
    sightline.add(center, targetPoint, { offsetHeight: 1.5, id: sightlineId })
    // 圆形通视分析不显示目标点
  }

  // 创建观察位置点
  const centerPoint = createPoint(center, true)

  // 存储通视线数据（只包含观察位置点，不包含目标点）
  sightlineData.set(sightlineId, {
    id: sightlineId,
    type: "circle",
    center,
    targetPoints,
    offsetHeight: 1.5,
    visible: true,
    centerPoint // 观察位置点
  })
  // console.log("已存储圆形通视线数据, id:", sightlineId, "当前通视线数量:", sightlineData.size)

  map.scene.globe.depthTestAgainstTerrain = false
}

// 可视域分析
export async function drawViewshed() {
  // 清空之前所有分析数据（线、圆、可视域）
  clearAllPreviousAnalysis()

  map.scene.globe.depthTestAgainstTerrain = true
  const graphic = await map.graphicLayer.startDraw({
    type: "viewShed",
    style: {
      angle: 60,
      angle2: 45,
      distance: 80,
      heading: 44,
      pitch: -12,
      addHeight: 0.5 // 在坐标点增加的高度值，规避遮挡，效果更友好
    },
    attr: {
      type: "ts_analysis_viewshed"
    },
    success: function (graphic) {
      fxEvent.fire("drawLineEnd", {
        extent: JSON.stringify({
          type: "ts_analysis",
          shijiao: map.getCameraView(),
          graphic: graphic.toJSON()
        })
      })
    }
  })
  return graphic
}

// 属性编辑
let selectedView
export function getGraphic(graphicId) {
  selectedView = map.graphicLayer.getGraphicById(graphicId)
  return selectedView
}

// 获取管理图层
export function getManagerLayer() {
  return map.graphicLayer
}
// 清除绘制
export function clearDrawing() {
  const layer = getManagerLayer()
  layer.clearDrawing()
}
