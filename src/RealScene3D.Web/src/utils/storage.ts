/**
 * 本地存储工具类
 * 提供统一的本地存储访问接口
 * 支持localStorage和sessionStorage
 */

const STORAGE_PREFIX = 'realscene3d_'

/**
 * 存储键名常量
 */
export const StorageKeys = {
  TOKEN: `${STORAGE_PREFIX}token`,
  USER_INFO: `${STORAGE_PREFIX}user_info`,
  REFRESH_TOKEN: `${STORAGE_PREFIX}refresh_token`,
  REMEMBER_ME: `${STORAGE_PREFIX}remember_me`
}

/**
 * 本地存储服务类
 */
class StorageService {
  /**
   * 设置存储项
   * @param key 键名
   * @param value 值（自动序列化对象）
   * @param useSessionStorage 是否使用sessionStorage（默认使用localStorage）
   */
  set(key: string, value: any, useSessionStorage = false): void {
    try {
      const storage = useSessionStorage ? sessionStorage : localStorage
      const serializedValue = typeof value === 'string' ? value : JSON.stringify(value)
      storage.setItem(key, serializedValue)
    } catch (error) {
      console.error('Storage set error:', error)
    }
  }

  /**
   * 获取存储项
   * @param key 键名
   * @param useSessionStorage 是否使用sessionStorage
   * @returns 存储的值（自动反序列化对象）
   */
  get<T = any>(key: string, useSessionStorage = false): T | null {
    try {
      const storage = useSessionStorage ? sessionStorage : localStorage
      const value = storage.getItem(key)
      if (value === null) return null

      // 尝试解析JSON
      try {
        return JSON.parse(value) as T
      } catch {
        return value as unknown as T
      }
    } catch (error) {
      console.error('Storage get error:', error)
      return null
    }
  }

  /**
   * 删除存储项
   * @param key 键名
   * @param useSessionStorage 是否使用sessionStorage
   */
  remove(key: string, useSessionStorage = false): void {
    try {
      const storage = useSessionStorage ? sessionStorage : localStorage
      storage.removeItem(key)
    } catch (error) {
      console.error('Storage remove error:', error)
    }
  }

  /**
   * 清空所有存储项
   * @param useSessionStorage 是否使用sessionStorage
   */
  clear(useSessionStorage = false): void {
    try {
      const storage = useSessionStorage ? sessionStorage : localStorage
      storage.clear()
    } catch (error) {
      console.error('Storage clear error:', error)
    }
  }

  /**
   * 检查键是否存在
   * @param key 键名
   * @param useSessionStorage 是否使用sessionStorage
   */
  has(key: string, useSessionStorage = false): boolean {
    try {
      const storage = useSessionStorage ? sessionStorage : localStorage
      return storage.getItem(key) !== null
    } catch (error) {
      console.error('Storage has error:', error)
      return false
    }
  }
}

// 导出单例
export const storage = new StorageService()

/**
 * 认证相关存储操作
 */
export const authStorage = {
  /**
   * 保存认证令牌
   */
  setToken(token: string): void {
    storage.set(StorageKeys.TOKEN, token)
  },

  /**
   * 获取认证令牌
   */
  getToken(): string | null {
    return storage.get<string>(StorageKeys.TOKEN)
  },

  /**
   * 删除认证令牌
   */
  removeToken(): void {
    storage.remove(StorageKeys.TOKEN)
  },

  /**
   * 保存刷新令牌
   */
  setRefreshToken(token: string): void {
    storage.set(StorageKeys.REFRESH_TOKEN, token)
  },

  /**
   * 获取刷新令牌
   */
  getRefreshToken(): string | null {
    return storage.get<string>(StorageKeys.REFRESH_TOKEN)
  },

  /**
   * 删除刷新令牌
   */
  removeRefreshToken(): void {
    storage.remove(StorageKeys.REFRESH_TOKEN)
  },

  /**
   * 保存用户信息
   */
  setUserInfo(userInfo: any): void {
    storage.set(StorageKeys.USER_INFO, userInfo)
  },

  /**
   * 获取用户信息
   */
  getUserInfo<T = any>(): T | null {
    return storage.get<T>(StorageKeys.USER_INFO)
  },

  /**
   * 删除用户信息
   */
  removeUserInfo(): void {
    storage.remove(StorageKeys.USER_INFO)
  },

  /**
   * 清空所有认证信息
   */
  clearAuth(): void {
    this.removeToken()
    this.removeRefreshToken()
    this.removeUserInfo()
  },

  /**
   * 检查是否已登录
   */
  isAuthenticated(): boolean {
    return !!this.getToken()
  }
}

export default storage
