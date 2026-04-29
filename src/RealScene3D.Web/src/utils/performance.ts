/**
 * 性能监控与优化工具
 * 提供防抖、节流、内存管理、帧率监控等功能
 */

/** 防抖函数 */
export function debounce<T extends (...args: any[]) => any>(fn: T, delay: number): T {
  let timer: ReturnType<typeof setTimeout> | null = null
  return ((...args: any[]) => {
    if (timer) clearTimeout(timer)
    timer = setTimeout(() => fn(...args), delay)
  }) as T
}

/** 节流函数 */
export function throttle<T extends (...args: any[]) => any>(fn: T, interval: number): T {
  let lastTime = 0
  return ((...args: any[]) => {
    const now = Date.now()
    if (now - lastTime >= interval) {
      lastTime = now
      return fn(...args)
    }
  }) as T
}

/** 帧率监控器 */
export class FPSMonitor {
  private frames = 0
  private lastTime = performance.now()
  private _fps = 0
  private rafId: number | null = null

  get fps() { return this._fps }

  start(callback?: (fps: number) => void) {
    const measure = () => {
      this.frames++
      const now = performance.now()
      if (now - this.lastTime >= 1000) {
        this._fps = Math.round((this.frames * 1000) / (now - this.lastTime))
        this.frames = 0
        this.lastTime = now
        callback?.(this._fps)
      }
      this.rafId = requestAnimationFrame(measure)
    }
    this.rafId = requestAnimationFrame(measure)
  }

  stop() {
    if (this.rafId) {
      cancelAnimationFrame(this.rafId)
      this.rafId = null
    }
  }
}

/** 内存监控 */
export function getMemoryUsage(): number {
  const perf = performance as any
  if (perf.memory) {
    return Math.round(perf.memory.usedJSHeapSize / 1048576) // MB
  }
  return 0
}

/** 对象池 - 复用对象减少GC压力 */
export class ObjectPool<T> {
  private pool: T[] = []
  private factory: () => T
  private reset: (obj: T) => void
  private maxSize: number

  constructor(factory: () => T, reset: (obj: T) => void, maxSize = 100) {
    this.factory = factory
    this.reset = reset
    this.maxSize = maxSize
  }

  acquire(): T {
    if (this.pool.length > 0) {
      return this.pool.pop()!
    }
    return this.factory()
  }

  release(obj: T) {
    if (this.pool.length < this.maxSize) {
      this.reset(obj)
      this.pool.push(obj)
    }
  }

  clear() {
    this.pool = []
  }

  get size() { return this.pool.length }
}

/** 性能计时器 */
export class PerfTimer {
  private starts: Map<string, number> = new Map()

  start(label: string) {
    this.starts.set(label, performance.now())
  }

  end(label: string): number {
    const start = this.starts.get(label)
    if (start !== undefined) {
      const duration = performance.now() - start
      this.starts.delete(label)
      return duration
    }
    return 0
  }
}

/** 全局性能计时器实例 */
export const perfTimer = new PerfTimer()

/** 请求空闲回调（兼容性处理） */
export function requestIdleCallbackCompat(callback: () => void, timeout = 1000) {
  if ('requestIdleCallback' in window) {
    (window as any).requestIdleCallback(callback, { timeout })
  } else {
    setTimeout(callback, 1)
  }
}
