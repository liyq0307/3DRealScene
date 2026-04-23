<template>
  <aside 
    class="sidebar-menu"
    :class="{
      'sidebar-menu-collapsed': collapsed,
      'sidebar-menu-mobile': isMobile,
      'sidebar-menu-mobile-open': isMobile && mobileMenuOpen
    }"
    :style="{ width: menuWidth }"
    role="navigation"
    aria-label="主导航菜单"
  >
    <!-- 移动端遮罩层 -->
    <div 
      v-if="isMobile && mobileMenuOpen" 
      class="sidebar-menu-overlay"
      @click="closeMobileMenu"
    ></div>
    
    <!-- 菜单头部 -->
    <div class="sidebar-menu-header">
      <div class="sidebar-menu-brand">
        <span class="brand-icon">🌍</span>
        <span v-if="!collapsed" class="brand-text">3D场景管理</span>
      </div>
      <MenuToggle 
        v-if="!isMobile"
        :collapsed="collapsed" 
        @toggle="handleToggle" 
      />
    </div>
    
    <!-- 菜单内容 -->
    <nav class="sidebar-menu-nav">
      <template v-for="item in menuItems" :key="item.id">
        <!-- 菜单项 -->
        <MenuItem
          :id="item.id"
          :icon="item.icon"
          :label="item.label"
          :path="item.path"
          :children="item.children"
          :collapsed="collapsed"
          :active="isItemActive(item)"
          :expanded="isExpanded(item.id)"
          :level="0"
          @click="handleNavigate"
          @toggle-expand="handleToggleExpand"
        />
        
        <!-- 子菜单 -->
        <SubMenu
          v-if="item.children && item.children.length > 0"
          :items="item.children"
          :collapsed="collapsed"
          :active-id="activeMenuId"
          :parent-id="item.id"
          :expanded="isExpanded(item.id)"
          @navigate="handleNavigate"
        />
      </template>
    </nav>
    
    <!-- 用户信息 -->
    <UserInfo :collapsed="collapsed" />
    
    <!-- 移动端汉堡菜单按钮 -->
    <button 
      v-if="isMobile"
      class="mobile-menu-btn"
      :class="{ 'mobile-menu-btn-active': mobileMenuOpen }"
      @click="toggleMobileMenu"
      aria-label="切换菜单"
    >
      <span class="hamburger-line"></span>
      <span class="hamburger-line"></span>
      <span class="hamburger-line"></span>
    </button>
  </aside>
</template>

<script setup lang="ts">
/**
 * SidebarMenu 组件
 * 左侧菜单容器组件，管理整体菜单布局和状态
 */
import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import MenuItem from './MenuItem.vue'
import SubMenu from './SubMenu.vue'
import MenuToggle from './MenuToggle.vue'
import UserInfo from './UserInfo.vue'
import { useMenuStateSingleton } from '@/composables/useMenuState'
import { useMenuConfig } from '@/composables/useMenuConfig'
import { useResponsiveSingleton } from '@/composables/useResponsive'
import type { MenuItemConfig } from '@/types/menu.types'

// Props定义
interface Props {
  /** 菜单是否折叠（v-model） */
  collapsed?: boolean
  /** 默认折叠状态 */
  defaultCollapsed?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  collapsed: undefined,
  defaultCollapsed: false
})

// Emits定义
const emit = defineEmits<{
  /** 更新折叠状态 */
  (e: 'update:collapsed', value: boolean): void
  /** 导航事件 */
  (e: 'navigate', path: string): void
}>()

const router = useRouter()
const route = useRoute()

// 获取菜单状态
const menuState = useMenuStateSingleton()
const { menuItems, getMenuByPath, isPathActive, getActiveMenuPath } = useMenuConfig()
const responsive = useResponsiveSingleton()

// 本地状态
const localCollapsed = ref(props.defaultCollapsed)
const mobileMenuOpen = ref(false)

// 计算折叠状态
const collapsed = computed({
  get: () => props.collapsed !== undefined ? props.collapsed : localCollapsed.value,
  set: (value) => {
    localCollapsed.value = value
    emit('update:collapsed', value)
  }
})

