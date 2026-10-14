import * as mars3d from "mars3d"
import * as turf from "@turf/turf"
import { ContourManager } from "@mars/utils/ContourManager"
import { map, eventTarget as fxEvent } from "@mars/widgets/analysis/map"
import { saveJSON } from "@mars/utils/file-util"

const Cesium = mars3d.Cesium

// 初始化 ContourManager
let contourManager: ContourManager | null = null

export function getContourManager(options?: any) {
  if (!contourManager) {
    contourManager = new ContourManager({
      map,
      interval: options?.interval ?? 30,
      showLabels: options?.showLabels ?? true,
      width: options?.width ?? 2,
      color: options?.color ? Cesium.Color.fromCssColorString(options.color) : Cesium.Color.fromCssColorString("#FF0000")
    })
  }
  return contourManager
}

// 添加矩形
let graphic: any
export async function btnDrawExtent() {
  // 切换模块回来后，恢复默认属性；同模块内再次绘制则保留用户修改的值
  checkAndResetIfNeeded()
  clearAll()
  graphic = await map.graphicLayer.startDraw({
    type: "rectangle",
    style: {
      color: "#007be6",
      opacity: 0.2,
      outline: true,
      outlineColor: "#007be6",
      outlineWidth: 2,
      height: 0,
      clampToGround: true,
      classificationType: Cesium.ClassificationType.BOTH
    }
  })

  if (!graphic) {
    return
  }

  const positions = graphic.getOutlinePositions(false)

  // 计算 bbox [minLng, minLat, maxLng, maxLat]
  const lnglats = mars3d.LngLatArray.toArray(positions)
  const lons = lnglats.map((item) => item[0])
  const lats = lnglats.map((item) => item[1])
  const bbox = [Math.min(...lons), Math.min(...lats), Math.max(...lons), Math.max(...lats)]

  const manager = getContourManager()
  const geojson = graphic.toGeoJSON()
  const polygonGeometry = geojson.type === "Feature" ? geojson : turf.feature(geojson)
  await manager.createContoursForBbox(bbox, polygonGeometry, 0.01, graphic)

  // 触发绘制完成事件，传递相关信息
  notifyUpdate()
}

// 属性改变后触发更新事件
function notifyUpdate(lineHeight?: number) {
  if (!graphic) {
    return
  }
  const manager = getContourManager()
  const options = {
    interval: manager.interval,
    showLabels: manager.showLabels,
    width: manager.width,
    color: manager.color?.toCssColorString(),
    show: manager.dataSource.show,
    lineHeight: lineHeight !== undefined ? lineHeight : undefined
  }

  // 1. 同步 store 状态
  fxEvent.fire("updateOptions", {
    analysisType: "dgx_analysis",
    id: graphic.id,
    options
  })

  // 2. 如果是新绘制区域，通知业务层保存初始数据
  if (lineHeight === undefined) {
    const positions = graphic.getOutlinePositions(false)
    const lnglats = mars3d.LngLatArray.toArray(positions)
    const lons = lnglats.map((item) => item[0])
    const lats = lnglats.map((item) => item[1])
    const bbox = [Math.min(...lons), Math.min(...lats), Math.max(...lons), Math.max(...lats)]

    fxEvent.fire("drawEnd", {
      analysisType: "dgx_analysis",
      data: {
        shijiao: map.getCameraView(),
        bbox,
        polygonGeometry: graphic.toGeoJSON(),
        options,
        data: graphic.toJSON()
      }
    })
  }
}

// 默认配置
const defaultOptions = {
  interval: 30,
  showLabels: true,
  width: 2,
  color: "#FF0000"
}

// 标记是否已离开模块（切换了其他模块）
let hasLeftModule = false

// 标记离开模块（组件卸载时调用）
export function markModuleLeft() {
  hasLeftModule = true
}

// 取消矩形绘制（组件销毁时清除鼠标绑定事件）
export function cancelDrawExtent() {
  if (map?.graphicLayer) {
    map.graphicLayer.clearDrawing()
  }
}

// 检查并重置：如果是从其他模块切回来，则恢复默认属性（绘制前调用）
export function checkAndResetIfNeeded() {
  if (!contourManager) {
    return
  }
  if (hasLeftModule) {
    contourManager.setInterval(defaultOptions.interval)
    contourManager.setShowLabels(defaultOptions.showLabels)
    contourManager.setWidth(defaultOptions.width)
    contourManager.setColor(Cesium.Color.fromCssColorString(defaultOptions.color))
    contourManager.dataSource.show = true
    hasLeftModule = false
  }
}

// 清除
export function clearAll() {
  if (map?.graphicLayer && graphic) {
    map.graphicLayer.removeGraphic(graphic)
    graphic = null
  }
  if (contourManager) {
    contourManager.clear()
  }
}

// 重置等高线管理器属性到默认值（切换模块时恢复初始状态）
export function resetToDefaults() {
  if (!contourManager) {
    return
  }
  contourManager.setInterval(defaultOptions.interval)
  contourManager.setShowLabels(defaultOptions.showLabels)
  contourManager.setWidth(defaultOptions.width)
  contourManager.setColor(Cesium.Color.fromCssColorString(defaultOptions.color))
  contourManager.dataSource.show = true
}

// 销毁
export function onDestroy() {
  if (map?.graphicLayer && graphic) {
    map.graphicLayer.removeGraphic(graphic)
    graphic = null
  }
  if (contourManager) {
    contourManager.destroy()
    contourManager = null
  }
}

// 线宽控制
export function changeWidth(val: number) {
  const manager = getContourManager()
  manager.setWidth(val)
  notifyUpdate()
}

// 等高距控制
export function changeSpacing(val: number) {
  const manager = getContourManager()
  manager.setInterval(val)
  notifyUpdate()
}

// 改变颜色
export function changeColor(val: string) {
  const manager = getContourManager()
  manager.setColor(Cesium.Color.fromCssColorString(val))
  notifyUpdate()
}

// 是否显示等高线 (这里理解为显示/隐藏数据源)
export function showDengGX(val: boolean) {
  if (contourManager) {
    contourManager.setShow(val)
    notifyUpdate()
  }
}

// 绘制并高亮指定高度 of the 等高线
export function drawHighlightContour(height: number) {
  const manager = getContourManager()
  // 首先清除之前可能存在的高亮线
  manager.removeEntitiesByName("HighlightContour")

  if (height !== undefined && height !== null) {
    manager.addHighlightContour(height, "rgba(255, 255, 0, 1)", 4)
  }
  notifyUpdate(height)
}

// 是否显示标注
export function showLabels(val: boolean) {
  const manager = getContourManager()
  manager.setShowLabels(val)
  notifyUpdate()
}

// 导出 GeoJSON
export function exportGeojson() {
  const geojson = getExportGeojson()
  if (geojson) {
    saveJSON(geojson)
  } else {
    globalMsg("当前没有可导出的等高线数据")
  }
}

// 获取导出的 GeoJSON
export function getExportGeojson() {
  if (contourManager) {
    return contourManager.getGeojson()
  }
  return null
}

function globalMsg(msg: string) {
  // 简单提示，实际项目中可能调用 mars-ui 的消息提示
  console.log(msg)
}
