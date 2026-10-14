<template>
  <div class=" toolbox-container-form toolbox">
    <!-- 绘图工具栏 -->
    <a-form ref="formRef" :model="formData" :label-col="{ span: 11 }" :wrapper-col="{ span: 13 }">
      <a-form-item label="容积率" name="far" style="margin: 0.5rem 0">
        <mars-input-number v-model:value="formData.far" :max="10"></mars-input-number>
      </a-form-item>
      <a-form-item label="限高(m)" name="height" style="margin: 0.5rem 0">
        <mars-input-number v-model:value="formData.height"></mars-input-number>
      </a-form-item>
      <a-form-item label="最小基底面积(㎡)" name="minBaseArea" style="margin: 0.5rem 0">
        <mars-input-number v-model:value="formData.minBaseArea" :min="1"></mars-input-number>
      </a-form-item>
      <!-- <a-form-item label="建筑密度" name="md" style="margin: .5rem 0">
          <mars-input-number v-model:value="formData.md"></mars-input-number>
        </a-form-item> -->
    </a-form>
    <a-space class="btn-group tools_btns">
      <mars-button class="btn" type="primary" @click="startDrawing"> 绘制地块 </mars-button>
      <!-- <mars-button class="btn clear-btn" danger @click="clearDrawing">
          <i class="el-icon-delete"></i> 清除地块
        </mars-button> -->
      <mars-button class="btn" type="primary" @click="generateBuildingLayout">生成建筑布局</mars-button>
      <!-- <mars-button class="btn" danger @click="clearBuildings">清除建筑体块</mars-button> -->
    </a-space>
  </div>
</template>

<script setup lang="ts">
import * as mapWork from "./map"
import * as mars3d from "mars3d"
import { eventTarget, map, graphicLayer } from "@mars/widgets/analysis/map"
import * as turf from "@turf/turf"
import useLifecycle from "@mars/common/uses/use-lifecycle"
import { onBeforeUnmount, onMounted, reactive, ref } from "vue"
import { useAnalysisStore } from "@mars/stores/analysis"
import { $message } from "@mars/components/mars-ui"

const Cesium = mars3d.Cesium
const analysisStore = useAnalysisStore()

// ==================== 常量定义 ====================
const CONSTANTS = {
  LNG_TO_METERS: 111320, // 1度经度约等于111320米（赤道处）
  LAT_TO_METERS: 110540, // 1度纬度约等于110540米
  MIN_SINGLE_BASE_AREA: 100, // 最小单栋基底面积（㎡）
  INITIAL_BUILDING_BASE_AREA: 200, // 初始计算用的单栋基底面积（㎡）
  MIN_PLOT_RATIO: 0.1, // 最小容积率阈值
  FLOOR_HEIGHT: 3, // 标准层高（米）
  MIN_BUILDING_DENSITY: 0.25, // 最小建筑密度 25%
  MAX_BUILDING_DENSITY: 0.3 // 最大建筑密度 30%
} as const

// ==================== 类型定义 ====================
interface PlotParams {
  area: number
  plotRatio: number
  maxHeight: number
  buildingDensity: number
}

interface DegreeFactors {
  lng: number
  lat: number
}

interface GridLayout {
  gridCols: number
  gridRows: number
  colSpacing: number
  rowSpacing: number
}

// ==================== 启用生命周期 ====================
useLifecycle(mapWork)

// ==================== 表单相关 ====================
const formData = reactive({
  far: 1.5, // 容积率（默认1.5）
  height: 100, // 限高（默认100米）
  md: 0.4, // 建筑密度（默认40%）
  minBaseArea: 400 // 最小单栋基底面积（默认400㎡）
})

// ==================== 地块相关 ====================
const selectedPlot = ref<any>(null) // 选中的地块 GeoJSON
const selectedPlotJson = ref<any>(null) // 选中的地块 graphics JSON
const plotParams = ref<PlotParams>({
  area: 0, // 地块面积（平方米）
  plotRatio: 1.5, // 容积率
  maxHeight: 50, // 建筑限高（米）
  buildingDensity: 0.4 // 建筑密度
})

// 地块与建筑的关联映射：地块图形ID -> 建筑数量
const plotBuildingCountMap = ref<Map<string, number>>(new Map())

