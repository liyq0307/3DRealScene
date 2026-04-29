import { ref, onUnmounted, watch, type Ref, type ShallowRef } from 'vue'
import { useAnalysisStore } from '@/stores/analysis'
import { Mars3DAnalysisTools } from '@/utils/mars3dAnalysis'
import type {
  AnalysisToolType,
  AnalysisError,
  VisibilityMode,
  VolumeMethod,
  ProfileResultData,
  SkylineResultData,
  VolumeResultData,
  HeightMeasurementResultData,
  CameraView
} from '@/types/analysis'

/**
 * 核心分析工具 composable
 * 统一管理 Mars3DAnalysisTools 的初始化、生命周期和错误处理
 */
export function useAnalysisTool(viewerInstance: Ref<any> | ShallowRef<any>) {
  const store = useAnalysisStore()
  const tools = ref<Mars3DAnalysisTools | null>(null)
  const isReady = ref(false)

  /** 初始化分析工具 */
  function init() {
    const map = viewerInstance.value?.map || viewerInstance.value
    if (map && !tools.value) {
      tools.value = new Mars3DAnalysisTools(map)
      isReady.value = true
    }
  }

  /** 等待viewer ready后初始化 */
  watch(
    () => viewerInstance.value?.map || viewerInstance.value,
    (map) => {
      if (map && !tools.value) {
        tools.value = new Mars3DAnalysisTools(map)
        isReady.value = true
      }
    },
    { immediate: true }
  )

  /** 安全执行分析操作 */
  async function safeExecute<T>(
    type: AnalysisToolType,
    operation: (t: Mars3DAnalysisTools) => Promise<T> | T,
    fallback?: () => T
  ): Promise<T | null> {
    store.startAnalysis(type)
    try {
      const t = tools.value
      if (!t) {
        throw new Error('分析工具未初始化，请确保3D场景已加载')
      }
      const result = await operation(t as Mars3DAnalysisTools)
      return result
    } catch (err) {
      const message = err instanceof Error ? err.message : '分析操作失败'
      const analysisError: AnalysisError = {
        type,
        message,
        code: 'ANALYSIS_FAILED',
        timestamp: new Date(),
        recoverable: true,
        details: err
      }
      store.setError(analysisError)
      console.error(`[分析错误] ${type}:`, err)
      if (fallback) {
        return fallback()
      }
      return null
    } finally {
      store.stopAnalysis()
    }
  }

  // ==================== 测量工具 ====================

  /** 空间距离测量 */
  async function measureDistance() {
    const result = await safeExecute('distance', (t) => t.measureDistance())
    if (result) {
      store.addResult({ type: 'distance', name: '空间距离测量', data: { distance: result.distance }, visible: true })
    }
    return result
  }

  /** 贴地距离测量 */
  async function measureDistanceSurface() {
    const result = await safeExecute('distance-surface', (t) => t.measureDistanceSurface())
    if (result) {
      store.addResult({ type: 'distance-surface', name: '贴地距离测量', data: { distance: result.distance }, visible: true })
    }
    return result
  }

  /** 水平面积测量 */
  async function measureArea() {
    const result = await safeExecute('area', (t) => t.measureArea())
    if (result) {
      store.addResult({ type: 'area', name: '水平面积测量', data: { area: result.area, perimeter: result.perimeter }, visible: true })
    }
    return result
  }

  /** 贴地面积测量 */
  async function measureAreaSurface() {
    const result = await safeExecute('area-surface', (t) => t.measureAreaSurface())
    if (result) {
      store.addResult({ type: 'area-surface', name: '贴地面积测量', data: { area: result.area, perimeter: result.perimeter }, visible: true })
    }
    return result
  }

  /** 高度差测量 */
  async function measureHeight() {
    const result = await safeExecute('height', (t) => t.measureHeight())
    if (result) {
      const heightData: HeightMeasurementResultData = {
        startHeight: result.startHeight,
        endHeight: result.endHeight,
        heightDiff: result.heightDiff
      }
      store.addResult({ type: 'height', name: '高度差测量', data: heightData, visible: true })
    }
    return result
  }

  /** 三角测量 */
  async function measureHeightTriangle() {
    const result = await safeExecute('height-triangle', (t) => t.measureHeightTriangle())
    if (result) {
      store.addResult({ type: 'height-triangle', name: '三角测量', data: result, visible: true })
    }
    return result
  }

  /** 方位角测量 */
  async function measureAngle() {
    const result = await safeExecute('bearing', (t) => t.measureAngle())
    if (result) {
      store.addResult({ type: 'bearing', name: '方位角测量', data: result, visible: true })
    }
    return result
  }

  /** 坐标测量 */
  async function measurePoint() {
    const result = await safeExecute('coordinate-measure', (t) => t.measurePoint())
    if (result) {
      store.addResult({ type: 'coordinate-measure', name: '坐标测量', data: result, visible: true })
    }
    return result
  }

  // ==================== 空间分析 ====================

  /** 通视分析 */
  async function analyzeVisibility(mode: VisibilityMode) {
    const typeName = mode === 'linear' ? '线通视' : '圆通视'
    const operation = mode === 'linear'
      ? (t: Mars3DAnalysisTools) => t.sightlineLinear()
      : (t: Mars3DAnalysisTools) => t.sightlineCircular()

    const result = await safeExecute('visibility', operation)
    if (result) {
      store.addResult({ type: 'visibility', name: `通视分析(${typeName})`, data: { mode, result }, visible: true })
    }
    return result
  }

  /** 剖面分析 */
  async function analyzeProfile(): Promise<ProfileResultData | null> {
    const result = await safeExecute('profile', (t) => t.measureSection())
    if (result && result.positions) {
      const distanceList: number[] = result.distanceList || []
      const positions = result.positions
      const len = positions.length

      let maxElevation = -Infinity
      let minElevation = Infinity

      const profileData = new Array(len)
      for (let i = 0; i < len; i++) {
        const elevation = positions[i].height || 0
        if (elevation > maxElevation) maxElevation = elevation
        if (elevation < minElevation) minElevation = elevation
        profileData[i] = { distance: distanceList[i] || 0, elevation }
      }

      const profileResult: ProfileResultData = {
        profileData,
        totalLength: len > 0 ? profileData[len - 1].distance : 0,
        maxElevation,
        minElevation,
        sampleInterval: 10,
        heightBaseline: 'sea'
      }

      store.addResult({ type: 'profile', name: '剖面分析', data: profileResult, visible: true })
      return profileResult
    }
    return null
  }

  /** 天际线分析 */
  function analyzeSkyline(): SkylineResultData | null {
    const t = tools.value
    if (!t) return null

    store.startAnalysis('skyline')
    try {
      const raw = t.analyzeSkyline()
      if (raw && raw.points) {
        const len = raw.points.length
        let maxHeight = -Infinity
        let minHeight = Infinity
        let sumHeight = 0

        const points = new Array(len)
        for (let i = 0; i < len; i++) {
          const height = raw.points[i].height || 0
          if (height > maxHeight) maxHeight = height
          if (height < minHeight) minHeight = height
          sumHeight += height
          points[i] = { angle: (i / len) * 360, height }
        }

        const avg = sumHeight / len
        let varianceSum = 0
        for (let i = 0; i < len; i++) {
          varianceSum += (points[i].height - avg) ** 2
        }
        const variance = Math.sqrt(varianceSum / len)

        const skylineResult: SkylineResultData = {
          points, maxHeight, minHeight, variance,
          lineWidth: 5, lineColor: '#ff6b6b', displayMode: '2d'
        }

        store.addResult({ type: 'skyline', name: '天际线分析', data: skylineResult, visible: true })
        store.stopAnalysis()
        return skylineResult
      }
      store.stopAnalysis()
    } catch (err) {
      const message = err instanceof Error ? err.message : '天际线分析失败'
      store.setError({ type: 'skyline', message, code: 'ANALYSIS_FAILED', timestamp: new Date(), recoverable: true })
      store.stopAnalysis()
    }
    return null
  }

  // ==================== 体积计算 ====================

  async function calculateVolume(baseHeight = 0, method: VolumeMethod = 'above'): Promise<VolumeResultData | null> {
    const areaResult = await safeExecute('volume', (t) => t.measureArea())
    if (areaResult && areaResult.area > 0) {
      const avgHeight = baseHeight + 10
      const totalVolume = areaResult.area * avgHeight
      const volumeResult: VolumeResultData = {
        totalVolume,
        fillVolume: method === 'above' ? totalVolume * 0.6 : method === 'below' ? totalVolume * 0.4 : totalVolume,
        cutVolume: method === 'below' ? totalVolume * 0.6 : method === 'above' ? totalVolume * 0.4 : 0,
        area: areaResult.area,
        avgHeight,
        baseHeight,
        calcMethod: method
      }
      store.addResult({ type: 'volume', name: '体积计算', data: volumeResult, visible: true })
      return volumeResult
    }
    return null
  }

  // ==================== 日照分析 ====================

  function startShadowsPlay(multiplier = 1600) {
    const t = tools.value
    if (t) {
      t.startShadowsPlay(multiplier)
      store.addResult({ type: 'sun', name: '日照分析', data: { playing: true, multiplier }, visible: true })
    }
  }

  function stopShadowsPlay() {
    tools.value?.stopShadowsPlay()
  }

  function setShadowsTime(date: string, hours: number, minutes: number) {
    tools.value?.setShadowsTime(date, hours, minutes)
  }

  function getShadowsTime() {
    return tools.value?.getShadowsTime()
  }

  // ==================== 等高线 ====================

  async function generateContourLine(options?: { spacing?: number; lineWidth?: number; lineColor?: string; showLabel?: boolean }) {
    const result = await safeExecute('contour', (t) => t.generateContourLine(options))
    if (result) {
      store.addResult({ type: 'contour', name: '等高线分析', data: result, visible: true })
    }
    return result
  }

  function setContourSpacing(spacing: number) { tools.value?.setContourSpacing(spacing) }
  function setContourWidth(width: number) { tools.value?.setContourWidth(width) }
  function setContourColor(color: string) { tools.value?.setContourColor(color) }
  function toggleContourVisible(visible: boolean) { tools.value?.toggleContourVisible(visible) }

  // ==================== 压平 ====================

  async function startFlatten(options?: { height?: number; tilesetUrl?: string }) {
    const result = await safeExecute('flatten', (t) => t.startFlatten(options))
    if (result) {
      store.addResult({ type: 'flatten', name: '压平', data: result, visible: true })
    }
    return result
  }

  function updateFlattenHeight(height: number) { tools.value?.updateFlattenHeight(height) }
  function clearFlatten() { tools.value?.clearFlatten() }

  // ==================== 图上标记 ====================

  async function drawPoint(style?: any) {
    const result = await safeExecute('map-marking', (t) => t.drawPoint(style))
    if (result) {
      store.addResult({ type: 'map-marking', name: '点标记', data: { markingType: 'point', positions: result.positions }, visible: true })
    }
    return result
  }

  async function drawPolyline(style?: any) {
    const result = await safeExecute('map-marking', (t) => t.drawPolyline(style))
    if (result) {
      store.addResult({ type: 'map-marking', name: '线标记', data: { markingType: 'polyline', positions: result.positions }, visible: true })
    }
    return result
  }

  async function drawPolygon(style?: any) {
    const result = await safeExecute('map-marking', (t) => t.drawPolygon(style))
    if (result) {
      store.addResult({ type: 'map-marking', name: '面标记', data: { markingType: 'polygon', positions: result.positions }, visible: true })
    }
    return result
  }

  async function drawCircle(style?: any) {
    const result = await safeExecute('map-marking', (t) => t.drawCircle(style))
    if (result) {
      store.addResult({ type: 'map-marking', name: '圆标记', data: { markingType: 'circle', positions: result.positions }, visible: true })
    }
    return result
  }

  function exportGeoJSON() { return tools.value?.exportGeoJSON() }

  // ==================== 坐标定位 ====================

  function bindMouseClickForCoordinate(callback: (lng: number, lat: number, alt: number) => void) {
    tools.value?.bindMouseClickForCoordinate(callback)
  }

  function addCoordinateMarker(lng: number, lat: number, alt: number, label?: string) {
    return tools.value?.addCoordinateMarker(lng, lat, alt, label)
  }

  function locateCoordinate(lng: number, lat: number, alt: number) {
    tools.value?.locateCoordinate(lng, lat, alt)
  }

  // ==================== 观测台 ====================

  function getCameraView(): CameraView | null {
    const view = tools.value?.getCameraView()
    if (view) {
      return {
        lng: view.lng || view.x || 0,
        lat: view.lat || view.y || 0,
        alt: view.alt || view.z || 0,
        heading: view.heading || 0,
        pitch: view.pitch || 0,
        roll: view.roll || 0
      }
    }
    return null
  }

  function flyToView(view: any, duration = 2) {
    tools.value?.flyToView(view, duration)
  }

  // ==================== 绘制区域 ====================

  async function drawRectangle(style?: any) {
    return await safeExecute('constraint', (t) => t.drawRectangle(style))
  }

  async function drawCircleArea(style?: any) {
    return await safeExecute('constraint', (t) => t.drawCircleArea(style))
  }

  async function drawPolygonArea(style?: any) {
    return await safeExecute('constraint', (t) => t.drawPolygonArea(style))
  }

  // ==================== 塔基建模 ====================

  async function drawTowerPole(options?: { height?: number; radius?: number; color?: string }) {
    const result = await safeExecute('tower-foundation', (t) => t.drawTowerPole(options))
    if (result) {
      store.addResult({ type: 'tower-foundation', name: '塔基建模', data: result, visible: true })
    }
    return result
  }

  // ==================== 管线分析 ====================

  async function drawPipeline(options?: { radius?: number; color?: string }) {
    const result = await safeExecute('pipeline', (t) => t.drawPipeline(options))
    if (result) {
      store.addResult({ type: 'pipeline', name: '管线分析', data: result, visible: true })
    }
    return result
  }

  // ==================== 卷帘对比 ====================

  function createSplitControl() { return tools.value?.createSplitControl() }
  function destroySplitControl(ctrl: any) { tools.value?.destroySplitControl(ctrl) }

  // ==================== 淹没分析 ====================

  function analyzeFlood(waterHeight: number) {
    const t = tools.value
    if (t) {
      const result = t.analyzeFlood(waterHeight)
      store.addResult({ type: 'flood', name: '淹没分析', data: { waterHeight }, visible: true })
      return result
    }
    return null
  }

  // ==================== 天际线样式控制 ====================

  function setSkylineWidth(width: number) { tools.value?.setSkylineWidth(width) }
  function setSkylineColor(color: string) { tools.value?.setSkylineColor(color) }

  // ==================== 清除操作 ====================

  function clearAll() { tools.value?.clearAll(); store.clearAll() }
  function clearMeasure() { tools.value?.clearMeasure() }
  function clearSightline() { tools.value?.clearSightline() }
  function clearSkyline() { tools.value?.clearSkyline() }
  function clearShadows() { tools.value?.clearShadows() }
  function clearContour() { tools.value?.clearContour() }
  function clearFlood() { tools.value?.clearFlood() }
  function clearGraphics() { tools.value?.clearGraphics() }

  // ==================== 生命周期 ====================

  onUnmounted(() => {
    if (tools.value) {
      tools.value.destroy()
      tools.value = null
    }
    isReady.value = false
  })

  return {
    // 状态
    tools, isReady,
    // 测量
    measureDistance, measureDistanceSurface, measureArea, measureAreaSurface,
    measureHeight, measureHeightTriangle, measureAngle, measurePoint,
    // 空间分析
    analyzeVisibility, analyzeProfile, analyzeSkyline,
    // 体积
    calculateVolume,
    // 日照
    startShadowsPlay, stopShadowsPlay, setShadowsTime, getShadowsTime,
    // 等高线
    generateContourLine, setContourSpacing, setContourWidth, setContourColor, toggleContourVisible,
    // 压平
    startFlatten, updateFlattenHeight, clearFlatten,
    // 图上标记
    drawPoint, drawPolyline, drawPolygon, drawCircle, exportGeoJSON,
    // 坐标定位
    bindMouseClickForCoordinate, addCoordinateMarker, locateCoordinate,
    // 观测台
    getCameraView, flyToView,
    // 绘制区域
    drawRectangle, drawCircleArea, drawPolygonArea,
    // 塔基建模
    drawTowerPole,
    // 管线
    drawPipeline,
    // 卷帘
    createSplitControl, destroySplitControl,
    // 淹没
    analyzeFlood,
    // 天际线样式
    setSkylineWidth, setSkylineColor,
    // 清除
    clearAll, clearMeasure, clearSightline, clearSkyline, clearShadows,
    clearContour, clearFlood, clearGraphics,
    // 底层
    safeExecute, init
  }
}
