import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type {
  AnalysisToolType,
  AnalysisResultBase,
  AnalysisError,
  PerformanceMetrics,
  CameraView,
  IAnalysisToolManager,
  IResultManager
} from '@/types/analysis'
import { getToolManager } from '@/services/AnalysisToolManager'
import { getResultManager } from '@/services/ResultManager'
import { ConcurrencyManager } from '@/services/ConcurrencyManager'
import { PerformanceMonitor } from '@/services/PerformanceMonitor'
import { GraphicObjectPool } from '@/services/GraphicObjectPool'
import { FallbackService } from '@/services/FallbackService'

// Re-export from types for backward compatibility
export type { AnalysisToolType, AnalysisResultBase as AnalysisResult, AnalysisError, PerformanceMetrics }

const STORAGE_KEY = 'analysis_results'
const CONFIG_KEY = 'analysis_config'

export const useAnalysisStore = defineStore('analysis', () => {
  // ==================== State ====================
  const results = ref<AnalysisResultBase[]>([])
  const currentToolType = ref<AnalysisToolType | null>(null)
  const isAnalyzing = ref(false)
  const isLoading = ref(false)
  const error = ref<AnalysisError | null>(null)

  const performanceMetrics = ref<PerformanceMetrics>({
    fps: 0,
    frameTime: 0,
    memory: 0,
    triangles: 0,
    drawCalls: 0,
    objects: 0
  })

  const performanceHistory = ref<Array<{ time: number; metrics: PerformanceMetrics }>>([])
  const maxHistorySize = 100

  // 观测台数据
  const viewpoints = ref<Array<{ id: string; name: string; cameraView: CameraView; thumbnail?: string }>>([])

  // 工具配置缓存
  const toolConfigs = ref<Record<string, any>>({})

  // ==================== Getters ====================
  const recentResults = computed(() =>
    results.value.slice(-10).reverse()
  )

  const resultsByType = computed(() => {
    const grouped: Record<string, AnalysisResultBase[]> = {}
    results.value.forEach(result => {
      if (!grouped[result.type]) {
        grouped[result.type] = []
      }
      grouped[result.type].push(result)
    })
    return grouped
  })

  const hasError = computed(() => error.value !== null)

  const successResults = computed(() =>
    results.value.filter(r => r.status === 'success')
  )

  const resultCount = computed(() => results.value.length)

  // ==================== Actions ====================

  function addResult(result: Omit<AnalysisResultBase, 'id' | 'timestamp' | 'status' | 'errorMessage'>): AnalysisResultBase {
    const newResult: AnalysisResultBase = {
      ...result,
      id: `analysis_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
      timestamp: new Date(),
      status: 'success'
    }
    results.value.push(newResult)
    return newResult
  }

  function addErrorResult(type: AnalysisToolType, name: string, errorMessage: string, data?: any): AnalysisResultBase {
    const newResult: AnalysisResultBase = {
      id: `analysis_err_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
      type,
      name,
      data: data ?? {},
      timestamp: new Date(),
      visible: true,
      status: 'error',
      errorMessage
    }
    results.value.push(newResult)
    return newResult
  }

  function removeResult(id: string) {
    const index = results.value.findIndex(r => r.id === id)
    if (index !== -1) {
      results.value.splice(index, 1)
    }
  }

  function removeResults(ids: string[]) {
    const idSet = new Set(ids)
    results.value = results.value.filter(r => !idSet.has(r.id))
  }

  function clearAll() {
    results.value = []
    error.value = null
  }

  function clearByType(type: AnalysisToolType) {
    results.value = results.value.filter(r => r.type !== type)
  }

  function getResult(id: string): AnalysisResultBase | undefined {
    return results.value.find(r => r.id === id)
  }

  function getResultsByType(type: AnalysisToolType): AnalysisResultBase[] {
    return results.value.filter(r => r.type === type)
  }

  // ==================== 可见性控制 ====================

  function toggleResultVisibility(id: string) {
    const result = results.value.find(r => r.id === id)
    if (result) {
      result.visible = !result.visible
    }
  }

  function setVisibility(id: string, visible: boolean) {
    const result = results.value.find(r => r.id === id)
    if (result) {
      result.visible = visible
    }
  }

  function setVisibilityBatch(ids: string[], visible: boolean) {
    const idSet = new Set(ids)
    results.value.forEach(r => {
      if (idSet.has(r.id)) {
        r.visible = visible
      }
    })
  }

  function setVisibilityByType(type: AnalysisToolType, visible: boolean) {
    results.value.forEach(r => {
      if (r.type === type) {
        r.visible = visible
      }
    })
  }

  function setAllVisible(visible: boolean) {
    results.value.forEach(r => { r.visible = visible })
  }

  // ==================== 结果更新 ====================

  function updateResultName(id: string, name: string) {
    const result = results.value.find(r => r.id === id)
    if (result) result.name = name
  }

  function updateResultData(id: string, data: any) {
    const result = results.value.find(r => r.id === id)
    if (result) result.data = data
  }

  // ==================== 导入导出 ====================

  function exportResults(ids?: string[]): string {
    const data = ids
      ? results.value.filter(r => ids.includes(r.id))
      : results.value
    return JSON.stringify(data, null, 2)
  }

  function importResults(json: string): number {
    try {
      const data = JSON.parse(json)
      const items = Array.isArray(data) ? data : [data]
      let count = 0
      items.forEach((item: any) => {
        if (item.type && item.data) {
          results.value.push({
            ...item,
            id: item.id || `imported_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
            timestamp: item.timestamp ? new Date(item.timestamp) : new Date()
          })
          count++
        }
      })
      return count
    } catch {
      return 0
    }
  }

  // ==================== 持久化 ====================

  function saveToStorage() {
    try {
      const data = results.value.map(r => ({
        ...r,
        timestamp: r.timestamp.toISOString()
      }))
      localStorage.setItem(STORAGE_KEY, JSON.stringify(data))
    } catch (e) {
      console.warn('[AnalysisStore] 保存到localStorage失败:', e)
    }
  }

  function loadFromStorage() {
    try {
      const json = localStorage.getItem(STORAGE_KEY)
      if (json) {
        const data = JSON.parse(json)
        results.value = data.map((r: any) => ({
          ...r,
          timestamp: new Date(r.timestamp)
        }))
      }
    } catch (e) {
      console.warn('[AnalysisStore] 从localStorage加载失败:', e)
    }
  }

  function saveToolConfig(type: string, config: any) {
    toolConfigs.value[type] = config
    try {
      localStorage.setItem(CONFIG_KEY, JSON.stringify(toolConfigs.value))
    } catch { /* ignore */ }
  }

  function loadToolConfig(type: string): any {
    return toolConfigs.value[type]
  }

  function loadAllConfigs() {
    try {
      const json = localStorage.getItem(CONFIG_KEY)
      if (json) {
        toolConfigs.value = JSON.parse(json)
      }
    } catch { /* ignore */ }
  }

  // ==================== 观测台管理 ====================

  function addViewpoint(name: string, cameraView: CameraView, thumbnail?: string) {
    viewpoints.value.push({
      id: `vp_${Date.now()}`,
      name,
      cameraView,
      thumbnail
    })
  }

  function removeViewpoint(id: string) {
    viewpoints.value = viewpoints.value.filter(v => v.id !== id)
  }

  function renameViewpoint(id: string, name: string) {
    const vp = viewpoints.value.find(v => v.id === id)
    if (vp) vp.name = name
  }

  // ==================== 性能指标 ====================

  function updatePerformanceMetrics(metrics: Partial<PerformanceMetrics>) {
    performanceMetrics.value = { ...performanceMetrics.value, ...metrics }
    performanceHistory.value.push({
      time: Date.now(),
      metrics: { ...performanceMetrics.value }
    })
    if (performanceHistory.value.length > maxHistorySize) {
      performanceHistory.value.shift()
    }
  }

  function clearPerformanceHistory() {
    performanceHistory.value = []
  }

  // ==================== 分析状态 ====================

  function startAnalysis(type: AnalysisToolType) {
    currentToolType.value = type
    isAnalyzing.value = true
    isLoading.value = true
    error.value = null
  }

  function stopAnalysis() {
    currentToolType.value = null
    isAnalyzing.value = false
    isLoading.value = false
  }

  function setError(err: AnalysisError) {
    error.value = err
    isAnalyzing.value = false
    isLoading.value = false
  }

  function clearError() {
    error.value = null
  }

  // ==================== 初始化 ====================

  const toolManager: IAnalysisToolManager = getToolManager()
  const resultManager: IResultManager = getResultManager()
  const concurrencyManager = new ConcurrencyManager()
  const performanceMonitor = new PerformanceMonitor()
  const graphicObjectPool = new GraphicObjectPool()
  const fallbackService = new FallbackService()

  function init() {
    loadFromStorage()
    loadAllConfigs()
    performanceMonitor.startFpsMonitor()
  }

  function dispose() {
    performanceMonitor.stopFpsMonitor()
    concurrencyManager.clearQueue()
    graphicObjectPool.clearAll()
  }

  // ==================== Return ====================
  return {
    // Core managers
    toolManager, resultManager,
    concurrencyManager, performanceMonitor, graphicObjectPool, fallbackService,
    // State
    results, currentToolType, isAnalyzing, isLoading, error,
    performanceMetrics, performanceHistory, maxHistorySize,
    viewpoints, toolConfigs,
    // Getters
    recentResults, resultsByType, hasError, successResults, resultCount,
    // Actions - CRUD
    addResult, addErrorResult, removeResult, removeResults,
    clearAll, clearByType, getResult, getResultsByType,
    // Actions - 可见性
    toggleResultVisibility, setVisibility, setVisibilityBatch,
    setVisibilityByType, setAllVisible,
    // Actions - 更新
    updateResultName, updateResultData,
    // Actions - 导入导出
    exportResults, importResults,
    // Actions - 持久化
    saveToStorage, loadFromStorage, saveToolConfig, loadToolConfig, loadAllConfigs,
    // Actions - 观测台
    addViewpoint, removeViewpoint, renameViewpoint,
    // Actions - 性能
    updatePerformanceMetrics, clearPerformanceHistory,
    // Actions - 分析状态
    startAnalysis, stopAnalysis, setError, clearError,
    // Actions - 初始化
    init, dispose
  }
})
