import { $message } from "@mars/components/mars-ui"
import { map, lineLayer, eventTarget as fxEvent } from "@mars/widgets/analysis/map"
import { tilesetLayer } from "@mars/widgets/analysis/layer-tree/map"
import * as mars3d from "mars3d"

import { useAnalysisStore } from "@mars/stores/analysis"
const store = useAnalysisStore() // 分析数据存储对象
// 保存当前所有压平区域的信息
let currentFlatAreas: any[] = []
// 保存线的ID与area的ID的对应关系，用于删除时能找到对应的线
const lineIdToAreaIdMap: Map<string, string> = new Map()
// 标记是否正在进行高度更新（防止 onAddFlatArea 事件重复创建线）
let isUpdatingHeight = false

// 拾取当前地形高度
export const pickTerrainHeight = (): Promise<number | null> => {
  return new Promise((resolve) => {
    $message("请点击地图获取当前地形高度")
    map.graphicLayer.startDraw({
      type: "point",
      style: {
        color: "#ff0000",
        pixelSize: 10
      },
      success: (graphic: any) => {
        const pos = graphic.positionShow || graphic.position
        let cartesianPos = pos
        if (pos && typeof pos.getValue === "function") {
          cartesianPos = pos.getValue(mars3d.Cesium.JulianDate.now())
        }
        map.graphicLayer.removeGraphic(graphic)
        if (cartesianPos) {
          const terrainHeight = mars3d.PointUtil.getHeight(map.scene, cartesianPos)
          resolve(terrainHeight)
        } else {
          resolve(null)
        }
      },
      cancel: () => {
        resolve(null)
      }
    })
  })
}

// 建筑压平
export const startBuildFlat = async (flatHeight) => {
  if (!tilesetLayer || !tilesetLayer?.flat) {
    return $message("目前仅支持3dtileset图层的压平")
  }
  let json, jsonPositon
  // 开始绘制压平区域
  const graphic = await map.graphicLayer.startDraw({
    type: "polygon",
    style: {
      color: "#007be6",
      opacity: 0.5
    },
    success: function (graphic) {
      json = graphic.toJSON()
      jsonPositon = json.positions
    }
  })
  const positions = graphic.positionsShow
  map.graphicLayer.removeGraphic(graphic)
  // 添加压平区域
  const area = tilesetLayer.flat.addArea(positions, {
    height: flatHeight,
    color: "#007be6",
    editHeight: 0
  })

  let lineId = area.lineId
  if (!lineId) {
    lineId = addTestLine(positions)
    lineIdToAreaIdMap.set(lineId, area.id)
  }

  // 保存当前压平区域的信息
  const flatAreaInfo = {
    id: area.id,
    positions,
    flatHeight,
    lineId,
    editHeight: 0
  }
  currentFlatAreas.push(flatAreaInfo)

  // 保存分析数据
  store.saveAnalysisDataToSelected({
    analysisType: "yp_ground",
    data: {
      data: json,
      analysisType: "yp_ground",
      flatHeight,
      currentFlatArea: flatAreaInfo,
      lineId,
      shijiao: map.getCameraView()
    }
  })
}

export function onAddFlatArea(event) {
  // 高度更新期间的 addArea 不需要创建线（线已存在，只需重建压平区域）
  if (isUpdatingHeight) {
    return
  }

  const areaObj = event.area

  // 检查是否已存在该区域的线，避免重复创建
  const existingLineId = Array.from(lineIdToAreaIdMap.entries()).find(([_, areaId]) => areaId === areaObj.id)?.[0]
  if (existingLineId) {
    // 已存在线，直接使用
    areaObj.lineId = existingLineId
  } else {
    // 不存在线，创建新线
    const newLineId = addTestLine(areaObj.positions)
    areaObj.lineId = newLineId
    // 建立映射关系
    lineIdToAreaIdMap.set(newLineId as string, areaObj.id)
  }

  // 触发自定义事件 addItem
  fxEvent.fire("addItem", event)
}
function addTestLine(positions) {
  const graphic = new mars3d.graphic.PolylineEntity({
    positions,
    style: {
      closure: true,
      color: "rgba(255, 255, 255, 1)",
      opacity: 0.8,
      width: 2,
      clampToGround: true
    }
  })
  lineLayer.addGraphic(graphic)
  return graphic.id
}

