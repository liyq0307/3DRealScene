/**
 * WebWorker 计算池
 * 支持并行计算、任务队列、优先级管理
 */

interface WorkerTask<T = any> {
  id: string
  type: string
  data: any
  priority: number
  resolve: (result: T) => void
  reject: (error: Error) => void
}

export class WorkerPool {
  private workers: Worker[] = []
  private taskQueue: WorkerTask[] = []
  private activeTasks: Map<Worker, WorkerTask> = new Map()
  private taskCounter = 0
  private poolSize: number

  constructor(poolSize = Math.min(navigator.hardwareConcurrency || 4, 8)) {
    this.poolSize = poolSize
  }

  /** 初始化Worker池 */
  init(workerScript: string) {
    this.destroy()
    for (let i = 0; i < this.poolSize; i++) {
      try {
        const worker = new Worker(workerScript, { type: 'module' })
        worker.onmessage = (e) => this.handleWorkerMessage(worker, e)
        worker.onerror = (e) => this.handleWorkerError(worker, e)
        this.workers.push(worker)
      } catch (err) {
        console.warn(`[WorkerPool] 创建Worker ${i} 失败:`, err)
      }
    }
  }

  /** 使用内联代码初始化 */
  initFromCode(code: string) {
    this.destroy()
    const blob = new Blob([code], { type: 'application/javascript' })
    const url = URL.createObjectURL(blob)
    for (let i = 0; i < this.poolSize; i++) {
      try {
        const worker = new Worker(url)
        worker.onmessage = (e) => this.handleWorkerMessage(worker, e)
        worker.onerror = (e) => this.handleWorkerError(worker, e)
        this.workers.push(worker)
      } catch (err) {
        console.warn(`[WorkerPool] 创建Worker ${i} 失败:`, err)
      }
    }
    URL.revokeObjectURL(url)
  }

  /** 执行任务 */
  execute<T>(type: string, data: any, priority = 0): Promise<T> {
    return new Promise((resolve, reject) => {
      const task: WorkerTask<T> = {
        id: `task_${++this.taskCounter}`,
        type,
        data,
        priority,
        resolve,
        reject
      }
      this.enqueueTask(task)
      this.dispatchTasks()
    })
  }

  /** 批量执行 */
  async executeBatch<T>(tasks: Array<{ type: string; data: any; priority?: number }>): Promise<T[]> {
    return Promise.all(
      tasks.map(t => this.execute<T>(t.type, t.data, t.priority || 0))
    )
  }

  /** 取消任务 */
  cancel(taskId: string): boolean {
    const index = this.taskQueue.findIndex(t => t.id === taskId)
    if (index !== -1) {
      const task = this.taskQueue.splice(index, 1)[0]
      task.reject(new Error('任务已取消'))
      return true
    }
    return false
  }

  /** 清空任务队列 */
  clearQueue() {
    this.taskQueue.forEach(t => t.reject(new Error('队列已清空')))
    this.taskQueue = []
  }

  /** 销毁Worker池 */
  destroy() {
    this.taskQueue.forEach(t => t.reject(new Error('Worker池已销毁')))
    this.taskQueue = []
    this.activeTasks.clear()
    this.workers.forEach(w => w.terminate())
    this.workers = []
  }

  /** 获取活动任务数 */
  getActiveTaskCount(): number {
    return this.activeTasks.size
  }

  /** 获取队列长度 */
  getQueueLength(): number {
    return this.taskQueue.length
  }

  /** 获取空闲Worker数 */
  getIdleWorkerCount(): number {
    return this.workers.length - this.activeTasks.size
  }

  // ==================== 私有方法 ====================

  private enqueueTask(task: WorkerTask) {
    // 按优先级插入（高优先级在前）
    let insertIndex = this.taskQueue.length
    for (let i = 0; i < this.taskQueue.length; i++) {
      if (this.taskQueue[i].priority < task.priority) {
        insertIndex = i
        break
      }
    }
    this.taskQueue.splice(insertIndex, 0, task)
  }

  private dispatchTasks() {
    while (this.taskQueue.length > 0 && this.getIdleWorkerCount() > 0) {
      const task = this.taskQueue.shift()!
      const worker = this.getIdleWorker()
      if (worker) {
        this.activeTasks.set(worker, task)
        worker.postMessage({ id: task.id, type: task.type, data: task.data })
      }
    }
  }

  private getIdleWorker(): Worker | null {
    for (const worker of this.workers) {
      if (!this.activeTasks.has(worker)) {
        return worker
      }
    }
    return null
  }

  private handleWorkerMessage(worker: Worker, e: MessageEvent) {
    const task = this.activeTasks.get(worker)
    if (task) {
      this.activeTasks.delete(worker)
      if (e.data.error) {
        task.reject(new Error(e.data.error))
      } else {
        task.resolve(e.data.result)
      }
      this.dispatchTasks()
    }
  }

  private handleWorkerError(worker: Worker, e: ErrorEvent) {
    const task = this.activeTasks.get(worker)
    if (task) {
      this.activeTasks.delete(worker)
      task.reject(new Error(e.message || 'Worker执行错误'))
      this.dispatchTasks()
    }
  }
}

/** 全局Worker池实例 */
let globalPool: WorkerPool | null = null

export function getWorkerPool(): WorkerPool {
  if (!globalPool) {
    globalPool = new WorkerPool()
  }
  return globalPool
}

export function destroyWorkerPool() {
  if (globalPool) {
    globalPool.destroy()
    globalPool = null
  }
}
