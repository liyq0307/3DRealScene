<template>
  <button 
    class="menu-toggle"
    :class="{ 'menu-toggle-collapsed': collapsed }"
    :title="collapsed ? '展开菜单' : '折叠菜单'"
    :aria-label="collapsed ? '展开菜单' : '折叠菜单'"
    @click="handleToggle"
  >
    <span class="menu-toggle-icon">◀</span>
  </button>
</template>

<script setup lang="ts">
/**
 * MenuToggle 组件
 * 菜单折叠/展开切换按钮组件
 */

// Props定义
interface Props {
  /** 菜单是否折叠 */
  collapsed: boolean
}

defineProps<Props>()

// Emits定义
const emit = defineEmits<{
  /** 切换事件 */
  (e: 'toggle'): void
}>()

/**
 * 处理切换
 */
function handleToggle() {
  emit('toggle')
}
</script>

<style scoped>
.menu-toggle {
  display: flex;
  align-items: center;
  justify-content: center;
  width: var(--menu-toggle-size);
  height: var(--menu-toggle-size);
  background: transparent;
  border: 1px solid var(--gray-200);
  border-radius: var(--border-radius);
  cursor: pointer;
  transition: var(--menu-transition);
  color: var(--gray-600);
  font-size: 0.875rem;
  padding: 0;
}

.menu-toggle:hover {
  background: var(--primary-lighter);
  border-color: var(--primary-color);
  color: var(--primary-color);
  transform: scale(1.05);
}

.menu-toggle:active {
  transform: scale(0.95);
}

/* 折叠状态下的图标旋转 */
.menu-toggle-collapsed .menu-toggle-icon {
  transform: rotate(180deg);
}

/* 图标 */
.menu-toggle-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  transition: transform 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

/* 可访问性：焦点样式 */
.menu-toggle:focus-visible {
  outline: 2px solid var(--primary-color);
  outline-offset: 2px;
}
</style>
