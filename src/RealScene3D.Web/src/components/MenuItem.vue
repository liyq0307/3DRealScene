<template>
  <div 
    class="menu-item"
    :class="{
      'menu-item-active': active,
      'menu-item-collapsed': collapsed,
      'menu-item-has-children': hasChildren
    }"
    :role="hasChildren ? 'treeitem' : 'menuitem'"
    :aria-current="active ? 'page' : undefined"
    :aria-expanded="hasChildren ? expanded : undefined"
    :aria-haspopup="hasChildren ? 'menu' : undefined"
    @click="handleClick"
    @keydown="handleKeydown"
  >
    <!-- 图标 -->
    <span class="menu-item-icon" :title="collapsed ? label : undefined">
      {{ icon }}
    </span>
    
    <!-- 文本（折叠时隐藏） -->
    <span v-if="!collapsed" class="menu-item-label">
      {{ label }}
    </span>
    
    <!-- 展开图标（有子菜单时显示） -->
    <span 
      v-if="hasChildren && !collapsed" 
      class="menu-item-expand-icon"
      :class="{ 'menu-item-expand-icon-rotated': expanded }"
    >
      ▾
    </span>
    
    <!-- Tooltip（折叠时显示） -->
    <div v-if="collapsed" class="menu-item-tooltip">
      {{ label }}
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * MenuItem 组件
 * 菜单项组件，支持图标、文本、子菜单展开/收起
 */
import { computed } from 'vue'
import type { MenuItemConfig } from '@/types/menu.types'

// Props定义
interface Props {
  /** 菜单项ID */
  id: string
  /** 图标 */
  icon: string
  /** 显示文本 */
  label: string
  /** 路由路径 */
  path?: string
  /** 子菜单项 */
  children?: MenuItemConfig[]
  /** 菜单是否折叠 */
  collapsed: boolean
  /** 是否激活状态 */
  active: boolean
  /** 子菜单是否展开 */
  expanded: boolean
  /** 菜单层级（0为顶级） */
  level: number
}

const props = defineProps<Props>()

// Emits定义
const emit = defineEmits<{
  /** 点击事件 */
  (e: 'click', id: string): void
  /** 切换子菜单展开状态 */
  (e: 'toggle-expand', id: string): void
}>()

// 是否有子菜单
const hasChildren = computed(() => 
  props.children && props.children.length > 0
)

/**
 * 处理点击事件
 */
function handleClick() {
  if (hasChildren.value) {
    // 有子菜单，切换展开状态
    emit('toggle-expand', props.id)
  } else if (props.path) {
    // 有路径，触发导航事件
    emit('click', props.id)
  }
}

/**
 * 处理鼠标点击（用于导航）
 */
function handleMouseClick(event: MouseEvent) {
  // 阻止默认行为
  event.preventDefault()
  handleClick()
}

/**
 * 处理键盘事件
 */
function handleKeydown(event: KeyboardEvent) {
  if (event.key === 'Enter' || event.key === ' ') {
    event.preventDefault()
    handleClick()
  }
}
</script>

<style scoped>
.menu-item {
  position: relative;
  display: flex;
  align-items: center;
  height: var(--menu-item-height);
  padding: var(--menu-item-padding);
  color: var(--menu-text-color);
  font-weight: 500;
  font-size: 0.95rem;
  cursor: pointer;
  transition: var(--menu-transition);
  border-radius: var(--border-radius);
  margin: 0.25rem 0.5rem;
  user-select: none;
  white-space: nowrap;
  overflow: hidden;
}

.menu-item:hover {
  background: var(--menu-item-hover-bg);
  color: var(--menu-text-active-color);
  transform: translateX(4px);
}

.menu-item-active {
  background: var(--menu-item-active-bg);
  color: var(--menu-text-active-color);
  font-weight: 600;
  box-shadow: inset 3px 0 0 var(--primary-color);
}

.menu-item-active:hover {
  transform: none;
}

/* 折叠状态 */
.menu-item-collapsed {
  justify-content: center;
  padding: 0;
  margin: 0.25rem;
}

.menu-item-collapsed .menu-item-icon {
  margin: 0;
}

/* 图标 */
.menu-item-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: var(--menu-icon-size);
  height: var(--menu-icon-size);
  font-size: var(--menu-icon-size);
  flex-shrink: 0;
  margin-right: 0.75rem;
}

.menu-item-collapsed .menu-item-icon {
  margin-right: 0;
}

/* 文本 */
.menu-item-label {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
}

/* 展开图标 */
.menu-item-expand-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.75rem;
  transition: transform 0.25s cubic-bezier(0.4, 0, 0.2, 1);
  margin-left: 0.5rem;
}

.menu-item-expand-icon-rotated {
  transform: rotate(180deg);
}

/* Tooltip */
.menu-item-tooltip {
  position: absolute;
  left: calc(100% + 0.5rem);
  top: 50%;
  transform: translateY(-50%);
  padding: 0.5rem 0.75rem;
  background: var(--gray-900);
  color: white;
  font-size: 0.85rem;
  font-weight: 500;
  border-radius: var(--border-radius-sm);
  white-space: nowrap;
  opacity: 0;
  pointer-events: none;
  transition: opacity 0.2s ease;
  z-index: var(--z-index-tooltip);
  box-shadow: var(--shadow-md);
}

.menu-item-collapsed:hover .menu-item-tooltip {
  opacity: 1;
}

/* 可访问性：焦点样式 */
.menu-item:focus-visible {
  outline: 2px solid var(--primary-color);
  outline-offset: -2px;
}
</style>
