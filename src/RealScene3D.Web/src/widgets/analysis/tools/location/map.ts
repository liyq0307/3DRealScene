import * as mars3d from "mars3d"
import { map, eventTarget as fxEventTarget } from "../../map"
import { useAnalysisStore } from "@mars/stores/analysis"
// mars3d.Map城市规划管理应用地图对象
export const eventTarget = new mars3d.BaseClass() // 事件对象，用于抛出事件到面板中
const store = useAnalysisStore() // 分析数据存储对象
// 坐标转化的三种方法
export function marsUtilFormtNum(item, num) {
  return mars3d.Util.formatNum(item, num)
}
// 度分秒转化的方法
export function marsPointTrans(item) {
  return mars3d.PointTrans.degree2dms(item)
}

// 转换成十进制的方法
export function marsDms2degree(du, fen, miao) {
  return mars3d.PointTrans.dms2degree(du, fen, miao)
}

// 地图选点
export function bindMourseClick() {
  map.setCursor("crosshair")
  map.off(mars3d.EventType.click, map_onclick)
  map.once(mars3d.EventType.click, map_onclick)
}

function map_onclick(event) {
  map.setCursor("")
  const cartesian = event.cartesian
  const point = mars3d.LngLatPoint.fromCartesian(cartesian)
  point.format() // 经度、纬度、高度

  // 控制台打印从图上拾取到的点坐标
  // console.log("从图上拾取到的点坐标:", point)

  eventTarget.fire("clickMap", { point })
}

export function updateMarker(hasCenter, jd, wd, alt) {
  const position = [jd, wd, alt || 0.05]

  const pointEntity = new mars3d.graphic.BillboardEntity({
    position,
    style: {
      image: "/img/marker/lace-blue.png",
      visibleDepth: false
      // clampToGround: true
    }
  })
  const cameraView = map.getCameraView()
  map.graphicLayer.addGraphic(pointEntity)
  store.saveAnalysisDataToSelected({
    analysisType: "zbdw_point",
    data: { data: pointEntity.toJSON(), analysisType: "zbdw_point", shijiao: cameraView }
  })

  if (hasCenter) {
    pointEntity.flyTo({ radius: 1000 })
  }
}
