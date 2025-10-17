/**
 * API缓存管理器
 * 实现简单的内存缓存,支持过期时间设置和自动清理
 *
 * @author liyq
 * @date 2025-10-15
 */

interface CacheItem {
  data: any
  timestamp: number
  ttl: number
}

export class ApiCache {
  private cache = new Map<string, CacheItem>()

  /**
   * 获取缓存数据
   * @param key 缓存键
   * @returns 缓存的数据,如果不存在或已过期则返回null
   */
  get(key: string): any | null {
    const item = this.cache.get(key)
    if (!item) return null

    // 检查是否过期
    if (Date.now() - item.timestamp > item.ttl) {
      this.cache.delete(key)
      return null
    }

    return item.data
  }

  /**
   * 设置缓存数据
   * @param key 缓存键
   * @param data 数据
   * @param ttl 过期时间(毫秒),默认5分钟
   */
  set(key: string, data: any, ttl = 5 * 60 * 1000) {
    this.cache.set(key, {
      data,
      timestamp: Date.now(),
      ttl
    })
  }

  /**
   * 检查缓存是否存在且未过期
   * @param key 缓存键
   */
  has(key: string): boolean {
    return this.get(key) !== null
  }

  /**
   * 清除指定缓存
   * @param key 缓存键
   */
  delete(key: string) {
    this.cache.delete(key)
  }

  /**
   * 清除所有缓存
   */
  clear() {
    this.cache.clear()
  }

  /**
   * 清除过期缓存
   */
  clearExpired() {
    const now = Date.now()
    for (const [key, item] of this.cache.entries()) {
      if (now - item.timestamp > item.ttl) {
        this.cache.delete(key)
      }
    }
  }

  /**
   * 获取缓存大小
   */
  get size(): number {
    return this.cache.size
  }

  /**
   * 生成缓存键
   * @param url API URL
   * @param params 请求参数
   */
  static generateKey(url: string, params?: any): string {
    if (!params) return url
    const sortedParams = Object.keys(params)
      .sort()
      .reduce((acc, key) => {
        acc[key] = params[key]
        return acc
      }, {} as any)
    return `${url}?${JSON.stringify(sortedParams)}`
  }
}

// 导出单例实例
export const apiCache = new ApiCache()

// 每5分钟清理一次过期缓存
setInterval(() => apiCache.clearExpired(), 5 * 60 * 1000)
