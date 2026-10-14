import * as mars3d from "mars3d"
import { PipelineManager } from "@mars/utils/PipelineManager"
import { RoamManager } from "@mars/utils/RoamManager"
import { RecorderManager } from "@mars/utils/RecorderManager"
import { map, eventTarget as fxEvent } from "@mars/widgets/analysis/map"
import { $message, $notify } from "@mars/components/mars-ui"

export let pipelineManager: PipelineManager | null = null // 管线管理器

/**
 * 设置管线坐标
 */
export async function setPipelinePositions(coordinates: number[][], options: { radius: number; smoothness: number }) {
  if (!pipelineManager) {
    return
  }

  pipelineManager.clear()
  pipelineManager.setRadius(options.radius)
  pipelineManager.setSmoothness(options.smoothness)
  roamManager?.stopRoam()

  // 转换为 Cartesian3
  const positions = mars3d.LngLatArray.toCartesians(coordinates)

  if (positions && positions.length >= 2) {
    // 导入时设置 clear: true (默认值)，不再叠加
    const pipelineGraphic = await pipelineManager.createPipeline(positions, { clear: true })

    // 使用创建的管线直接飞行
    if (pipelineGraphic) {
      pipelineGraphic.flyTo({
        radius: 1000,
        pitch: -45
      })
    }
  } else {
    $notify("error", "导入失败", "导入的坐标数据无效")
  }
}

let roamManager: RoamManager | null = null // 漫游管理器
let recorderManager: RecorderManager | null = null // 录制管理器

/**
 * 初始化管理器（切换回来时复用已有实例）
 */
export function onMounted() {
  if (!map) {
    return
  }

  // 仅在首次初始化时创建管理器，切换回来时复用已有实例
  ensurePipelineManager()

  if (!roamManager) {
    // 初始化漫游管理器
    roamManager = new RoamManager({
      map,
      speed: 100,
      onStop: () => {
        // 漫游结束时触发事件，通知UI更新状态
        fxEvent.fire("roamEnd")
      }
    })

    // 初始化录制管理器
    recorderManager = new RecorderManager(map)
  }
}

/**
 * 销毁清理
 * 注意：不再销毁 pipelineManager，以保持管线在地图上的显示
 * 只有在用户主动调用 clearAll 时才销毁
 */
export function onUnmounted() {
  // 停止漫游和录制
  roamManager?.stopRoam()
  recorderManager?.stopRecording()

  // 清除鼠标绑定事件
  if (map?.graphicLayer) {
    map.graphicLayer.clearDrawing()
  }

  // 不再销毁 pipelineManager，保持管线显示在地图上
  // roamManager 和 recorderManager 的引用保留，以便切换回来时继续使用

  // 漫游和录制管理器引用清空
  roamManager = null
  recorderManager = null
}

/**
 * 初始化或重新初始化管线管理器
 */
/**
 * 获取或初始化管线管理器
 */
export function getPipelineManager() {
  if (ensurePipelineManager()) {
    return pipelineManager
  }
  return null
}

// 确保管线管理器存在且有效，如果需要则重新初始化
function ensurePipelineManager() {
  if (!map) {
    return false
  }

  // 如果 manager 已存在且有效，直接复用，不重新创建
  if (pipelineManager && pipelineManager.isValid()) {
    console.log("PipelineManager: 复用已有管理器，保持管线显示")
    return true
  }

  // 需要初始化：manager 不存在 或 资源已失效
  // 初始化管线管理器
  pipelineManager = new PipelineManager({
    map,
    radius: 5,
    smoothness: 0.1,
    opacity: 0.8,
    color: "#00ffff"
  })
  console.log("PipelineManager: 创建新管理器")

  return true
}

/**
 * 开始绘制管线
 * @param {Object} options 配置项
 * @param {number} options.radius 管径(m)
 * @param {number} options.smoothness 路径平滑度(0-1)
 */
export async function startDrawPipe(options: { radius: number; smoothness: number }) {
  if (!ensurePipelineManager() || !pipelineManager) {
    $notify("error", "绘制失败", "地图未初始化")
    return
  }

  pipelineManager.clear()
  pipelineManager.setRadius(options.radius)
  pipelineManager.setSmoothness(options.smoothness)
  roamManager?.stopRoam()

  // 1. 使用交互绘制工具开始绘制
  const graphic = await map.graphicLayer.startDraw({
    type: "polyline",
    style: {
      color: "#00d4ff",
      width: options.radius
      // clampToGround: true
    }
  })
  // 绘制完成之后将内容存到store中
  fxEvent.fire("drawPipe", {
    graphic: graphic.toJSON()
  })


  // 2. 直接获取绘制图形的笛卡尔坐标数组
  const positions = graphic.positionsShow || (typeof graphic.getPositions === "function" ? graphic.getPositions() : graphic.positions)

  // 3. 创建管线
  if (positions && positions.length >= 2) {
    await pipelineManager.createPipeline(positions)
    // console.log("map.ts: Pipeline created, getPositions():", pipelineManager.getPositions().length)
    map.graphicLayer.removeGraphic(graphic) // 移除临时的绘制线
  } else {
    $notify("error", "绘制失败", "未能获取到有效的路径点，请重试")
  }
}

/**
 * 开始漫游
 * @param {number} speed 漫游速度(km/h)
 */
export function startRoam(speed: number) {
  if (!pipelineManager || !roamManager) {
    return false
  }
  console.log("positions:", pipelineManager.getPositions())
  const positions = pipelineManager.getPositions()
  if (!positions || positions.length < 2) {
    $message("请先绘制管线")
    return false
  }

  roamManager.setSpeed(speed)
  roamManager.startRoam(positions)
  return true
}

/**
 * 停止漫游
 */
export function stopRoam() {
  roamManager?.stopRoam()
}

/**
 * 开始录制
 */
export function startRecording() {
  if (!recorderManager) {
    return
  }
  recorderManager.startRecording()
  // $notify("", "录制已开始", "视频正在录制中...")
  $message("录制已开始")
}

/**
 * 停止录制
 */
export async function stopRecording() {
  if (!recorderManager) {
    return
  }
  await recorderManager.stopRecording()
  // $notify("", "录制已停止", "视频已保存")
  $message("录制已停止")
}

/**
 * 清空数据
 */
export function clearAll() {
  pipelineManager?.clear()
  roamManager?.stopRoam()
}
