import * as mars3d from 'mars3d'
import * as Cesium from 'mars3d-cesium'
import type { SightlineItemData, ViewshedPropertyData } from '@/types/analysis'

export class Mars3DAnalysisTools {
  private map: mars3d.Map
  private graphicLayer: mars3d.layer.GraphicLayer
  private lineLayer: mars3d.layer.GraphicLayer
  private sightlineLayer: mars3d.layer.GraphicLayer  // 通视线专用图层
  private measure: mars3d.thing.Measure | null = null
  private sightline: mars3d.thing.Sightline | null = null
  private skyline: mars3d.thing.Skyline | null = null
  private shadows: mars3d.thing.Shadows | null = null
  private contourLine: any = null
  private viewshed: any = null
  private flood: any = null
  private flatObj: any = null

  // 通视线数据管理
  private sightlineData = new Map<string, SightlineItemData>()
  // 当前选中的可视域图形
  private selectedViewshed: any = null
  // 当前是否正在绘制
  private _isDrawing = false

  constructor(map: mars3d.Map) {
    this.map = map
    this.graphicLayer = new mars3d.layer.GraphicLayer({
      isAutoEditing: true
    })
    map.addLayer(this.graphicLayer)
    this.lineLayer = new mars3d.layer.GraphicLayer()
    map.addLayer(this.lineLayer)
    // 通视线专用图层，用于管理观察点/目标点
    this.sightlineLayer = new mars3d.layer.GraphicLayer()
    map.addLayer(this.sightlineLayer)
  }

  get isDrawing() { return this._isDrawing }

  // ==================== 初始化方法 ====================

  private initMeasure() {
    if (!this.measure) {
      this.measure = new mars3d.thing.Measure({
        label: {
          color: '#ffffff',
          font_family: '楷体',
          font_size: 16
        },
        isAutoEditing: false
      })
      this.map.addThing(this.measure)
    }
    return this.measure
  }

  private initSightline() {
    if (!this.sightline) {
      this.sightline = new mars3d.thing.Sightline({
        visibleColor: new Cesium.Color(0, 1, 0, 1),
        hiddenColor: new Cesium.Color(1, 0, 0, 1),
        depthFailColor: new Cesium.Color(1, 0, 0, 1)
      })
      this.map.addThing(this.sightline)
    }
    return this.sightline
  }

  private initSkyline() {
    if (!this.skyline) {
      this.skyline = new mars3d.thing.Skyline({
        enabled: false,
        width: 5
      })
      this.map.addThing(this.skyline)
      this.map.scene.globe.depthTestAgainstTerrain = true
    }
    return this.skyline
  }

  private initShadows() {
    if (!this.shadows) {
      this.shadows = new (mars3d.thing as any).Shadows({
        darkness: 0.4,
        enabled: false,
        multiplier: 1600,
        terrain: false,
        lighting: false
      })
      this.map.addThing(this.shadows!)
    }
    return this.shadows!
  }

  // ==================== 测量功能 ====================

  /** 空间距离测量 */
  async measureDistance(): Promise<{ distance: number; positions: any }> {
    const measure = this.initMeasure()
    return new Promise((resolve) => {
      (measure as any).distance({
        unit: 'meter',
        showAddText: true,
        style: { clampToGround: false },
        label: { text: '距离测量' },
        success: (graphic: any) => {
          resolve({
            distance: graphic.measured?.distance || 0,
            positions: graphic.positions
          })
        }
      })
    })
  }

  /** 贴地距离测量 */
  async measureDistanceSurface(): Promise<{ distance: number; positions: any }> {
    const measure = this.initMeasure()
    return new Promise((resolve) => {
      (measure as any).distanceSurface({
        unit: 'meter',
        exact: false,
        splitNum: 5,
        showAddText: true,
        label: { text: '贴地距离' },
        success: (graphic: any) => {
          resolve({
            distance: graphic.measured?.distance || 0,
            positions: graphic.positions
          })
        }
      })
    })
  }

  /** 水平面积测量 */
  async measureArea(): Promise<{ area: number; perimeter: number; positions: any }> {
    const measure = this.initMeasure()
    return new Promise((resolve) => {
      (measure as any).area({
        unit: 'meter',
        style: { color: '#00ffff', opacity: 0.3 },
        label: { text: '面积测量' },
        success: (graphic: any) => {
          resolve({
            area: graphic.measured?.area || 0,
            perimeter: graphic.measured?.perimeter || 0,
            positions: graphic.positions
          })
        }
      })
    })
  }

