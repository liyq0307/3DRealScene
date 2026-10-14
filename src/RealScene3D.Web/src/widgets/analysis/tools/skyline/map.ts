import { skyline } from "@mars/widgets/analysis/map"
import * as mars3d from "mars3d"

// 天际线修改颜色
export function changeColor() {
  skyline.color = mars3d.Cesium.Color.fromRandom()
  return skyline.color
}

// 天际线宽度
export function lineWidth(val) {
  skyline.width = val
  return skyline.width
}


export function isVChecked(value) {
  skyline.enabled = value
  return skyline.enabled
}
