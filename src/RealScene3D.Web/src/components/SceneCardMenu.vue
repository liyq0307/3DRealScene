<template>
  <!--
    场景卡片三点菜单组件
    提供查看、编辑、删除操作选项
  -->
  <div class="scene-card-menu" @click.stop role="menu" aria-label="场景操作菜单">
    <button
      class="menu-item"
      @click="handleView"
      @keydown.enter="handleView"
      role="menuitem"
    >
      <span class="item-icon">👁️</span>
      <span class="item-text">查看场景</span>
    </button>

    <button
      class="menu-item"
      @click="handleEdit"
      @keydown.enter="handleEdit"
      role="menuitem"
    >
      <span class="item-icon">✏️</span>
      <span class="item-text">编辑</span>
    </button>

    <button
      class="menu-item menu-item-danger"
      @click="handleDelete"
      @keydown.enter="handleDelete"
      role="menuitem"
    >
      <span class="item-icon">🗑️</span>
      <span class="item-text">删除</span>
    </button>
  </div>
</template>

<script setup lang="ts">
/**
 * 场景卡片三点菜单组件 - Vue 3 组合式API实现
 *
 * 功能说明：
 * - 提供查看、编辑、删除三个操作选项
 * - 支持键盘操作
 * - 点击菜单项后自动关闭
 *
 * 技术栈：Vue 3 + TypeScript
 * 作者：liyq
 * 创建时间：2025-01-22
 */

import { onMounted, onUnmounted } from 'vue'

// ==================== Emits定义 ====================

/**
 * 组件Emits接口定义
 */
const emit = defineEmits<{
  (e: 'view'): void      // 查看场景
  (e: 'edit'): void      // 编辑场景
  (e: 'delete'): void    // 删除场景
  (e: 'close'): void     // 关闭菜单
}>()

// ==================== 事件处理 ====================

/**
 * 处理查看场景
 */
const handleView = (): void => {
  emit('view')
}

/**
 * 处理编辑场景
 */
const handleEdit = (): void => {
  emit('edit')
}

/**
 * 处理删除场景
 */
const handleDelete = (): void => {
  emit('delete')
}

/**
 * 处理键盘事件（Esc关闭菜单）
 */
const handleKeyDown = (event: KeyboardEvent): void => {
  if (event.key === 'Escape') {
    emit('close')
  }
}

// ==================== 生命周期钩子 ====================

/**
 * 组件挂载：添加键盘监听器
 */
onMounted(() => {
  document.addEventListener('keydown', handleKeyDown)
})

/**
 * 组件卸载：移除键盘监听器
 */
onUnmounted(() => {
  document.removeEventListener('keydown', handleKeyDown)
})
</script>

<style scoped>
/**
 * 三点菜单样式定义
 */

/* 菜单容器 */
.scene-card-menu {
  position: absolute;
  bottom: calc(100% + 8px);
  right: 0;
  min-width: 140px;
  background: #ffffff;
  border-radius: 6px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  overflow: hidden;
  z-index: 100;
  animation: menuSlideIn 0.2s ease;
}

/* 菜单展开动画 */
@keyframes menuSlideIn {
  from {
    opacity: 0;
    transform: translateY(8px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

/* 菜单项 */
.menu-item {
  width: 100%;
  padding: 0.75rem 1rem;
  border: none;
  background: transparent;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.9rem;
  color: #333333;
  transition: background 0.15s ease;
  text-align: left;
}

.menu-item:hover {
  background: #f5f5f5;
}

.menu-item:focus {
  outline: none;
  background: #f0f0f0;
}

/* 危险操作项（删除） */
.menu-item-danger {
  color: #ef4444;
  border-top: 1px solid #e5e7eb;
  margin-top: 0.25rem;
  padding-top: 0.75rem;
}

.menu-item-danger:hover {
  background: #fee2e2;
}

/* 图标 */
.item-icon {
  font-size: 1rem;
  line-height: 1;
}

/* 文本 */
.item-text {
  flex: 1;
  font-weight: 500;
}
</style>