  /** 贴地面积测量 */
  async measureAreaSurface(): Promise<{ area: number; perimeter: number; positions: any }> {
    const measure = this.initMeasure()
    return new Promise((resolve) => {
      (measure as any).areaSurface({
        unit: 'meter',
        exact: false,
        splitNum: 10,
        style: { color: '#00ffff', opacity: 0.3 },
        label: { text: '贴地面积' },
        success: (graphic: any) => {
          resolve({
            area: graphic.measured?.area || 0,
            perimeter: graphic.measured?.perimeter || 0,
            positions: graphic.positions
          })
        }
      })
    })
  }

  /** 高度差测量 */
  async measureHeight(): Promise<{ startHeight: number; endHeight: number; heightDiff: number }> {
    const measure = this.initMeasure()
    return new Promise((resolve) => {
      (measure as any).height({
        label: { text: '高度测量' },
        success: (graphic: any) => {
          const measured = graphic.measured || {}
          resolve({
            startHeight: measured.startHeight || 0,
            endHeight: measured.endHeight || 0,
            heightDiff: measured.height || 0
          })
        }
      })
    })
  }

  /** 三角测量 */
  async measureHeightTriangle(): Promise<any> {
    const measure = this.initMeasure()
    return new Promise((resolve) => {
      (measure as any).heightTriangle({
        label: { text: '三角测量' },
        success: (graphic: any) => {
          resolve(graphic.measured || graphic.toJSON?.() || {})
        }
      })
    })
  }

  /** 方位角测量 */
  async measureAngle(): Promise<any> {
    const measure = this.initMeasure()
    return new Promise((resolve) => {
      (measure as any).angle({
        label: { text: '方位角测量' },
        success: (graphic: any) => {
          resolve(graphic.measured || graphic.toJSON?.() || {})
        }
      })
    })
  }

  /** 坐标测量 */
  async measurePoint(): Promise<{ longitude: number; latitude: number; height: number }> {
    const measure = this.initMeasure()
    return new Promise((resolve) => {
      (measure as any).point({
        label: { text: '坐标测量' },
        success: (graphic: any) => {
          const pos = graphic.positionShow || graphic.position
          resolve({
            longitude: pos?.lng || 0,
            latitude: pos?.lat || 0,
            height: pos?.alt || 0
          })
        }
      })
    })
  }

  /** 剖面分析 */
  async measureSection(): Promise<any> {
    const measure = this.initMeasure()
    return new Promise((resolve) => {
      (measure as any).section({
        splitNum: 100,
        exact: false,
        style: { color: '#ff6b6b', width: 3 },
        success: (graphic: any) => {
          resolve(graphic.measured)
        }
      })
    })
  }

  // ==================== 通视分析 ====================

  /** 线通视分析 */
  async sightlineLinear(observerHeight = 1.5, visibleColor = '#00ff00', hiddenColor = '#ff0000'): Promise<any> {
    const sightline = this.initSightline()
    // 更新通视线颜色
    sightline.visibleColor = Cesium.Color.fromCssColorString(visibleColor)
    sightline.hiddenColor = Cesium.Color.fromCssColorString(hiddenColor)

    const sightlineId = 'sightline_' + new Date().getTime()
    this._isDrawing = true
    try {
      const graphic = await this.sightlineLayer.startDraw({
        type: 'polyline',
        maxPointNum: 2,
        style: { color: '#55ff33', width: 5 }
      })
      const positions = graphic.positionsShow
      // 移除绘制的线，只保留通视分析结果
      this.sightlineLayer.getGraphicById(graphic.id)?.remove()

      const center = positions[0]
      const targetPoint = positions[1]

      this.map.scene.globe.depthTestAgainstTerrain = true
      sightline.add(center, targetPoint, { offsetHeight: observerHeight })

      // 创建观察位置点和目标点
      const centerPoint = this.createSightlinePoint(center, true)
      const targetPointGraphic = this.createSightlinePoint(targetPoint, false)

      // 计算距离
      const distance = Cesium.Cartesian3.distance(center, targetPoint)

      // 存储通视线数据
      const data: SightlineItemData = {
        id: sightlineId,
        type: 'line',
        visible: true,
        timestamp: new Date(),
        center: positions[0],
        targetPoint: positions[1],
        offsetHeight: observerHeight,
        centerPointGraphic: centerPoint,
        targetPointGraphic: targetPointGraphic
      }
      this.sightlineData.set(sightlineId, data)

      this.map.scene.globe.depthTestAgainstTerrain = false
      return {
        observer: center,
        target: targetPoint,
        distance,
        sightlineId,
        result: sightline.toJSON()
      }
    } finally {
      this._isDrawing = false
    }
  }

