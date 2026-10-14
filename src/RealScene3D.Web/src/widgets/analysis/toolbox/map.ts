import { map, sightline, skyline, eventTarget as fxEvent, measure } from "@mars/widgets/analysis/map"

import { clearInterResult } from "../tools/cut-fill/map"
import { clearBuildFlat } from "../tools/swipe/map"
import { getContourManager } from "../tools/contour/map"
import { pipelineManager } from "../tools/pipeline/map"
import { destroyControl } from "../tools/building-comparison/map"
import { removeModel } from "../tools/site-selection/map"

// 下载场景出图
export function downloadMapImg(options = {}) {
  return map.expImage()
}

// 清除所有标绘
export function clearDrawings() {
  // 清除基础绘制图层(点、线、面、圆等)
  // clearBasicDrawings()

  // 清除矢量数据图层
  if (map.graphicLayer) {
    map.graphicLayer.clear()
  }

  if (map.lineLayer) {
    map.lineLayer.clear()
  }

  // 清空基础绘制组件中的drawings值
  fxEvent.fire("clearBasicDrawings")
  clearInterResult()

  // 清除建筑压平
  clearBuildFlat()

  // 清除测量结果
  if (measure) {
    measure.clear()
  }

  // 清除通视线
  if (sightline) {
    sightline.clear()
  }

  // 清除天际线
  if (skyline) {
    skyline.enabled = false
  }


  // 清除等高线

  if (getContourManager()) {
    getContourManager().clear()
  }

  // 清除管线数据
  if (pipelineManager) {
    pipelineManager.clear()
    // analysisStore.clearGxfxPipeDataFromStore()
  }

  // 清除卷帘数据
  destroyControl()
  return true
}

// 清除所有内容（包括标绘、历史记录和store）
export function clearAll() {
  // 清除所有标绘
  clearDrawings()

  // 清除在线选址模型
  removeModel()

  // 触发清除所有store数据的事件
  fxEvent.fire("clearAllStoreData")

  return true
}


