import { map, eventTarget as fxEvent } from "@mars/widgets/analysis/map"
import * as mars3d from "mars3d"
const Cesium = mars3d.Cesium
// 绘制矩形
export async function drawRectangle() {
  // console.log("绘制矩形")
  const drawGraphic = await map.graphicLayer.startDraw({
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
  const drawGraphic = await map.graphicLayer.startDraw({
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
  const drawGraphic = await map.graphicLayer.startDraw({
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

// 取消绘制（组件销毁时清除鼠标绑定事件）
export function cancelDrawing() {
  if (map?.graphicLayer) {
    map.graphicLayer.clearDrawing()
  }
}
