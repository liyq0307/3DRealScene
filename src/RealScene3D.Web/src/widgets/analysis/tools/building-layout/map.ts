import * as mars3d from "mars3d"
import { measure, graphicLayer, eventTarget, map } from "@mars/widgets/analysis/map"

const Cesium = mars3d.Cesium

// ==================== 全局变量 ====================
const terrainFlat: mars3d.thing.TerrainFlat | null = null // 地形平坦化工具
// export let measure: mars3d.thing.Measure | null = null // 测量工具
export let isEditing = false // 是否正在编辑建筑体块


// ==================== 绘制功能 ====================

/**
 * 开始绘制地块区域
 * 用户在地图上点击绘制多边形区域，绘制完成后触发 drawEnd 事件
 */
export async function startDrawingPolygon() {
  const options: any = {
    type: "polygon",
    style: {
      color: "#29cf34", // 填充颜色（绿色）
      opacity: 0.5, // 填充透明度
      outline: true, // 显示边框
      outlineWidth: 3, // 边框宽度
      outlineColor: "#29cf34", // 边框颜色（绿色）
      clampToGround: true, // 贴地显示
      classificationType: Cesium.ClassificationType.BOTH, // 同时分类地形和3D Tiles
      label: {
        color: "rgba(255, 255, 255, 1)",
        distanceDisplayCondition: true,
        distanceDisplayCondition_far: 500000,
        distanceDisplayCondition_near: 0
      }
    },
    success: (graphic: any) => {
      // 标记地块所属模块，用于区分不同模块的绘制内容
      graphic.attr = { ...graphic.attr, analysisType: "jzbj" }
      // 标绘完成，触发事件传递数据到面板
      eventTarget.fire("drawEnd", {
        value: {
          shijiao: map.getCameraView(), // 当前相机视角
          geoJson: graphic.toGeoJSON(), // GeoJSON 格式的几何数据
          graphics: graphic.toJSON(), // 图形 JSON 数据
          graphic // 图形对象
        }
      })
    }
  }
  await map.graphicLayer.startDraw(options)

  // // 将绘制区域的地形展平，便于建筑贴地显示
  // const positions = graphic.positionsShow
  // terrainFlat.addArea(positions, { height: 0 })
}

/**
 * 清除已绘制的地块
 */
export function clearDrawing() {
  measure?.clear()
}

/**
 * 取消绘制（组件销毁时清除鼠标绑定事件）
 */
export function cancelDrawing() {
  if (map?.graphicLayer) {
    map.graphicLayer.clearDrawing()
  }
}

/**
 * 清除之前绘制的地块，保留当前绘制的地块
 * @param currentGraphic - 当前绘制的地块图形对象
 */
export function clearPreviousDrawings(currentGraphic: any) {
  if (!measure) {
    return
  }

  // 获取所有绘制的地块
  // @ts-ignore - getGraphics 方法在运行时可用
  const graphics = measure.getGraphics ? measure.getGraphics() : measure.graphics

  if (!graphics || graphics.length === 0) {
    return
  }

  // 遍历并删除除当前地块外的所有地块
  graphics.forEach((graphic: any) => {
    if (graphic !== currentGraphic) {
      // @ts-ignore - removeGraphic 方法在运行时可用
      if (typeof measure.removeGraphic === "function") {
        measure.graphicLayer.removeGraphic(graphic)
      }
    }
  })
}

// ==================== 建筑体块功能 ====================

/**
 * 创建贴地的建筑体块
 * @param buildingLng - 建筑经度
 * @param buildingLat - 建筑纬度
 * @param buildingTerrainHeight - 建筑所在位置的地面高度
 * @param singleSideLength - 单栋建筑边长（正方形）
 * @param randomBuildingHeight - 建筑高度
 * @param actualBuildingIndex - 建筑索引
 * @param singleBaseArea - 单栋建筑基底面积
 * @returns 建筑体块的 JSON 数据
 */
export async function createBuilding(
  buildingLng: number,
  buildingLat: number,
  buildingTerrainHeight: number,
  singleSideLength: number,
  randomBuildingHeight: number,
  actualBuildingIndex: number,
  singleBaseArea: number
) {
  const buildingPrimitive = new mars3d.graphic.BoxEntity({
    position: [buildingLng, buildingLat, buildingTerrainHeight || 0], // 使用各建筑的地面高度
    style: {
      dimensions: new Cesium.Cartesian3(singleSideLength, singleSideLength, randomBuildingHeight), // 长、宽、高
      color: "rgba(255,200,100,0.7)", // 橙黄色半透明
      outline: true, // 显示边框
      outlineColor: "#ff9900",
      outlineWidth: 1,
      clampToGround: true // 贴地
    },
    hasEdit: true, // 开启编辑功能
    attr: {
      // id: `building_${actualBuildingIndex}`, // 建筑唯一标识
      baseArea: singleBaseArea, // 基底面积
      floorArea: singleBaseArea * randomBuildingHeight // 建筑面积 = 基底面积 × 高度
    }
  })

  map.graphicLayer.addGraphic(buildingPrimitive)

  return buildingPrimitive.toJSON()
}

/**
 * 清空已有建筑体块
 * 如果有建筑正在编辑，先停止编辑再清除
 * 注意：只清除建筑体块，不清除地块
 */
export function clearBuildings() {
  if (!map.graphicLayer) {
    return
  }

  // 如果有建筑正在编辑，先停止编辑
  if (isEditing) {
    map.graphicLayer.stopEditing()
    isEditing = false
  }

  // 只清除建筑体块（BoxEntity类型），不清除地块（polygon类型）
  const graphics = map.graphicLayer.getGraphics()
  if (!graphics || graphics.length === 0) {
    return
  }

  // 筛选出建筑体块并清除
  graphics.forEach((graphic: any) => {
    // BoxEntity 是建筑体块，polygon 是地块
    if (graphic.type === "box" || graphic instanceof mars3d.graphic.BoxEntity) {
      map.graphicLayer.removeGraphic(graphic)
    }
  })
}
