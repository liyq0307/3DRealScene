import { $message } from "@mars/components/mars-ui"
import { eventTarget as fxEvent, measure, map, graphicLayer } from "@mars/widgets/analysis/map"
import * as mars3d from "mars3d"
const Cesium = mars3d.Cesium

let measureVolume
// 点选高度
function showHeightVal(graphic = null) {
  console.log(graphic)
  const baseHeight = measureVolume.height.toFixed(1)
  const minHeight = measureVolume.minHeight.toFixed(1)
  const maxHeight = getFixedNum(measureVolume.maxHeight)

  const measured = measureVolume.measured || {}
  const fillVol = measured.fillVolume ?? 0
  const digVol = measured.digVolume ?? 0
  const totalArea = measured.totalArea ?? 0

  const fillStr = fillVol >= 10000 ? (fillVol / 10000).toFixed(2) + "万方" : fillVol.toFixed(2) + "方"
  const digStr = digVol >= 10000 ? (digVol / 10000).toFixed(2) + "万方" : digVol.toFixed(2) + "方"
  const areaStr = totalArea.toFixed(2) + "平方米"

  // 更新地图上的测量标签
  if (measureVolume._measureLable) {
    try {
      measureVolume._measureLable.text = `填方体积：${fillStr}\n挖方体积：${digStr}\n横切面积：${areaStr}`
      if (measureVolume._measureLable.style) {
        measureVolume._measureLable.style.background = true
        measureVolume._measureLable.style.backgroundColor = "rgba(0, 0, 0, 0.6)"
      }
    } catch (e) {
      console.warn("更新标签失败:", e)
    }
  }

  // 触发自定义事件 heightVal ，改变组件面板中的值
  fxEvent.fire("heightVal", {
    baseHeight,
    minHeight,
    maxHeight,
    graphic: JSON.parse(JSON.stringify(graphic)),
    shijiao: map.getCameraView(),
    analysisData: JSON.stringify(graphicsMap),
    depth: true,
    fillVolume: fillStr,
    digVolume: digStr,
    totalArea: areaStr
  })
}

function getFixedNum(val) {
  return Math.ceil(val * 10) / 10
}

// 方量分析
export function analysisMeasure() {
  // 清除上一次绘制的内容
  if (measureVolume) {
    measureVolume.destroy()
    measureVolume = undefined
  }
  clearInterResult()

  // 手动绘制的方式分析
  measure
    .volume({
      depth: true,
      // splitNum: 6, // 面内插值次数，控制精度[注意精度越大，分析时间越长]
      // // minHeight: 50 , //可以设置一个固定的最低高度
      // exact: false, // 是否进行精确计算， 传false时是否快速概略计算方式，该方式计算精度较低，但计算速度快，仅能计算在当前视域内坐标的高度
      label: {
        font_family: "楷体",
        font_size: 20,
        color: "rgba(255, 255, 255, 1)"
      }
    })
    .then((e) => {
      measureVolume = e
      console.log("analysisMeasure", e.toJSON())
      showHeightVal(e.toJSON())
    })
}

export function showResult(reslut) {
  if (reslut && measureVolume && measureVolume.interPolygonObj) {
    showInterResult(measureVolume.interPolygonObj.list)
    return true
  } else {
    clearInterResult()
    return false
  }
}

// 修改基础高度
export function baseHeight(num) {
  measureVolume.height = num
  showHeightVal()
}

// 修改底高
export function txtMinHeight(num) {
  if (num > measureVolume.height) {
    $message("墙底部高度不能高于基准面高")
    fxEvent.fire("resetMinHeight", { value: measureVolume.minHeight })
    return
  }
  measureVolume.minHeight = num
}

// 修改顶高
export function txtMaxHeight(num) {
  const maxHeight = getFixedNum(measureVolume.polygonMaxHeight)
  if (num < maxHeight) {
    $message("墙顶部高度不能低于区域内的地表高" + maxHeight)
    fxEvent.fire("resetMaxHeight", { value: measureVolume.maxHeight })
    return
  }
  if (num < measureVolume.height) {
    $message("墙顶部高度不能低于基准面高")
    fxEvent.fire("resetMaxHeight", { value: measureVolume.maxHeight })
    return
  }
  measureVolume.maxHeight = num
}

export async function selHeight() {
  if (!measureVolume || !measure) {
    $message("请先绘制分析区域")
    return
  }

  // 拾取高度
  const graphic = await map.graphicLayer.startDraw({
    type: "point",
    style: {
      color: "#00fff2"
    }
  })
  const height = graphic.point?.alt
  map.graphicLayer.removeGraphic(graphic)

  if (!height) {
    return
  }

  measureVolume.height = height

  showHeightVal(measureVolume ? measureVolume.toJSON() : null)
}

// 显示mars3d.polygon.interPolygon处理后的面内插值分析结果，主要用于测试对比

