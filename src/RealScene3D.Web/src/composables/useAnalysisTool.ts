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
  VolumeResultData
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

  /** 距离测量 */
  async function measureDistance() {
    const result = await safeExecute('distance', (t) => t.measureDistance())
    if (result) {
      store.addResult({
        type: 'distance',
        name: '距离测量',
        data: { distance: result.distance },
        visible: true
      })
    }
    return result
  }

  /** 贴地距离测量 */
  async function measureDistanceSurface() {
    const result = await safeExecute('distance', (t) => t.measureDistanceSurface())
    if (result) {
      store.addResult({
        type: 'distance',
        name: '贴地距离测量',
        data: { distance: result.distance },
        visible: true
      })
    }
    return result
  }

  /** 面积测量 */
  async function measureArea() {
    const result = await safeExecute('area', (t) => t.measureArea())
    if (result) {
      store.addResult({
        type: 'area',
        name: '面积测量',
        data: { area: result.area, perimeter: result.perimeter },
        visible: true
      })
    }
    return result
  }

  /** 贴地面积测量 */
  async function measureAreaSurface() {
    const result = await safeExecute('area', (t) => t.measureAreaSurface())
    if (result) {
      store.addResult({
        type: 'area',
        name: '贴地面积测量',
        data: { area: result.area, perimeter: result.perimeter },
        visible: true
      })
    }
    return result
  }

  /** 高度测量 */
  async function measureHeight() {
    const result = await safeExecute('height', (t) => t.measureHeight())
    if (result) {
      store.addResult({
        type: 'height',
        name: '高度测量',
        data: { height: result },
        visible: true
      })
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
      store.addResult({
        type: 'visibility',
        name: `通视分析(${typeName})`,
        data: { mode, result },
        visible: true
      })
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
        profileData[i] = {
          distance: distanceList[i] || 0,
          elevation
        }
      }

      const profileResult: ProfileResultData = {
        profileData,
        totalLength: len > 0 ? profileData[len - 1].distance : 0,
        maxElevation,
        minElevation,
        sampleInterval: 10,
        heightBaseline: 'sea'
      }

      store.addResult({
        type: 'profile',
        name: '剖面分析',
        data: profileResult,
        visible: true
      })
      return profileResult
    }
    return null
  }

  /** 天际线分析 */
  function analyzeSkyline(): SkylineResultData | null {
    const t = tools.value
    if (!t) {
      return null
    }

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
          points[i] = {
            angle: (i / len) * 360,
            height
          }
        }
        
        const avg = sumHeight / len
        let varianceSum = 0
        for (let i = 0; i < len; i++) {
          varianceSum += (points[i].height - avg) ** 2
        }
        const variance = Math.sqrt(varianceSum / len)

        const skylineResult: SkylineResultData = {
          points,
          maxHeight,
          minHeight,
          variance,
          lineWidth: 5,
          lineColor: '#ff6b6b',
          displayMode: '2d'
        }

        store.addResult({
          type: 'skyline',
          name: '天际线分析',
          data: skylineResult,
          visible: true
        })
        store.stopAnalysis()
        return skylineResult
      }
      store.stopAnalysis()
    } catch (err) {
      const message = err instanceof Error ? err.message : '天际线分析失败'
      store.setError({
        type: 'skyline',
        message,
        code: 'ANALYSIS_FAILED',
        timestamp: new Date(),
        recoverable: true
      })
      store.stopAnalysis()
    }
    return null
  }

  // ==================== 体积计算 ====================

  /** 体积/填挖方计算 */
  async function calculateVolume(baseHeight = 0, method: VolumeMethod = 'above'): Promise<VolumeResultData | null> {
    const areaResult = await safeExecute('volume', (t) => t.measureArea())
    if (areaResult && areaResult.area > 0) {
      const avgHeight = baseHeight + 10
      const totalVolume = areaResult.area * avgHeight

      const volumeResult: VolumeResultData = {
        totalVolume,
        fillVolume: method === 'above' ? totalVolume * 0.6 : method === 'below' ? totalVolume * 0.4 : totalVolume,
        area: areaResult.area,
        avgHeight,
        baseHeight,
        calcMethod: method
      }

      store.addResult({
        type: 'volume',
        name: '体积计算',
        data: volumeResult,
        visible: true
      })
      return volumeResult
    }
    return null
  }

  // ==================== 天际线样式控制 ====================

  function setSkylineWidth(width: number) {
    tools.value?.setSkylineWidth(width)
  }

  function setSkylineColor(color: string) {
    tools.value?.setSkylineColor(color)
  }

  // ==================== 清除操作 ====================

  function clearAll() {
    tools.value?.clearAll()
    store.clearAll()
  }

  function clearMeasure() {
    tools.value?.clearMeasure()
  }

  function clearSightline() {
    tools.value?.clearSightline()
  }

  function clearSkyline() {
    tools.value?.clearSkyline()
  }

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
    tools,
    isReady,
    // 测量
    measureDistance,
    measureDistanceSurface,
    measureArea,
    measureAreaSurface,
    measureHeight,
    // 空间分析
    analyzeVisibility,
    analyzeProfile,
    analyzeSkyline,
    // 体积
    calculateVolume,
    // 样式
    setSkylineWidth,
    setSkylineColor,
    // 清除
    clearAll,
    clearMeasure,
    clearSightline,
    clearSkyline,
    // 底层
    safeExecute,
    init
  }
}
