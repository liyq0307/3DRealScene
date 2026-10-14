import { useAnalysisStore } from "@mars/stores/analysis"
import * as mars3d from "mars3d"

export let map // mars3d.Map三维地图对象
let mapSplit
const analysisStore = useAnalysisStore()

export function onMounted(mapInstance: any) {
  map = mapInstance
}

// 释放当前地图业务的生命周期函数
export function onUnmounted() {
  map = null
}

export function commonControl(left: string, right: string, node?: any) {
  if (left && right) {
    createControl(left, right, node)
  }
}

export function createControl(leftUrl: string, rightUrl: string, node?: any) {
  destroyControl()

  console.log("创建MapSplit - leftUrl:", leftUrl, "rightUrl:", rightUrl, "node:", node)

  // 构建图层配置（复用layer-tree的配置方式）
  const buildLayerConfig = (url: string, name: string): any => ({
    id: name,
    name,
    type: "tileset", // 使用tileset而非3dtiles（与layer-tree一致）
    url,
    center: {
      lat: node?.latitude || map.getCenter().lat,
      lng: node?.longitude || map.getCenter().lng,
      alt: node?.sightheight || 0
    },
    editHeight: 0,
    flat: {
      precise: false,
      enabled: true,
      raise: false
    }
  })

  const leftLayerConfig = buildLayerConfig(leftUrl, "左侧模型")
  const rightLayerConfig = buildLayerConfig(rightUrl, "右侧模型")

  console.log("左侧图层配置:", leftLayerConfig)
  console.log("右侧图层配置:", rightLayerConfig)

  mapSplit = new mars3d.control.MapSplit({
    leftLayer: [leftLayerConfig],
    rightLayer: [rightLayerConfig],
    style: { zIndex: 1 }
  })
  map.addControl(mapSplit)

  console.log("MapSplit 控件已添加到地图", mapSplit)

  // 延迟检查内部图层状态
  setTimeout(() => {
    const leftLayers: any[] = (mapSplit as any).leftLayer || []
    const rightLayers: any[] = (mapSplit as any).rightLayer || []
    console.log("[MapSplit] 延迟检查 - leftLayer 数量:", leftLayers.length, "rightLayer 数量:", rightLayers.length)

    const checkLayer = (layer: any, label: string) => {
      if (!layer) { return }
      console.log(`[MapSplit] ${label}:`, {
        id: layer.id,
        type: layer.type,
        state: layer.state,
        show: layer.show,
        loaded: !!layer.tileset,
        center: layer.center ? { lng: layer.center.lng, lat: layer.center.lat, alt: layer.center.alt } : null
      })
      try {
        layer.on(mars3d.EventType.load, () => console.log(`[MapSplit] ${label} load 成功!`))
        layer.on("loadError" as any, (err: any) => console.error(`[MapSplit] ${label} 加载失败:`, err))
      } catch (_e) { /* ignore */ }
    }

    leftLayers.forEach((l, i) => checkLayer(l, `左侧图层[${i}]`))
    rightLayers.forEach((l, i) => checkLayer(l, `右侧图层[${i}]`))
  }, 3000)

  ;(mapSplit as any)?.on(mars3d.EventType.mouseMove, (_event: any) => {})

  // 检查是否已存在 jlzd_comparison 类型的数据
  const existingItem = analysisStore.getAnalysisSyncArray.find(
    (item: any) => item.analysisType === "jlzd_comparison"
  )

  if (existingItem) {
    existingItem.data.leftUrl = leftUrl
    existingItem.data.rightUrl = rightUrl
  } else {
    analysisStore.saveAnalysisDataToSelected({
      analysisType: "jlzd_comparison",
      data: {
        data: {},
        analysisType: "jlzd_comparison",
        shijiao: map.getCameraView(),
        leftUrl,
        rightUrl
      }
    })
  }
}

export function destroyControl() {
  if (mapSplit) {
    try {
      if (map) {
        map.removeControl(mapSplit)
      } else {
        mapSplit.destroy()
      }
    } catch (e) {
      console.warn("销毁卷帘控件时出错:", e)
    }
    mapSplit = null
  }
}