// ==================== 建筑相关 ====================
const state = reactive({
  buildingPrimitives: [] as any[], // 生成的建筑体块 Promise 数组
  hasGeneratedBuildings: false // 是否已经生成过建筑体块
})

// 计算参数
const totalBaseArea = ref(0) // 总建筑基底面积
const totalBuildingArea = ref(0) // 总建筑面积
const avgHeight = ref(0) // 平均建筑高度
const buildingCount = ref(0) // 建筑数量
const singleBaseArea = ref(0) // 单栋建筑基底面积
const singleSideLength = ref(0) // 单栋建筑边长（正方形）
const shijiao = ref<any>(null) // 视角信息

/**
 * 本地暂存地块数据（借鉴 tsbh 的 currentDrawData 模式）
 * 绘制完成后保存在此，点击"生成建筑布局"后提交到 store 并清空
 */
const localPlotData = ref<any>(null)
// ==================== 绘制事件监听 ====================
/**
 * 监听地块绘制完成事件
 * 将地块数据暂存到本地（借鉴 tsbh 模式），不直接使用
 */
const handleDrawEnd = (event: any) => {

  const plotData = event.value

  // 确保 GeoJSON Polygon 坐标首尾闭合
  if (plotData.geoJson?.geometry?.type === "Polygon") {
    const coords = plotData.geoJson.geometry.coordinates[0]
    if (coords.length > 0) {
      const first = coords[0]
      const last = coords[coords.length - 1]
      if (first[0] !== last[0] || first[1] !== last[1]) {
        plotData.geoJson.geometry.coordinates[0].push([...first])
      }
    }
  }

  // 暂存到本地数据
  localPlotData.value = plotData
  console.log("📝 地块绘制完成，数据已暂存到本地")
}



onMounted(() => {
  eventTarget.on("drawEnd", handleDrawEnd)
})
/**
 * 组件卸载前清理事件监听器和本地暂存数据
 */
onBeforeUnmount(() => {
  // 清空本地暂存数据
  localPlotData.value = null
  // 同时清除地图上对应的地块图形（如果有）
  clearLastLocalPlotGraphic()
  // 清除鼠标绑定事件
  mapWork.cancelDrawing()
  eventTarget.off("drawEnd", handleDrawEnd)
})

// ==================== 工具函数 ====================

/**
 * 计算经纬度转米的转换因子
 */
function getDegreeToMetersFactors(lat: number): DegreeFactors {
  const cosLat = Math.cos((lat * Math.PI) / 180)
  return {
    lng: CONSTANTS.LNG_TO_METERS * cosLat,
    lat: CONSTANTS.LAT_TO_METERS
  }
}

/**
 * 验证地块几何数据是否有效
 */
function isValidGeometry(geometry: any): geometry is any {
  if (!geometry?.coordinates?.[0]?.[0]) {
    return false
  }
  return geometry.type === "Polygon" || geometry.type === "MultiPolygon"
}

/**
 * 验证坐标格式
 */
function isValidCoordinates(coords: number[][]): boolean {
  return coords.every((coord) => typeof coord[0] === "number" && typeof coord[1] === "number" && !isNaN(coord[0]) && !isNaN(coord[1]))
}

/**
 * 计算单栋建筑基底面积和边长
 * 确保单栋基底面积不小于最小限制
 */
function calculateSingleBuildingArea(totalBaseArea: number, buildingCount: number, minBaseArea: number): { baseArea: number; sideLength: number } {
  let baseArea = totalBaseArea / buildingCount
  let sideLength = Math.sqrt(baseArea)

  // 确保单栋基底面积不小于最小限制
  if (baseArea < minBaseArea) {
    console.warn(`⚠️ 单栋基底面积${baseArea.toFixed(2)}㎡小于最小限制${minBaseArea}㎡，调整建筑数量`)
    const adjustedBuildingCount = Math.floor(totalBaseArea / minBaseArea)
    baseArea = totalBaseArea / adjustedBuildingCount
    sideLength = Math.sqrt(baseArea)
  }

  return { baseArea, sideLength }
}

/**
 * 计算网格布局参数
 * 确保能够容纳所有建筑，并保持合理的间距
 */