// 清除建筑压平
export function clearBuildFlat() {
  map.graphicLayer.clear()
  tilesetLayer?.flat?.clear()
  // 清除所有当前压平区域的信息
  currentFlatAreas = []
  // 清除 lineId 到 areaId 的映射
  lineIdToAreaIdMap.clear()
  lineLayer.clear()
}

// 取消建筑压平绘制（组件销毁时调用）
export function cancelBuildFlat() {
  // 取消正在进行的绘制操作，清除鼠标绑定事件
  if (map.graphicLayer) {
    map.graphicLayer.clearDrawing()
  }
}

// 根据ID从currentFlatAreas中移除指定压平区域（删除记录时调用，防止updateFlatHeight重建已删除区域）
export function removeFlatAreaById(areaId: string) {
  const index = currentFlatAreas.findIndex(item => item.id === areaId)
  if (index !== -1) {
    // 同时清除 lineId 到 areaId 的映射
    const flatArea = currentFlatAreas[index]
    if (flatArea.lineId) {
      lineIdToAreaIdMap.delete(flatArea.lineId)
    }
    currentFlatAreas.splice(index, 1)
  }
}

// 更新建筑压平区域的高度
export async function updateFlatHeight(flatHeight: number, areaId?: string) {
  // 如果指定了areaId，只更新对应的压平区域；否则更新全部
  const targets = areaId
    ? currentFlatAreas.filter(item => item.id === areaId)
    : [...currentFlatAreas]

  if (targets.length === 0) {
    console.warn("没有当前压平区域")
    return
  }

  // 收集新旧id映射，用于后续同步store中可能脱离引用的数据
  const updatedIdMap = new Map<string, string>()

  // 设置标志，防止 onAddFlatArea 事件在 addArea 时重复创建线
  isUpdatingHeight = true
  try {
    for (const targetArea of targets) {
      // 检查是否真的需要更新
      if (targetArea.flatHeight === flatHeight) {
        continue
      }

      // 保存旧的id和lineId
      const oldAreaId = targetArea.id
      const oldLineId = targetArea.lineId

      // 移除旧的压平区域
      tilesetLayer.flat.removeArea(targetArea.id)

      // 更新高度参数
      targetArea.flatHeight = flatHeight

      // 重新添加压平区域（onAddFlatArea 会被触发但因 isUpdatingHeight 跳过）
      const area = tilesetLayer.flat.addArea(targetArea.positions, {
        height: flatHeight,
        color: "#007be6"
      })

      // 更新压平区域ID
      targetArea.id = area.id

      // 记录新旧id映射
      if (oldAreaId) {
        updatedIdMap.set(oldAreaId, area.id)
      }

      // 同步更新 lineId 到 areaId 的映射关系，确保删除/隐藏时能正确关联
      if (oldLineId) {
        lineIdToAreaIdMap.set(oldLineId, area.id)
      }
    }
  } finally {
    isUpdatingHeight = false
  }

  // 同步更新store中yp_ground类型的所有相关字段
  store.analysisSyncArray.forEach((item: any) => {
    if (item.analysisType === "yp_ground" && item.data) {
      item.data.flatHeight = flatHeight
      if (item.data.currentFlatArea) {
        item.data.currentFlatArea.flatHeight = flatHeight
        // 同步更新重建后的新id（防止store深拷贝导致引用断开）
        const newId = updatedIdMap.get(item.data.currentFlatArea.id)
        if (newId) {
          item.data.currentFlatArea.id = newId
        }
        // 同步更新 lineId
        if (item.data.currentFlatArea.lineId) {
          item.data.lineId = item.data.currentFlatArea.lineId
        }
      }
    }
  })
}

