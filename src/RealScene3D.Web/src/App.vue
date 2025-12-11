<template>
  <!-- 主应用容器，采用Flexbox布局结构 -->
  <div class="container">
    <!-- 应用头部，显示品牌和标题信息 -->
    <header v-if="!hideLayout" class="header">
      <h1>实景三维</h1>
      <!-- 导航菜单 -->
      <nav class="nav">
        <router-link to="/" class="nav-link">首页</router-link>

        <!-- 场景管理下拉菜单 -->
        <div class="nav-dropdown" @mouseenter="showDropdown('scenes')" @mouseleave="hideDropdown('scenes')">
          <div class="nav-link dropdown-toggle" :class="{ active: isSceneActive }">
            场景管理
            <span class="dropdown-icon">▾</span>
          </div>
          <div class="dropdown-menu" v-show="activeDropdown === 'scenes'">
            <router-link to="/scenes" class="dropdown-item">场景列表</router-link>
            <router-link to="/scene-objects" class="dropdown-item">场景对象</router-link>
            <router-link to="/slicing" class="dropdown-item">切片管理</router-link>
          </div>
        </div>

        <!-- 元数据管理下拉菜单 -->
        <div class="nav-dropdown" @mouseenter="showDropdown('metadata')" @mouseleave="hideDropdown('metadata')">
          <div class="nav-link dropdown-toggle" :class="{ active: isMetadataActive }">
            元数据管理
            <span class="dropdown-icon">▾</span>
          </div>
          <div class="dropdown-menu" v-show="activeDropdown === 'metadata'">
            <router-link to="/video-metadata" class="dropdown-item">视频元数据</router-link>
            <router-link to="/bim-model-metadata" class="dropdown-item">BIM模型</router-link>
            <router-link to="/tilt-photography-metadata" class="dropdown-item">倾斜摄影</router-link>
          </div>
        </div>

        <!-- 工作流下拉菜单 -->
        <div class="nav-dropdown" @mouseenter="showDropdown('workflow')" @mouseleave="hideDropdown('workflow')">
          <div class="nav-link dropdown-toggle" :class="{ active: isWorkflowActive }">
            工作流
            <span class="dropdown-icon">▾</span>
          </div>
          <div class="dropdown-menu" v-show="activeDropdown === 'workflow'">
            <router-link to="/workflow-designer" class="dropdown-item">工作流设计</router-link>
            <router-link to="/workflow-instances" class="dropdown-item">工作流实例</router-link>
          </div>
        </div>

        <router-link to="/monitoring" class="nav-link">系统监控</router-link>
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
  font-size: 0.9rem;
  padding: 0.6rem 1.2rem;
  border-radius: var(--border-radius);
  transition: all var(--transition-base);
  white-space: nowrap;
  overflow: hidden;
  display: block;
  cursor: pointer;
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
  min-width: 160px;
  
  background: white;
  border-radius: var(--border-radius);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1), 0 0 0 1px rgba(0, 0, 0, 0.05);
  overflow: hidden;
  animation: dropdownSlide 0.2s cubic-bezier(0.4, 0, 0.2, 1);
  z-index: 1000;
}

@keyframes dropdownSlide {
  from {
    opacity: 0;
    transform: translateY(-10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.dropdown-item {
  display: block;
  padding: 0.75rem 1.2rem;
  color: var(--gray-700);
  text-decoration: none;
  font-size: 0.9rem;
  font-weight: 500;
  transition: all var(--transition-base);
  border-left: 3px solid transparent;
}

.dropdown-item:hover {
  background: linear-gradient(90deg, var(--primary-lighter) 0%, transparent 100%);
  color: var(--primary-color);
  border-left-color: var(--primary-color);
  padding-left: 1.4rem;
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
    padding: 0.5rem 0.8rem;
    font-size: 0.85rem;
  }

  .dropdown-item {
    padding: 0.6rem 1rem;
    font-size: 0.85rem;
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
    width: 100%;
    order: 3;
    
    justify-content: flex-start;
    overflow-x: auto;
    padding-bottom: 0.25rem;
  }

  .nav::-webkit-scrollbar {
    height: 4px;
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

  /* 移动端将下拉菜单改为点击展开 */
  .dropdown-menu {
    position: static;
    box-shadow: none;
    margin-top: 0.25rem;
    border-radius: 0;
    border-left: 3px solid var(--primary-color);
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