// 响应式状态
const isMobile = computed(() => responsive.isMobile.value)

// 菜单宽度
const menuWidth = computed(() => {
  if (isMobile.value) {
    return mobileMenuOpen.value ? '280px' : '0'
  }
  return collapsed.value ? 'var(--menu-width-collapsed)' : 'var(--menu-width-expanded)'
})

// 激活的菜单ID
const activeMenuId = computed(() => menuState.activeMenuId.value)

// 展开的菜单列表
const expandedMenus = computed(() => menuState.expandedMenus.value)

/**
 * 检查菜单项是否激活
 */
function isItemActive(item: MenuItemConfig): boolean {
  return isPathActive(item, route.path)
}

/**
 * 检查子菜单是否展开
 */
function isExpanded(menuId: string): boolean {
  return expandedMenus.value.includes(menuId)
}

/**
 * 处理导航
 */
function handleNavigate(id: string) {
  // 根据ID查找菜单项
  const menuItem = menuItems.value.find(item => item.id === id) || 
                   menuItems.value.find(item => item.children?.some(child => child.id === id))
  
  // 查找子菜单项
  let targetItem = menuItem
  if (menuItem && menuItem.children) {
    targetItem = menuItem.children.find(child => child.id === id)
  }
  
  // 如果找到菜单项且有路径，执行导航
  if (targetItem && targetItem.path) {
    // 设置激活菜单
    menuState.setActiveMenu(id)
    
    // 执行路由导航
    router.push(targetItem.path)
    
    // 关闭移动端菜单
    if (isMobile.value) {
      closeMobileMenu()
    }
  }
}

/**
 * 处理子菜单展开/收起
 */
function handleToggleExpand(menuId: string) {
  menuState.toggleExpand(menuId)
}

/**
 * 处理折叠切换
 */
function handleToggle() {
  collapsed.value = !collapsed.value
  menuState.setCollapsed(collapsed.value)
}

/**
 * 切换移动端菜单
 */
function toggleMobileMenu() {
  mobileMenuOpen.value = !mobileMenuOpen.value
}

/**
 * 关闭移动端菜单
 */
function closeMobileMenu() {
  mobileMenuOpen.value = false
}

// 监听路由变化，更新激活菜单
watch(
  () => route.path,
  (path) => {
    const menuItem = getMenuByPath(path)
    if (menuItem) {
      menuState.setActiveMenu(menuItem.id)
      
      // 自动展开父菜单
      const activePath = getActiveMenuPath(path)
      activePath.forEach(menuId => {
        menuState.expandMenu(menuId)
      })
    }
  },
  { immediate: true }
)

// 监听响应式变化，自动调整折叠状态
watch(
  () => responsive.defaultCollapsed.value,
  (defaultCollapsed) => {
    if (props.collapsed === undefined) {
      collapsed.value = defaultCollapsed
    }
  },
  { immediate: true }
)

// 键盘导航支持
function handleKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape' && isMobile.value && mobileMenuOpen.value) {
    closeMobileMenu()
  }
}

// 触摸滑动支持
let touchStartX = 0
let touchEndX = 0

function handleTouchStart(event: TouchEvent) {
  touchStartX = event.touches[0].clientX
}

function handleTouchMove(event: TouchEvent) {
  touchEndX = event.touches[0].clientX
}

function handleTouchEnd() {
  const touchDiff = touchStartX - touchEndX
  const SWIPE_THRESHOLD = 50 // 滑动阈值
  
  // 向左滑动关闭菜单
  if (touchDiff > SWIPE_THRESHOLD && mobileMenuOpen.value) {
    closeMobileMenu()
  }
  
  // 重置
  touchStartX = 0
  touchEndX = 0
}

onMounted(() => {
  document.addEventListener('keydown', handleKeydown)
  
  // 添加触摸事件监听
  if (isMobile.value) {
    document.addEventListener('touchstart', handleTouchStart, { passive: true })
    document.addEventListener('touchmove', handleTouchMove, { passive: true })
    document.addEventListener('touchend', handleTouchEnd)
  }
})

