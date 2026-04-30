import type {
  AnalysisToolType,
  AnalysisResultBase,
  IResultManager
} from '@/types/analysis'

const STORAGE_KEY = 'analysis_results'
const MAX_STORAGE_ITEMS = 200

export class ResultManager implements IResultManager {
  private results = new Map<string, AnalysisResultBase>()
  private listeners = new Map<string, Array<(event: any) => void>>()

  addResult(result: AnalysisResultBase): void {
    this.results.set(result.id, result)
    this.emit('added', { result })
    this.autoSave()
  }

  removeResult(id: string): void {
    const result = this.results.get(id)
    if (result) {
      this.results.delete(id)
      this.emit('removed', { id, result })
      this.autoSave()
    }
  }

  removeResults(ids: string[]): void {
    const idSet = new Set(ids)
    for (const id of idSet) {
      this.results.delete(id)
    }
    this.emit('batchRemoved', { ids })
    this.autoSave()
  }

  clearAll(): void {
    this.results.clear()
    this.emit('cleared', {})
    this.autoSave()
  }

  clearByType(type: AnalysisToolType): void {
    const toRemove: string[] = []
    for (const [id, result] of this.results) {
      if (result.type === type) {
        toRemove.push(id)
      }
    }
    toRemove.forEach(id => this.results.delete(id))
    this.emit('clearedByType', { type })
    this.autoSave()
  }

  getResult(id: string): AnalysisResultBase | undefined {
    return this.results.get(id)
  }

  getAllResults(): AnalysisResultBase[] {
    return Array.from(this.results.values())
  }

  getResultsByType(type: AnalysisToolType): AnalysisResultBase[] {
    return Array.from(this.results.values()).filter(r => r.type === type)
  }

  setVisibility(id: string, visible: boolean): void {
    const result = this.results.get(id)
    if (result) {
      result.visible = visible
      this.emit('visibilityChanged', { id, visible })
    }
  }

  setVisibilityBatch(ids: string[], visible: boolean): void {
    const idSet = new Set(ids)
    for (const [id, result] of this.results) {
      if (idSet.has(id)) {
        result.visible = visible
      }
    }
    this.emit('batchVisibilityChanged', { ids, visible })
  }

  updateName(id: string, name: string): void {
    const result = this.results.get(id)
    if (result) {
      result.name = name
      this.emit('updated', { id, field: 'name', value: name })
    }
  }

  updateData(id: string, data: unknown): void {
    const result = this.results.get(id)
    if (result) {
      result.data = data
      this.emit('updated', { id, field: 'data' })
    }
  }

  exportResults(ids?: string[], format?: 'json' | 'csv'): string | Blob {
    const data = ids
      ? ids.map(id => this.results.get(id)).filter((r): r is AnalysisResultBase => r != null)
      : this.getAllResults()

    if (format === 'csv') {
      return this.exportToCSV(data)
    }

    return JSON.stringify(data, null, 2)
  }

  saveToStorage(): void {
    try {
      const data = this.getAllResults()
        .slice(-MAX_STORAGE_ITEMS)
        .map(r => ({
          ...r,
          timestamp: r.timestamp instanceof Date
            ? r.timestamp.toISOString()
            : r.timestamp
        }))
      localStorage.setItem(STORAGE_KEY, JSON.stringify(data))
    } catch (e) {
      console.warn('[ResultManager] 保存到localStorage失败:', e)
    }
  }

  loadFromStorage(): void {
    try {
      const json = localStorage.getItem(STORAGE_KEY)
      if (json) {
        const data = JSON.parse(json)
        this.results.clear()
        for (const item of data) {
          this.results.set(item.id, {
            ...item,
            timestamp: item.timestamp ? new Date(item.timestamp) : new Date()
          })
        }
        this.emit('loaded', { count: this.results.size })
      }
    } catch (e) {
      console.warn('[ResultManager] 从localStorage加载失败:', e)
    }
  }

  get count(): number {
    return this.results.size
  }

  on(event: string, handler: (event: any) => void): void {
    if (!this.listeners.has(event)) {
      this.listeners.set(event, [])
    }
    this.listeners.get(event)!.push(handler)
  }

  off(event: string, handler: (event: any) => void): void {
    const handlers = this.listeners.get(event)
    if (handlers) {
      const idx = handlers.indexOf(handler)
      if (idx !== -1) handlers.splice(idx, 1)
    }
  }

  private emit(event: string, data: any): void {
    const handlers = this.listeners.get(event)
    if (handlers) {
      handlers.forEach(h => {
        try { h(data) } catch { /* ignore */ }
      })
    }
  }

  private autoSave(): void {
    if (this.results.size % 5 === 0) {
      this.saveToStorage()
    }
  }

  private exportToCSV(data: AnalysisResultBase[]): Blob {
    const headers = ['id', 'type', 'name', 'timestamp', 'visible', 'status']
    const rows = data.map(r =>
      headers.map(h => {
        const val = (r as any)[h]
        return typeof val === 'string' ? `"${val}"` : val
      }).join(',')
    )
    const csv = [headers.join(','), ...rows].join('\n')
    return new Blob([csv], { type: 'text/csv;charset=utf-8' })
  }
}

let _instance: ResultManager | null = null

export function getResultManager(): ResultManager {
  if (!_instance) {
    _instance = new ResultManager()
    _instance.loadFromStorage()
  }
  return _instance
}

export function resetResultManager(): void {
  if (_instance) {
    _instance.saveToStorage()
    _instance = null
  }
}