  /** 圆形通视分析 */
  async sightlineCircular(observerHeight = 1.5, sampleCount = 45, visibleColor = '#00ff00', hiddenColor = '#ff0000'): Promise<any> {
    const sightline = this.initSightline()
    sightline.visibleColor = Cesium.Color.fromCssColorString(visibleColor)
    sightline.hiddenColor = Cesium.Color.fromCssColorString(hiddenColor)

    const sightlineId = 'sightline_' + new Date().getTime()
    this._isDrawing = true
    try {
      const graphic = await this.sightlineLayer.startDraw({
        type: 'circle',
        style: {
          color: 'rgba(255, 255, 0, 0.2)',
          opacity: 0.2,
          clampToGround: true
        }
      })

      let center = graphic.positionShow
      center = mars3d.PointUtil.addPositionsHeight(center, observerHeight)
      const targetPoints = graphic.getOutlinePositions(false, sampleCount)
      // 移除绘制的圆
      this.sightlineLayer.getGraphicById(graphic.id)?.remove()

      this.map.scene.globe.depthTestAgainstTerrain = true
      const results: any[] = []
      for (let i = 0; i < targetPoints.length; i++) {
        let targetPoint = targetPoints[i]
        targetPoint = mars3d.PointUtil.getSurfacePosition(this.map.scene, targetPoint)
        sightline.add(center, targetPoint, { offsetHeight: observerHeight })
        results.push(targetPoint)
      }

      // 创建观察位置点
      const centerPoint = this.createSightlinePoint(center, true)

      // 存储通视线数据
      const data: SightlineItemData = {
        id: sightlineId,
        type: 'circle',
        visible: true,
        timestamp: new Date(),
        center,
        targetPoints,
        offsetHeight: observerHeight,
        centerPointGraphic: centerPoint,
        targetPointGraphics: []
      }
      this.sightlineData.set(sightlineId, data)

      this.map.scene.globe.depthTestAgainstTerrain = false
      return { observer: center, targets: results, targetCount: results.length, sightlineId, result: sightline.toJSON() }
    } finally {
      this._isDrawing = false
    }
  }

  /** 可视域分析（交互式绘制） */
  async drawViewshed(options: {
    horizontalAngle?: number
    verticalAngle?: number
    distance?: number
    heading?: number
    pitch?: number
    addHeight?: number
  } = {}): Promise<any> {
    const sightlineId = 'sightline_' + new Date().getTime()
    this._isDrawing = true
    try {
      this.map.scene.globe.depthTestAgainstTerrain = true
      // 使用 sightlineLayer 绘制，避免 graphicLayer 的 isAutoEditing 干扰
      const graphic = await this.sightlineLayer.startDraw({
        type: 'viewShed',
        style: {
          angle: options.horizontalAngle || 60,
          angle2: options.verticalAngle || 45,
          distance: options.distance || 80,
          heading: options.heading || 44,
          pitch: options.pitch || -12,
          addHeight: options.addHeight ?? 0.5
        }
      })

      // 设置当前选中的可视域图形
      this.selectedViewshed = graphic

      // 存储通视线数据
      const data: SightlineItemData = {
        id: sightlineId,
        type: 'viewshed',
        visible: true,
        timestamp: new Date(),
        offsetHeight: 0,
        viewshedGraphic: graphic
      }
      this.sightlineData.set(sightlineId, data)

      this.map.scene.globe.depthTestAgainstTerrain = false
      return {
        sightlineId,
        graphic,
        angle: graphic.angle,
        angle2: graphic.angle2,
        distance: graphic.distance,
        heading: graphic.heading,
        pitch: graphic.pitch,
        opacity: graphic.opacity
      }
    } finally {
      this._isDrawing = false
    }
  }

