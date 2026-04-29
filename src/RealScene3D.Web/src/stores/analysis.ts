import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type {
  AnalysisToolType,
  AnalysisResultBase,
  AnalysisError,
  PerformanceMetrics,
  AnalysisState
} from '@/types/analysis'

// Re-export from types for backward compatibility
export type { AnalysisToolType, AnalysisResultBase as AnalysisResult, AnalysisError, PerformanceMetrics }

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

  function clearAll() {
    results.value = []
    error.value = null
  }

  function clearByType(type: AnalysisToolType) {
    results.value = results.value.filter(r => r.type !== type)
  }

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

  function toggleResultVisibility(id: string) {
    const result = results.value.find(r => r.id === id)
    if (result) {
      result.visible = !result.visible
    }
  }

  // ==================== Return ====================
  return {
    // State
    results,
    currentToolType,
    isAnalyzing,
    isLoading,
    error,
    performanceMetrics,
    performanceHistory,
    maxHistorySize,
    // Getters
    recentResults,
    resultsByType,
    hasError,
    successResults,
    // Actions
    addResult,
    addErrorResult,
    removeResult,
    clearAll,
    clearByType,
    updatePerformanceMetrics,
    clearPerformanceHistory,
    startAnalysis,
    stopAnalysis,
    setError,
    clearError,
    toggleResultVisibility
  }
})
