<template>
  <div class="user-info" :class="{ 'user-info-collapsed': collapsed }">
    <!-- 已登录状态 -->
    <template v-if="isAuthenticated">
      <router-link to="/profile" class="user-info-link" @click="handleUserClick">
        <!-- 头像 -->
        <div class="user-avatar" :title="collapsed ? currentUser?.username : undefined">
          <img v-if="currentUser?.avatarUrl" :src="currentUser.avatarUrl" alt="头像" />
          <span v-else>👤</span>
        </div>
        
        <!-- 用户名（折叠时隐藏） -->
        <span v-if="!collapsed" class="user-name">
          {{ currentUser?.username || '用户' }}
        </span>
      </router-link>
      
      <!-- 退出按钮 -->
      <button 
        v-if="!collapsed"
        class="btn-logout"
        @click="handleLogout"
      >
        退出
      </button>
    </template>
    
    <!-- 未登录状态 -->
    <template v-else>
      <router-link to="/login" class="btn-login" :class="{ 'btn-login-collapsed': collapsed }">
        <span class="login-icon">🔐</span>
        <span v-if="!collapsed" class="login-text">登录</span>
      </router-link>
    </template>
    
    <!-- Tooltip（折叠时显示） -->
    <div v-if="collapsed && isAuthenticated" class="user-info-tooltip">
      {{ currentUser?.username || '用户' }}
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * UserInfo 组件
 * 用户信息区域组件，显示用户信息和认证操作
 */
import { useRouter } from 'vue-router'
import authStore from '@/stores/auth'

// Props定义
interface Props {
  /** 菜单是否折叠 */
  collapsed: boolean
}

defineProps<Props>()

const router = useRouter()

// 认证状态
const isAuthenticated = authStore.isAuthenticated
const currentUser = authStore.currentUser

/**
 * 处理用户点击
 */
function handleUserClick() {
  // 可以在这里添加额外的点击处理逻辑
}

/**
 * 处理退出登录
 */
async function handleLogout() {
  try {
    authStore.logout()
    await router.push('/login')
  } catch (error) {
    console.error('Logout error:', error)
  }
}
</script>

<style scoped>
.user-info {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 1rem;
  border-top: 1px solid var(--gray-200);
  margin-top: auto;
}

.user-info-collapsed {
  align-items: center;
  padding: 0.75rem 0.5rem;
}

/* 用户信息链接 */
.user-info-link {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  text-decoration: none;
  color: inherit;
  padding: 0.5rem;
  border-radius: var(--border-radius);
  transition: var(--menu-transition);
  width: 100%;
}

.user-info-link:hover {
  background: var(--primary-lighter);
}

/* 头像 */
.user-avatar {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  background: var(--gradient-primary-alt);
  border-radius: var(--border-radius-full);
  box-shadow: 0 2px 8px rgba(99, 102, 241, 0.3);
  overflow: hidden;
  flex-shrink: 0;
  font-size: 1.25rem;
}

.user-avatar img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

/* 用户名 */
.user-name {
  font-size: 0.9rem;
  font-weight: 600;
  color: var(--gray-800);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* 退出按钮 */
.btn-logout {
  width: 100%;
  padding: 0.5rem 1rem;
  background: transparent;
  border: 1px solid var(--gray-300);
  border-radius: var(--border-radius);
  color: var(--gray-700);
  font-size: 0.85rem;
  font-weight: 500;
  cursor: pointer;
  transition: var(--menu-transition);
}

.btn-logout:hover {
  background: var(--danger-light);
  border-color: var(--danger-color);
  color: var(--danger-color);
}

/* 登录按钮 */
.btn-login {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  width: 100%;
  padding: 0.75rem 1rem;
  background: var(--gradient-primary-alt);
  border: none;
  border-radius: var(--border-radius);
  color: white;
  font-size: 0.9rem;
  font-weight: 600;
  text-decoration: none;
  cursor: pointer;
  transition: var(--menu-transition);
  box-shadow: var(--shadow-colored);
}

.btn-login:hover {
  transform: translateY(-2px);
  box-shadow: var(--shadow-xl);
}

.btn-login-collapsed {
  padding: 0.5rem;
  width: auto;
}

.login-icon {
  font-size: 1.25rem;
}

.login-text {
  flex: 1;
}

/* Tooltip */
.user-info-tooltip {
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

.user-info-collapsed:hover .user-info-tooltip {
  opacity: 1;
}

/* 可访问性：焦点样式 */
.user-info-link:focus-visible,
.btn-logout:focus-visible,
.btn-login:focus-visible {
  outline: 2px solid var(--primary-color);
  outline-offset: 2px;
}
</style>
