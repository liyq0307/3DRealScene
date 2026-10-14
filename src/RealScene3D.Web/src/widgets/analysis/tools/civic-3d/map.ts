import { map, graphicLayer, eventTarget as fxEvent } from "@mars/widgets/analysis/map"
import * as mars3d from "mars3d"
import roadPic from "@mars/assets/imgs/road.jpg"
// 导入DWG文件

// 道路铺设
export async function drawRoad() {
  const graphic = await map.graphicLayer.startDraw({
    type: "road",
    style: {
      image: roadPic,
      width: 15,
      height: 1
    },
    attr: {
      type: "dlps"
    }
  })
  // console.log("标绘完成", graphic.toJSON())
  return graphic.toJSON()
}

// 导入道路
export async function setDlpsPositions(positions: any) {
  const road = new mars3d.graphic.Road({
    positions,
    style: {
      image: roadPic,
      width: 15,
      height: 1
    },
    attr: {
      type: "dlps"
    }
  })
  map.graphicLayer.addGraphic(road)
  if (road) {
    road.flyTo({
      radius: 1000,
      pitch: -45
    })
  }
  return { road: road.toJSON(), sj: { lat: positions[0][1], lng: positions[0][0], alt: positions[0][2], heading: 0, pitch: -45 } }
}

export const clearRoad = () => {
  const graphics = map.graphicLayer.getGraphics()
  graphics.forEach((graphic: any) => {
    if (graphic.type === "road" || graphic.attr?.type === "dlps") {
      map.graphicLayer.removeGraphic(graphic)
    }
  })
}

// 取消道路绘制（组件销毁时清除鼠标绑定事件）
export function cancelDrawRoad() {
  if (map?.graphicLayer) {
    map.graphicLayer.clearDrawing()
  }
}
