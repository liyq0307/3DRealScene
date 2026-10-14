import { measure, map, eventTarget as fxEvent, graphicLayer } from "@mars/widgets/analysis/map"
import * as mars3d from "mars3d"

export async function startDraw() {
  measure.areaSurface({
    style: {
      color: "rgba(255, 255, 0, 1)"
    },
    label: {
      font_family: "楷体",
      font_size: 20,
      color: "rgba(255, 255, 255, 1)",
      background: false
    },
    splitNum: 10, // step插值分割的个数
    exact: true, // 是否进行精确计算，传true时使用精确计算方式，精度更高
    success: function (graphic) {
      // 标绘完成
      fxEvent.fire("drawEnd", { extent: { type: "rjl", shijiao: map.getCameraView(), graphic: graphic.toJSON(), graphicInstance: graphic } })
    }
  })


}


export function addPoint(position?: any, sj?: any) {

  const graphic = new mars3d.graphic.PointEntity({
    position,
    style: {
      color: "rgba(0, 255, 0, 1)",
      scale: 8,
      diffHeight: 0.01
    }

  })

  map.graphicLayer.addGraphic(graphic)
  initGraphicManager(graphic, sj)
  return graphic
}

function initGraphicManager(graphic, sj) {
  // 绑定Popup（点击时显示）
  const inthtml = `<table style="width: auto;">
            <tr>
              <th scope="col" colspan="2" style="text-align:center;font-size:15px;z-index:1">容积率为：${sj}</th>
            </tr>
          </table>`
  graphic.bindPopup(inthtml, { closeButton: false, closeOnClick: false }).openPopup()

  return graphic.toJSON()
}

// 取消面积测量绘制（组件销毁时清除鼠标绑定事件）
export function cancelDraw() {
  if (measure) {
    measure.clear()
  }
}