  /** 创建通视分析标记点（参考LuZhou项目风格） */
  private createSightlinePoint(position: any, isObserver: boolean) {
    const graphic = new mars3d.graphic.PointEntity({
      position,
      style: {
        color: Cesium.Color.fromCssColorString('rgba(51, 136, 255, 1)'),
        pixelSize: 6,
        outlineColor: Cesium.Color.fromCssColorString('rgba(255, 255, 255, 1)'),
        outlineWidth: 2,
        scaleByDistance: new Cesium.NearFarScalar(1.5e2, 1.0, 8.0e6, 0.2),
        label: {
          text: isObserver ? '观察位置' : '目标点',
          font_size: 14,
          font_family: '楷体',
          color: Cesium.Color.AZURE,
          outline: true,
          outlineColor: Cesium.Color.BLACK,
          outlineWidth: 2,
          horizontalOrigin: Cesium.HorizontalOrigin.CENTER,
          verticalOrigin: Cesium.VerticalOrigin.BOTTOM,
          pixelOffset: new Cesium.Cartesian2(0, -20),
          distanceDisplayCondition: new Cesium.DistanceDisplayCondition(0.0, 2000000)
        }
      }
    })
    this.sightlineLayer.addGraphic(graphic)
    return graphic
  }

  // ==================== 通视线管理 ====================

  /** 获取所有通视线数据 */
  getAllSightlineData(): SightlineItemData[] {
    return Array.from(this.sightlineData.values())
  }

  /** 切换指定通视线的显示/隐藏 */
  toggleSightlineVisibility(id: string, visible?: boolean): boolean {
    const data = this.sightlineData.get(id)
    if (!data) return false

    const targetVisible = visible !== undefined ? visible : !data.visible

    if (targetVisible) {
      // 显示：重新添加通视线
      this.map.scene.globe.depthTestAgainstTerrain = true
      const sightline = this.sightline
      if (sightline) {
        if (data.type === 'line' && data.center && data.targetPoint) {
          sightline.add(data.center, data.targetPoint, { offsetHeight: data.offsetHeight })
        } else if (data.type === 'circle' && data.center && data.targetPoints) {
          for (const tp of data.targetPoints) {
            sightline.add(data.center, tp, { offsetHeight: data.offsetHeight })
          }
        }
      }
      // 显示观察位置点和目标点
      if (data.centerPointGraphic) data.centerPointGraphic.show = true
      if (data.targetPointGraphic) data.targetPointGraphic.show = true
      if (data.targetPointGraphics) data.targetPointGraphics.forEach((g: any) => { g.show = true })
      if (data.viewshedGraphic) data.viewshedGraphic.show = true
      this.map.scene.globe.depthTestAgainstTerrain = false
    } else {
      // 隐藏
      if (data.centerPointGraphic) data.centerPointGraphic.show = false
      if (data.targetPointGraphic) data.targetPointGraphic.show = false
      if (data.targetPointGraphics) data.targetPointGraphics.forEach((g: any) => { g.show = false })
      if (data.viewshedGraphic) data.viewshedGraphic.show = false
      // 清除所有通视线，稍后重建可见的
      if (this.sightline) this.sightline.clear()
      // 重新添加其他可见的通视线
      this.rebuildVisibleSightlines()
    }
    data.visible = targetVisible
    return true
  }

  /** 重建所有可见的通视线 */
  private rebuildVisibleSightlines() {
    if (!this.sightline) return
    // sightline.clear() 已在调用方执行
    this.sightlineData.forEach(data => {
      if (!data.visible) return
      if (data.type === 'line' && data.center && data.targetPoint) {
        this.sightline!.add(data.center, data.targetPoint, { offsetHeight: data.offsetHeight })
      } else if (data.type === 'circle' && data.center && data.targetPoints) {
        for (const tp of data.targetPoints) {
          this.sightline!.add(data.center, tp, { offsetHeight: data.offsetHeight })
        }
      }
    })
  }

  /** 移除指定通视线 */
  removeSightlineById(id: string): boolean {
    const data = this.sightlineData.get(id)
    if (!data) return false

    // 移除观察位置点和目标点
    if (data.centerPointGraphic) {
      this.sightlineLayer.removeGraphic(data.centerPointGraphic)
    }
    if (data.targetPointGraphic) {
      this.sightlineLayer.removeGraphic(data.targetPointGraphic)
    }
    if (data.targetPointGraphics) {
      data.targetPointGraphics.forEach((g: any) => this.sightlineLayer.removeGraphic(g))
    }
    if (data.viewshedGraphic) {
      this.sightlineLayer.removeGraphic(data.viewshedGraphic)
    }

    this.sightlineData.delete(id)

    // 清除并重建通视线
    if (this.sightline) {
      this.sightline.clear()
      this.rebuildVisibleSightlines()
    }
    return true
  }

