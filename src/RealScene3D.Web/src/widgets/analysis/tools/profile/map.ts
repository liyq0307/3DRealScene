import { map, measure, eventTarget as fxEvent } from "@mars/widgets/analysis/map"

import * as mars3d from "mars3d"
export function measureSection() {
  measure.section({
    // maxPointNum:2,
    splitNum: 100, // 插值次数 - 优化性能,从300降到100
    exact: false, // 是否进行精确计算， 传false时是否快速概略计算方式，该方式计算精度较低，但计算速度快，仅能计算在当前视域内坐标的高度
    // style: {
    //   clampToGround: false // 是否贴地
    // }
    label: {
      font_family: "楷体",
      font_size: 20,
      color: "rgba(255, 255, 255, 1)",
      background: false
    },
    success: function (graphic) {
      // 标绘完成
      fxEvent.fire("drawEnd", {
        analysisType: "pmfx_analysis",
        value: JSON.stringify({ analysisType: "pmfx_analysis", shijiao: map.getCameraView(), graphic: graphic.toJSON() })
      })
    }
  })

}


let tipGraphic
/**
 *  echart图表中的图标
 *
 * @export
 * @param {Array} point 坐标点
 * @param {number} z 海拔高度
 * @param {html} inthtml html
 * @returns {void}
 */
// export function showTipMarker(point, z, inthtml) {
//   const _position_draw = Cesium.Cartesian3.fromDegrees(point.lng, point.lat, z)

//   if (!tipGraphic) {
//     tipGraphic = new mars3d.graphic.BillboardEntity({
//       name: "当前点",
//       position: _position_draw,
//       style: {
//         image: "https://data.mars3d.cn/img/marker/mark-blue.png",
//         scale: 1,
//         horizontalOrigin: Cesium.HorizontalOrigin.CENTER,
//         verticalOrigin: Cesium.VerticalOrigin.BOTTOM,
//         scaleByDistance: new Cesium.NearFarScalar(10000, 1.0, 500000, 0.2)
//       }
//     }).addTo(map.graphicLayer)
//     tipGraphic._setPositionsToCallback()
//   }
//   tipGraphic._position_draw = _position_draw
//   tipGraphic.bindPopup(inthtml).openPopup()
// }

export function hideTipMarker() {
  if (!tipGraphic) {
    return
  }
  tipGraphic.remove(true)
  tipGraphic = null
}

// 取消剖面测量绘制（组件销毁时清除鼠标绑定事件）
export function cancelMeasureSection() {
  if (measure) {
    measure.clear()
  }
}

export function calculation(params) {
  const len = mars3d.MeasureUtil.formatDistance(Number(params.axisValue))
  const hbgdStr = mars3d.MeasureUtil.formatDistance(Number(params.value))

  return { len, hbgdStr }
}
