import type {
  AnalysisToolType,
  AnalysisCategory,
  AnalysisParams,
  AnalysisResultBase,
  IAnalysisTool,
  AnalysisToolDefinition,
  IAnalysisToolManager
} from '@/types/analysis'

export class AnalysisToolManager implements IAnalysisToolManager {
  private definitions = new Map<AnalysisToolType, AnalysisToolDefinition>()
  private instances = new Map<AnalysisToolType, IAnalysisTool>()
  private activeTools = new Set<AnalysisToolType>()
  private listeners = new Map<string, Array<(event: any) => void>>()

  registerTool(definition: AnalysisToolDefinition): void {
    if (this.definitions.has(definition.type)) {
      console.warn(`[ToolManager] 工具 ${definition.type} 已注册，将覆盖`)
    }
    this.definitions.set(definition.type, definition)
  }

  registerTools(definitions: AnalysisToolDefinition[]): void {
    definitions.forEach(def => this.registerTool(def))
  }

  activateTool(type: AnalysisToolType): IAnalysisTool | null {
    const definition = this.definitions.get(type)
    if (!definition) {
      console.error(`[ToolManager] 未注册的工具类型: ${type}`)
      return null
    }

    if (this.activeTools.has(type)) {
      return this.instances.get(type) || null
    }

    if (definition.exclusive !== false) {
      for (const activeType of this.activeTools) {
        const activeDef = this.definitions.get(activeType)
        if (activeDef?.exclusive !== false) {
          this.deactivateTool(activeType)
        }
      }
    }

    let instance = this.instances.get(type)
    if (!instance && definition.factory) {
      try {
        instance = definition.factory(null)
        this.instances.set(type, instance)
      } catch (e) {
        console.error(`[ToolManager] 创建工具实例失败: ${type}`, e)
        this.emit('error', { type, error: e })
        return null
      }
    }

    this.activeTools.add(type)
    this.emit('activated', { type })
    return instance || null
  }

  deactivateTool(type: AnalysisToolType): void {
    if (!this.activeTools.has(type)) return

    const instance = this.instances.get(type)
    if (instance) {
      try {
        instance.clear()
      } catch (e) {
        console.warn(`[ToolManager] 清理工具 ${type} 失败:`, e)
      }
    }

    this.activeTools.delete(type)
    this.emit('deactivated', { type })
  }

  async executeTool(type: AnalysisToolType, params?: AnalysisParams): Promise<AnalysisResultBase> {
    const definition = this.definitions.get(type)
    if (!definition) {
      throw new Error(`未注册的工具类型: ${type}`)
    }

    let instance = this.activateTool(type)
    if (!instance) {
      throw new Error(`无法激活工具: ${type}`)
    }

    this.emit('executing', { type })

    try {
      const result = await instance.execute(params || definition.defaultParams)
      this.emit('executed', { type, result })
      return result
    } catch (e) {
      this.emit('error', { type, error: e })
      throw e
    }
  }

  getActiveTools(): IAnalysisTool[] {
    return Array.from(this.activeTools)
      .map(type => this.instances.get(type))
      .filter((inst): inst is IAnalysisTool => inst != null)
  }

  getToolDefinition(type: AnalysisToolType): AnalysisToolDefinition | undefined {
    return this.definitions.get(type)
  }

  getAllToolDefinitions(): AnalysisToolDefinition[] {
    return Array.from(this.definitions.values())
  }

  getToolsByCategory(category: AnalysisCategory): AnalysisToolDefinition[] {
    return Array.from(this.definitions.values())
      .filter(def => def.category === category)
  }

  searchTools(keyword: string): AnalysisToolDefinition[] {
    const lower = keyword.toLowerCase()
    return Array.from(this.definitions.values())
      .filter(def =>
        def.name.toLowerCase().includes(lower) ||
        def.description.toLowerCase().includes(lower) ||
        def.type.toLowerCase().includes(lower)
      )
  }

  deactivateAll(): void {
    for (const type of Array.from(this.activeTools)) {
      this.deactivateTool(type)
    }
  }

  isToolActive(type: AnalysisToolType): boolean {
    return this.activeTools.has(type)
  }

  destroyTool(type: AnalysisToolType): void {
    this.deactivateTool(type)
    const instance = this.instances.get(type)
    if (instance) {
      try {
        instance.destroy()
      } catch { /* ignore */ }
      this.instances.delete(type)
    }
  }

  destroyAll(): void {
    this.deactivateAll()
    for (const [, instance] of this.instances) {
      try {
        instance.destroy()
      } catch { /* ignore */ }
    }
    this.instances.clear()
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
}

let _instance: AnalysisToolManager | null = null

export function getToolManager(): AnalysisToolManager {
  if (!_instance) {
    _instance = new AnalysisToolManager()
  }
  return _instance
}

export function resetToolManager(): void {
  if (_instance) {
    _instance.destroyAll()
    _instance = null
  }
}
