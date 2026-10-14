import * as mars3d from "mars3d"
import { map, eventTarget as fxEvent, graphicLayer } from "@mars/widgets/analysis/map"
import { $message } from "@mars/components/mars-ui"
const Cesium = mars3d.Cesium

// 存储所有创建的线杆
let poleGraphics: any[] = []
// // 标签
// let poleLabels: any[] = []
// 存储所有连接线
let wireGraphics: any[] = [] // 存储所有连接线

// 存储已保存的图形ID集合
const savedGraphicIds: Set<string> = new Set()

// 线杆颜色
let currentPoleColor = "#808080"

// 开始绘制线杆
export async function startDrawPole(options: { height: number; radius: number }) {
  const graphic = await map.graphicLayer.startDraw({
    type: "point",
    style: {
      color: "#FF0000",
      pixelSize: 10,
      outlineColor: "rgba(255, 255, 255, 1)",
      outlineWidth: 2,
      visibleDepth: false,
      clampToGround: true
    }
  })

  if (graphic) {
    const position = graphic.toJSON().position

    // 移除临时绘制的点
    graphicLayer.removeGraphic(graphic)


    // 创建圆柱体模拟高压线杆
    const poleGraphic = createPole(position, options)

    // 如果不是第一根线杆，则创建与上一根线杆的连接线
    if (poleGraphics.filter(g => g._state !== "destroy").length > 1) {
      console.log("poleGraphics", poleGraphics)
      createWireConnection(poleGraphics, options)
    }

    fxEvent.fire("drawPoleEnd", {
      analysisType: "tjjm_pole",
      data: {
        data: {
          ...poleGraphic.toJSON()
        },
        name: "高压线杆",
        height: options.height,
        radius: options.radius,
        position,
        analysisType: "tjjm_pole",
        shijiao: map.getCameraView()
      }
    })
  }
}

// 创建圆柱体（高压线杆）
function createPole(position: any, options: { height: number; radius: number }) {

  const poleGraphic = new mars3d.graphic.CircleEntity({
    position,
    style: {
      radius: options.radius,
      color: currentPoleColor,
      opacity: 1,
      // outline: true,
      // outlineColor: "rgba(255, 255, 255, 1)",
      // outlineWidth: 2,
      diffHeight: options.height, // 使用diffHeight控制圆柱体高度
      closeTop: true,
      closeBottom: true

    },
    attr: { name: "高压线杆", isSaved: false } // 添加保存状态标识
  })

  map.graphicLayer.addGraphic(poleGraphic)
  poleGraphics.push(poleGraphic) // 将线杆对象添加到数组中
  return poleGraphic
}

// 创建圆弧连接线（模拟高压线）
/**
 * 在相邻两个电杆之间创建高压线连接
 * @param poleGraphics - 电杆图形数组，用于获取最后两个电杆的位置信息
 * @param options - 连接配置选项
 * @param options.height - 连接线相对于电杆底部的高度偏移量（米）
 * @param options.radius - 连接线的半径参数
 * @returns {mars3d.graphic.PolylineEntity} 创建的高压线图形对象
 */
function createWireConnection(poleGraphics, options: { height: number; radius: number }) {
  const lastPole = poleGraphics[poleGraphics.length - 2]
  const currentPole = poleGraphics[poleGraphics.length - 1]
  console.log("createWireConnection", poleGraphics, lastPole, currentPole)

  const lastPoleData = lastPole.toJSON()
  const currentPoleData = currentPole.toJSON()

  const position1 = lastPoleData.position
  const position2 = currentPoleData.position

  // 记录关联的两个线杆ID，用于后续按组显示/隐藏
  const poleId1 = lastPole.id || lastPoleData.id
  const poleId2 = currentPole.id || currentPoleData.id

  const startPoint = Cesium.Cartesian3.fromDegrees(position1[0], position1[1], position1[2] + options.height)
  const endPoint = Cesium.Cartesian3.fromDegrees(position2[0], position2[1], position2[2] + options.height)


  const positions = mars3d.PolyUtil.getLinkedPointList(startPoint, endPoint, -3000, 50) // 计算曲线点

  const wireGraphic = new mars3d.graphic.PolylineEntity({
    positions,
    style: {
      width: 3,
      color: "#000000",
      opacity: 1
    },
    attr: { remark: "示例7", isSaved: false, connectedPoleIds: [poleId1, poleId2] } // 关联两个线杆ID
  })
  console.log("wireGraphic", wireGraphic, wireGraphic.toJSON())

  map.graphicLayer.addGraphic(wireGraphic)
  wireGraphics.push(wireGraphic)
  return wireGraphic
}