  /** 清除之前的所有通视分析（线、圆、可视域），保证唯一存在 */
  clearAllSightlineAnalysis() {
    const ids = Array.from(this.sightlineData.keys())
    ids.forEach(id => this.removeSightlineById(id))
    // 确保彻底移除所有 viewShed 类型的 graphic
    const sightlineGraphics = this.sightlineLayer.getGraphics()
    sightlineGraphics.forEach((g: any) => {
      if (g.type === 'viewShed') {
        this.sightlineLayer.removeGraphic(g)
      }
    })
  }

  /** 取消绘制 */
  cancelDrawing() {
    this.sightlineLayer.clearDrawing()
    this.graphicLayer.clearDrawing()
    this._isDrawing = false
  }

  // ==================== 可视域属性编辑 ====================

  /** 设置当前选中的可视域图形 */
  setSelectedViewshed(graphic: any) {
    this.selectedViewshed = graphic
  }

  /** 获取当前选中的可视域图形 */
  getSelectedViewshed() {
    return this.selectedViewshed
  }

  /** 更新可视域水平张角 */
  setViewshedAngle(value: number) {
    if (this.selectedViewshed) this.selectedViewshed.angle = value
  }

  /** 更新可视域垂直张角 */
  setViewshedAngle2(value: number) {
    if (this.selectedViewshed) this.selectedViewshed.angle2 = value
  }

  /** 更新可视域投射距离 */
  setViewshedDistance(value: number) {
    if (this.selectedViewshed) this.selectedViewshed.distance = value
  }

  /** 更新可视域四周方向 */
  setViewshedHeading(value: number) {
    if (this.selectedViewshed) this.selectedViewshed.heading = value
  }

  /** 更新可视域俯仰角度 */
  setViewshedPitch(value: number) {
    if (this.selectedViewshed) this.selectedViewshed.pitch = value
  }

  /** 切换视椎框线显示 */
  setViewshedFrustum(show: boolean) {
    if (this.selectedViewshed) this.selectedViewshed.showFrustum = show
  }

  /** 更新可视域透明度 */
  setViewshedOpacity(value: number) {
    if (this.selectedViewshed) this.selectedViewshed.opacity = value
  }

  /** 点选相机位置 */
  async pickCameraPosition() {
    if (!this.selectedViewshed) return
    const graphic = await this.graphicLayer.startDraw({ type: 'point' })
    const point = graphic.point
    graphic.remove()
    this.selectedViewshed.position = point
  }

  /** 点选四周视角目标 */
  async pickViewTarget() {
    if (!this.selectedViewshed) return
    const graphic = await this.graphicLayer.startDraw({ type: 'point' })
    const point = graphic.point
    graphic.remove()
    this.selectedViewshed.targetPosition = point
  }

  /** 切换整个通视线图层的可见性 */
  setSightlineVisible(visible: boolean) {
    if (this.sightline) {
      ;(this.sightline as any).show = visible
    }
  }

  /** 移除通视线 */
  removeSightline() {
    if (this.sightline) {
      this.sightline.clear()
    }
  }

  // ==================== 天际线分析 ====================

  /** 天际线分析 */
  analyzeSkyline(): any {
    const skyline = this.initSkyline()
    skyline.enabled = true
    return skyline.toJSON()
  }

  setSkylineWidth(width: number) {
    if (this.skyline) this.skyline.width = width
  }

  setSkylineColor(color: string) {
    if (this.skyline) this.skyline.color = Cesium.Color.fromCssColorString(color)
  }

  // ==================== 日照分析 ====================

  /** 日照分析 - 设置时间 */
  setShadowsTime(date: string, hours: number, minutes: number) {
    const shadows = this.initShadows() as any
    shadows.date = date
    shadows.time = hours * 3600 + minutes * 60
    return shadows
  }

  /** 日照分析 - 开始播放 */
  startShadowsPlay(multiplier = 1600) {
    const shadows = this.initShadows() as any
    shadows.enabled = true
    shadows.multiplier = multiplier
    return shadows
  }

