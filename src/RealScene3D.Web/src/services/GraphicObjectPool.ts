export class GraphicObjectPool {
  private pool = new Map<string, any[]>()
  private maxSize = 100
  private activeObjects = new Map<string, WeakSet<any>>()

  registerType(type: string, _initialSize = 10): void {
    if (!this.pool.has(type)) {
      this.pool.set(type, [])
      this.activeObjects.set(type, new WeakSet())
    }
  }

  acquire(type: string, factory: () => any): any {
    const poolArr = this.pool.get(type)
    if (poolArr && poolArr.length > 0) {
      const obj = poolArr.pop()!
      const active = this.activeObjects.get(type)
      if (active) active.add(obj)
      return obj
    }

    const obj = factory()
    const active = this.activeObjects.get(type)
    if (active) active.add(obj)
    return obj
  }

  release(type: string, obj: any, reset?: (obj: any) => void): void {
    if (reset) {
      try { reset(obj) } catch { return }
    }

    const poolArr = this.pool.get(type)
    if (poolArr && poolArr.length < this.maxSize) {
      poolArr.push(obj)
    }
  }

  releaseAll(type: string, objects: any[], reset?: (obj: any) => void): void {
    objects.forEach(obj => this.release(type, obj, reset))
  }

  getPoolSize(type: string): number {
    return this.pool.get(type)?.length ?? 0
  }

  getTotalPoolSize(): number {
    let total = 0
    for (const arr of this.pool.values()) {
      total += arr.length
    }
    return total
  }

  clearType(type: string): void {
    this.pool.get(type)?.splice(0)
  }

  clearAll(): void {
    for (const arr of this.pool.values()) {
      arr.splice(0)
    }
  }

  getStats(): Record<string, { pooled: number }> {
    const stats: Record<string, { pooled: number }> = {}
    for (const [type, arr] of this.pool) {
      stats[type] = { pooled: arr.length }
    }
    return stats
  }
}

let _instance: GraphicObjectPool | null = null

export function getGraphicObjectPool(): GraphicObjectPool {
  if (!_instance) {
    _instance = new GraphicObjectPool()
  }
  return _instance
}