// 创建高度标签
function createHeightLabel(position: any, terrainHeight: number, options: { height: number; radius: number }) {
  const cartographic = Cesium.Cartographic.fromCartesian(position)
  const topPosition = Cesium.Cartesian3.fromRadians(
    cartographic.longitude,
    cartographic.latitude,
    terrainHeight + options.height
  )

  const labelGraphic = new mars3d.graphic.DivGraphic({
    position: topPosition,
    style: {
      html: `<div style="
        background: rgba(128, 128, 128, 0.9);
        color: rgba(255, 255, 255, 1);
        padding: 4px 8px;
        border-radius: 3px;
        font-size: 12px;
        font-weight: bold;
        white-space: nowrap;
        pointer-events: none;
        border: 1px solid rgba(255, 255, 255, 0.5);
      ">${options.height}m</div>`,
      horizontalOrigin: Cesium.HorizontalOrigin.CENTER,
      verticalOrigin: Cesium.VerticalOrigin.BOTTOM,
      pixelOffset: new Cesium.Cartesian2(0, 10),
      distanceDisplayCondition: new Cesium.DistanceDisplayCondition(0, 10000)
    }
  })

  map.graphicLayer.addGraphic(labelGraphic)
  return labelGraphic
}

// 改变线杆颜色
export function changePoleColor(color: string) {
  currentPoleColor = color
  poleGraphics.forEach((graphic) => {
    if (graphic.style) {
      graphic.style.color = color
    }
  })
}

// 切换标签显示
// export function toggleLabels(show: boolean) {
//   poleLabels.forEach((label) => {
//     label.show = show
//   })
// }

// 清除所有未保存的图形
export function clearAll() {
  // 只清除未保存的线杆
  poleGraphics = poleGraphics.filter((graphic) => {
    if (!graphic.attr?.isSaved) {
      map.graphicLayer.removeGraphic(graphic)
      return false
    }
    return true
  })

  // 只清除未保存的连接线
  wireGraphics = wireGraphics.filter((wire) => {
    if (!wire.attr?.isSaved) {
      map.graphicLayer.removeGraphic(wire)
      return false
    }
    return true
  })
}



// 清除所有图形（包括已保存的）
export function clearAllGraphics() {
  // 清除所有线杆
  poleGraphics.forEach((graphic) => {
    map.graphicLayer.removeGraphic(graphic)
  })
  poleGraphics = []

  // 清除所有连接线
  wireGraphics.forEach((wire) => {
    map.graphicLayer.removeGraphic(wire)
  })
  wireGraphics = []
}

// 根据ID数组移除指定的线杆和连接线（只操作当前记录关联的线和杆）
export function removePoleByIds(ids: string[]) {
  console.log("[removePoleByIds] ids:", ids)
  // 线杆添加在 map.graphicLayer 上，必须从 map.graphicLayer 查找
  ids.forEach((id) => {
    const graphic = map.graphicLayer.getGraphicById(id)
    console.log("[removePoleByIds] getGraphicById(" + id + "):", graphic)
    if (graphic) {
      map.graphicLayer.removeGraphic(graphic)
    }
  })

  // 只删除与当前记录线杆关联的连接线（polyline 类型）
  const allGraphics = map.graphicLayer.getGraphics()
  const toRemove: any[] = []
  allGraphics.forEach((g: any) => {
    if (g.type === "polyline" || g.style?.width !== undefined) {
      // 检查这条线的 connectedPoleIds 是否与当前记录的任意一个杆ID匹配
      const connectedIds = g.attr?.connectedPoleIds
      if (Array.isArray(connectedIds)) {
        const shouldRemove = connectedIds.some((poleId: string) => ids.includes(poleId))
        if (shouldRemove) {
          toRemove.push(g)
        }
      }
    }
  })
  // 遍历完成后统一删除，避免遍历时修改集合
  toRemove.forEach((g) => {
    map.graphicLayer.removeGraphic(g)
  })
}

