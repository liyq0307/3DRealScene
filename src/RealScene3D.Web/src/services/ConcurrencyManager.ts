import type { AnalysisToolType } from '@/types/analysis'

interface ConcurrentTask {
  type: AnalysisToolType
  execute: () => Promise<any>
  resolve: (value: any) => void
  reject: (error: Error) => void
}

const MEASURE_TOOLS: AnalysisToolType[] = [
  'distance', 'distance-surface', 'area', 'area-surface',
  'height', 'height-triangle', 'bearing', 'coordinate-measure'
]

const INTERACTIVE_DRAW_TOOLS: AnalysisToolType[] = [
  'coordinate', 'map-marking', 'flatten', 'visibility', 'viewshed',
  'profile', 'contour', 'volume', 'plot-ratio', 'building-layout',
  'building-spacing', 'business-format', 'constraint', 'site-selection',
  'tower-foundation', 'pipeline', 'layer-comparison'
]

export class ConcurrencyManager {
  private maxConcurrency: number
  private runningCount = 0
  private queue: ConcurrentTask[] = []
  private runningTypes = new Set<AnalysisToolType>()

  constructor(maxConcurrency = 4) {
    this.maxConcurrency = maxConcurrency
  }

  async execute<T>(type: AnalysisToolType, task: () => Promise<T>): Promise<T> {
    if (this.isInteractiveDrawTool(type) && this.hasActiveInteractiveTool()) {
      throw new Error(`交互式绘制工具互斥，请先关闭当前活动工具`)
    }

    return new Promise<T>((resolve, reject) => {
      const entry: ConcurrentTask = {
        type,
        execute: task,
        resolve: (value: any) => resolve(value as T),
        reject
      }

      if (this.canRun(type)) {
        this.startTask(entry)
      } else {
        this.queue.push(entry)
      }
    })
  }

  cancelByType(type: AnalysisToolType): void {
    this.queue = this.queue.filter(t => {
      if (t.type === type) {
        t.reject(new Error('任务已取消'))
        return false
      }
      return true
    })
  }

  clearQueue(): void {
    this.queue.forEach(t => t.reject(new Error('队列已清空')))
    this.queue = []
  }

  getQueueLength(): number {
    return this.queue.length
  }

  getRunningCount(): number {
    return this.runningCount
  }

  getRunningTypes(): AnalysisToolType[] {
    return Array.from(this.runningTypes)
  }

  setMaxConcurrency(max: number): void {
    this.maxConcurrency = Math.max(1, max)
    this.dispatchQueue()
  }

  isMeasureTool(type: AnalysisToolType): boolean {
    return MEASURE_TOOLS.includes(type)
  }

  isInteractiveDrawTool(type: AnalysisToolType): boolean {
    return INTERACTIVE_DRAW_TOOLS.includes(type)
  }

  private canRun(type: AnalysisToolType): boolean {
    if (this.isMeasureTool(type)) {
      return this.runningCount < this.maxConcurrency
    }
    return this.runningCount === 0 || !this.hasActiveInteractiveTool()
  }

  private hasActiveInteractiveTool(): boolean {
    for (const type of this.runningTypes) {
      if (this.isInteractiveDrawTool(type)) return true
    }
    return false
  }

  private startTask(task: ConcurrentTask): void {
    this.runningCount++
    this.runningTypes.add(task.type)

    task.execute()
      .then(result => {
        this.finishTask(task)
        task.resolve(result)
      })
      .catch(error => {
        this.finishTask(task)
        task.reject(error)
      })
  }

  private finishTask(task: ConcurrentTask): void {
    this.runningCount--
    this.runningTypes.delete(task.type)
    this.dispatchQueue()
  }

  private dispatchQueue(): void {
    while (this.queue.length > 0) {
      const next = this.queue[0]
      if (this.canRun(next.type)) {
        this.queue.shift()
        this.startTask(next)
      } else {
        break
      }
    }
  }
}

let _instance: ConcurrencyManager | null = null

export function getConcurrencyManager(): ConcurrencyManager {
  if (!_instance) {
    _instance = new ConcurrencyManager()
  }
  return _instance
}