function calculateGridLayout(buildingCount: number, plotWidth: number, plotHeight: number, buildingSize: number): GridLayout {
  const spacingRatio = 1.8 // 间距系数
  const cellSize = buildingSize * spacingRatio // 网格单元尺寸（建筑+最小间距）

  // 计算地块最多能容纳的列数和行数
  const maxCols = Math.max(1, Math.floor(plotWidth / cellSize))
  const maxRows = Math.max(1, Math.floor(plotHeight / cellSize))
  const maxCapacity = maxCols * maxRows

  // 计算需要的行列数
  let gridCols: number
  let gridRows: number

  if (buildingCount <= maxCapacity) {
    // 建筑数量在容量范围内，按宽高比分配行列
    const aspectRatio = plotWidth / plotHeight
    const idealCols = Math.ceil(Math.sqrt(buildingCount * aspectRatio))
    const idealRows = Math.ceil(buildingCount / idealCols)

    // 使用较大的值确保能放下所有建筑
    gridCols = Math.max(1, Math.min(idealCols, Math.ceil(buildingCount / Math.max(1, idealRows))))
    gridRows = Math.ceil(buildingCount / gridCols)
  } else {
    // 建筑数量超过容量，使用最大布局
    gridCols = Math.ceil(buildingCount / maxRows)
    gridRows = maxRows
  }

  // 使用 cellSize 作为间距，确保间距系数为 1.8
  const colSpacing = cellSize
  const rowSpacing = cellSize

  return { gridCols, gridRows, colSpacing, rowSpacing }
}

/**
 * 计算建筑在地块中的位置
 */
function calculateBuildingPosition(
  index: number,
  gridCols: number,
  gridRows: number,
  colSpacing: number,
  rowSpacing: number,
  plotCenter: mars3d.LngLatPoint,
  degreeFactors: DegreeFactors
) {
  const col = index % gridCols
  const row = Math.floor(index / gridCols)

  const gridCenterOffsetX = ((gridCols - 1) / 2) * colSpacing
  const gridCenterOffsetY = ((gridRows - 1) / 2) * rowSpacing

  const offsetX = col * colSpacing - gridCenterOffsetX
  const offsetY = row * rowSpacing - gridCenterOffsetY

  return {
    lng: plotCenter.lng + offsetX / degreeFactors.lng,
    lat: plotCenter.lat + offsetY / degreeFactors.lat
  }
}

/**
 * 生成地块内所有候选位置
 * 从中心向外螺旋生成，确保建筑所有角点都在地块内
 * 如果螺旋方式生成位置不足，改用网格方式兜底
 */
/**
 * 检查两个建筑体块是否重叠
 * 使用建筑中心点距离判断，如果距离小于建筑边长则认为重叠
 * @param pos1 第一个建筑位置
 * @param pos2 第二个建筑位置
 * @param buildingSize 建筑边长
 * @param degreeFactors 经纬度转米转换因子
 * @param minSpacingRatio 最小间距比例（1.0表示刚好接触，<1.0表示重叠）
 */
function isOverlapping(
  pos1: { lng: number; lat: number },
  pos2: { lng: number; lat: number },
  buildingSize: number,
  degreeFactors: DegreeFactors,
  minSpacingRatio: number = 1.0
): boolean {
  // 计算两个建筑中心点之间的距离（米）
  const dx = (pos1.lng - pos2.lng) * degreeFactors.lng
  const dy = (pos1.lat - pos2.lat) * degreeFactors.lat
  const distance = Math.sqrt(dx * dx + dy * dy)

  // 如果距离小于建筑边长 * 最小间距比例，则认为重叠
  // 1.0 = 刚好接触（不重叠）
  // > 1.0 = 有间距
  // < 1.0 = 重叠
  return distance < buildingSize * minSpacingRatio
}

/**
 * 检查候选位置是否与已有位置重叠
 */
function isOverlappingWithExisting(
  newPos: { lng: number; lat: number },
  existingPositions: Array<{ lng: number; lat: number }>,
  buildingSize: number,
  degreeFactors: DegreeFactors
): boolean {
  return existingPositions.some(pos => isOverlapping(newPos, pos, buildingSize, degreeFactors))
}