onUnmounted(() => {
  document.removeEventListener('keydown', handleKeydown)
  
  // 移除触摸事件监听
  document.removeEventListener('touchstart', handleTouchStart)
  document.removeEventListener('touchmove', handleTouchMove)
  document.removeEventListener('touchend', handleTouchEnd)
})
</script>

<style scoped>
.sidebar-menu {
  position: fixed;
  top: 0;
  left: 0;
  height: 100vh;
  background: var(--menu-bg);
  backdrop-filter: blur(20px) saturate(180%);
  -webkit-backdrop-filter: blur(20px) saturate(180%);
  border-right: 1px solid var(--menu-border);
  box-shadow: var(--menu-shadow);
  display: flex;
  flex-direction: column;
  z-index: var(--z-index-fixed);
  transition: width 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  overflow: hidden;
}

.sidebar-menu-collapsed {
  width: var(--menu-width-collapsed);
}

/* 移动端样式 */
.sidebar-menu-mobile {
  width: 0;
  transition: width 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  /* 防止移动端滚动穿透 */
  overscroll-behavior: contain;
}

.sidebar-menu-mobile-open {
  width: 280px;
  /* 阻止背景滚动 */
  position: fixed;
  top: 0;
  left: 0;
  height: 100vh;
  overflow-y: auto;
  overflow-x: hidden;
}

/* 移动端遮罩层 */
.sidebar-menu-overlay {
  position: fixed;
  top: 0;
  left: 280px;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  z-index: -1;
  animation: fadeIn 0.3s ease;
}

@keyframes fadeIn {
  from { opacity: 0; }
  to { opacity: 1; }
}

/* 菜单头部 */
.sidebar-menu-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 1rem;
  border-bottom: 1px solid var(--gray-200);
  min-height: 60px;
}

.sidebar-menu-brand {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  font-weight: 700;
  font-size: 1.1rem;
  color: var(--gray-800);
}

.brand-icon {
  font-size: 1.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
}

.brand-text {
  background: var(--gradient-primary);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
  white-space: nowrap;
}

/* 菜单导航 */
.sidebar-menu-nav {
  flex: 1;
  overflow-y: auto;
  overflow-x: hidden;
  padding: 0.5rem 0;
}

/* 滚动条样式 */
.sidebar-menu-nav::-webkit-scrollbar {
  width: 6px;
}

.sidebar-menu-nav::-webkit-scrollbar-track {
  background: transparent;
}

.sidebar-menu-nav::-webkit-scrollbar-thumb {
  background: var(--gray-300);
  border-radius: 3px;
}

.sidebar-menu-nav::-webkit-scrollbar-thumb:hover {
  background: var(--gray-400);
}

/* 移动端汉堡菜单按钮 */
.mobile-menu-btn {
  position: fixed;
  top: 1rem;
  left: 1rem;
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  width: 40px;
  height: 40px;
  background: white;
  border: 1px solid var(--gray-200);
  border-radius: var(--border-radius);
  cursor: pointer;
  padding: 0;
  z-index: calc(var(--z-index-fixed) + 1);
  box-shadow: var(--shadow-md);
  transition: var(--menu-transition);
}

.mobile-menu-btn:hover {
  background: var(--primary-lighter);
  border-color: var(--primary-color);
}

.mobile-menu-btn-active {
  background: var(--primary-color);
  border-color: var(--primary-color);
}

.hamburger-line {
  width: 20px;
  height: 2px;
  background: var(--gray-700);
  margin: 2px 0;
  transition: all 0.3s ease;
  border-radius: 1px;
}

.mobile-menu-btn-active .hamburger-line:nth-child(1) {
  transform: rotate(45deg) translate(5px, 5px);
  background: white;
}

.mobile-menu-btn-active .hamburger-line:nth-child(2) {
  opacity: 0;
}

.mobile-menu-btn-active .hamburger-line:nth-child(3) {
  transform: rotate(-45deg) translate(5px, -5px);
  background: white;
}

/* 可访问性：焦点样式 */
.sidebar-menu:focus-visible {
  outline: 2px solid var(--primary-color);
  outline-offset: -2px;
}
</style>
