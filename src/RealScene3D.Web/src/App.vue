<template>
  <!-- 主应用容器，采用Flexbox布局结构 -->
  <div class="container">
    <!-- 应用头部，显示品牌和标题信息 -->
    <header v-if="!hideLayout" class="header">
      <!-- 移动端汉堡菜单按钮 -->
      <button @click="toggleMobileMenu" class="mobile-menu-btn" :class="{ active: isMobileMenuOpen }">
        <span class="hamburger-line"></span>
        <span class="hamburger-line"></span>
        <span class="hamburger-line"></span>
      </button>

      <!-- 导航菜单 -->
      <nav class="nav" :class="{ 'nav-open': isMobileMenuOpen }">
        <router-link to="/" class="nav-link" @click="closeMobileMenu">
          <span class="nav-icon">🏠</span>
          <span class="nav-text">首页</span>
        </router-link>

        <!-- 场景管理下拉菜单 -->
        <div class="nav-dropdown" @mouseenter="showDropdown('scenes')" @mouseleave="hideDropdown('scenes')" @click="toggleDropdown('scenes')" :class="{ 'dropdown-open': activeDropdown === 'scenes' }">
          <div class="nav-link dropdown-toggle" :class="{ active: isSceneActive }">
            <span class="nav-icon">🌍</span>
            <span class="nav-text">场景管理</span>
            <span class="dropdown-icon" :class="{ rotated: activeDropdown === 'scenes' }">▾</span>
          </div>
          <div class="dropdown-menu" :class="{ 'dropdown-visible': activeDropdown === 'scenes' }">
            <router-link to="/scenes" class="dropdown-item" @click="closeMobileMenu">
              <span class="item-icon">📋</span>
              <span class="item-text">场景列表</span>
            </router-link>
            <router-link to="/scene-objects" class="dropdown-item" @click="closeMobileMenu">
              <span class="item-icon">🔧</span>
              <span class="item-text">场景对象</span>
            </router-link>
            <router-link to="/slicing" class="dropdown-item" @click="closeMobileMenu">
              <span class="item-icon">✂️</span>
              <span class="item-text">切片管理</span>
            </router-link>
          </div>
        </div>

        <!-- 元数据管理下拉菜单 -->
        <div class="nav-dropdown" @mouseenter="showDropdown('metadata')" @mouseleave="hideDropdown('metadata')" @click="toggleDropdown('metadata')" :class="{ 'dropdown-open': activeDropdown === 'metadata' }">
          <div class="nav-link dropdown-toggle" :class="{ active: isMetadataActive }">
            <span class="nav-icon">📊</span>
            <span class="nav-text">元数据管理</span>
            <span class="dropdown-icon" :class="{ rotated: activeDropdown === 'metadata' }">▾</span>
          </div>
          <div class="dropdown-menu" :class="{ 'dropdown-visible': activeDropdown === 'metadata' }">
            <router-link to="/video-metadata" class="dropdown-item" @click="closeMobileMenu">
              <span class="item-icon">🎥</span>
              <span class="item-text">视频元数据</span>
            </router-link>
            <router-link to="/bim-model-metadata" class="dropdown-item" @click="closeMobileMenu">
              <span class="item-icon">🏗️</span>
              <span class="item-text">BIM模型</span>
            </router-link>
            <router-link to="/tilt-photography-metadata" class="dropdown-item" @click="closeMobileMenu">
              <span class="item-icon">📸</span>
              <span class="item-text">倾斜摄影</span>
            </router-link>
          </div>
        </div>

        <!-- 工作流下拉菜单 -->
        <div class="nav-dropdown" @mouseenter="showDropdown('workflow')" @mouseleave="hideDropdown('workflow')" @click="toggleDropdown('workflow')" :class="{ 'dropdown-open': activeDropdown === 'workflow' }">
          <div class="nav-link dropdown-toggle" :class="{ active: isWorkflowActive }">
            <span class="nav-icon">⚡</span>
            <span class="nav-text">工作流</span>
            <span class="dropdown-icon" :class="{ rotated: activeDropdown === 'workflow' }">▾</span>
          </div>
          <div class="dropdown-menu" :class="{ 'dropdown-visible': activeDropdown === 'workflow' }">
            <router-link to="/workflow-designer" class="dropdown-item" @click="closeMobileMenu">
              <span class="item-icon">🎨</span>
              <span class="item-text">工作流设计</span>
            </router-link>
            <router-link to="/workflow-instances" class="dropdown-item" @click="closeMobileMenu">
              <span class="item-icon">📈</span>
              <span class="item-text">工作流实例</span>
            </router-link>
          </div>
        </div>

        <router-link to="/monitoring" class="nav-link" @click="closeMobileMenu">
          <span class="nav-icon">📊</span>
          <span class="nav-text">系统监控</span>
        </router-link>
      </nav>

      <!-- 用户信息和操作 -->
      <div class="user-section">
        <template v-if="isAuthenticated">
          <router-link to="/profile" class="user-info">
            <div class="user-avatar">
              <img v-if="currentUser?.avatarUrl" :src="currentUser.avatarUrl" alt="头像" />
              <span v-else>👤</span>
            </div>
            <span class="username">{{ currentUser?.username || '用户' }}</span>
          </router-link>
          <button @click="handleLogout" class="btn-logout">退出</button>
        </template>
        <template v-else>
          <router-link to="/login" class="btn-login">登录</router-link>
        </template>
      </div>
    </header>

    <!-- 主内容区域，使用路由视图显示不同页面 -->
    <main class="main-content">
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
import { ref, computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import authStore from '@/stores/auth'
import MessageToast from '@/components/MessageToast.vue'

const router = useRouter()
const route = useRoute()

// 认证状态 - authStore的isAuthenticated和currentUser已经是computed,直接使用
const isAuthenticated = authStore.isAuthenticated
const currentUser = authStore.currentUser

// 下拉菜单状态
const activeDropdown = ref<string | null>(null)

// 移动端菜单状态
const isMobileMenuOpen = ref(false)

// 根据路由meta控制是否隐藏布局（用于全屏预览页面）
const hideLayout = computed(() => {
  return route.meta.hideLayout === true
})

// 显示下拉菜单
const showDropdown = (menu: string) => {
  activeDropdown.value = menu
}

// 隐藏下拉菜单
const hideDropdown = (menu: string) => {
  if (activeDropdown.value === menu) {
    activeDropdown.value = null
  }
}

// 切换移动端菜单
const toggleMobileMenu = () => {
  isMobileMenuOpen.value = !isMobileMenuOpen.value
}

// 关闭移动端菜单
const closeMobileMenu = () => {
  isMobileMenuOpen.value = false
}

// 切换下拉菜单（移动端）
const toggleDropdown = (menu: string) => {
  activeDropdown.value = activeDropdown.value === menu ? null : menu
}

// 判断当前路由是否在场景管理分组
const isSceneActive = computed(() => {
  const sceneRoutes = ['/scenes', '/scene-objects', '/slicing']
  return sceneRoutes.includes(route.path)
})

// 判断当前路由是否在元数据管理分组
const isMetadataActive = computed(() => {
  const metadataRoutes = ['/video-metadata', '/bim-model-metadata', '/tilt-photography-metadata']
  return metadataRoutes.includes(route.path)
})

// 判断当前路由是否在工作流分组
const isWorkflowActive = computed(() => {
  const workflowRoutes = ['/workflow-designer', '/workflow-instances']
  return workflowRoutes.includes(route.path)
})
// 切换移动端菜单nst toggleMobileMenu = () => {  isMobileMenuOpen.value = !isMobileMenuOpen.value}
// 关闭移动端菜单nst closeMobileMenu = () => {  isMobileMenuOpen.value = false}
// 切换下拉菜单（移动端）nst toggleDropdown = (menu: string) => {  activeDropdown.value = activeDropdown.value === menu ? null : menu}


// 处理登出
const handleLogout = async () => {
  try {
    authStore.logout()
    await router.push('/login')
  } catch (error) {
    console.error('Logout error:', error)
  }
}
</script>

<style scoped>
.container {
  height: 100vh;
  display: flex;
  flex-direction: column;
  background: var(--gray-100);
}

.header {
  flex-shrink: 0;
  position: sticky;
  top: 0;
  z-index: var(--z-index-sticky);
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem 2rem;
  background: rgba(255, 255, 255, 0.9);
  backdrop-filter: blur(20px) saturate(180%);
  -webkit-backdrop-filter: blur(20px) saturate(180%);
  border-bottom: 1px solid rgba(229, 231, 235, 0.5);
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
  gap: 2rem;
  animation: slideDown 0.4s cubic-bezier(0.4, 0, 0.2, 1);
  transition: all var(--transition-base);
}

/* 移动端汉堡菜单按钮 */
.mobile-menu-btn {
  display: none;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  width: 40px;
  height: 40px;
  background: transparent;
  border: none;
  cursor: pointer;
  padding: 0;
  border-radius: var(--border-radius);
  transition: all var(--transition-base);
  z-index: 1001;
}

.mobile-menu-btn:hover {
  background: var(--primary-lighter);
}

.mobile-menu-btn.active {
  background: var(--primary-color);
}

.hamburger-line {
  width: 24px;
  height: 2px;
  background: var(--gray-700);
  margin: 2px 0;
  transition: all 0.3s ease;
  border-radius: 1px;
}

.mobile-menu-btn.active .hamburger-line:nth-child(1) {
  transform: rotate(45deg) translate(6px, 6px);
}

.mobile-menu-btn.active .hamburger-line:nth-child(2) {
  opacity: 0;
}

.mobile-menu-btn.active .hamburger-line:nth-child(3) {
  transform: rotate(-45deg) translate(6px, -6px);
}

@keyframes slideDown {
  from {
    transform: translateY(-100%);
    opacity: 0;
  }
  to {
    transform: translateY(0);
    opacity: 1;
  }
}

.header h1 {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 700;
  background: var(--gradient-primary);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
  letter-spacing: -0.5px;
  white-space: nowrap;
}

.nav {
  display: flex;
  gap: 0.5rem;
  flex: 1;
  justify-content: center;
  align-items: center;
}

/* 下拉菜单容器 */
.nav-dropdown {
  position: relative;
}

.nav-link {
  position: relative;
  text-decoration: none;
  color: var(--gray-700);
  font-weight: 500;
  font-size: 1rem;
  padding: 0.7rem 1.4rem;
  border-radius: var(--border-radius);
  transition: all var(--transition-base);
  white-space: nowrap;
  overflow: hidden;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  cursor: pointer;
}

.nav-icon {
  font-size: 1.1rem;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 20px;
  height: 20px;
}

.nav-text {
  flex: 1;
}

.dropdown-toggle {
  display: flex;
  align-items: center;
  gap: 0.3rem;
}

.dropdown-icon {
  font-size: 0.8rem;
  transition: transform var(--transition-base);
}

.nav-dropdown:hover .dropdown-icon {
  transform: rotate(180deg);
}

.dropdown-icon.rotated {
  transform: rotate(180deg);
}

.nav-link::before {
  content: '';
  position: absolute;
  bottom: 4px;
  left: 50%;
  transform: translateX(-50%) scaleX(0);
  width: calc(100% - 1.5rem);
  height: 3px;
  background: var(--gradient-primary-alt);
  border-radius: var(--border-radius-full);
  transition: transform var(--transition-base);
}

.nav-link::after {
  content: '';
  position: absolute;
  inset: 0;
  border-radius: var(--border-radius);
  background: var(--primary-light);
  opacity: 0;
  transition: opacity var(--transition-base);
  z-index: -1;
}

.nav-link:hover {
  color: var(--primary-color);
  transform: translateY(-2px);
}

.nav-link:hover::after {
  opacity: 1;
}

.nav-link:hover::before {
  transform: translateX(-50%) scaleX(1);
}

.nav-link.router-link-active,
.nav-link.active {
  color: var(--primary-color);
  font-weight: 600;
  background: linear-gradient(135deg, var(--primary-lighter) 0%, var(--primary-light) 100%);
  box-shadow: var(--shadow-sm), inset 0 1px 2px rgba(99, 102, 241, 0.1);
}

.nav-link.router-link-active::before,
.nav-link.active::before {
  transform: translateX(-50%) scaleX(1);
  background: var(--gradient-primary-alt);
}

/* 下拉菜单样式 */
.dropdown-menu {
  position: absolute;
  top: 100%;
  left: 0;
  min-width: 200px;
  max-width: 280px;

  background: white;
  border-radius: var(--border-radius);
  box-shadow: 0 8px 25px rgba(0, 0, 0, 0.15), 0 0 0 1px rgba(0, 0, 0, 0.05);
  overflow: hidden;
  opacity: 0;
  transform: translateY(-10px) scale(0.95);
  pointer-events: none;
  transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1);
  z-index: 1000;
}