function generateCandidatePositions(
  plotCenter: mars3d.LngLatPoint,
  plotWidth: number,
  plotHeight: number,
  buildingSize: number,
  degreeFactors: DegreeFactors,
  geometry: any
): Array<{ lng: number; lat: number }> {
  const positions: Array<{ lng: number; lat: number }> = []
  const spacingRatio = 1.8
  const cellSize = buildingSize * spacingRatio

  // 计算网格范围（稍微收紧，确保边缘留有余量）
  // 确保 halfWidth 和 halfHeight 至少为 0
  const halfWidth = Math.max(0, plotWidth / 2 - cellSize / 2 - buildingSize * 0.5)
  const halfHeight = Math.max(0, plotHeight / 2 - cellSize / 2 - buildingSize * 0.5)

  // 如果边界有效，使用螺旋方式生成
  if (halfWidth > 0 || halfHeight > 0) {
    // 从中心向外螺旋生成位置
    let x = 0
    let y = 0
    let dx = cellSize
    let dy = 0
    let steps = 1
    let turnCount = 0

    // 最大尝试次数，防止无限循环
    const maxAttempts = 1000
    let attempts = 0

    while (attempts < maxAttempts) {
      attempts++

      const lng = plotCenter.lng + x / degreeFactors.lng
      const lat = plotCenter.lat + y / degreeFactors.lat

      // 检查是否在有效范围内
      const absX = Math.abs(x)
      const absY = Math.abs(y)

      if (absX <= halfWidth && absY <= halfHeight) {
        // 检查建筑四个角点是否都在地块内
        const halfSideLng = buildingSize / 2 / degreeFactors.lng
        const halfSideLat = buildingSize / 2 / degreeFactors.lat

        const buildingCorners = [
          [lng - halfSideLng, lat - halfSideLat],
          [lng + halfSideLng, lat - halfSideLat],
          [lng + halfSideLng, lat + halfSideLat],
          [lng - halfSideLng, lat + halfSideLat]
        ]

        const allCornersInside = buildingCorners.every(([cornerLng, cornerLat]) => turf.booleanPointInPolygon(turf.point([cornerLng, cornerLat]), geometry))

        // 检查是否与已有建筑重叠
        const newPos = { lng, lat }
        const notOverlapping = !isOverlappingWithExisting(newPos, positions, buildingSize, degreeFactors)

        if (allCornersInside && notOverlapping) {
          positions.push(newPos)
        }
      }

      // 螺旋移动
      x += dx
      y += dy
      steps--

      if (steps === 0) {
        turnCount++
        const temp = dx
        dx = -dy
        dy = temp
        steps = Math.floor(turnCount / 2) + 1
      }

      // 如果已经超出范围很多，停止
      if (absX > halfWidth * 2 + cellSize && absY > halfHeight * 2 + cellSize) {
        break
      }
    }
  }

  // 兜底：如果螺旋生成位置不足，使用更紧凑的网格方式
  if (positions.length < 2) {
    const compactSpacing = buildingSize * 1.2 // 更紧凑的间距（1.2倍）
    const compactHalfWidth = Math.max(0, plotWidth / 2 - buildingSize / 2 - 1)
    const compactHalfHeight = Math.max(0, plotHeight / 2 - buildingSize / 2 - 1)

    const maxCols = Math.max(1, Math.floor(plotWidth / compactSpacing))
    const maxRows = Math.max(1, Math.floor(plotHeight / compactSpacing))

    for (let row = 0; row < maxRows; row++) {
      for (let col = 0; col < maxCols; col++) {
        const x = (col - (maxCols - 1) / 2) * compactSpacing
        const y = (row - (maxRows - 1) / 2) * compactSpacing

        const lng = plotCenter.lng + x / degreeFactors.lng
        const lat = plotCenter.lat + y / degreeFactors.lat

        // 检查建筑四个角点是否都在地块内
        const halfSideLng = buildingSize / 2 / degreeFactors.lng
        const halfSideLat = buildingSize / 2 / degreeFactors.lat

        const buildingCorners = [
          [lng - halfSideLng, lat - halfSideLat],
          [lng + halfSideLng, lat - halfSideLat],
          [lng + halfSideLng, lat + halfSideLat],
          [lng - halfSideLng, lat + halfSideLat]
        ]

        const allCornersInside = buildingCorners.every(([cornerLng, cornerLat]) => turf.booleanPointInPolygon(turf.point([cornerLng, cornerLat]), geometry))

        // 检查是否与已有建筑重叠
        const newPos = { lng, lat }
        const notOverlapping = !isOverlappingWithExisting(newPos, positions, buildingSize, degreeFactors)

        if (allCornersInside && notOverlapping) {
          positions.push(newPos)
        }
      }
    }

    console.log(`📊 网格兜底生成候选位置: ${positions.length}个 (紧凑间距: ${compactSpacing.toFixed(2)}m)`)
  }

  return positions
}

