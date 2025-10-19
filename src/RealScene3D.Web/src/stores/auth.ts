/**
 * 用户认证状态管理
 * 使用Vue 3 Composition API实现全局状态管理
 */

import { ref, computed } from 'vue'
import { authStorage } from '@/utils/storage'
import { authService } from '@/services/api'

/**
 * 用户信息接口
 */
export interface UserInfo {
  id: string
  username: string
  email: string
  role?: string
  avatar?: string
}

/**
 * 登录响应接口
 */
export interface LoginResponse {
  token: string
  refreshToken?: string
  user: UserInfo
}

// 响应式状态
const token = ref<string | null>(authStorage.getToken())
const userInfo = ref<UserInfo | null>(authStorage.getUserInfo<UserInfo>())
const loading = ref(false)
const error = ref<string | null>(null)

/**
 * 认证状态管理
 */
export const useAuthStore = () => {
  // 计算属性
  const isAuthenticated = computed(() => !!token.value)
  const currentUser = computed(() => userInfo.value)

  /**
   * 登录
   */
  const login = async (email: string, password: string, rememberMe = false): Promise<boolean> => {
    loading.value = true
    error.value = null

    try {
      const response: LoginResponse = await authService.login(email, password)

      // 保存令牌和用户信息
      token.value = response.token
      userInfo.value = response.user

      authStorage.setToken(response.token)
      authStorage.setUserInfo(response.user)

      if (response.refreshToken) {
        authStorage.setRefreshToken(response.refreshToken)
      }

      return true
    } catch (err: any) {
      error.value = err.response?.data?.message || '登录失败，请检查用户名和密码'
      return false
    } finally {
      loading.value = false
    }
  }

  /**
   * 注册
   */
  const register = async (username: string, email: string, password: string): Promise<boolean> => {
    loading.value = true
    error.value = null

    try {
      const response: LoginResponse = await authService.register(username, email, password)

      // 注册成功后自动登录，保存令牌和用户信息
      token.value = response.token
      userInfo.value = response.user

      authStorage.setToken(response.token)
      authStorage.setUserInfo(response.user)

      if (response.refreshToken) {
        authStorage.setRefreshToken(response.refreshToken)
      }

      return true
    } catch (err: any) {
      error.value = err.response?.data?.message || '注册失败，请稍后重试'
      return false
    } finally {
      loading.value = false
    }
  }

  /**
   * 登出
   */
  const logout = () => {
    token.value = null
    userInfo.value = null
    authStorage.clearAuth()
  }

  /**
   * 刷新令牌
   */
  const refreshToken = async (): Promise<boolean> => {
    const refreshTokenValue = authStorage.getRefreshToken()
    if (!refreshTokenValue) return false

    try {
      // TODO: 调用刷新令牌API
      // const response = await userService.refreshToken(refreshTokenValue)
      // token.value = response.token
      // authStorage.setToken(response.token)
      return true
    } catch (err) {
      logout()
      return false
    }
  }

  /**
   * 更新用户信息
   */
  const updateUserInfo = (newUserInfo: Partial<UserInfo>) => {
    if (userInfo.value) {
      userInfo.value = { ...userInfo.value, ...newUserInfo }
      authStorage.setUserInfo(userInfo.value)
    }
  }

  /**
   * 从存储恢复认证状态
   */
  const restoreAuth = () => {
    token.value = authStorage.getToken()
    userInfo.value = authStorage.getUserInfo<UserInfo>()
  }

  /**
   * 清除错误信息
   */
  const clearError = () => {
    error.value = null
  }

  return {
    // 状态
    token,
    userInfo,
    loading,
    error,
    // 计算属性
    isAuthenticated,
    currentUser,
    // 方法
    login,
    register,
    logout,
    refreshToken,
    updateUserInfo,
    restoreAuth,
    clearError
  }
}

// 导出默认实例（单例模式）
const authStore = useAuthStore()
export default authStore
