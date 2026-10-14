/**
 * 项目内通用 静态Util方法
 *
 * @copyright 火星科技 mars3d.cn
 * @author 木遥 2022-01-01
 */
import axios from "axios"
import * as mars3d from "mars3d"

/**
 * 判断是否 "经度,纬度" 字符串值
 *
 * @export
 * @param {string} text 传入的字符串
 * @return {boolean} 是否 经度,纬度
 */
export function isLonLat(text: string): boolean {
  if (!text || typeof text !== "string") {
    return false
  }
  const parts = text.split(",")
  if (parts.length !== 2) {
    return false
  }
  const lon = parseFloat(parts[0])
  const lat = parseFloat(parts[1])
  return (
    !isNaN(lon) &&
    !isNaN(lat) &&
    lon >= -180 &&
    lon <= 180 &&
    lat >= -90 &&
    lat <= 90
  )
}

/**
 * 设置自动高度值
 * @param {function} callback 窗口大小变化时的回调,返回当前计算完成的高度
 * @param {number} [lose=0] 窗口高度基础上减少的值
 * @param {string} [container="sanbox-warpper"] 窗口id
 * @return {void}
 */
export function setAutoHeight(callback: (v: number) => void, lose = 0, container = "sanbox-warpper"): () => void {
  const wrapper = document.getElementById(container) || document.body
  if (!wrapper) {
    console.warn(`[setAutoHeight] 未找到容器: #${container} 或 document.body`)
    return () => {}
  }

  let wrapperHeight = wrapper.clientHeight || 0
  const result = wrapperHeight - lose
  callback(result)

  const resize = () => {
    wrapperHeight = wrapper.clientHeight || 0
    const resizeHeight = wrapperHeight - lose
    callback(resizeHeight)
  }

  window.addEventListener("resize", resize)
  resize()

  return () => {
    window.removeEventListener("resize", resize)
  }
}

/**
 *  获取URL参数
 *
 * @export
 * @param {string} parameter url值
 * @param {string} [defaultVal] 默认值
 * @return {string | null}  参数值
 */
export function getQueryString(parameter: string, defaultVal?: string): string | null {
  try {
    return new URL(window.location.href).searchParams.get(parameter) ?? defaultVal ?? null
  } catch {
    return defaultVal ?? null
  }
}

/**
 * 将指定的异步方法转为Promise
 *
 * @param {*} context
 * @param {string} apiName
 * @param {string} [success="success"]
 * @param {string} [error="error"]
 * @return {*} Promise
 */
export function apiToSync(context: any, apiName: string, success = "success", error = "error") {
  return apiArrayToSync(context, [apiName], success, error)[0]
}

/**
 * 将指定的多个异步方法转为Promise
 *
 * @param {*} context
 * @param {string[]} apiNames
 * @param {string} [success="success"]
 * @param {string} [error="error"]
 * @return {*} Promise[]
 */
export function apiArrayToSync(context: any, apiNames: string[], success = "success", error = "error") {
  return apiNames.map((name) => {
    const apiFunc = context[name]
    if (typeof apiFunc !== "function") {
      throw new TypeError(`[apiArrayToSync] context 上不存在方法: ${name}`)
    }

    return (options: any) =>
      new Promise((resolve, reject) => {
        options[success] = function (result: any) {
          resolve(result)
        }
        options[error] = function (error) {
          reject(error)
        }
        apiFunc.call(context, options)
      })
  })
}

/**
 * 地图打印，连接打印机，设置参数
 *
 * @param {*} base64 map.expImage方法的回调函数参数
 */
export function printImage(base64: string) {
  const iframe: HTMLIFrameElement = mars3d.DomUtil.create("iframe", "", document.body)
  iframe.setAttribute("style", "position:absolute;width:0px;height:0px;left:-500px;top:-500px;")

  const win = iframe.contentWindow
  if (!win) {
    console.error("[printImage] 无法访问 iframe contentWindow")
    return
  }
  const doc = win.document
  // 对 base64 进行 HTML 属性转义，防止 XSS
  const safeBase64 = String(base64).replace(/[&<>"']/g, (m) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[m]!))
  doc.write(`<div><img src="${safeBase64}" style="margin:0" /></div>`)
  doc.close()

  iframe.focus()
  win.focus()
  setTimeout(() => {
    win.print()
    if (iframe.parentNode) {
      iframe.parentNode.removeChild(iframe)
    }
  }, 500)
}

/**
 * 请求服务返回JSON结果
 *
 * @param {object} options 请求参数
 * @param {string} options.url 服务URL地址
 * @return {Promise<object>} 返回Promise异步处理结果，对象为JSON数据
 */
export async function fetchJson(options: { url: string; [key: string]: any }) {
  const result: any = await axios.get(options.url, options)
  return result.data
}

/**
 * 读取 public/config 下的 json 配置文件
 * 与 cityplan 的 readfileConfig 行为兼容：返回 { data: { result: string } }
 *
 * @param {string} name 配置文件名（如 "config.json"）
 * @return {Promise<{data: {result: string}}>}
 */
export async function readfileConfig(name: string): Promise<{ data: { result: string } }> {
  // 清理路径分隔符，防止路径遍历
  const safeName = name.replace(/[\\/]/g, "")
  const base = (import.meta as any).env?.BASE_URL || "/"
  const url = `${base}config/${safeName}`.replace(/\/+/g, "/")
  const result = await axios.get(url, { responseType: "text", transformResponse: [(d) => d] })
  return { data: { result: typeof result.data === "string" ? result.data : JSON.stringify(result.data) } }
}


// @ts-ignore 需要开启时请F12控制台执行：window.devProject()
window.devProject = function (open = true) {
  localStorage.setItem("project-debugger", open ? "1" : "0")
}

// 是否打印开发日志
const hasWriteLog = localStorage.getItem("project-debugger") === "1"
mars3d.Log.hasInfo(hasWriteLog)

// 打印普通日志信息,方便开发调试
export function logInfo(...sources: any[]) {
  if (!hasWriteLog) {
    return
  }

  // eslint-disable-next-line no-console
  console.log("%c %s", "padding: 2px 1px; border-radius: 3px 0 0 3px; color: #fff; background: rgba(17, 106, 48, 0.85);", ...sources)
}

// 打印警告日志信息,方便开发调试
export function logWarn(...sources: any[]) {
  if (!hasWriteLog) {
    return
  }

  // eslint-disable-next-line no-console
  console.log(
    "%c %s",
    "padding: 2px 1px; border-radius: 0 3px 3px 0; color: #fff; background: rgba(181, 27, 29, 0.8); font-weight: bold;",
    ...sources
  )
}

export const aloneTypeStyle = {
  fixedRoute: [["clockLoop"], "label", "billboard", "point", "model", "circle", "coneTrack", "path", "polyline", "wall"],
  route: [["clockLoop"], "label", "billboard", "point", "model", "circle", "coneTrack", "path", "polyline", "wall"],
  satellite: [["tle1", "tle2"], "model", "label", "billboard", "point", "path"]
}