// 切换指定线杆和连接线的可见性（只操作当前记录关联的线和杆）
export function togglePoleVisibility(ids: string[], visible: boolean) {
  console.log("[togglePoleVisibility] ids:", ids, "visible:", visible)
  // 切换线杆可见性
  ids.forEach((id) => {
    const graphic = map.graphicLayer.getGraphicById(id)
    console.log("[togglePoleVisibility] getGraphicById(" + id + "):", graphic)
    if (graphic) {
      graphic.show = visible
    }
  })

  // 只切换与当前记录线杆关联的连接线，不影响其他记录的线
  const allGraphics = map.graphicLayer.getGraphics()
  console.log("[togglePoleVisibility] allGraphics count:", allGraphics.length)
  allGraphics.forEach((g: any) => {
    if (g.type === "polyline" || g.style?.width !== undefined) {
      // 检查这条线的 connectedPoleIds 是否与当前记录的任意一个杆ID匹配
      const connectedIds = g.attr?.connectedPoleIds
      if (Array.isArray(connectedIds)) {
        // 如果这条线关联的两个杆ID中有一个属于当前记录，则切换该线的可见性
        const shouldToggle = connectedIds.some((poleId: string) => ids.includes(poleId))
        if (shouldToggle) {
          g.show = visible
        }
      }
    }
  })
}
// 标记所有当前图形为已保存
export function markGraphicsAsSaved() {
  poleGraphics.forEach((graphic) => {
    if (graphic.attr) {
      graphic.attr.isSaved = true
    }
  })

  wireGraphics.forEach((wire) => {
    if (wire.attr) {
      wire.attr.isSaved = true
    }
  })
}

// 只清空本地数组引用，不清除图上图形（用于保存后重置状态）
export function clearLocalArrays() {
  poleGraphics = []
  wireGraphics = []
}

// 取消线杆绘制（组件销毁时清除鼠标绑定事件）
export function cancelDrawPole() {
  if (map?.graphicLayer) {
    map.graphicLayer.clearDrawing()
  }
}

// 批量绘制塔基（从Excel导入）
export function drawTj(coordinates: any[], options: any) {
  console.log("drawTj", coordinates, options)


  // 遍历所有坐标点创建线杆
  coordinates.forEach((coordinate, index) => {
    // 坐标格式处理：coordinate 可能是 [lon, lat] 或 {lng, lat} 格式
    let position: any
    if (Array.isArray(coordinate)) {
      // [lon, lat] 格式
      position = [coordinate[0], coordinate[1], coordinate[2]]
    }

    // 创建线杆
    const poleGraphic = createPole(position, options)
    // 如果不是第一根线杆，则创建与上一根线杆的连接线
    if (poleGraphics.filter(g => g._state !== "destroy").length > 1) {
      console.log("poleGraphics", poleGraphics)
      createWireConnection(poleGraphics, options)
    }

    // 触发事件保存数据到 store
    fxEvent.fire("drawPoleEnd", {
      analysisType: "tjjm_pole",
      data: {
        data: {
          ...JSON.parse(JSON.stringify(poleGraphic.toJSON()))
        },
        name: `高压线杆_${index + 1}`,
        height: options.height,
        radius: options.radius,
        position,
        analysisType: "tjjm_pole",
        shijiao: map.getCameraView()
      }
    })
  })

  // 定位到最后一个线杆的位置
  if (poleGraphics.length > 0) {

    const lastPole = poleGraphics[poleGraphics.length - 1]
    const lastPoleData = lastPole?.toJSON()
    const position = lastPoleData?.position

    // 设置相机视角到最后一个线杆位置，高度100
    map.setCameraView({
      lat: position[1],
      lng: position[0],
      alt: position[2] + 100,
      heading: 0,
      pitch: -45
    })
  }
}
