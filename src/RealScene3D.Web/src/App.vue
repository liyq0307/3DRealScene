<template>
  <!-- 主应用容器，采用Flexbox布局结构 -->
  <div class="container">
    <!-- Skip Link - 可访问性支持 -->
    <a href="#main-content" class="skip-link">
      跳转到主内容
    </a>
    
    <!-- 左侧菜单 -->
    <SidebarMenu v-if="!hideLayout" v-model:collapsed="menuCollapsed" />
    
    <!-- 主内容区域，使用路由视图显示不同页面 -->
    <main 
      id="main-content"
      class="main-content"
      :class="{ 'main-content-fullscreen': hideLayout }"
      :style="{ marginLeft: hideLayout ? '0' : menuMarginLeft }"
      tabindex="-1"
    >
      <router-view />
    </main>

    <!-- Toast消息提示 -->
    <MessageToast />
  </div>
</template>

<script setup lang="ts">
/**
 * Vue 3 组合式API主应用组件
 * 负责整体布局和路由视图管理
 * 采用TypeScript增强类型安全性
 */
import { ref, computed, watch } from 'vue'
import { useRoute } from 'vue-router'
import SidebarMenu from '@/components/SidebarMenu.vue'
import MessageToast from '@/components/MessageToast.vue'
import { useResponsiveSingleton } from '@/composables/useResponsive'

const route = useRoute()

// 响应式检测
const responsive = useResponsiveSingleton()

// 菜单折叠状态
const menuCollapsed = ref(responsive.defaultCollapsed.value)

// 根据路由meta控制是否隐藏布局（用于全屏预览页面）
const hideLayout = computed(() => {
  return route.meta.hideLayout === true
})

// 菜单左边距
const menuMarginLeft = computed(() => {
  if (responsive.isMobile.value) {
    return '0'
  }
  return menuCollapsed.value ? 'var(--menu-width-collapsed)' : 'var(--menu-width-expanded)'
})

// 监听响应式变化，自动调整菜单折叠状态
watch(
  () => responsive.defaultCollapsed.value,
  (defaultCollapsed) => {
    menuCollapsed.value = defaultCollapsed
  }
)
</script>

<style scoped>
.container {
  height: 100vh;
  display: flex;
  background: var(--gray-100);
}

/* Skip Link 样式 */
.skip-link {
  position: absolute;
  top: -100px;
  left: 50%;
  transform: translateX(-50%);
  background: var(--neon-cyan);
  color: var(--dark-bg-primary);
  padding: 0.75rem 1.5rem;
  border-radius: var(--border-radius);
  font-weight: 600;
  text-decoration: none;
  z-index: 9999;
  transition: top var(--transition-fast);
  box-shadow: var(--glow-cyan);
}

.skip-link:focus {
  top: 1rem;
  outline: 2px solid var(--dark-bg-primary);
  outline-offset: 2px;
}

.main-content {
  flex: 1;
  overflow-y: auto;
  overflow-x: hidden;
  min-height: 0;
  width: 100%;
  transition: margin-left 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  /* 添加滚动条样式 */
  scrollbar-width: thin;
  scrollbar-color: var(--primary-color) var(--gray-200);
}

.main-content:focus {
  outline: none;
}

.main-content-fullscreen {
  margin-left: 0 !important;
}

/* Webkit浏览器滚动条样式 */
.main-content::-webkit-scrollbar {
  width: 10px;
}

.main-content::-webkit-scrollbar-track {
  background: var(--gray-100);
}

.main-content::-webkit-scrollbar-thumb {
  background: var(--primary-color);
  border-radius: 5px;
}

.main-content::-webkit-scrollbar-thumb:hover {
  background: var(--primary-hover);
}

/* 路由过渡动画 */
.fade-enter-active,
.fade-leave-active {
  transition: opacity var(--transition-base), transform var(--transition-base);
}

.fade-enter-from {
  opacity: 0;
  transform: translateY(10px);
}

.fade-leave-to {
  opacity: 0;
  transform: translateY(-10px);
}

.slide-left-enter-active,
.slide-left-leave-active {
  transition: all var(--transition-base);
}

.slide-left-enter-from {
  opacity: 0;
  transform: translateX(30px);
}

.slide-left-leave-to {
  opacity: 0;
  transform: translateX(-30px);
}

.slide-right-enter-active,
.slide-right-leave-active {
  transition: all var(--transition-base);
}

.slide-right-enter-from {
  opacity: 0;
  transform: translateX(-30px);
}

.slide-right-leave-to {
  opacity: 0;
  transform: translateX(30px);
}

.zoom-enter-active,
.zoom-leave-active {
  transition: all var(--transition-base);
}

.zoom-enter-from {
  opacity: 0;
  transform: scale(0.95);
}

.zoom-leave-to {
  opacity: 0;
  transform: scale(1.05);
}
</style>
