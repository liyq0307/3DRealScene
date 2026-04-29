// ==================== 分析类型枚举 ====================

/** 分析工具类型 */
export type AnalysisToolType =
  | 'performance'
  | 'visibility'
  | 'profile'
  | 'skyline'
  | 'distance'
  | 'area'
  | 'volume'
  | 'plot-ratio'
  | 'building-layout'
  | 'layer-comparison'
  | 'sun'
  | 'building-spacing'
  | 'slope'
  | 'flood'
  | 'coordinate'
  | 'height'

/** 分析工具类别 */
export type AnalysisCategory = 'monitoring' | 'spatial' | 'measurement' | 'planning' | 'comparison' | 'environment' | 'engineering'

// ==================== 分析模式枚举 ====================

/** 通视分析模式 */
export type VisibilityMode = 'linear' | 'circular'

/** 天际线显示模式 */
export type SkylineDisplayMode = '2d' | '3d'

/** 高度基准 */
export type HeightBaseline = 'sea' | 'ground' | 'relative'

/** 体积计算方式 */
export type VolumeMethod = 'above' | 'below' | 'both'

// ==================== 分析工具定义 ====================

/** 单个工具定义 */
export interface AnalysisToolDef {
  key: AnalysisToolType
  name: string
  icon: string
  category: AnalysisCategory
  description?: string
}

/** 工具分类 */
export interface ToolCategory {
  name: string
  category: AnalysisCategory
  tools: AnalysisToolDef[]
}

// ==================== 分析结果类型 ====================

/** 基础分析结果 */
export interface AnalysisResultBase<T = any> {
  id: string
  type: AnalysisToolType
  name: string
  data: T
  timestamp: Date
  visible: boolean
  status: 'success' | 'error' | 'cancelled'
  errorMessage?: string
}

/** 通视分析结果数据 */
export interface VisibilityResultData {
  mode: VisibilityMode
  observerHeight: number
  observerPosition?: { longitude: number; latitude: number; height: number }
  visibleCount?: number
  totalCount?: number
  visibleRatio?: number
  result?: any
}

/** 剖面分析结果数据 */
export interface ProfileResultData {
  profileData: Array<{ distance: number; elevation: number }>
  totalLength: number
  maxElevation: number
  minElevation: number
  sampleInterval: number
  heightBaseline: HeightBaseline
}

/** 天际线分析结果数据 */
export interface SkylineResultData {
  points: Array<{ angle: number; height: number }>
  maxHeight: number
  minHeight: number
  variance: number
  lineWidth: number
  lineColor: string
  displayMode: SkylineDisplayMode
}

/** 体积计算结果数据 */
export interface VolumeResultData {
  totalVolume: number
  fillVolume: number
  area: number
  avgHeight: number
  baseHeight: number
  calcMethod: VolumeMethod
}

/** 性能分析结果数据 */
export interface PerformanceResultData {
  avgFps: number
  minFps: number
  maxFps: number
  avgFrameTime: number
  memoryUsage: number
  triangleCount: number
  drawCalls: number
  objectCount: number
  duration: number
  historyLength: number
}

/** 日照分析结果数据 */
export interface SunAnalysisResultData {
  date: string
  startTime: string
  endTime: string
  totalSunHours: number
  sunPath: Array<{ time: string; altitude: number; azimuth: number; shadowLength: number }>
  shadowAreas: number
  coverage: number
}

/** 建筑间距分析结果数据 */
export interface BuildingSpacingResultData {
  pairs: Array<{
    buildingA: string
    buildingB: string
    distance: number
    minRequiredDistance: number
    compliant: boolean
  }>
  totalViolations: number
  totalPairs: number
  complianceRate: number
}

/** 坡度分析结果数据 */
export interface SlopeResultData {
  minSlope: number
  maxSlope: number
  avgSlope: number
  areaByGrade: Array<{ grade: string; range: string; area: number; percentage: number }>
  totalArea: number
}

// ==================== 分析错误类型 ====================

/** 分析错误 */
export interface AnalysisError {
  type: AnalysisToolType
  message: string
  code: string
  timestamp: Date
  recoverable: boolean
  details?: any
}

// ==================== 性能指标类型 ====================

export interface PerformanceMetrics {
  fps: number
  frameTime: number
  memory: number
  triangles: number
  drawCalls: number
  objects: number
}

// ==================== 工具配置类型 ====================

