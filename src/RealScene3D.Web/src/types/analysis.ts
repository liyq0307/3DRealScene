// ==================== 分析类型枚举 ====================

/** 分析工具类型 */
export type AnalysisToolType =
  | 'performance'
  | 'visibility'
  | 'viewshed'
  | 'profile'
  | 'skyline'
  | 'distance'
  | 'distance-surface'
  | 'area'
  | 'area-surface'
  | 'volume'
  | 'height'
  | 'height-triangle'
  | 'bearing'
  | 'coordinate'
  | 'coordinate-measure'
  | 'plot-ratio'
  | 'building-layout'
  | 'building-spacing'
  | 'layer-comparison'
  | 'sun'
  | 'slope'
  | 'flood'
  | 'contour'
  | 'flatten'
  | 'map-marking'
  | 'viewpoint'
  | 'site-selection'
  | 'tower-foundation'
  | 'pipeline'
  | 'constraint'
  | 'business-format'

/** 分析工具类别 */
export type AnalysisCategory = 'basic' | 'measurement' | 'spatial' | 'environment' | 'planning' | 'engineering' | 'comparison' | 'monitoring'

// ==================== 分析模式枚举 ====================

/** 通视分析模式 */
export type VisibilityMode = 'linear' | 'circular'

/** 天际线显示模式 */
export type SkylineDisplayMode = '2d' | '3d'

/** 高度基准 */
export type HeightBaseline = 'sea' | 'ground' | 'relative'

/** 体积计算方式 */
export type VolumeMethod = 'above' | 'below' | 'both'

/** 图上标记类型 */
export type MarkingType = 'point' | 'polyline' | 'polygon' | 'circle' | 'text'

/** 绘制区域类型 */
export type DrawAreaType = 'rectangle' | 'circle' | 'polygon'

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
  cameraView?: CameraView
  graphicData?: any
}