/**
 * 检查建筑四个角点是否都在地块内
 */
function isBuildingInsidePolygon(buildingLng: number, buildingLat: number, sideLength: number, degreeFactors: DegreeFactors, geometry: any): boolean {
  const halfSideLng = sideLength / 2 / degreeFactors.lng
  const halfSideLat = sideLength / 2 / degreeFactors.lat

  // 检查建筑四个角点
  const buildingCorners = [
    [buildingLng - halfSideLng, buildingLat - halfSideLat],
    [buildingLng + halfSideLng, buildingLat - halfSideLat],
    [buildingLng + halfSideLng, buildingLat + halfSideLat],
    [buildingLng - halfSideLng, buildingLat + halfSideLat]
  ]

  // 所有角点都必须在地块内
  return buildingCorners.every(([cornerLng, cornerLat]) => turf.booleanPointInPolygon(turf.point([cornerLng, cornerLat]), geometry))
}

// ==================== 建筑布局生成 ====================
/**
 * 根据指标生成建筑体块布局
 * 核心逻辑：
 * 1. 先根据地块尺寸和间距计算最大可容纳建筑数量
 * 2. 根据目标容积率和最大容量确定栋数（减少栋数或增加高度）
 * 3. 网格化布局，确保所有建筑在地块内且不重叠
 */