// 显示面的插值计算结果，方便比较分析
// let graphicLayer
// 存储所有图形对象的Map，用于管理显示隐藏
// key: 索引，value: { points: [], polygon: primitivePoly, line: primitiveLine }
const graphicsMap = new Map<number, {
  points: mars3d.graphic.PointPrimitive[]
  polygon: mars3d.graphic.PolygonPrimitive
  line: mars3d.graphic.PolylinePrimitive
}>()

export function clearInterResult() {
  if (!graphicLayer) {
    map.addLayer(graphicLayer)
  }

  graphicLayer.clear()
  graphicsMap.clear()
}

// 清空未保存的所有内容
export function clearAll() {
  if (measureVolume) {
    measureVolume.destroy()
    measureVolume = undefined
  }
  clearInterResult()
}

// 取消点选高度绘制（组件销毁时清除鼠标绑定事件）
export function cancelSelHeightDraw() {
  if (map?.graphicLayer) {
    map.graphicLayer.clearDrawing()
  }
}

/**
 * 根据索引显示或隐藏对应的图形对象
 * @param index 图形索引
 * @param show true-显示，false-隐藏
 */
export function setGraphicVisible(index: number, show: boolean) {
  const graphics = graphicsMap.get(index)
  if (!graphics) { return }

  // 控制点的显示隐藏
  graphics.points.forEach(point => {
    point.show = show
  })

  // 控制面的显示隐藏
  if (graphics.polygon) {
    graphics.polygon.show = show
  }

  // 控制线的显示隐藏
  if (graphics.line) {
    graphics.line.show = show
  }
}

/**
 * 批量设置多个图形的显示隐藏
 * @param indices 图形索引数组
 * @param show true-显示，false-隐藏
 */
export function setBatchGraphicsVisible(indices: number[], show: boolean) {
  indices.forEach(index => {
    setGraphicVisible(index, show)
  })
}

/**
 * 统一控制所有图形的显示隐藏
 * @param show true-显示，false-隐藏
 */
export function setAllGraphicsVisible(show: boolean) {
  graphicsMap.forEach((graphics) => {
    // 控制点的显示隐藏
    graphics.points.forEach(point => {
      point.show = show
    })

    // 控制面的显示隐藏
    if (graphics.polygon) {
      graphics.polygon.show = show
    }

    // 控制线的显示隐藏
    if (graphics.line) {
      graphics.line.show = show
    }
  })
}

/**
 * 获取指定索引的图形对象
 * @param index 图形索引
 */
export function getGraphics(index: number) {
  return graphicsMap.get(index)
}

/**
 * 获取所有图形的数量
 */
export function getGraphicsCount() {
  return graphicsMap.size
}

function showInterResult(list) {
  // 分析结果用于测试分析的，不做太多处理，直接清除之前的，只保留一个
  // clearInterResult()

  let pt1, pt2, pt3
  // var geometryInstances = [];
  for (let i = 0, len = list.length; i < len; i++) {
    const item = list[i]

    pt1 = item.point1.pointDM
    pt2 = item.point2.pointDM
    pt3 = item.point3.pointDM

    // 存储当前索引的所有图形对象
    const currentGraphics: {
      points: mars3d.graphic.PointPrimitive[]
      polygon: mars3d.graphic.PolygonPrimitive
      line: mars3d.graphic.PolylinePrimitive
    } = {
      points: [],
      polygon: null as any,
      line: null as any
    }

    // 点
    for (const pt of [item.point1, item.point2, item.point3]) {
      const graphic = new mars3d.graphic.PointPrimitive({
        position: pt.pointDM,
        style: {
          pixelSize: 9,
          color: Cesium.Color.fromCssColorString("#ff0000").withAlpha(0.5)
        }
      })
      graphicLayer.addGraphic(graphic)

      graphic.bindTooltip("点高度:" + mars3d.MeasureUtil.formatDistance(pt.height))

      // 将点图形对象存入数组
      currentGraphics.points.push(graphic)
    }

    // 横截面面积
    item.area = item.area || mars3d.MeasureUtil.getTriangleArea(pt1, pt2, pt3)

    // 三角网及边线
    const positions = [pt1, pt2, pt3, pt1]

    // 三角网面（单击用）
    const primitivePoly = new mars3d.graphic.PolygonPrimitive({
      positions,
      style: {
        color: Cesium.Color.fromCssColorString("rgba(255, 255, 255, 1)").withAlpha(0.01)
      }
    })
    graphicLayer.addGraphic(primitivePoly)
    primitivePoly.bindTooltip("三角面积:" + mars3d.MeasureUtil.formatArea(item.area) + "(第" + i + "个)")

    // 存储面图形对象
    currentGraphics.polygon = primitivePoly

    // 三角网边线
    const primitiveLine = new mars3d.graphic.PolylinePrimitive({
      positions,
      style: {
        width: 1,
        color: Cesium.Color.fromCssColorString("rgba(255, 255, 0, 1)").withAlpha(0.3)
      }
    })
    graphicLayer.addGraphic(primitiveLine)

    // 存储线图形对象
    currentGraphics.line = primitiveLine

    // 将当前索引的所有图形对象存入Map
    graphicsMap.set(i, currentGraphics)
  }
}
