export interface PerformanceRecord {
  operation: string
  startTime: number
  endTime: number
  duration: number
  success: boolean
  error?: string
}

export class PerformanceMonitor {
  private records: PerformanceRecord[] = []
  private maxRecords = 500
  private timers = new Map<string, number>()
  private fpsHistory: number[] = []
  private lastFrameTime = 0
  private frameCount = 0
  private fpsInterval: ReturnType<typeof setInterval> | null = null

  startTimer(operation: string): string {
    const id = `${operation}_${Date.now()}_${Math.random().toString(36).substr(2, 5)}`
    this.timers.set(id, performance.now())
    return id
  }

  endTimer(id: string, success = true, error?: string): PerformanceRecord | null {
    const startTime = this.timers.get(id)
    if (startTime == null) return null

    this.timers.delete(id)
    const endTime = performance.now()
    const record: PerformanceRecord = {
      operation: id.split('_')[0],
      startTime,
      endTime,
      duration: endTime - startTime,
      success,
      error
    }

    this.records.push(record)
    if (this.records.length > this.maxRecords) {
      this.records.shift()
    }

    return record
  }

  async measureAsync<T>(operation: string, fn: () => Promise<T>): Promise<T> {
    const id = this.startTimer(operation)
    try {
      const result = await fn()
      this.endTimer(id, true)
      return result
    } catch (e) {
      this.endTimer(id, false, (e as Error).message)
      throw e
    }
  }

  measureSync<T>(operation: string, fn: () => T): T {
    const id = this.startTimer(operation)
    try {
      const result = fn()
      this.endTimer(id, true)
      return result
    } catch (e) {
      this.endTimer(id, false, (e as Error).message)
      throw e
    }
  }

  startFpsMonitor(): void {
    if (this.fpsInterval) return
    this.lastFrameTime = performance.now()
    this.frameCount = 0

    this.fpsInterval = setInterval(() => {
      const now = performance.now()
      const elapsed = now - this.lastFrameTime
      const fps = (this.frameCount / elapsed) * 1000

      this.fpsHistory.push(Math.round(fps))
      if (this.fpsHistory.length > 100) {
        this.fpsHistory.shift()
      }

      this.frameCount = 0
      this.lastFrameTime = now
    }, 1000)

    const trackFrame = () => {
      this.frameCount++
      requestAnimationFrame(trackFrame)
    }
    requestAnimationFrame(trackFrame)
  }

  stopFpsMonitor(): void {
    if (this.fpsInterval) {
      clearInterval(this.fpsInterval)
      this.fpsInterval = null
    }
  }

  getAverageFps(): number {
    if (this.fpsHistory.length === 0) return 0
    return this.fpsHistory.reduce((a, b) => a + b, 0) / this.fpsHistory.length
  }

  getCurrentFps(): number {
    return this.fpsHistory[this.fpsHistory.length - 1] || 0
  }

  getFpsHistory(): number[] {
    return [...this.fpsHistory]
  }

  getMemoryUsage(): { used: number; total: number; limit: number } | null {
    const perf = performance as any
    if (perf.memory) {
      return {
        used: perf.memory.usedJSHeapSize / (1024 * 1024),
        total: perf.memory.totalJSHeapSize / (1024 * 1024),
        limit: perf.memory.jsHeapSizeLimit / (1024 * 1024)
      }
    }
    return null
  }

  getOperationStats(operation: string): {
    count: number
    avgDuration: number
    maxDuration: number
    minDuration: number
    successRate: number
  } {
    const opRecords = this.records.filter(r => r.operation === operation)
    if (opRecords.length === 0) {
      return { count: 0, avgDuration: 0, maxDuration: 0, minDuration: 0, successRate: 0 }
    }

    const durations = opRecords.map(r => r.duration)
    const successCount = opRecords.filter(r => r.success).length

    return {
      count: opRecords.length,
      avgDuration: durations.reduce((a, b) => a + b, 0) / durations.length,
      maxDuration: Math.max(...durations),
      minDuration: Math.min(...durations),
      successRate: successCount / opRecords.length
    }
  }

  getRecords(operation?: string): PerformanceRecord[] {
    return operation
      ? this.records.filter(r => r.operation === operation)
      : [...this.records]
  }

  getSlowOperations(thresholdMs = 2000): PerformanceRecord[] {
    return this.records.filter(r => r.duration > thresholdMs)
  }

  clearRecords(): void {
    this.records = []
    this.fpsHistory = []
  }

  generateReport(): string {
    const operations = [...new Set(this.records.map(r => r.operation))]
    const stats = operations.map(op => {
      const s = this.getOperationStats(op)
      return `${op}: count=${s.count}, avg=${s.avgDuration.toFixed(1)}ms, max=${s.maxDuration.toFixed(1)}ms, success=${(s.successRate * 100).toFixed(1)}%`
    })

    const mem = this.getMemoryUsage()
    const memStr = mem ? `Memory: ${mem.used.toFixed(1)}MB / ${mem.total.toFixed(1)}MB` : 'Memory: N/A'

    return [
      `=== Performance Report ===`,
      `Generated: ${new Date().toISOString()}`,
      `Avg FPS: ${this.getAverageFps().toFixed(1)}`,
      memStr,
      `Total Records: ${this.records.length}`,
      '',
      '--- Operation Stats ---',
      ...stats
    ].join('\n')
  }
}

let _instance: PerformanceMonitor | null = null

export function getPerformanceMonitor(): PerformanceMonitor {
  if (!_instance) {
    _instance = new PerformanceMonitor()
  }
  return _instance
}