async function generateBuildingLayout() {
  // 1. 先判断本地是否有绘制地块的数据，没有则提示
  if (!localPlotData.value) {
    $message("请先绘制地块")
    return
  }

  // 2. 使用本地暂存的地块数据
  selectedPlotJson.value = localPlotData.value.graphics
  selectedPlot.value = localPlotData.value.geoJson
  plotParams.value.area = localPlotData.value.graphic.area
  shijiao.value = localPlotData.value.shijiao

  // 检查当前地块是否已有建筑体块
  const currentPlotId = selectedPlotJson.value?.id || selectedPlotJson.value?.attr?.id || JSON.stringify(selectedPlotJson.value?.positions)
  const existingBuildingCount = currentPlotId ? plotBuildingCountMap.value.get(currentPlotId) : 0

  if (existingBuildingCount && existingBuildingCount > 0) {
    console.log(`🗑️ 当前地块已有 ${existingBuildingCount} 栋建筑，清除后重新生成`)
    clearBuildings()
  }

  // 更新参数
  plotParams.value.plotRatio = formData.far
  plotParams.value.maxHeight = formData.height
  plotParams.value.buildingDensity = formData.md

  if (!plotParams.value.area || plotParams.value.area <= 0) {
    $message("地块面积无效")
    return
  }

  // 获取并验证地块几何信息
  const geometry = selectedPlot.value.geometry
  if (!isValidGeometry(geometry)) {
    console.error("无法获取有效的地块 geometry:", selectedPlot)
    $message("地块几何数据无效")
    return
  }

  const coords = geometry.coordinates[0]
  if (!isValidCoordinates(coords)) {
    $message("地块坐标数据无效")
    return
  }

  // 计算地块中心点和边界
  const plotCenterPoint = turf.centerOfMass(geometry)
  const plotCenter = new mars3d.LngLatPoint(plotCenterPoint.geometry.coordinates[0], plotCenterPoint.geometry.coordinates[1])

  const bbox = turf.bbox(geometry)
  const degreeFactors = getDegreeToMetersFactors(plotCenter.lat)

  const plotWidth = (bbox[2] - bbox[0]) * degreeFactors.lng
  const plotHeight = (bbox[3] - bbox[1]) * degreeFactors.lat

  // ==================== 第一步：确定单栋基底面积 ====================
  // 使用用户指定的最小基底面积（或默认值）
  const userMinBaseArea = formData.minBaseArea || CONSTANTS.INITIAL_BUILDING_BASE_AREA
  const spacingRatio = 1.8 // 间距系数

  // 动态计算合适的单栋边长：确保能在地块内放置至少1栋建筑
  // 单栋边长 = min(用户指定的, 地块能容纳的最大边长)
  // 地块最大边长 = min(地块宽, 地块长) * 0.6 (留20%边距)
  const maxAllowedSide = Math.min(plotWidth, plotHeight) * 0.6
  let buildingSideLength = Math.sqrt(userMinBaseArea)

  // 如果按用户指定面积计算的边长太大，动态调整
  let actualBaseArea = userMinBaseArea
  if (buildingSideLength > maxAllowedSide) {
    console.log(`⚠️ 用户指定的基底面积(${userMinBaseArea}㎡, 边长${buildingSideLength.toFixed(2)}m)过大，动态调整为${maxAllowedSide.toFixed(2)}m边长`)
    buildingSideLength = maxAllowedSide
    actualBaseArea = buildingSideLength * buildingSideLength
  }

  // ==================== 第二步：计算地块最大容量 ====================
  const cellSize = buildingSideLength * spacingRatio // 每个网格单元尺寸

  const maxCols = Math.max(1, Math.floor(plotWidth / cellSize))
  const maxRows = Math.max(1, Math.floor(plotHeight / cellSize))
  const maxCapacity = maxCols * maxRows

  console.log(`📊 地块尺寸: ${plotWidth.toFixed(2)}m × ${plotHeight.toFixed(2)}m`)
  console.log(`📊 单栋边长: ${buildingSideLength.toFixed(2)}m`)
  console.log(`📊 间距系数: ${spacingRatio}`)
  console.log(`📊 网格单元: ${cellSize.toFixed(2)}m`)
  console.log(`📊 最大容量: ${maxCols}列 × ${maxRows}行 = ${maxCapacity}栋`)

  // ==================== 第三步：确定建筑数量和高度 ====================
  // 目标总建筑面积
  const targetTotalArea = plotParams.value.area * plotParams.value.plotRatio
  // 目标建筑密度
  const targetDensity = (CONSTANTS.MIN_BUILDING_DENSITY + CONSTANTS.MAX_BUILDING_DENSITY) / 2 // 27.5%
  // 按目标密度计算需要的栋数（使用实际基底面积）
  const densityBasedCount = Math.round((plotParams.value.area * targetDensity) / actualBaseArea)

  let finalBuildingCount: number
  let buildingHeight: number

  if (densityBasedCount <= maxCapacity) {
    // 栋数在容量范围内
    finalBuildingCount = densityBasedCount
    // 高度 = 目标总面积 / (栋数 × 单栋基底面积)
    const neededFloors = targetTotalArea / (finalBuildingCount * actualBaseArea)
    const neededHeight = neededFloors * CONSTANTS.FLOOR_HEIGHT

    if (neededHeight <= plotParams.value.maxHeight) {
      buildingHeight = neededHeight
    } else {
      // 高度超过限高，使用限高
      buildingHeight = plotParams.value.maxHeight
      console.log(`⚠️ 计算高度${neededHeight.toFixed(2)}m超过限高${plotParams.value.maxHeight}m，已调整为限高`)
    }
  } else {
    // 栋数超过容量，需要减少栋数、增加高度
    // 先用最大容量，计算需要的最小高度
    finalBuildingCount = maxCapacity
    const neededFloors = targetTotalArea / (finalBuildingCount * actualBaseArea)
    const neededHeight = neededFloors * CONSTANTS.FLOOR_HEIGHT

    if (neededHeight <= plotParams.value.maxHeight) {
      buildingHeight = neededHeight
    } else {
      // 即使达到限高也不够，说明地块太小
      // 使用限高和最大容量
      buildingHeight = plotParams.value.maxHeight
      console.log(`⚠️ 容积率${plotParams.value.plotRatio}无法达到，地块过小，已使用最大容量${maxCapacity}栋和限高${buildingHeight}m`)
    }
  }

  // 计算实际总基底面积和建筑数量
  buildingCount.value = finalBuildingCount
  totalBaseArea.value = finalBuildingCount * actualBaseArea
  singleBaseArea.value = actualBaseArea
  singleSideLength.value = buildingSideLength
  totalBuildingArea.value = totalBaseArea.value * (buildingHeight / CONSTANTS.FLOOR_HEIGHT)

  // 计算实际容积率
  const actualFloorCount = buildingHeight / CONSTANTS.FLOOR_HEIGHT
  const actualPlotRatio = (totalBaseArea.value * actualFloorCount) / plotParams.value.area

  // 输出计算结果
  console.log(`📊 地块面积: ${plotParams.value.area.toFixed(2)}㎡`)
  console.log(`📊 目标容积率: ${plotParams.value.plotRatio}`)
  console.log(`📊 限高: ${plotParams.value.maxHeight}m`)
  console.log(`📊 建筑数量: ${buildingCount.value}栋`)
  console.log(`📊 建筑密度: ${((totalBaseArea.value / plotParams.value.area) * 100).toFixed(2)}%`)
  console.log(`📊 建筑高度: ${buildingHeight.toFixed(2)}m`)
  console.log(`📊 单栋基底面积: ${singleBaseArea.value.toFixed(2)}㎡`)
  console.log(`📊 实际容积率: ${actualPlotRatio.toFixed(2)}`)

  // 计算平均建筑高度
  avgHeight.value = buildingHeight

  // ==================== 第四步：计算网格布局 ====================
  const gridLayout = calculateGridLayout(buildingCount.value, plotWidth, plotHeight, singleSideLength.value)

  console.log(`📊 网格列数: ${gridLayout.gridCols}, 行数: ${gridLayout.gridRows}`)
  // ==================== 第五步：生成候选位置并布局 ====================
  // 生成地块内所有候选位置
  const candidatePositions = generateCandidatePositions(
    plotCenter,
    plotWidth,
    plotHeight,
    singleSideLength.value,
    degreeFactors,
    geometry
  )

  console.log(`📊 生成候选位置数量: ${candidatePositions.length}个`)

  // 实际使用的位置数量（不能超过候选位置数量）
  const actualPositionCount = Math.min(buildingCount.value, candidatePositions.length)

  // 如果候选位置不足，需要调整栋数和高度
  if (actualPositionCount < buildingCount.value) {
    console.log(`⚠️ 候选位置${candidatePositions.length}个少于需要栋数${buildingCount.value}，调整栋数`)
    buildingCount.value = actualPositionCount
    totalBaseArea.value = actualPositionCount * actualBaseArea

    // 重新计算高度以满足容积率
    const neededFloors = targetTotalArea / (actualPositionCount * actualBaseArea)
    const neededHeight = neededFloors * CONSTANTS.FLOOR_HEIGHT

    if (neededHeight <= plotParams.value.maxHeight) {
      buildingHeight = neededHeight
    } else {
      buildingHeight = plotParams.value.maxHeight
    }

    // 更新相关值
    totalBuildingArea.value = totalBaseArea.value * (buildingHeight / CONSTANTS.FLOOR_HEIGHT)
    avgHeight.value = buildingHeight

    console.log(`📊 调整后建筑数量: ${buildingCount.value}栋`)
    console.log(`📊 调整后建筑高度: ${buildingHeight.toFixed(2)}m`)
    console.log(`📊 调整后实际容积率: ${((totalBaseArea.value * (buildingHeight / CONSTANTS.FLOOR_HEIGHT)) / plotParams.value.area).toFixed(2)}`)
  }

  // 生成高度数组
  const buildingHeights = Array(buildingCount.value).fill(buildingHeight)

  // 生成建筑体块
  let actualBuildingIndex = 0
  const buildingsArray: any[] = []

  for (let i = 0; i < buildingCount.value && i < candidatePositions.length; i++) {
    try {
      const position = candidatePositions[i]

      // 获取地面高度
      const buildingTerrainHeight = mars3d.PointUtil.getHeight(map.scene, Cesium.Cartesian3.fromDegrees(position.lng, position.lat))

      // 创建建筑体块
      const buildingPrimitive = mapWork.createBuilding(
        position.lng,
        position.lat,
        buildingTerrainHeight,
        singleSideLength.value,
        buildingHeights[i],
        i,
        singleBaseArea.value
      )

      state.buildingPrimitives.push(buildingPrimitive)
      buildingsArray.push(await buildingPrimitive)
      actualBuildingIndex++
    } catch (error) {
      console.error(`创建建筑体块 ${i} 时出错:`, error)
      continue
    }
  }

  console.log(`📊 实际生成建筑数量: ${actualBuildingIndex}栋`)

  if (actualBuildingIndex === 0) {
    $message("所选地块区域面积不足，达不到建筑生成最小尺寸要求，请扩大绘制范围")
    localPlotData.value = null
    clearLastLocalPlotGraphic()
    return
  }

  // 标记已经生成过建筑体块
  state.hasGeneratedBuildings = true

  // 更新地块与建筑的关联映射
  if (selectedPlotJson.value) {
    const plotId = selectedPlotJson.value.id || selectedPlotJson.value.attr?.id || JSON.stringify(selectedPlotJson.value.positions)
    plotBuildingCountMap.value.set(plotId, actualBuildingIndex)
    console.log(`📊 地块 ${plotId} 生成建筑数量: ${actualBuildingIndex}栋`)
  }

  // 将地块和建筑布局数据保存到 analysisStore
  saveToAnalysisStore(buildingsArray)
}