/** 分析工具参数配置 */
export interface AnalysisParams {
  visibility?: {
    observerHeight: number
    analysisRadius: number
    visibleColor: string
    hiddenColor: string
  }
  profile?: {
    sampleInterval: number
    heightBaseline: HeightBaseline
    lineColor: string
  }
  skyline?: {
    lineWidth: number
    lineColor: string
    displayMode: SkylineDisplayMode
  }
  volume?: {
    baseHeight: number
    calcMethod: VolumeMethod
  }
  sun?: {
    date: string
    startTime: string
    endTime: string
    timeStep: number
  }
  ['building-spacing']?: {
    minDistance: number
    checkNorthSouth: boolean
    checkEastWest: boolean
  }
  slope?: {
    sampleInterval: number
    classification: Array<{ min: number; max: number; label: string; color: string }>
  }
}

// ==================== Store 状态类型 ====================

export interface AnalysisState {
  results: AnalysisResultBase[]
  currentToolType: AnalysisToolType | null
  isAnalyzing: boolean
  error: AnalysisError | null
  isLoading: boolean
  performanceMetrics: PerformanceMetrics
  performanceHistory: Array<{ time: number; metrics: PerformanceMetrics }>
  maxHistorySize: number
}

// ==================== 组件事件类型 ====================

export interface AnalysisComponentProps {
  viewerInstance?: any
  sceneObjects?: any[]
}

export interface AnalysisComponentEmits {
  (e: 'close'): void
  (e: 'error', error: AnalysisError): void
  (e: 'complete', result: AnalysisResultBase): void
}

// ==================== 工具分类数据 ====================

export const TOOL_CATEGORIES: ToolCategory[] = [
  {
    name: '性能监控',
    category: 'monitoring',
    tools: [
      { key: 'performance', name: '性能分析', icon: '📊', category: 'monitoring', description: '监控场景渲染性能' }
    ]
  },
  {
    name: '空间分析',
    category: 'spatial',
    tools: [
      { key: 'visibility', name: '通视分析', icon: '👁️', category: 'spatial', description: '分析两点间通视情况' },
      { key: 'profile', name: '剖面分析', icon: '📈', category: 'spatial', description: '生成地形剖面图' },
      { key: 'skyline', name: '天际线', icon: '🌆', category: 'spatial', description: '分析城市天际线' }
    ]
  },
  {
    name: '测量工具',
    category: 'measurement',
    tools: [
      { key: 'distance', name: '距离测量', icon: '📏', category: 'measurement', description: '测量空间距离' },
      { key: 'area', name: '面积测量', icon: '📐', category: 'measurement', description: '测量水平或贴地面积' },
      { key: 'volume', name: '体积计算', icon: '📦', category: 'measurement', description: '计算填挖方体积' },
      { key: 'height', name: '高度测量', icon: '📏', category: 'measurement', description: '测量建筑物高度' },
      { key: 'coordinate', name: '坐标定位', icon: '📍', category: 'measurement', description: '获取经纬度坐标' }
    ]
  },
  {
    name: '环境分析',
    category: 'environment',
    tools: [
      { key: 'sun', name: '日照分析', icon: '☀️', category: 'environment', description: '日照时长与阴影分析' },
      { key: 'slope', name: '坡度分析', icon: '⛰️', category: 'environment', description: '地形坡度分级' },
      { key: 'flood', name: '淹没分析', icon: '🌊', category: 'environment', description: '水位淹没模拟' }
    ]
  },
  {
    name: '规划分析',
    category: 'planning',
    tools: [
      { key: 'plot-ratio', name: '容积率', icon: '🏗️', category: 'planning', description: '计算地块容积率' },
      { key: 'building-layout', name: '建筑布局', icon: '🏢', category: 'planning', description: '优化建筑布局' },
      { key: 'building-spacing', name: '建筑间距', icon: '📐', category: 'planning', description: '检查建筑间距合规' }
    ]
  },
  {
    name: '对比工具',
    category: 'comparison',
    tools: [
      { key: 'layer-comparison', name: '卷帘对比', icon: '🔄', category: 'comparison', description: '对比不同设计方案' }
    ]
  }
]

/** 工具类型 -> 组件映射 */
export const TOOL_COMPONENT_MAP: Record<AnalysisToolType, string> = {
  'performance': 'PerformanceAnalysis',
  'visibility': 'VisibilityAnalysis',
  'profile': 'ProfileAnalysis',
  'skyline': 'SkylineAnalysis',
  'distance': 'DistanceMeasurement',
  'area': 'AreaMeasurement',
  'volume': 'VolumeCalculation',
  'plot-ratio': 'PlotRatioAnalysis',
  'building-layout': 'BuildingLayoutAnalysis',
  'layer-comparison': 'LayerComparison',
  'sun': 'SunAnalysis',
  'building-spacing': 'BuildingSpacingAnalysis',
  'slope': 'SlopeAnalysis',
  'flood': 'FloodAnalysis',
  'coordinate': 'CoordinateAnalysis',
  'height': 'HeightMeasurement'
}