  /** 日照分析 - 停止播放 */
  stopShadowsPlay() {
    if (this.shadows) {
      ;(this.shadows as any).enabled = false
      ;(this.shadows as any).multiplier = 0
    }
  }

  /** 获取当前阴影时间 */
  getShadowsTime(): any {
    if (this.shadows) {
      return (this.shadows as any).time
    }
    return null
  }

  // ==================== 淹没分析 ====================

  /** 淹没分析 */
  analyzeFlood(waterHeight: number): any {
    if (!this.flood) {
      this.flood = new (mars3d.thing as any).Flood({
        waterHeight,
        opacity: 0.6
      })
      this.map.addThing(this.flood)
    }
    this.flood.waterHeight = waterHeight
    this.flood.start()
    return this.flood
  }

  // ==================== 等高线分析 ====================

  /** 生成等高线 */
  async generateContourLine(options: {
    spacing?: number
    lineWidth?: number
    lineColor?: string
    showLabel?: boolean
  } = {}): Promise<any> {
    const spacing = options.spacing || 10
    const lineWidth = options.lineWidth || 2
    const lineColor = options.lineColor || '#ff0000'

    const graphic = await this.map.graphicLayer.startDraw({
      type: 'rectangle',
      style: {
        color: 'rgba(255, 255, 0, 0.1)',
        outline: true,
        outlineColor: '#ffff00'
      }
    })

    if (!this.contourLine) {
      this.contourLine = new (mars3d.thing as any).ContourLine({
        spacing,
        color: lineColor,
        width: lineWidth,
        show: true
      })
      this.map.addThing(this.contourLine)
    } else {
      this.contourLine.spacing = spacing
      this.contourLine.color = Cesium.Color.fromCssColorString(lineColor)
      this.contourLine.width = lineWidth
    }

    this.contourLine.area = graphic.positionsShow
    return { positions: graphic.positionsShow, spacing, lineWidth, lineColor }
  }

  /** 设置等高线间距 */
  setContourSpacing(spacing: number) {
    if (this.contourLine) this.contourLine.spacing = spacing
  }

  /** 设置等高线宽度 */
  setContourWidth(width: number) {
    if (this.contourLine) this.contourLine.width = width
  }

  /** 设置等高线颜色 */
  setContourColor(color: string) {
    if (this.contourLine) this.contourLine.color = Cesium.Color.fromCssColorString(color)
  }

  /** 显示/隐藏等高线 */
  toggleContourVisible(visible: boolean) {
    if (this.contourLine) this.contourLine.show = visible
  }

  // ==================== 压平功能 ====================

  /** 开始压平 */
  async startFlatten(options: { height?: number; tilesetUrl?: string } = {}): Promise<any> {
    const height = options.height || 0
    const graphic = await this.map.graphicLayer.startDraw({
      type: 'polygon',
      style: {
        color: 'rgba(255, 255, 0, 0.2)',
        outline: true,
        outlineColor: '#ffff00'
      }
    })

    if (!this.flatObj) {
      this.flatObj = new (mars3d.thing as any).Flat({
        positions: graphic.positionsShow,
        height
      })
      this.map.addThing(this.flatObj)
    }

    return { positions: graphic.positionsShow, height }
  }

  /** 更新压平高度 */
  updateFlattenHeight(height: number) {
    if (this.flatObj) this.flatObj.height = height
  }

  /** 清除压平 */
  clearFlatten() {
    if (this.flatObj) {
      this.map.removeThing(this.flatObj, true)
      this.flatObj = null
    }
  }

  // ==================== 图上标记 ====================

  /** 绘制点标记 */
  async drawPoint(style?: any): Promise<any> {
    const graphic = await this.graphicLayer.startDraw({
      type: 'point',
      style: style || {
        pixelSize: 10,
        color: '#00ff00',
        outlineColor: '#ffffff',
        outlineWidth: 2
      }
    })
    return graphic
  }

  /** 绘制线标记 */
  async drawPolyline(style?: any): Promise<any> {
    const graphic = await this.graphicLayer.startDraw({
      type: 'polyline',
      style: style || { color: '#ffff00', width: 3 }
    })
    return graphic
  }

  /** 绘制面标记 */
  async drawPolygon(style?: any): Promise<any> {
    const graphic = await this.graphicLayer.startDraw({
      type: 'polygon',
      style: style || { color: 'rgba(255,255,0,0.3)', outline: true, outlineColor: '#ffff00' }
    })
    return graphic
  }

