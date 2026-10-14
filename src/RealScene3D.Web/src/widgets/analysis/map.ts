import * as mars3d from "mars3d"
import { $hideLoading, $showLoading } from "@mars/components/mars-ui"
import { clearInterResult } from "./tools/cut-fill/map"
import { onDestroy as destroyDgx } from "./tools/contour/map"

export let map // mars3d.Map 城市规划管理应用地图对象
export let point
export let measure
export let graphicLayer // 矢量图层对象，用于 graphic 绑定展示
export let lineLayer // 矢量图层对象，用于 graphic 绑定展示
export let zxxzGraphicLayer // 矢量图层对象，用于 graphic 绑定展示
export let sightline
export let skyline
export let shadows

const Cesium = mars3d.Cesium
// 事件对象，用于接收 zbdw 组件的点位数据
export const eventTarget = new mars3d.BaseClass()

// 事件对象，用于抛出事件给 vue
export function onMounted(mapInstance) {
  map = mapInstance // 记录 map

  // 坐标定位 - 坐标拾取与转换
  point = map?.getCenter()
  point.format()

  // 创建测量工具
  measure = new mars3d.thing.Measure({
    label: {
      color: "rgba(255, 255, 255, 1)",
      font_family: "楷体",
      font_size: 20,
      background: false
    },
    isAutoEditing: false // 绘制完成后是否自动激活编辑
  })
  map.addThing(measure)

  measure.on(mars3d.EventType.start, function (e) {
    clearInterResult()
    $showLoading()
    // 抛出测量开始事件
    eventTarget.fire("measureStart", e)
  })
  measure.on(mars3d.EventType.end, function (e) {
    $hideLoading()
    // 抛出测量完成事件，用于取消选中样式
    eventTarget.fire("measureEnd", e)
  })

  // 创建矢量数据图层
  graphicLayer = new mars3d.layer.GraphicLayer({
    isAutoEditing: true
  })
  map.addLayer(graphicLayer)

  // 监听图层上的编辑事件，用于同步属性到 store
  graphicLayer.on(mars3d.EventType.editStyle, (e: any) => {
    eventTarget.fire("graphicEditorUpdate", e)
  })
  graphicLayer.on(mars3d.EventType.editMovePoint, (e: any) => {
    eventTarget.fire("graphicEditorUpdate", e)
  })
  graphicLayer.on(mars3d.EventType.editRemovePoint, (e: any) => {
    eventTarget.fire("graphicEditorUpdate", e)
  })
  // 创建矢量数据图层
  lineLayer = new mars3d.layer.GraphicLayer()
  map.addLayer(lineLayer)

  // 在线选址 GraphicLayer
  zxxzGraphicLayer = new mars3d.layer.GraphicLayer({
    isAutoEditing: true // 绘制完成后是否自动激活编辑
  })
  map.addLayer(zxxzGraphicLayer)

  // 在 layer 上绑定监听事件
  zxxzGraphicLayer.on(mars3d.EventType.click, function (event) {
    console.log("监听 layer，单击了矢量对象", event)
  })

  zxxzGraphicLayer.on(mars3d.EventType.drawCreated, function(event) {
    console.log("在线选址 - 绘制完成", event)
  })

  // 监听 zxxzGraphicLayer 上的编辑事件，用于同步编辑后的属性到 store
  zxxzGraphicLayer.on(mars3d.EventType.editStyle, (e: any) => {
    console.log("zxxzGraphicLayer-editStyle", e)
    eventTarget.fire("zxxzGraphicEditorUpdate", { graphic: e.graphic })
  })
  zxxzGraphicLayer.on(mars3d.EventType.editStop, (e: any) => {
    console.log("zxxzGraphicLayer-editStop", e)
    eventTarget.fire("zxxzGraphicEditorUpdate", { graphic: e.graphic })
  })

  sightline = new mars3d.thing.Sightline({
    visibleColor: new Cesium.Color(0, 1, 0, 1),
    hiddenColor: new Cesium.Color(1, 0, 0, 1),
    depthFailColor: new Cesium.Color(1, 0, 0, 1)
  })

  map.addThing(sightline)

  sightline.on(mars3d.EventType.end, function (e) {
    // console.log("结束视域分析", sightline.toJSON())
  })

  // 天际线
  skyline = new mars3d.thing.Skyline({
    enabled: false
  })
  map.addThing(skyline)
  map.scene.globe.depthTestAgainstTerrain = true

  shadows = new mars3d.thing.Shadows({
    darkness: 0.4,
    enabled: false,
    multiplier: 1600,
    terrain: false,
    lighting: false
  })
  map.addThing(shadows)

  // 监听阴影时间变化，触发事件更新 UI
  shadows.on(mars3d.EventType.change, function (e) {
    eventTarget.fire("changeShadows", { shadowTime: shadows.time })
  })
}

// 释放当前地图业务
export function onUnmounted() {
  if (!map) {
    return
  }

  if (sightline) {
    sightline.clear()
    map.removeThing(sightline)
    sightline = null
  }

  if (skyline) {
    map.removeThing(skyline)
    skyline = null
  }

  if (shadows) {
    map.removeThing(shadows)
    shadows = null
  }

  if (measure) {
    map.removeThing(measure)
    measure = null
  }

  if (graphicLayer) {
    graphicLayer.clear()
    map.removeLayer(graphicLayer)
    graphicLayer = null
  }

  if (zxxzGraphicLayer) {
    zxxzGraphicLayer.clear()
    map.removeLayer(zxxzGraphicLayer)
    zxxzGraphicLayer = null
  }

  if (lineLayer) {
    lineLayer.clear()
    map.removeLayer(lineLayer)
    lineLayer = null
  }

  destroyDgx()

  map = null
  point = null
}

// 创建图层
export function createLayer(layer: any) {
  return mars3d.LayerUtil.create(layer)
}
// 添加图层
export function addLayer(layer: mars3d.layer.BaseLayer) {
  map.addLayer(layer)
}
// 移除图层
export function removeLayer(layer: any) {
  map.removeLayer(layer)
}
