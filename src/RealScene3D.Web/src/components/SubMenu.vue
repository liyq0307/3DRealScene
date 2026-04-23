<template>
  <transition name="submenu-expand">
    <div 
      v-show="visible"
      class="submenu"
      role="menu"
      :aria-label="`${parentId}子菜单`"
    >
      <MenuItem
        v-for="item in items"
        :key="item.id"
        :id="item.id"
        :icon="item.icon"
        :label="item.label"
        :path="item.path"
        :children="item.children"
        :collapsed="collapsed"
        :active="activeId === item.id"
        :expanded="false"
        :level="1"
        @click="handleNavigate"
        @toggle-expand="() => {}"
      />
    </div>
  </transition>
</template>

<script setup lang="ts">
/**
 * SubMenu 组件
 * 子菜单组件，渲染子菜单项列表，支持展开/收起动画
 */
import { computed } from 'vue'
import MenuItem from './MenuItem.vue'
import type { MenuItemConfig } from '@/types/menu.types'

// Props定义
interface Props {
  /** 子菜单项列表 */
  items: MenuItemConfig[]
  /** 菜单是否折叠 */
  collapsed: boolean
  /** 当前激活的菜单项ID */
  activeId: string | null
  /** 父菜单项ID */
  parentId: string
  /** 是否展开 */
  expanded: boolean
}

const props = defineProps<Props>()

// Emits定义
const emit = defineEmits<{
  /** 导航事件 */
  (e: 'navigate', id: string): void
}>()

// 是否可见
const visible = computed(() => !props.collapsed && props.expanded)

/**
 * 处理导航
 */
function handleNavigate(id: string) {
  emit('navigate', id)
}
</script>

<style scoped>
.submenu {
  overflow: hidden;
  background: rgba(0, 0, 0, 0.02);
  border-radius: var(--border-radius);
  margin: 0.25rem 0;
  margin-left: var(--submenu-indent);
}

/* 展开/收起动画 */
.submenu-expand-enter-active,
.submenu-expand-leave-active {
  transition: opacity 0.25s cubic-bezier(0.4, 0, 0.2, 1),
              transform 0.25s cubic-bezier(0.4, 0, 0.2, 1);
}

.submenu-expand-enter-from {
  opacity: 0;
  transform: translateY(-8px);
}

.submenu-expand-leave-to {
  opacity: 0;
  transform: translateY(-8px);
}

/* 优化动画性能 */
.submenu-expand-enter-active .menu-item,
.submenu-expand-leave-active .menu-item {
  will-change: transform, opacity;
}
</style>
