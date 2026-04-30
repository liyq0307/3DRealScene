import { ref, onUnmounted, type Ref } from 'vue'
import type {
  AnalysisToolType,
  ToolLifecycleState,
  ToolLifecycleEvent
} from '@/types/analysis'
import { getToolManager } from '@/services/AnalysisToolManager'
import { getConcurrencyManager } from '@/services/ConcurrencyManager'

export function useToolLifecycle(_viewerInstance?: Ref<any>) {
  const toolManager = getToolManager()
  const concurrencyManager = getConcurrencyManager()

  const currentTool = ref<AnalysisToolType | null>(null)
  const lifecycleState = ref<ToolLifecycleState>('idle')
  const lastEvent = ref<ToolLifecycleEvent | null>(null)
  const isExecuting = ref(false)

  const eventHistory = ref<ToolLifecycleEvent[]>([])
  const maxHistory = 50

  function recordEvent(type: AnalysisToolType, state: ToolLifecycleState, error?: Error) {
    const event: ToolLifecycleEvent = {
      type,
      state,
      timestamp: new Date(),
      error
    }
    lastEvent.value = event
    eventHistory.value.push(event)
    if (eventHistory.value.length > maxHistory) {
      eventHistory.value.shift()
    }
  }

  function activate(type: AnalysisToolType) {
    try {
      lifecycleState.value = 'activating'
      recordEvent(type, 'activating')

      const instance = toolManager.activateTool(type)
      if (instance) {
        currentTool.value = type
        lifecycleState.value = 'active'
        recordEvent(type, 'active')
      } else {
        lifecycleState.value = 'error'
        recordEvent(type, 'error', new Error(`激活工具 ${type} 失败`))
      }
    } catch (e) {
      lifecycleState.value = 'error'
      recordEvent(type, 'error', e as Error)
    }
  }

  function deactivate(type?: AnalysisToolType) {
    const target = type || currentTool.value
    if (!target) return

    try {
      lifecycleState.value = 'deactivating'
      recordEvent(target, 'deactivating')

      toolManager.deactivateTool(target)

      if (currentTool.value === target) {
        currentTool.value = null
      }
      lifecycleState.value = 'idle'
      recordEvent(target, 'idle')
    } catch (e) {
      lifecycleState.value = 'error'
      recordEvent(target, 'error', e as Error)
    }
  }

  async function execute(type: AnalysisToolType, params?: any) {
    try {
      lifecycleState.value = 'executing'
      isExecuting.value = true
      recordEvent(type, 'executing')

      const result = await concurrencyManager.execute(type, () =>
        toolManager.executeTool(type, params)
      )

      lifecycleState.value = 'active'
      isExecuting.value = false
      recordEvent(type, 'active')

      return result
    } catch (e) {
      lifecycleState.value = 'error'
      isExecuting.value = false
      recordEvent(type, 'error', e as Error)
      throw e
    }
  }

  function deactivateAll() {
    toolManager.deactivateAll()
    currentTool.value = null
    lifecycleState.value = 'idle'
  }

  function isToolActive(type: AnalysisToolType): boolean {
    return toolManager.isToolActive(type)
  }

  function resetState() {
    lifecycleState.value = 'idle'
    isExecuting.value = false
    lastEvent.value = null
  }

  const onActivated = (handler: (e: any) => void) => {
    toolManager.on('activated', handler)
    onUnmounted(() => toolManager.off('activated', handler))
  }

  const onDeactivated = (handler: (e: any) => void) => {
    toolManager.on('deactivated', handler)
    onUnmounted(() => toolManager.off('deactivated', handler))
  }

  const onExecuted = (handler: (e: any) => void) => {
    toolManager.on('executed', handler)
    onUnmounted(() => toolManager.off('executed', handler))
  }

  const onError = (handler: (e: any) => void) => {
    toolManager.on('error', handler)
    onUnmounted(() => toolManager.off('error', handler))
  }

  onUnmounted(() => {
    if (currentTool.value) {
      deactivate()
    }
  })

  return {
    currentTool,
    lifecycleState,
    lastEvent,
    isExecuting,
    eventHistory,
    activate,
    deactivate,
    execute,
    deactivateAll,
    isToolActive,
    resetState,
    onActivated,
    onDeactivated,
    onExecuted,
    onError
  }
}
