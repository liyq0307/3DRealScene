/**
 * 消息提示组件的状态管理
 */

import { ref } from 'vue'

export type MessageType = 'success' | 'error' | 'warning' | 'info'

export interface Message {
  id: string
  type: MessageType
  content: string
  duration: number
}

const messages = ref<Message[]>([])

let messageIdCounter = 0

/**
 * 消息提示服务
 */
export const useMessage = () => {
  /**
   * 显示消息
   */
  const showMessage = (content: string, type: MessageType = 'info', duration = 3000) => {
    const id = `message-${++messageIdCounter}`
    const message: Message = {
      id,
      type,
      content,
      duration
    }

    messages.value.push(message)

    // 自动移除消息
    if (duration > 0) {
      setTimeout(() => {
        removeMessage(id)
      }, duration)
    }

    return id
  }

  /**
   * 移除消息
   */
  const removeMessage = (id: string) => {
    const index = messages.value.findIndex(m => m.id === id)
    if (index !== -1) {
      messages.value.splice(index, 1)
    }
  }

  /**
   * 清空所有消息
   */
  const clearMessages = () => {
    messages.value = []
  }

  /**
   * 快捷方法
   */
  const success = (content: string, duration = 3000) => {
    return showMessage(content, 'success', duration)
  }

  const error = (content: string, duration = 3000) => {
    return showMessage(content, 'error', duration)
  }

  const warning = (content: string, duration = 3000) => {
    return showMessage(content, 'warning', duration)
  }

  const info = (content: string, duration = 3000) => {
    return showMessage(content, 'info', duration)
  }

  return {
    messages,
    showMessage,
    removeMessage,
    clearMessages,
    success,
    error,
    warning,
    info
  }
}

// 导出默认实例
const messageService = useMessage()
export default messageService
