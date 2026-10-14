import axios, { AxiosInstance } from "axios"
import { message } from "ant-design-vue"

/**
 * 通用 axios 请求封装。
 *
 * 设计对齐 cityplan/src/utils/request.ts,差异:
 * - 移除 cityplan 的 VITE_LOGIN_URL 重定向 (本项目 admin 壳自有认证)
 * - 401 时仅清 token + console.error,跳转留给上层路由守卫
 * - baseURL 优先 VITE_API_BASE_URL,缺省 '/api'
 */

export const service = axios.create({
  baseURL: (import.meta.env.VITE_API_BASE_URL as string) || (import.meta.env.VITE_API_URL as string) || "/api",
  timeout: 50000,
  headers: { "Content-Type": "application/json" }
})

export const accessTokenKey = "access-token"
export const refreshAccessTokenKey = `x-${accessTokenKey}`

export const clearAccessTokens = () => {
  window.localStorage.removeItem(accessTokenKey)
  window.localStorage.removeItem(refreshAccessTokenKey)
}

export const axiosInstance: AxiosInstance = axios

service.interceptors.request.use(
  (config) => {
    const accessToken = window.localStorage.getItem("token") || window.localStorage.getItem("bdctoken")
    if (accessToken && config.headers) {
      config.headers.Authorization = `Bearer ${accessToken}`
    }
    return config
  },
  (error) => Promise.reject(error)
)

service.interceptors.response.use(
  (res) => {
    const serve = res.data
    if (serve == null || serve.code === undefined) {
      return Promise.resolve(res)
    }

    if (serve.code !== 200) {
      let msg: string
      if (serve.message && typeof serve.message === "object") {
        msg = JSON.stringify(serve.message)
      } else {
        msg = serve.message || "请求失败"
      }

      if (serve.code === 500) {
        message.error("服务器异常，请稍后重试或联系管理员")
      } else {
        message.error(msg)
      }

      if (serve.code === 401) {
        clearAccessTokens()
        console.error("[request] 401 unauthorized — cleared local tokens")
      }

      throw new Error(msg)
    }

    return res
  },
  (error) => {
    if (error.response) {
      const { status } = error.response

      switch (status) {
        case 401:
          message.error("无权限访问，请重新登录")
          clearAccessTokens()
          return Promise.reject(error)
        case 403:
          message.error("无权限访问")
          return Promise.reject(error)
        case 500:
          message.error("服务器异常，请稍后重试或联系管理员")
          return Promise.reject(error)
        default:
          break
      }
    }

    if (error.message?.includes("timeout")) {
      message.error("网络超时")
    } else if (error.message === "Network Error") {
      message.error("网络连接错误")
    } else {
      const statusText = error.response?.statusText
      message.error(statusText || "接口路径找不到")
    }

    return Promise.reject(error)
  }
)

/**
 * 参数序列化
 */
export function tansParams(params: Record<string, any>): string {
  let result = ""
  for (const propName of Object.keys(params)) {
    const value = params[propName]
    const part = encodeURIComponent(propName) + "="
    if (value != null && value !== "" && typeof value !== "undefined") {
      if (typeof value === "object" && !Array.isArray(value)) {
        for (const key of Object.keys(value)) {
          if (value[key] != null && value[key] !== "" && typeof value[key] !== "undefined") {
            const subKey = propName + "[" + key + "]"
            const subPart = encodeURIComponent(subKey) + "="
            result += subPart + encodeURIComponent(value[key]) + "&"
          }
        }
      } else {
        result += part + encodeURIComponent(value) + "&"
      }
    }
  }
  return result
}

/**
 * 解密 JWT payload
 */
export function decryptJWT(token: string): any {
  token = token.replace(/_/g, "/").replace(/-/g, "+")
  const base64 = token.split(".")[1]
  const jsonUrlSafe = window.atob(base64).split("").map(c =>
    "%" + ("00" + c.charCodeAt(0).toString(16)).slice(-2)
  ).join("")
  return JSON.parse(decodeURIComponent(jsonUrlSafe))
}

export function getJWTDate(timestamp: number): Date {
  return new Date(timestamp * 1000)
}

export default service