/** 相机视角 */
export interface CameraView {
  lng: number
  lat: number
  alt: number
  heading: number
  pitch: number
  roll: number
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

/** 可视域分析结果数据 */
export interface ViewshedResultData {
  observerPosition: { longitude: number; latitude: number; height: number }
  direction: number
  pitch: number
  horizontalFov: number
  verticalFov: number
  distance: number
  visibleArea?: number
  hiddenArea?: number
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
  cutVolume: number
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

/** 坐标定位结果数据 */
export interface CoordinateLocationResultData {
  longitude: number
  latitude: number
  height: number
  format: 'decimal' | 'dms'
}

/** 高度测量结果数据 */
export interface HeightMeasurementResultData {
  startHeight: number
  endHeight: number
  heightDiff: number
}

/** 三角测量结果数据 */
export interface TriangleMeasurementResultData {
  sideA: number
  sideB: number
  sideC: number
  angleA: number
  angleB: number
  angleC: number
  area: number
}

/** 方位角测量结果数据 */
export interface BearingMeasurementResultData {
  bearing: number
  distance: number
  fromPosition: { longitude: number; latitude: number }
  toPosition: { longitude: number; latitude: number }
}

/** 等高线分析结果数据 */
export interface ContourLineResultData {
  contourSpacing: number
  lineWidth: number
  lineColor: string
  regionPositions: any
  showLabel: boolean
}

/** 压平结果数据 */
export interface FlattenResultData {
  height: number
  regionPositions: any
  tilesetUrl?: string
}

/** 图上标记结果数据 */
export interface MapMarkingResultData {
  markingType: MarkingType
  positions: any
  style: Record<string, any>
  text?: string
}

/** 观测台结果数据 */
export interface ViewpointResultData {
  name: string
  cameraView: CameraView
  thumbnail?: string
}

/** 在线选址结果数据 */
export interface SiteSelectionResultData {
  position: { longitude: number; latitude: number; height: number }
  modelUrl?: string
  rotation?: { x: number; y: number; z: number }
}

/** 塔基建模结果数据 */
export interface TowerFoundationResultData {
  position: { longitude: number; latitude: number; height: number }
  poleHeight: number
  poleRadius: number
  poleColor: string
  linePositions?: any
}

/** 管线分析结果数据 */
export interface PipelineResultData {
  positions: any
  pipeRadius: number
  smoothness: number
  pipeColor: string
}

/** 限制分析结果数据 */
export interface ConstraintResultData {
  areaType: DrawAreaType
  regionPositions: any
  constraints: Array<{ type: string; value: any; description: string }>
}

/** 业态分析结果数据 */
export interface BusinessFormatResultData {
  areaType: DrawAreaType
  regionPositions: any
  statistics: Array<{ category: string; count: number; area: number; percentage: number }>
}

/** 容积率分析结果数据 */
export interface PlotRatioResultData {
  totalBuildingArea: number
  landArea: number
  plotRatio: number
  positions: any
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
  viewshed?: {
    direction: number
    pitch: number
    horizontalFov: number
    verticalFov: number
    distance: number
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
  contour?: {
    spacing: number
    lineWidth: number
    lineColor: string
    showLabel: boolean
  }
  flatten?: {
    height: number
  }
  pipeline?: {
    radius: number
    smoothness: number
    color: string
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
    name: '基础工具',
    category: 'basic',
    tools: [
      { key: 'coordinate', name: '坐标定位', icon: '📍', category: 'basic', description: '坐标拾取与定位' },
      { key: 'flatten', name: '压平', icon: '⬇️', category: 'basic', description: '3DTiles建筑压平' },
      { key: 'map-marking', name: '图上标记', icon: '✏️', category: 'basic', description: '点/线/面/圆绘制' },
      { key: 'viewpoint', name: '观测台', icon: '🔭', category: 'basic', description: '视角保存与切换' }
    ]
  },
  {
    name: '测量工具',
    category: 'measurement',
    tools: [
      { key: 'distance', name: '空间距离', icon: '📏', category: 'measurement', description: '两点间空间直线距离' },
      { key: 'distance-surface', name: '贴地距离', icon: '📏', category: 'measurement', description: '沿地形表面的距离' },
      { key: 'height', name: '高度差', icon: '↕️', category: 'measurement', description: '两点间高度差' },
      { key: 'area', name: '水平面积', icon: '📐', category: 'measurement', description: '水平面投影面积' },
      { key: 'area-surface', name: '贴地面积', icon: '📐', category: 'measurement', description: '沿地形表面的面积' },
      { key: 'coordinate-measure', name: '坐标测量', icon: '📍', category: 'measurement', description: '单点坐标测量' },
      { key: 'height-triangle', name: '三角测量', icon: '📐', category: 'measurement', description: '三角形参数测量' },
      { key: 'bearing', name: '方位角', icon: '🧭', category: 'measurement', description: '两点间方位角' }
    ]
  },
  {
    name: '空间分析',
    category: 'spatial',
    tools: [
      { key: 'visibility', name: '通视分析', icon: '👁️', category: 'spatial', description: '线通视/圆通视/可视域' },
      { key: 'profile', name: '剖面分析', icon: '📈', category: 'spatial', description: '生成地形剖面图' },
      { key: 'skyline', name: '天际线', icon: '🌆', category: 'spatial', description: '分析城市天际线' },
      { key: 'business-format', name: '业态分析', icon: '🏪', category: 'spatial', description: '区域业态分布分析' },
      { key: 'building-spacing', name: '建筑间距', icon: '📐', category: 'spatial', description: '多建筑间距分析' }
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
      { key: 'building-layout', name: '建筑布局', icon: '🏢', category: 'planning', description: '建筑体块布局' },
      { key: 'site-selection', name: '在线选址', icon: '📌', category: 'planning', description: '模型放置与编辑' },
      { key: 'tower-foundation', name: '塔基建模', icon: '⚡', category: 'planning', description: '高压线杆建模' },
      { key: 'pipeline', name: '管线分析', icon: '🔧', category: 'planning', description: '管线绘制与漫游' },
      { key: 'constraint', name: '限制分析', icon: '🚫', category: 'planning', description: '周边地块限制性分析' }
    ]
  },
  {
    name: '工程分析',
    category: 'engineering',
    tools: [
      { key: 'volume', name: '挖方填方', icon: '📦', category: 'engineering', description: '计算填挖方体积' },
      { key: 'contour', name: '等高线', icon: '🗺️', category: 'engineering', description: '等高线生成与分析' }
    ]
  },
  {
    name: '对比工具',
    category: 'comparison',
    tools: [
      { key: 'layer-comparison', name: '卷帘对比', icon: '🔄', category: 'comparison', description: '对比不同设计方案' }
    ]
  },
  {
    name: '性能监控',
    category: 'monitoring',
    tools: [
      { key: 'performance', name: '性能分析', icon: '📊', category: 'monitoring', description: '监控场景渲染性能' }
    ]
  }
]

/** 工具类型 -> 组件映射 */
export const TOOL_COMPONENT_MAP: Record<AnalysisToolType, string> = {
  'performance': 'PerformanceAnalysis',
  'visibility': 'VisibilityAnalysis',
  'viewshed': 'ViewshedAnalysis',
  'profile': 'ProfileAnalysis',
  'skyline': 'SkylineAnalysis',
  'distance': 'DistanceMeasurement',
  'distance-surface': 'DistanceSurfaceMeasurement',
  'area': 'AreaMeasurement',
  'area-surface': 'AreaSurfaceMeasurement',
  'volume': 'VolumeCalculation',
  'height': 'HeightMeasurement',
  'height-triangle': 'TriangleMeasurement',
  'bearing': 'BearingMeasurement',
  'coordinate': 'CoordinateLocation',
  'coordinate-measure': 'CoordinateMeasure',
  'plot-ratio': 'PlotRatioAnalysis',
  'building-layout': 'BuildingLayoutAnalysis',
  'building-spacing': 'BuildingSpacingAnalysis',
  'layer-comparison': 'LayerComparison',
  'sun': 'SunAnalysis',
  'slope': 'SlopeAnalysis',
  'flood': 'FloodAnalysis',
  'contour': 'ContourLineAnalysis',
  'flatten': 'FlattenTerrain',
  'map-marking': 'MapMarking',
  'viewpoint': 'ViewpointManager',
  'site-selection': 'SiteSelection',
  'tower-foundation': 'TowerFoundationModeling',
  'pipeline': 'PipelineAnalysis',
  'constraint': 'ConstraintAnalysis',
  'business-format': 'BusinessFormatAnalysis'
}