.dropdown-menu.dropdown-visible {
  opacity: 1;
  transform: translateY(0) scale(1);
  pointer-events: auto;
}

@keyframes dropdownSlide {
  from {
    opacity: 0;
    transform: translateY(-10px) scale(0.95);
  }
  to {
    opacity: 1;
    transform: translateY(0) scale(1);
  }
}

.dropdown-item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem 1.2rem;
  color: var(--gray-700);
  text-decoration: none;
  font-size: 0.9rem;
  font-weight: 500;
  transition: all var(--transition-base);
  border-left: 3px solid transparent;
}

.item-icon {
  font-size: 1rem;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 18px;
  height: 18px;
}

.item-text {
  flex: 1;
}

.dropdown-item:hover {
  background: linear-gradient(90deg, var(--primary-lighter) 0%, transparent 100%);
  color: var(--primary-color);
  border-left-color: var(--primary-color);
  padding-left: 1.4rem;
  transform: translateX(4px);
}

.dropdown-item.router-link-active {
  background: linear-gradient(90deg, var(--primary-light) 0%, var(--primary-lighter) 100%);
  color: var(--primary-color);
  font-weight: 600;
  border-left-color: var(--primary-color);
}

.main-content {
  flex: 1;
  overflow-y: auto;
  overflow-x: hidden;
  min-height: 0;
  width: 100%;
  /* 添加滚动条样式 */
  scrollbar-width: thin;
  scrollbar-color: var(--primary-color) var(--gray-200);
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

.user-section {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.user-info {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 1rem;
  background: linear-gradient(135deg, #ffffff 0%, #fafafa 100%);
  border-radius: var(--border-radius-full);
  border: 1px solid var(--gray-200);
  box-shadow: var(--shadow-sm);
  transition: all var(--transition-base);
  cursor: pointer;
  text-decoration: none;
  color: inherit;
}

.user-info:hover {
  box-shadow: var(--shadow-md);
  transform: translateY(-2px);
  border-color: var(--primary-color);
  background: linear-gradient(135deg, #ffffff 0%, var(--primary-lighter) 100%);
}

.user-avatar {
  font-size: 1.2rem;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  background: var(--gradient-primary-alt);
  border-radius: var(--border-radius-full);
  box-shadow: 0 2px 8px rgba(99, 102, 241, 0.3);
  transition: all var(--transition-base);
  overflow: hidden;
}

.user-avatar img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.user-avatar span {
  display: flex;
  align-items: center;
  justify-content: center;
}

.user-info:hover .user-avatar {
  transform: scale(1.1) rotate(5deg);
  box-shadow: 0 4px 12px rgba(99, 102, 241, 0.4);
}

.username {
  font-size: 0.9rem;
  font-weight: 600;
  color: var(--gray-800);
  letter-spacing: -0.2px;
}

.btn-logout,
.btn-login {
  position: relative;
  padding: 0.6rem 1.4rem;
  border: 1px solid var(--gray-200);
  border-radius: var(--border-radius);
  background: white;
  color: var(--gray-700);
  text-decoration: none;
  cursor: pointer;
  transition: all var(--transition-base);
  font-size: 0.9rem;
  font-weight: 600;
  box-shadow: var(--shadow-sm);
  white-space: nowrap;
  overflow: hidden;
  display: inline-block;
}

.btn-logout::before,
.btn-login::before {
  content: '';
  position: absolute;
  inset: 0;
  border-radius: var(--border-radius);
  background: var(--gradient-primary-alt);
  opacity: 0;
  transition: opacity var(--transition-base);
  z-index: -1;
}

.btn-logout:hover,
.btn-login:hover {
  box-shadow: var(--shadow-md);
  transform: translateY(-2px);
  border-color: transparent;
}

.btn-logout:hover {
  background: var(--danger-light);
  color: var(--danger-color);
  border-color: var(--danger-color);
}

.btn-logout:active {
  transform: translateY(0);
  box-shadow: var(--shadow-sm);
}

.btn-login {
  background: var(--gradient-primary-alt);
  color: white;
  border: none;
  box-shadow: var(--shadow-colored);
}

.btn-login::before {
  background: var(--gradient-info);
}

.btn-login:hover {
  color: white;
  box-shadow: var(--shadow-xl);
}

.btn-login:hover::before {
  opacity: 1;
}

.btn-login:active {
  transform: translateY(0);
  box-shadow: var(--shadow-colored);
}

/* 响应式设计 */
@media (max-width: 1200px) {
  .nav {
    gap: 0.25rem;
  }

  .nav-link {
    padding: 0.6rem 1rem;
    font-size: 0.95rem;
  }

  .dropdown-item {
    padding: 0.7rem 1.1rem;
    font-size: 0.9rem;
  }
}

@media (max-width: 992px) {
  .header {
    flex-wrap: wrap;
    padding: 1rem;
  }

  .header h1 {
    font-size: 1.25rem;
  }

  .nav {
    position: fixed;
    top: 100%;
    left: 0;
    right: 0;
    width: 100%;
    height: calc(100vh - 80px);
    background: rgba(255, 255, 255, 0.95);
    backdrop-filter: blur(20px);
    flex-direction: column;
    justify-content: flex-start;
    align-items: stretch;
    padding: 2rem 1rem;
    transform: translateX(-100%);
    transition: transform 0.3s cubic-bezier(0.4, 0, 0.2, 1);
    z-index: 999;
    overflow-y: auto;
  }

  .nav.nav-open {
    transform: translateX(0);
  }

  .mobile-menu-btn {
    display: flex;
  }

  .nav-link {
    padding: 1.2rem 1.5rem;
    font-size: 1.1rem;
    border-radius: var(--border-radius);
    margin-bottom: 0.25rem;
  }

  .dropdown-menu {
    position: static;
    box-shadow: none;
    background: rgba(52, 152, 219, 0.05);
    border-radius: var(--border-radius);
    margin-top: 0.5rem;
    margin-left: 1rem;
    max-width: none;
    opacity: 1;
    transform: none;
    pointer-events: auto;
    border-left: 3px solid var(--primary-color);
  }

  .dropdown-menu.dropdown-visible {
    opacity: 1;
    transform: none;
  }

  .dropdown-item {
    padding: 0.9rem 1.5rem;
    font-size: 1rem;
    border-left: none;
    margin-bottom: 0.25rem;
  }

  .dropdown-item:hover {
    background: rgba(52, 152, 219, 0.1);
    transform: translateX(8px);
    padding-left: 2rem;
  }
}

@media (max-width: 768px) {
  .header h1 {
    font-size: 1.1rem;
  }

  .user-info {
    padding: 0.4rem 0.8rem;
  }

  .username {
    display: none;
  }

  .btn-logout,
  .btn-login {
    padding: 0.5rem 0.8rem;
    font-size: 0.85rem;
  }

  .nav {
    padding: 1.5rem 1rem;
  }

  .nav-link {
    padding: 1rem 1.2rem;
    font-size: 1rem;
  }

  .dropdown-item {
    padding: 0.8rem 1.2rem;
    font-size: 0.95rem;
  }
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