/**
 * 将地块和建筑布局数据保存到 analysisStore
 * 借鉴 tsbh 的保存逻辑：直接提交到 store，不检查是否已存在
 */
function saveToAnalysisStore(buildingsArray: any[]) {
  const newData = {
    shijiao: shijiao.value,
    analysisType: "jzbj_analysis",
    data: selectedPlotJson.value,
    plot: {
      geoJson: selectedPlot.value,
      graphic: selectedPlotJson.value,
      area: plotParams.value.area,
      params: {
        far: formData.far,
        height: formData.height,
        md: formData.md
      }
    },
    buildings: buildingsArray,
    buildingCount: buildingsArray.length,
    totalBaseArea: totalBaseArea.value,
    totalBuildingArea: totalBuildingArea.value
  }

  // 直接提交到 store（借鉴 tsbh 的 saveToHistory 模式）
  analysisStore.saveAnalysisDataToSelected({
    analysisType: "jzbj_analysis",
    data: newData
  })
  console.log(`➕ 新增地块建筑布局数据`)

  // 保存成功后清除本地暂存数据
  localPlotData.value = null
}

// ==================== 清除功能 ====================

/**
 */
function clearBuildings() {
  if (mapWork.isEditing) {
    $message("正在编辑建筑，请先完成编辑")
    return
  }
  mapWork.clearBuildings()
  state.buildingPrimitives = []
  state.hasGeneratedBuildings = false
}

