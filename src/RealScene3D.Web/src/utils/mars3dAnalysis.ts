import * as mars3d from 'mars3d'
import * as Cesium from 'mars3d-cesium'

export class Mars3DAnalysisTools {
  private map: mars3d.Map
  private graphicLayer: mars3d.layer.GraphicLayer
  private measure: mars3d.thing.Measure | null = null
  private sightline: mars3d.thing.Sightline | null = null
  private skyline: mars3d.thing.Skyline | null = null
  private measureInitPromise: Promise<void> | null = null

  constructor(map: mars3d.Map) {
    this.map = map
    this.graphicLayer = new mars3d.layer.GraphicLayer()
    map.addLayer(this.graphicLayer)
  }

  // 初始化测量工具
  initMeasure() {
    if (!this.measure) {
      this.measure = new mars3d.thing.Measure({
        label: {
          color: '#ffffff',
          font_family: '楷体',
          font_size: 16
        }
      })
      this.map.addThing(this.measure)
    }
    return this.measure
  }

  // 距离测量
  async measureDistance(): Promise<{ distance: number; positions: any }> {
    const measure = this.initMeasure()
    
    return new Promise((resolve) => {
      (measure as any).distance({
        unit: 'meter',
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

  // 贴地距离测量
  async measureDistanceSurface(): Promise<{ distance: number; positions: any }> {
    const measure = this.initMeasure()
    
    return new Promise((resolve) => {
      (measure as any).distanceSurface({
        unit: 'meter',
        exact: false,
        splitNum: 5,
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

  // 面积测量
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

  // 贴地面积测量
  async measureAreaSurface(): Promise<{ area: number; perimeter: number; positions: any }> {
    const measure = this.initMeasure()
    
    return new Promise((resolve) => {
      (measure as any).areaSurface({
        unit: 'meter',
        exact: false,
        splitNum: 5,
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

  // 剖面分析
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

  // 初始化通视分析
  initSightline() {
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

  // 线通视分析
  async sightlineLinear(): Promise<any> {
    const sightline = this.initSightline()
    
    if (!this.map.graphicLayer) {
      const layer = new mars3d.layer.GraphicLayer()
      this.map.addLayer(layer)
    }
    
    const graphic = await this.map.graphicLayer.startDraw({
      type: 'polyline',
      maxPointNum: 2,
      style: {
        color: '#55ff33',
        width: 5
      }
    })
    
    const positions = graphic.positionsShow
    const center = positions[0]
    const targetPoint = positions[1]
    
    this.map.graphicLayer.clear()
    this.map.scene.globe.depthTestAgainstTerrain = true
    
    sightline.add(center, targetPoint, { offsetHeight: 1.5 })
    
    this.createPointMarker(center, '观察点')
    this.createPointMarker(targetPoint, '目标点')
    
    this.map.scene.globe.depthTestAgainstTerrain = false
    
    return {
      observer: center,
      target: targetPoint,
      result: sightline.toJSON()
    }
  }

  // 圆形通视分析
  async sightlineCircular(): Promise<any> {
    const sightline = this.initSightline()
    
    if (!this.map.graphicLayer) {
      const layer = new mars3d.layer.GraphicLayer()
      this.map.addLayer(layer)
    }
    
    const graphic = await this.map.graphicLayer.startDraw({
      type: 'circle',
      style: {
        color: 'rgba(255, 255, 0, 0.2)',
        outline: true,
        outlineColor: '#ffff00'
      }
    })
    
    let center = graphic.positionShow
    center = mars3d.PointUtil.addPositionsHeight(center, 1.5)
    
    const targetPoints = graphic.getOutlinePositions(false, 45)
    
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
    
    return {
      observer: center,
      targets: results,
      result: sightline.toJSON()
    }
  }

  // 初始化天际线分析
  initSkyline() {
    if (!this.skyline) {
      this.skyline = new mars3d.thing.Skyline({
        width: 5
      })
      this.map.addThing(this.skyline)
      this.map.scene.globe.depthTestAgainstTerrain = true
    }
    return this.skyline
  }

  // 天际线分析
  analyzeSkyline(): any {
    const skyline = this.initSkyline()
    return skyline.toJSON()
  }

  // 修改天际线宽度
  setSkylineWidth(width: number) {
    if (this.skyline) {
      this.skyline.width = width
    }
  }

  // 修改天际线颜色
  setSkylineColor(color: string) {
    if (this.skyline) {
      this.skyline.color = Cesium.Color.fromCssColorString(color)
    }
  }

  // 创建点标记
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

  // 添加标注点
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

  // 清除所有图形
  clearAll() {
    this.graphicLayer.clear()
    if (this.measure) {
      this.measure.clear()
    }
    if (this.sightline) {
      this.sightline.clear()
    }
    if (this.skyline) {
      this.map.removeThing(this.skyline, true)
      this.skyline = null
    }
  }

  // 高度测量
  async measureHeight(): Promise<number> {
    const measure = this.initMeasure()

    return new Promise((resolve) => {
      (measure as any).height({
        label: { text: '高度测量' },
        success: (graphic: any) => {
          resolve(graphic.measured?.height || 0)
        }
      })
    })
  }

  // 坐标测量
  async measurePoint(): Promise<{ longitude: number; latitude: number; height: number }> {
    const measure = this.initMeasure()

    return new Promise((resolve) => {
      (measure as any).point({
        label: { text: '坐标测量' },
        success: (graphic: any) => {
          const pos = graphic.positionShow || graphic.position
          resolve({
            longitude: pos?.lng || pos?.x || 0,
            latitude: pos?.lat || pos?.y || 0,
            height: pos?.alt || pos?.z || 0
          })
        }
      })
    })
  }

  // 日照分析
  async analyzeSun(
    options: { date: string; startTime: string; endTime: string }
  ): Promise<any> {
    return new Promise((resolve) => {
      const sun = new (mars3d.thing as any).Sun({
        date: options.date,
        startTime: options.startTime,
        endTime: options.endTime
      })
      this.map.addThing(sun)
      sun.start()

      setTimeout(() => {
        const result = sun.toJSON ? sun.toJSON() : {}
        resolve(result)
      }, 100)
    })
  }

  // 淹没分析
  async analyzeFlood(waterHeight: number): Promise<any> {
    return new Promise((resolve) => {
      const flood = new (mars3d.thing as any).Flood({
        waterHeight,
        opacity: 0.6
      })
      this.map.addThing(flood)
      flood.start()

      resolve({ waterHeight, result: flood.toJSON ? flood.toJSON() : {} })
    })
  }

  // 清除测量
  clearMeasure() {
    if (this.measure) {
      this.measure.clear()
    }
  }

  // 清除通视分析
  clearSightline() {
    if (this.sightline) {
      this.sightline.clear()
    }
    this.graphicLayer.clear()
  }

  // 清除天际线
  clearSkyline() {
    if (this.skyline) {
      this.map.removeThing(this.skyline, true)
      this.skyline = null
    }
  }

  // 获取相机视角
  getCameraView(): any {
    return (this.map as any).getCameraView()
  }

  // 飞行到视角
  flyToView(view: any, duration: number = 2) {
    (this.map as any).flyToView(view, { duration })
  }

  // 销毁
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
  }
}