  /** 绘制圆标记 */
  async drawCircle(style?: any): Promise<any> {
    const graphic = await this.graphicLayer.startDraw({
      type: 'circle',
      style: style || { color: 'rgba(255,255,0,0.3)', outline: true, outlineColor: '#ffff00' }
    })
    return graphic
  }

  /** 导出GeoJSON */
  exportGeoJSON(): string {
    const graphics = this.graphicLayer.graphics
    const geojson: any = { type: 'FeatureCollection', features: [] }
    graphics.forEach((g: any) => {
      if (g.toGeoJSON) {
        geojson.features.push(g.toGeoJSON())
      }
    })
    return JSON.stringify(geojson)
  }

  // ==================== 观测台 ====================

  /** 获取当前相机视角 */
  getCameraView(): any {
    return (this.map as any).getCameraView()
  }

  /** 飞行到视角 */
  flyToView(view: any, duration: number = 2) {
    (this.map as any).flyToView(view, { duration })
  }

  // ==================== 坐标定位 ====================

  /** 绑定鼠标点击获取坐标 */
  bindMouseClickForCoordinate(callback: (lng: number, lat: number, alt: number) => void) {
    this.map.on(mars3d.EventType.click, (e: any) => {
      const cartesian = e.cartesian
      if (cartesian) {
        const point = mars3d.LngLatPoint.fromCartesian(cartesian)
        if (point) {
          callback(point.lng, point.lat, point.alt)
        }
      }
    })
  }

  /** 添加坐标标记点 */
  addCoordinateMarker(lng: number, lat: number, alt: number, label?: string): any {
    const marker = new mars3d.graphic.PointEntity({
      position: [lng, lat, alt],
      style: {
        pixelSize: 12,
        color: '#00ff00',
        outlineColor: '#ffffff',
        outlineWidth: 2,
        label: {
          text: label || `${lng.toFixed(6)}, ${lat.toFixed(6)}`,
          font_size: 14,
          color: '#ffffff',
          pixelOffsetY: -20
        }
      }
    })
    this.graphicLayer.addGraphic(marker)
    return marker
  }

  /** 定位到坐标 */
  locateCoordinate(lng: number, lat: number, alt: number, duration = 2) {
    this.map.flyToPoint([lng, lat, alt], { duration })
  }

  // ==================== 绘制区域 ====================

  /** 绘制矩形区域 */
  async drawRectangle(style?: any): Promise<any> {
    return await this.map.graphicLayer.startDraw({
      type: 'rectangle',
      style: style || { color: 'rgba(255,255,0,0.2)', outline: true, outlineColor: '#ffff00' }
    })
  }

  /** 绘制圆形区域 */
  async drawCircleArea(style?: any): Promise<any> {
    return await this.map.graphicLayer.startDraw({
      type: 'circle',
      style: style || { color: 'rgba(255,255,0,0.2)', outline: true, outlineColor: '#ffff00' }
    })
  }

  /** 绘制多边形区域 */
  async drawPolygonArea(style?: any): Promise<any> {
    return await this.map.graphicLayer.startDraw({
      type: 'polygon',
      style: style || { color: 'rgba(255,255,0,0.2)', outline: true, outlineColor: '#ffff00' }
    })
  }

  // ==================== 塔基建模 ====================

  /** 绘制塔基线杆 */
  async drawTowerPole(options: { height?: number; radius?: number; color?: string } = {}): Promise<any> {
    const height = options.height || 30
    const radius = options.radius || 0.5
    const color = options.color || '#ff0000'

    const graphic = await this.map.graphicLayer.startDraw({
      type: 'point',
      style: { pixelSize: 10, color }
    })

    const position = graphic.positionShow
    this.map.graphicLayer.clear()

    // 创建圆柱体
    const cylinder = new (mars3d.graphic as any).CylinderEntity({
      position,
      style: {
        length: height,
        topRadius: radius,
        bottomRadius: radius,
        color
      }
    })
    this.graphicLayer.addGraphic(cylinder)
    return cylinder
  }

  // ==================== 管线分析 ====================

