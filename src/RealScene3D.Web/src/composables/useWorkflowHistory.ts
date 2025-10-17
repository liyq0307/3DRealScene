/**
 * 工作流历史记录管理 - 实现撤销/重做功能
 */

import { ref, Ref } from 'vue'
import type { WorkflowDefinition } from '@/types/workflow'

interface HistoryState {
  workflow: WorkflowDefinition
  timestamp: number
}

export function useWorkflowHistory(initialWorkflow: Ref<WorkflowDefinition>) {
  // 历史记录栈
  const history = ref<HistoryState[]>([])
  const currentIndex = ref(-1)
  const maxHistorySize = 50 // 最大历史记录数量

  // 计算属性
  const canUndo = ref(false)
  const canRedo = ref(false)

  /**
   * 保存当前状态到历史记录
   */
  const saveState = (workflow: WorkflowDefinition) => {
    // 深拷贝工作流状态
    const state: HistoryState = {
      workflow: JSON.parse(JSON.stringify(workflow)),
      timestamp: Date.now()
    }

    // 如果当前不在历史记录末尾,清除后面的记录
    if (currentIndex.value < history.value.length - 1) {
      history.value = history.value.slice(0, currentIndex.value + 1)
    }

    // 添加新状态
    history.value.push(state)

    // 限制历史记录大小
    if (history.value.length > maxHistorySize) {
      history.value.shift()
    } else {
      currentIndex.value++
    }

    // 更新可用状态
    updateAvailability()
  }

  /**
   * 撤销操作
   */
  const undo = (): WorkflowDefinition | null => {
    if (!canUndo.value) return null

    currentIndex.value--
    updateAvailability()

    return history.value[currentIndex.value].workflow
  }

  /**
   * 重做操作
   */
  const redo = (): WorkflowDefinition | null => {
    if (!canRedo.value) return null

    currentIndex.value++
    updateAvailability()

    return history.value[currentIndex.value].workflow
  }

  /**
   * 更新撤销/重做可用状态
   */
  const updateAvailability = () => {
    canUndo.value = currentIndex.value > 0
    canRedo.value = currentIndex.value < history.value.length - 1
  }

  /**
   * 清空历史记录
   */
  const clear = () => {
    history.value = []
    currentIndex.value = -1
    updateAvailability()
  }

  /**
   * 初始化历史记录
   */
  const init = (workflow: WorkflowDefinition) => {
    clear()
    saveState(workflow)
  }

  return {
    canUndo,
    canRedo,
    saveState,
    undo,
    redo,
    clear,
    init
  }
}
