/**
 * 项目管理后端接口 — stub。
 *
 * 复用 cityplan/src/api/xmgl.ts 函数签名,本项目后端未提供对应路由,
 * 调用时抛错。接入后端时按 cityplan 实现填充。
 */
const notImpl = (fnName: string) => async (_params?: any): Promise<never> => {
  throw new Error(`[api/xmgl] ${fnName} not implemented — wire up backend before use`)
}

export const pageXmgl = notImpl("pageXmgl")
export const deleteXmgl = notImpl("deleteXmgl")
export const detailXmgl = notImpl("detailXmgl")
export const addXmgl = notImpl("addXmgl")
export const editXmgl = notImpl("editXmgl")
export const getXmgl = notImpl("getXmgl")
export const getXmYearSelectTree = notImpl("getXmYearSelectTree")
export const getXmSelectWithYear = notImpl("getXmSelectWithYear")
