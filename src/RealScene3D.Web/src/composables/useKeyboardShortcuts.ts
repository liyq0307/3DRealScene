/**
 * 键盘快捷键管理
 */

import { onMounted, onUnmounted } from 'vue'

export interface KeyboardShortcut {
  key: string
  ctrl?: boolean
  shift?: boolean
  alt?: boolean
  handler: (event: KeyboardEvent) => void
  description?: string
}

export function useKeyboardShortcuts(shortcuts: KeyboardShortcut[]) {
  const handleKeyDown = (event: KeyboardEvent) => {
    for (const shortcut of shortcuts) {
      // 检查修饰键
      const ctrlMatch = shortcut.ctrl === undefined || shortcut.ctrl === event.ctrlKey
      const shiftMatch = shortcut.shift === undefined || shortcut.shift === event.shiftKey
      const altMatch = shortcut.alt === undefined || shortcut.alt === event.altKey

      // 检查主键
      const keyMatch = event.key.toLowerCase() === shortcut.key.toLowerCase()

      if (ctrlMatch && shiftMatch && altMatch && keyMatch) {
        event.preventDefault()
        shortcut.handler(event)
        break
      }
    }
  }

  onMounted(() => {
    document.addEventListener('keydown', handleKeyDown)
  })

  onUnmounted(() => {
    document.removeEventListener('keydown', handleKeyDown)
  })

  return {
    handleKeyDown
  }
}

/**
 * 常用快捷键列表
 */
export const COMMON_SHORTCUTS = {
  UNDO: { key: 'z', ctrl: true, description: '撤销' },
  REDO: { key: 'y', ctrl: true, description: '重做' },
  SAVE: { key: 's', ctrl: true, description: '保存' },
  DELETE: { key: 'Delete', description: '删除选中项' },
  ZOOM_IN: { key: '+', ctrl: true, description: '放大' },
  ZOOM_OUT: { key: '-', ctrl: true, description: '缩小' },
  DUPLICATE: { key: 'd', ctrl: true, description: '复制' },
  SELECT_ALL: { key: 'a', ctrl: true, description: '全选' },
  FIND: { key: 'f', ctrl: true, description: '查找' },
  EXPORT: { key: 'e', ctrl: true, description: '导出' }
}
