import * as mars3d from 'mars3d'
import * as Cesium from 'mars3d-cesium'

export class Mars3DAnalysisTools {
  private map: mars3d.Map
  private graphicLayer: mars3d.layer.GraphicLayer
  private lineLayer: mars3d.layer.GraphicLayer
  private measure: mars3d.thing.Measure | null = null
  private sightline: mars3d.thing.Sightline | null = null
  private skyline: mars3d.thing.Skyline | null = null
  private shadows: mars3d.thing.Shadows | null = null
  private contourLine: any = null
  private viewshed: any = null
  private flood: any = null
  private flatObj: any = null

  constructor(map: mars3d.Map) {
    this.map = map
    this.graphicLayer = new mars3d.layer.GraphicLayer({
      isAutoEditing: true
    })
    map.addLayer(this.graphicLayer)
    this.lineLayer = new mars3d.layer.GraphicLayer()
    map.addLayer(this.lineLayer)
  }

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
  async sightlineLinear(observerHeight = 1.5): Promise<any> {
    const sightline = this.initSightline()
    const graphic = await this.map.graphicLayer.startDraw({
      type: 'polyline',
      maxPointNum: 2,
      style: { color: '#55ff33', width: 5 }
    })
    const positions = graphic.positionsShow
    const center = positions[0]
    const targetPoint = positions[1]
    this.map.graphicLayer.clear()
    this.map.scene.globe.depthTestAgainstTerrain = true
    sightline.add(center, targetPoint, { offsetHeight: observerHeight })
    this.createPointMarker(center, '观察点')
    this.createPointMarker(targetPoint, '目标点')
    this.map.scene.globe.depthTestAgainstTerrain = false
    return { observer: center, target: targetPoint, result: sightline.toJSON() }
  }

  /** 圆形通视分析 */
  async sightlineCircular(observerHeight = 1.5, sampleCount = 45): Promise<any> {
    const sightline = this.initSightline()
    const graphic = await this.map.graphicLayer.startDraw({
      type: 'circle',
      style: { color: 'rgba(255, 255, 0, 0.2)', outline: true, outlineColor: '#ffff00' }
    })
    let center = graphic.positionShow
    center = mars3d.PointUtil.addPositionsHeight(center, observerHeight)
    const targetPoints = graphic.getOutlinePositions(false, sampleCount)
    this.map.graphicLayer.clear()
    this.map.scene.globe.depthTestAgainstTerrain = true
    const results: any[] = []
    for (let i = 0; i < targetPoints.length; i++) {
      let targetPoint = targetPoints[i]
      targetPoint = mars3d.PointUtil.getSurfacePosition(this.map.scene, targetPoint)
      sightline.add(center, targetPoint)
      results.push(targetPoint)
    }
    this.createPointMarker(center, '观察点')
    this.map.scene.globe.depthTestAgainstTerrain = false
    return { observer: center, targets: results, result: sightline.toJSON() }
  }

  /** 可视域分析 */
  analyzeViewshed(options: {
    direction?: number
    pitch?: number
    horizontalFov?: number
    verticalFov?: number
    distance?: number
  } = {}): any {
    if (!this.viewshed) {
      this.viewshed = new (mars3d.thing as any).Viewshed({
        ...options
      })
      this.map.addThing(this.viewshed)
    }
    return this.viewshed
  }

  /** 切换通视线可见性 */
  toggleSightlineVisibility(visible: boolean) {
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
    if (this.sightline) this.sightline.clear()
    this.graphicLayer.clear()
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
  }
}
