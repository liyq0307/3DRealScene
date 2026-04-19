/**
 * 倾斜摄影切片生成相关类型定义
 */

/**
 * 数据类型枚举
 */
export enum DataType {
  General = 'General',      // 通用3D模型
  Oblique = 'Oblique'       // 倾斜摄影
}

/**
 * 倾斜摄影表单数据
 */
export interface ObliqueSliceFormData {
  // 通用字段
  name: string
  description: string
  
  // 输入文件配置
  dataPath: string           // DATA文件夹路径
  spatialReference: string   // 空间参考坐标系
  zeroPoint: string          // 零点坐标
  
  // 处理参数配置
  highQualityReconstruction: boolean  // 高质量重建
  forceDoubleSided: boolean           // 强制双面
  noLighting: boolean                 // 无光照
  textureCompression: boolean         // 纹理压缩
  vertexCompression: boolean          // 顶点压缩
  
  // 存储类型配置
  store3DTiles11: boolean    // 存储3DTiles1.1
  storageType: 'hash' | 'hierarchy'  // 存储类型
  outputPath: string         // 输出路径
}

/**
 * 通用表单数据（现有）
 */
export interface GeneralSliceFormData {
  name: string
  description: string
  modelPath: string
  outputPath: string
  outputFormat: string
  textureStrategy: number
  lodLevels: number
  divisions: number
  enableCompression: boolean
  enableIncrementalUpdate: boolean
  enableMeshDecimation: boolean
  generateTileset: boolean
}

/**
 * 统一的表单数据类型
 */
export type SliceFormData = ObliqueSliceFormData | GeneralSliceFormData

/**
 * 对话框状态管理
 */
export interface DialogState {
  // 当前数据类型
  dataType: DataType
  
  // 表单数据
  formData: SliceFormData
  
  // UI状态
  isSubmitting: boolean
  validationErrors: Map<string, string>
  
  // 状态保持
  preservedFields: {
    name: string
    description: string
  }
}

/**
 * 倾斜摄影切片请求参数
 */
export interface ObliqueSlicingRequest {
  name: string
  sourceModelPath: string
  modelType: string
  outputPath: string
  userId?: string
  slicingConfig: {
    spatialReference: string
    zeroPoint: string
    highQualityReconstruction: boolean
    forceDoubleSided: boolean
    noLighting: boolean
    textureCompression: boolean
    vertexCompression: boolean
    store3DTiles11: boolean
    storageType: string
    outputFormat: string
    coordinateSystem: string
  }
}
