import { map, eventTarget as fxEvent } from "@mars/widgets/analysis/map"
import * as mars3d from "mars3d"
const Cesium = mars3d.Cesium
// 当前绘制的图形
let currentGraphic: any = null
// 绘制矩形
export async function drawRectangle() {
  // console.log("绘制矩形")
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
      fxEvent.fire("drawEnd", {
        value: JSON.stringify({
          graphic: graphic.toJSON(),
          shijiao: map.getCameraView(),
          drawType: "sqare",
          type: "xzfx"
        })
      })
    }
  })
}
// 绘制圆
export async function drawCircle() {
  await map.graphicLayer.startDraw({
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
        value: JSON.stringify({
          graphic: graphic.toJSON(),
          shijiao: map.getCameraView(),
          drawType: "circle",
          type: "xzfx"
        })
      })
    }
  })
}
// 绘制多边行
export async function drawPolygon() {
  await map.graphicLayer.startDraw({
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
        value: JSON.stringify({
          graphic: graphic.toJSON(),
          shijiao: map.getCameraView(),
          drawType: "sqare",
          type: "xzfx"
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


// 取消矩形绘制（组件销毁时清除鼠标绑定事件）
export function cancelDrawExtent() {
  if (map?.graphicLayer) {
    map.graphicLayer.clearDrawing()
  }
}