  /** 绘制管线 */
  async drawPipeline(options: { radius?: number; color?: string } = {}): Promise<any> {
    const radius = options.radius || 1
    const color = options.color || '#00ffff'

    const graphic = await this.map.graphicLayer.startDraw({
      type: 'polyline',
      style: { color, width: 3 }
    })

    // 创建管线（使用管道效果）
    const pipe = new (mars3d.graphic as any).PolylineEntity({
      positions: graphic.positionsShow,
      style: {
        color,
        width: radius * 2,
        closure: true
      }
    })
    this.graphicLayer.addGraphic(pipe)
    this.map.graphicLayer.clear()
    return pipe
  }

  // ==================== 卷帘对比 ====================

  /** 创建卷帘控制 */
  createSplitControl(): any {
    const mapSplit = new (mars3d as any).MapSplit({
      direction: 0 // 水平方向
    })
    this.map.addThing(mapSplit)
    return mapSplit
  }

  /** 销毁卷帘控制 */
  destroySplitControl(splitControl: any) {
    if (splitControl) {
      this.map.removeThing(splitControl, true)
    }
  }

  // ==================== 辅助方法 ====================

  /** 创建点标记 */
  private createPointMarker(position: any, label: string) {
    const point = new mars3d.graphic.PointEntity({
      position,
      style: {
        pixelSize: 10,
        color: '#00ff00',
        outlineColor: '#ffffff',
        outlineWidth: 2,
        label: {
          text: label,
          font_size: 14,
          color: '#ffffff',
          pixelOffsetY: -20
        }
      }
    })
    this.graphicLayer.addGraphic(point)
    return point
  }

  /** 添加标注点 */
  addLabelMarker(position: any, text: string) {
    const marker = new (mars3d.graphic as any).DivLightEntity({
      position,
      style: {
        html: `<div style="background: rgba(0,0,0,0.7); padding: 5px 10px; border-radius: 4px; color: white; font-size: 14px;">${text}</div>`
      }
    })
    this.graphicLayer.addGraphic(marker)
    return marker
  }

  // ==================== 清除方法 ====================

  clearAll() {
    this.graphicLayer.clear()
    this.lineLayer.clear()
    this.sightlineLayer.clear()
    this.sightlineData.clear()
    this.selectedViewshed = null
    if (this.measure) this.measure.clear()
    if (this.sightline) this.sightline.clear()
    if (this.skyline) {
      this.map.removeThing(this.skyline, true)
      this.skyline = null
    }
    if (this.shadows) {
      ;(this.shadows as any).enabled = false
      this.map.removeThing(this.shadows, true)
      this.shadows = null
    }
    if (this.contourLine) {
      this.map.removeThing(this.contourLine, true)
      this.contourLine = null
    }
    if (this.flood) {
      this.map.removeThing(this.flood, true)
      this.flood = null
    }
    if (this.flatObj) {
      this.map.removeThing(this.flatObj, true)
      this.flatObj = null
    }
    if (this.viewshed) {
      this.map.removeThing(this.viewshed, true)
      this.viewshed = null
    }
  }

  clearMeasure() {
    if (this.measure) this.measure.clear()
  }

  clearSightline() {
    this.clearAllSightlineAnalysis()
    if (this.sightline) this.sightline.clear()
    this.sightlineLayer.clear()
    this.selectedViewshed = null
  }

  clearSkyline() {
    if (this.skyline) {
      this.map.removeThing(this.skyline, true)
      this.skyline = null
    }
  }

  clearShadows() {
    if (this.shadows) {
      ;(this.shadows as any).enabled = false
      this.map.removeThing(this.shadows, true)
      this.shadows = null
    }
  }

  clearContour() {
    if (this.contourLine) {
      this.map.removeThing(this.contourLine, true)
      this.contourLine = null
    }
  }

  clearFlood() {
    if (this.flood) {
      this.map.removeThing(this.flood, true)
      this.flood = null
    }
  }

  clearGraphics() {
    this.graphicLayer.clear()
    this.lineLayer.clear()
  }

  // ==================== 销毁 ====================

  destroy() {
    this.clearAll()
    this.sightlineData.clear()
    this.selectedViewshed = null
    if (this.measure) {
      this.map.removeThing(this.measure, true)
      this.measure = null
    }
    if (this.sightline) {
      this.map.removeThing(this.sightline, true)
      this.sightline = null
    }
    if (this.graphicLayer) {
      this.map.removeLayer(this.graphicLayer, true)
    }
    if (this.lineLayer) {
      this.map.removeLayer(this.lineLayer, true)
    }
    if (this.sightlineLayer) {
      this.map.removeLayer(this.sightlineLayer, true)
    }
  }
}
