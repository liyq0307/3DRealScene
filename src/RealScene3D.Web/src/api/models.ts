/**
 * 模型库后端接口 — stub。
 *
 * 复用 cityplan/src/api/models.ts 函数签名,本项目后端未提供对应路由,
 * 调用时抛错。接入后端时按 cityplan 实现填充。
 */
const notImpl = (fnName: string) => async (_params?: any, _extra?: any): Promise<never> => {
  throw new Error(`[api/models] ${fnName} not implemented — wire up backend before use`)
}

export const getModelFolderTree = notImpl("getModelFolderTree")
export const addModelFolder = notImpl("addModelFolder")
export const updateModelFolder = notImpl("updateModelFolder")
export const deleteModelFolder = notImpl("deleteModelFolder")
export const getModelFileTreev2 = notImpl("getModelFileTreev2")
export const uploadModelFile = notImpl("uploadModelFile")
export const fetchBuiltInModels = notImpl("fetchBuiltInModels")