// ==================== 绘制功能 ====================
/**
 * 开始绘制地块
 * 先检查本地是否有绘制地块的数据，有则清除后重新绘制
 */
const startDrawing = () => {
  // 检查本地是否有暂存的地块数据
  if (localPlotData.value) {
    console.log("🗑️ 清除之前绘制的地块数据，重新绘制")
    localPlotData.value = null

    // 同时清除地图上对应的地块图形（如果有）
    clearLastLocalPlotGraphic()
  }

  // 开始新的地块绘制
  mapWork.startDrawingPolygon()
}

/**
 * 清除地图上最后一个未提交的本地地块图形
 */
function clearLastLocalPlotGraphic() {
  if (!map.graphicLayer) {
    return
  }

  const graphics = map.graphicLayer.getGraphics()
  if (!graphics || graphics.length === 0) {
    return
  }

  // 筛选出当前模块(jzbj)的地块(polygon类型)
  const jzbjPlots = graphics.filter(
    (g: any) =>
      (g.type === "polygon" || g instanceof mars3d.graphic.PolygonEntity) && g.attr?.analysisType === "jzbj"
  )

  if (jzbjPlots.length > 0) {
    const lastGraphic = jzbjPlots[jzbjPlots.length - 1]
    const plotId = lastGraphic.id || lastGraphic.attr?.id || JSON.stringify(lastGraphic.positionsShow)

    // 只清除没有生成过建筑的地块
    const buildingCount = plotBuildingCountMap.value.get(plotId)
    if (!buildingCount || buildingCount === 0) {
      graphicLayer.removeGraphic(lastGraphic)
      plotBuildingCountMap.value.delete(plotId)
      console.log(`🗑️ 已清除未提交的地块: ${plotId}`)
    }
  }
}

</script>

<style lang="less" scoped>

.clear-btn {
  margin-top: 0px;
}
</style>
