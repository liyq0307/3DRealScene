const ERROR_MESSAGES: Record<string, string> = {
  'terrain_load_failed': '地形数据加载失败，请检查网络连接或地形服务配置',
  'tileset_load_failed': '3DTiles模型加载失败，请确认模型URL可访问',
  'analysis_timeout': '分析操作超时，可能是计算量过大，请缩小分析范围',
  'backend_unavailable': '后端服务不可用，已切换到前端简化计算模式',
  'invalid_coordinates': '坐标值超出有效范围，经度-180~180，纬度-90~90',
  'draw_cancelled': '绘制操作已取消',
  'no_terrain_data': '无法获取地形高程数据，贴地计算不可用',
  'insufficient_points': '选点不足，请继续在地图上选择点',
  'zero_area': '分析区域面积为零，请重新绘制',
  'sightline_failed': '通视线计算失败，可能是地形数据缺失',
  'profile_sample_failed': '剖面高程采样失败，请检查地形数据是否加载',
  'volume_calc_failed': '体积计算失败，请确认分析区域和基准高度设置正确',
  'unknown': '操作失败，请重试或联系管理员'
}

export class FallbackService {
  private backendAvailable = true
  private retryCount = new Map<string, number>()
  private maxRetries = 3
  private lastError: { code: string; message: string; timestamp: Date } | null = null

  getErrorMessage(code: string): string {
    return ERROR_MESSAGES[code] || ERROR_MESSAGES['unknown']
  }

  recordError(code: string, _originalError?: Error): string {
    this.lastError = {
      code,
      message: this.getErrorMessage(code),
      timestamp: new Date()
    }

    const count = (this.retryCount.get(code) || 0) + 1
    this.retryCount.set(code, count)

    if (code === 'backend_unavailable') {
      this.backendAvailable = false
    }

    return this.lastError.message
  }

  canRetry(code: string): boolean {
    const count = this.retryCount.get(code) || 0
    return count < this.maxRetries
  }

  async withRetry<T>(
    operation: string,
    fn: () => Promise<T>,
    fallback?: () => Promise<T>
  ): Promise<T> {
    try {
      const result = await fn()
      this.retryCount.delete(operation)
      return result
    } catch (e) {
      const error = e as Error
      this.recordError(operation, error)

      if (this.canRetry(operation)) {
        try {
          return await fn()
        } catch { /* retry failed */ }
      }

      if (fallback && !this.backendAvailable) {
        console.warn(`[FallbackService] 降级执行: ${operation}`)
        return await fallback()
      }

      throw new Error(this.getErrorMessage(operation))
    }
  }

  isBackendAvailable(): boolean {
    return this.backendAvailable
  }

  setBackendAvailable(available: boolean): void {
    this.backendAvailable = available
    if (available) {
      this.retryCount.delete('backend_unavailable')
    }
  }

  getLastError(): { code: string; message: string; timestamp: Date } | null {
    return this.lastError
  }

  getRetryCount(code: string): number {
    return this.retryCount.get(code) || 0
  }

  resetRetryCount(code?: string): void {
    if (code) {
      this.retryCount.delete(code)
    } else {
      this.retryCount.clear()
    }
  }

  async checkBackendHealth(healthUrl: string): Promise<boolean> {
    try {
      const response = await fetch(healthUrl, {
        method: 'HEAD',
        signal: AbortSignal.timeout(5000)
      })
      this.backendAvailable = response.ok
      return response.ok
    } catch {
      this.backendAvailable = false
      return false
    }
  }
}

let _instance: FallbackService | null = null

export function getFallbackService(): FallbackService {
  if (!_instance) {
    _instance = new FallbackService()
  }
  return _instance
}
