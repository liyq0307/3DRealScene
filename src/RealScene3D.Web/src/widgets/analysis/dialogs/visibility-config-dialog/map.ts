import { map } from "@mars/widgets/analysis/map"

// 当前选中的可视域图形
let selectedView: any = null

// 设置当前选中的可视域图形
export function setGraphic(graphic: any) {
  selectedView = graphic
  return selectedView
}

// 获取当前选中的可视域图形
export function getGraphic() {
  return selectedView
}

// 点选相机位置
export async function selCamera() {
  if (!selectedView) {
    return
  }

  const graphic = await map.graphicLayer.startDraw({
    type: "point"
  })
  const point = graphic.point
  graphic.remove() // 删除绘制的点

  selectedView.position = point
}

// 修改水平角度
export function onChangeAngle(value: number) {
  if (selectedView) {
    selectedView.angle = value
  }
}

// 修改垂直角度
export function onChangeAngle2(value: number) {
  if (selectedView) {
    selectedView.angle2 = value
  }
}

// 修改投射距离
export function onChangeDistance(value: number) {
  if (selectedView) {
    selectedView.distance = value
  }
}

// 修改四周方向
export function onChangeHeading(value: number) {
  if (selectedView) {
    selectedView.heading = value
  }
}

// 四周视角选点
export async function onClickSelView() {
  if (!selectedView) {
    return
  }

  const graphic = await map.graphicLayer.startDraw({
    type: "point"
  })
  const point = graphic.point
  graphic.remove() // 删除绘制的点

  selectedView.targetPosition = point
}

// 修改俯仰角
export function onChangePitch(value: number) {
  if (selectedView) {
    selectedView.pitch = value
  }
}

// 线框是否显示
export function showFrustum(isChecked: boolean) {
  if (selectedView) {
    selectedView.showFrustum = isChecked
  }
}

// 修改视频透明度
export function onChangeOpacity(opacity: number) {
  if (selectedView) {
    selectedView.opacity = opacity
  }
}

// 获取图层管理器
export function getManagerLayer() {
  return map.graphicLayer
}
