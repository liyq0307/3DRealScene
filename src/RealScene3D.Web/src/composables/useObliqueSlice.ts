/**
 * 倾斜摄影切片生成组合式函数
 */
import { ref } from 'vue'
import { 
  DataType, 
  type ObliqueSliceFormData, 
  type GeneralSliceFormData,
  type SliceFormData,
  type ObliqueSlicingRequest
} from '@/types/obliqueSlice'

/**
 * 根据文件路径识别数据类型
 * @param filePath 文件路径
 * @returns 数据类型
 */
export function detectDataType(filePath: string): DataType {
  if (!filePath || !filePath.trim()) {
    return DataType.General
  }
  
  const lowerPath = filePath.toLowerCase()
  const extension = lowerPath.split('.').pop()
  
  // 1. 优先检查明确的扩展名
  if (extension === 'osgb') {
    return DataType.Oblique
  }
  
  // 2. 检查路径中是否包含倾斜摄影关键字
  // 倾斜摄影通常包含 osgb, tilt, reconstruction, photogrammetry, Production 等关键字
  if (lowerPath.includes('osgb') || 
      lowerPath.includes('tilt') || 
      lowerPath.includes('reconstruction') || 
      lowerPath.includes('photogrammetry') ||
      (lowerPath.includes('production') && !lowerPath.includes('obj'))) {
    return DataType.Oblique
  }
  
  // 其他格式为通用3D模型
  return DataType.General
}

/**
 * 获取默认的倾斜摄影表单数据
 */
export function getDefaultObliqueFormData(): ObliqueSliceFormData {
  return {
    name: '',
    description: '',
    dataPath: '',
    spatialReference: '',
    zeroPoint: '',
    highQualityReconstruction: true,
    forceDoubleSided: false,
    noLighting: true,
    textureCompression: true,
    vertexCompression: true,
    store3DTiles11: true,
    storageType: 'hash',
    outputPath: ''
  }
}

/**
 * 获取默认的通用表单数据
 */
export function getDefaultGeneralFormData(): GeneralSliceFormData {
  return {
    name: '',
    description: '',
    modelPath: '',
    outputPath: '',
    outputFormat: 'b3dm',
    textureStrategy: 2,
    lodLevels: 3,
    divisions: 2,
    enableCompression: true,
    enableIncrementalUpdate: false,
    enableMeshDecimation: true,
    generateTileset: true
  }
}

/**
 * 验证坐标系格式
 * @param value 坐标系字符串
 */
function isValidCoordinateSystem(value: string): boolean {
  if (!value) return true
  // 支持EPSG:xxxx格式或自定义格式
  return /^EPSG:\d+$/.test(value) || value.length > 0
}

/**
 * 验证坐标格式
 * @param value 坐标字符串
 */
function isValidCoordinate(value: string): boolean {
  if (!value) return true
  // 支持x,y,z 或 x y z 格式
  const parts = value.split(/[,\s]+/).filter(p => p)
  if (parts.length !== 3) return false
  return parts.every(p => !isNaN(parseFloat(p)))
}

/**
 * 倾斜摄影表单验证
 * @param data 表单数据
 * @returns 错误信息Map
 */
export function validateObliqueForm(data: ObliqueSliceFormData): Map<string, string> {
  const errors = new Map<string, string>()
  
  // 必填字段验证
  if (!data.name?.trim()) {
    errors.set('name', '请输入任务名称')
  }
  
  if (!data.dataPath?.trim()) {
    errors.set('dataPath', '请选择数据路径')
  }
  
  // ✅ 输出路径可选：为空时自动存储到 MinIO
  // 不再验证输出路径是否为空
  
  // 格式验证
  if (data.spatialReference && !isValidCoordinateSystem(data.spatialReference)) {
    errors.set('spatialReference', '空间参考格式不正确，应为EPSG:xxxx格式')
  }
  
  if (data.zeroPoint && !isValidCoordinate(data.zeroPoint)) {
    errors.set('zeroPoint', '零点坐标格式不正确，应为x,y,z格式')
  }
  
  return errors
}

/**
 * 将前端表单数据映射为后端API请求格式
 * @param formData 表单数据
 * @param userId 用户ID
 */
export function mapObliqueFormDataToRequest(
  formData: ObliqueSliceFormData,
  userId: string,
  sceneObjectId?: string
): ObliqueSlicingRequest {
  const outputPathRaw = (formData.outputPath || '').trim()
  
  // 判断存储位置：
  // 1. 空路径 → MinIO (0)
  // 2. 相对路径 → MinIO (0)
  // 3. 绝对路径 → LocalFileSystem (1)
  const isEmpty = !outputPathRaw
  const isRelative = outputPathRaw && !outputPathRaw.includes(':') && !outputPathRaw.startsWith('/')
  const targetStorage = (isEmpty || isRelative) ? 0 : 1 // 0=MinIO, 1=LocalFileSystem
  const finalOutputPath = outputPathRaw.replace(/\\/g, '/')

  return {
    name: formData.name,
    sourceModelPath: formData.dataPath,
    modelType: 'ObliquePhotography',
    outputPath: finalOutputPath,
    sceneObjectId: sceneObjectId || undefined,
    slicingConfig: {
      spatialReference: formData.spatialReference,
      zeroPoint: formData.zeroPoint,
      highQualityReconstruction: formData.highQualityReconstruction,
      forceDoubleSided: formData.forceDoubleSided,
      noLighting: formData.noLighting,
      textureCompression: formData.textureCompression,
      vertexCompression: formData.vertexCompression,
      store3DTiles11: formData.store3DTiles11,
      storageType: formData.storageType,
      outputFormat: 'b3dm',
      coordinateSystem: formData.spatialReference || 'EPSG:4326',
      storageLocation: targetStorage
    }
  }
}

/**
 * 界面切换处理组合式函数
 */
export function useInterfaceSwitch() {
  const dataType = ref<DataType>(DataType.General)
  const formData = ref<SliceFormData>(getDefaultGeneralFormData())
  const preservedFields = ref({ name: '', description: '' })
  
  /**
   * 监听模型路径变化，触发界面切换
   */
  const handleModelPathChange = (path: string) => {
    const newDataType = detectDataType(path)
    
    // 保存通用字段
    preservedFields.value = {
      name: formData.value.name,
      description: formData.value.description
    }
    
    // 切换数据类型
    if (newDataType !== dataType.value) {
      dataType.value = newDataType
      
      // 根据新类型初始化表单数据
      if (newDataType === DataType.Oblique) {
        formData.value = getDefaultObliqueFormData()
      } else {
        formData.value = getDefaultGeneralFormData()
      }
      
      // 恢复通用字段
      formData.value.name = preservedFields.value.name
      formData.value.description = preservedFields.value.description
    }
  }
  
  return {
    dataType,
    formData,
    preservedFields,
    handleModelPathChange
  }
}
