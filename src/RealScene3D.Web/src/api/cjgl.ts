/**
 * 场景管理后端接口 — stub。
 *
 * 复用 cityplan/src/api/cjgl.ts 函数签名,本项目后端未提供对应路由,
 * 调用时抛错。接入后端时按 cityplan 实现填充。
 */
const notImpl = (fnName: string) => async (_params?: any): Promise<never> => {
  throw new Error(`[api/cjgl] ${fnName} not implemented — wire up backend before use`)
}

export const pageCsgh_cjgl = notImpl("pageCsgh_cjgl")
export const saveAnalysisDataApi = notImpl("saveAnalysisDataApi")
export const updateCsgh_cjgl = notImpl("updateCsgh_cjgl")
export const deleteCsgh_cjgl = notImpl("deleteCsgh_cjgl")
export const detailCsgh_cjgl = notImpl("detailCsgh_cjgl")
