/**
 * 文件处理相关 静态 Util 方法
 *
 * `saveJSON` / `readJSON` 无外部依赖,直接实现。
 * `saveGeoJSON2Kml` (kml-geojson) / `downloadExcelTemplate` / `readExcel` (xlsx) 当前未安装依赖,
 * 保留导出但抛错以兼容 widget import,实际调用前需先 `npm install kml-geojson xlsx`。
 */
import { createApp, type Component } from "vue"

/**
 * 将 Vue3 组件实例化为 Popup 的 DOM 容器
 *
 * @param comp Vue 组件
 * @param para 组件 props
 * @returns Popup 根 DOM 元素 (用于 mars3d graphic.bindPopup 回调)
 */
export function initVue3Popup(comp: Component, para?: Record<string, unknown>): HTMLElement {
  const vNodeDom = document.createElement("div")
  document.body.appendChild(vNodeDom)

  const vNode = createApp(comp, { ...(para || {}) })
  vNode.mount(vNodeDom)
  // @ts-expect-error createApp 实例 _container 为内部属性,mars3d 调用方期望此返回
  return vNode._container as HTMLElement
}

export function saveJSON(data: any) {
  if (data == null || !data) {
    alert("当前未标记任何数据！")
    return
  }
  const strData = JSON.stringify(data)
  const aLink = document.createElement("a")
  aLink.download = "我的标记点.json"
  const blob = new Blob([strData])
  aLink.href = URL.createObjectURL(blob)
  document.body.appendChild(aLink)
  aLink.click()
  document.body.removeChild(aLink)
}

export function readJSON<T>(file: any): Promise<T> {
  return new Promise((resolve) => {
    const fileName = file.name
    const fileType = fileName.substring(fileName.lastIndexOf(".") + 1).toLowerCase()
    if (fileType !== "json") {
      alert("文件类型不合法,请选择json格式标注文件！")
      return
    }
    if (window.FileReader) {
      const reader = new FileReader()
      reader.readAsText(file, "UTF-8")
      reader.onloadend = function () {
        resolve(JSON.parse(this.result as string))
      }
    }
  })
}

export function saveGeoJSON2Kml(_geojson: object, _options: any): any {
  throw new Error("saveGeoJSON2Kml requires `kml-geojson` dependency — run `npm i kml-geojson` and restore impl from cityplan/src/utils/file-util.ts")
}

export function downloadExcelTemplate(_outputFileName?: string): void {
  throw new Error("downloadExcelTemplate requires `xlsx` dependency — run `npm i xlsx` and restore impl from cityplan/src/utils/file-util.ts")
}

export function readExcel(_file: File): Promise<number[][]> {
  return Promise.reject(new Error("readExcel requires `xlsx` dependency — run `npm i xlsx` and restore impl from cityplan/src/utils/file-util.ts"))
}
