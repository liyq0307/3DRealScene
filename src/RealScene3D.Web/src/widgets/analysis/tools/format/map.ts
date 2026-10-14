import { map, eventTarget as fxEvent } from "@mars/widgets/analysis/map"
import * as mars3d from "mars3d"
const Cesium = mars3d.Cesium
// 当前绘制的图形
let currentGraphic: any = null
// 绘制矩形
export async function drawRectangle() {
  console.log("绘制矩形")
  map.graphicLayer.startDraw({
    type: "rectangle",
    style: {
      color: "#00ffff",
      opacity: 0.3,
      outline: true,
      outlineWidth: 2,
      outlineColor: "rgba(255, 255, 255, 1)",
      clampToGround: true,
      classificationType: Cesium.ClassificationType.BOTH
    },
    success: function (graphic) {
      currentGraphic = graphic
      console.log("绘制完成", graphic)
      fxEvent.fire("drawEnd", {
        analysisType: "ytfx_analysis",
        value: JSON.stringify({
          graphic: graphic.toJSON(),
          shijiao: map.getCameraView()
        })
      })
    }
  })
}
// 绘制圆
export function drawCircle() {
  map.graphicLayer.startDraw({
    type: "circle",
    style: {
      color: "#00ffff",
      opacity: 0.3,
      outline: true,
      outlineWidth: 2,
      outlineColor: "rgba(255, 255, 255, 1)",
      clampToGround: true,
      classificationType: Cesium.ClassificationType.BOTH
    },
    success: function (graphic) {
      currentGraphic = graphic
      fxEvent.fire("drawEnd", {
        analysisType: "ytfx_analysis",
        value: JSON.stringify({
          graphic: graphic.toJSON(),
          shijiao: map.getCameraView()
        })
      })
    }
  })
}
// 绘制多边行
export function drawPolygon() {
  map.graphicLayer.startDraw({
    type: "polygon",
    style: {
      color: "#00ffff",
      opacity: 0.3,
      outline: true,
      outlineWidth: 2,
      outlineColor: "rgba(255, 255, 255, 1)",
      clampToGround: true,
      classificationType: Cesium.ClassificationType.BOTH
    },
    success: function (graphic) {
      currentGraphic = graphic
      fxEvent.fire("drawEnd", {
        analysisType: "ytfx_analysis",
        value: JSON.stringify({
          graphic: graphic.toJSON(),
          shijiao: map.getCameraView()
        })
      })
    }
  })
}
// 清除当前绘制的图形
export function clearGraphic() {
  if (currentGraphic) {
    map.graphicLayer.removeGraphic(currentGraphic)
    currentGraphic = null
  }
}

// 取消绘制（组件销毁时清除鼠标绑定事件）
export function cancelDrawing() {
  if (map?.graphicLayer) {
    map.graphicLayer.clearDrawing()
  }
}
