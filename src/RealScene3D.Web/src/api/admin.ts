import request from "@mars/utils/request"

/**
 * admin / 通用接口
 *
 * 与 cityplan/src/api/admin.ts 对齐,差异点:
 * - readfileConfig 改为读 public/config/<filename>,不走后端
 *   (本项目 mars3d 场景配置存于 public/config/, vite 启动即可访问)
 * - 其他接口暂保持 cityplan 同名,后端联调阶段统一调整
 */

// 根据字典类型编码获取字典值集合
export const sysDictDataList = (params?: any) =>
  request({
    url: `/api/sysDictData/dataList/${params}`,
    method: "get"
  })

// 图层树
export const layertreeList = (_params?: any) =>
  request({
    url: "/api/cityplat/fw_layer/mylayertree",
    method: "get"
  })

/**
 * 获取地图配置文件
 *
 * cityplan 走后端 `/api/pc/readfile/<filename>` 接口;
 * 本项目改为前端直读 `public/config/<filename>`,返回结构对齐 cityplan 调用方:
 *   { data: { result: <stringified json> } }
 *
 * 调用方 (如 mars-map.vue) 通常会 JSON.parse(res.data.result)。
 */
export async function readfileConfig(filename: string, _params?: any): Promise<{ data: { result: string } }> {
  const base = (import.meta as any).env?.BASE_URL || "/"
  const url = `${base}config/${filename}`
  const resp = await fetch(url)
  if (!resp.ok) {
    throw new Error(`readfileConfig failed: ${resp.status} ${url}`)
  }
  const text = await resp.text()
  return { data: { result: text } }
}

// 图层树 (业务模块场景)
export const layertreeListyl = (params?: any) =>
  request({
    url: "/api/cityplat/fw_layer/mycjlayertree",
    method: "get",
    params
  })
