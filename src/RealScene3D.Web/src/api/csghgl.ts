/**
 * 城市规划 / 高级分析后端接口 — stub。
 *
 * 复用 cityplan/src/api/csghgl.ts 函数签名,本项目后端未提供对应路由,
 * 调用时抛错。接入后端时按 cityplan 实现填充。
 */
const notImpl = (fnName: string) => async (_params?: any): Promise<never> => {
  throw new Error(`[api/csghgl] ${fnName} not implemented — wire up backend before use`)
}

export const savekshData = notImpl("savekshData")
export const getProjectListApi = notImpl("getProjectListApi")
export const getCgListApi = notImpl("getCgListApi")
export const getInterestPointApi = notImpl("getInterestPointApi")
export const getAllPointApi = notImpl("getAllPointApi")
export const getYtfxDataApi = notImpl("getYtfxDataApi")
export const getPublicAllListApi = notImpl("getPublicAllListApi")
export const getSheSListApiByArea = notImpl("getSheSListApiByArea")
export const getAroundXZfx = notImpl("getAroundXZfx")
export const getDjzjfx = notImpl("getDjzjfx")
export const getTemplateList = notImpl("getTemplateList")
export const pageGl = notImpl("pageGl")
export const deleteGl = notImpl("deleteGl")
export const detailGl = notImpl("detailGl")
