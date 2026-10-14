import * as mars3d from "mars3d"
import * as turf from "@turf/turf"

const Cesium = mars3d.Cesium

export interface PipelineOptions {
  map: mars3d.Map
  radius?: number
  smoothness?: number
  opacity?: number
  color?: string
}

export class PipelineManager {
  private map: mars3d.Map
  private radius: number
  private smoothness: number
  private opacity: number
  private color: string
  private graphicLayer: mars3d.layer.GraphicLayer
  private pipelineGraphic: any | null = null
  private originalPositions: any[] = []
  private finalPositions: any[] = []

  constructor(options: PipelineOptions) {
    this.map = options.map
    this.radius = options.radius || 0.5
    this.smoothness = options.smoothness || 0.5
    this.opacity = options.opacity || 0.6
    this.color = options.color || "#00d4ff"

    this.graphicLayer = new mars3d.layer.GraphicLayer({
      name: "PipelineLayer",
      id: "pipeline-layer",
      show: true
    })
    this.map.addLayer(this.graphicLayer)
    console.log("PipelineManager: 图层已创建并添加到地图，图层 ID:", this.graphicLayer.id)
  }

  setRadius(radius: number) {
    this.radius = radius
    if (this.originalPositions.length > 0) {
      this.updatePipeline()
    }
  }

  setSmoothness(smoothness: number) {
    this.smoothness = smoothness
    if (this.originalPositions.length > 0) {
      this.updatePipeline()
    }
  }

  setOpacity(opacity: number) {
    this.opacity = opacity
    if (this.pipelineGraphic) {
      this.pipelineGraphic.setStyle({ opacity: this.opacity })
    }
  }

  async createPipeline(positions: any[], options: { clear?: boolean } = { clear: true }) {
    this.originalPositions = positions
    return await this.updatePipeline(options.clear !== false)
  }

  private async updatePipeline(clear: boolean = true) {
    if (!this.map || !this.map.viewer) {
      console.error("PipelineManager: 地图或 viewer 未初始化，无法创建管线")
      return null
    }

    if (clear) {
      this.graphicLayer.clear()
    }

    if (this.originalPositions.length < 2) {
      return null
    }

    let finalPositions = this.originalPositions

    console.log("PipelineManager: 开始处理管线路径，原始位置数量:", this.originalPositions.length)

    try {
      const coordinates = this.originalPositions.map((pos) => {
        const cartographic = Cesium.Cartographic.fromCartesian(pos)
        return [Cesium.Math.toDegrees(cartographic.longitude), Cesium.Math.toDegrees(cartographic.latitude), cartographic.height || 0]
      })

      console.log("PipelineManager: 坐标转换完成，经纬度坐标数量:", coordinates.length, "第一个坐标:", coordinates[0])

      let line = turf.lineString(coordinates)

      if (this.smoothness > 0) {
        try {
          console.log("PipelineManager: 开始 Bezier 平滑处理，smoothness:", this.smoothness)
          line = turf.bezierSpline(line, { resolution: 2000, sharpness: this.smoothness }) as any
          console.log("PipelineManager: Bezier 平滑完成，平滑后坐标数量:", line.geometry.coordinates.length)
        } catch (bezierError) {
          console.warn("Bezier smoothing failed, using original line:", bezierError)
        }
      }

      const smoothCoords = line.geometry.coordinates

      if (!this.map.viewer) {
        console.error("PipelineManager: viewer 未定义，无法进行地形采样")
        return null
      }

      const terrainProvider = this.map.viewer.terrainProvider

      console.log("PipelineManager: 开始生成最终坐标，使用地形采样:", !!terrainProvider && !(terrainProvider instanceof Cesium.EllipsoidTerrainProvider))

      if (!terrainProvider || terrainProvider instanceof Cesium.EllipsoidTerrainProvider) {
        finalPositions = smoothCoords.map((coord: any) => Cesium.Cartesian3.fromDegrees(coord[0], coord[1], coord[2] || 0))
        console.log("PipelineManager: 无地形数据，使用固定高度")
      } else {
        try {
          const smoothPositions = smoothCoords.map((coord: any) => Cesium.Cartographic.fromDegrees(coord[0], coord[1]))
          const updatedPositions = await Cesium.sampleTerrainMostDetailed(terrainProvider, smoothPositions)
          finalPositions = updatedPositions.map((pos: any, index: number) =>
            Cesium.Cartesian3.fromRadians(pos.longitude, pos.latitude, pos.height || smoothCoords[index][2] || 0)
          )
          console.log("PipelineManager: 地形采样完成")
        } catch (sampleError) {
          console.warn("Terrain sampling failed, using coordinates height:", sampleError)
          finalPositions = smoothCoords.map((coord: any) => Cesium.Cartesian3.fromDegrees(coord[0], coord[1], coord[2] || 0))
        }
      }

      console.log("PipelineManager: 最终位置数量:", finalPositions.length)
    } catch (error) {
      console.error("Error processing pipeline path:", error)
      console.warn("使用原始位置作为降级方案")
    }

    this.finalPositions = finalPositions

    if (!this.graphicLayer) {
      console.error("PipelineManager: 图层未初始化，无法创建管线实体")
      return null
    }

    console.log("PipelineManager: 开始创建管线实体", {
      positionsLength: this.finalPositions.length,
      radius: this.radius,
      color: this.color,
      opacity: this.opacity
    })

    this.pipelineGraphic = new mars3d.graphic.PolylineVolumeEntity({
      positions: this.finalPositions,
      style: {
        shape: "circle",
        radius: this.radius,
        color: this.color,
        opacity: this.opacity,
        outline: true,
        outlineColor: "rgba(255, 255, 255, 1)",
        outlineOpacity: 0.5,
        outlineWidth: 2
      }
    })

    this.graphicLayer.addGraphic(this.pipelineGraphic)

    console.log("PipelineManager: 管线实体已添加到图层", {
      graphicId: this.pipelineGraphic.id,
      graphicType: this.pipelineGraphic.type,
      layerGraphicsCount: this.graphicLayer.length,
      graphicVisible: this.pipelineGraphic.show
    })

    if (!this.graphicLayer.show) {
      console.warn("PipelineManager: 图层被隐藏，尝试显示图层")
      this.graphicLayer.show = true
    }

    if (!this.pipelineGraphic.show) {
      console.warn("PipelineManager: 管线被隐藏，尝试显示管线")
      this.pipelineGraphic.show = true
    }

    return this.pipelineGraphic
  }

  getPositions() {
    if (this.finalPositions && this.finalPositions.length > 0) {
      return this.finalPositions
    }
    return []
  }

  isValid(): boolean {
    if (!this.map) {
      return false
    }
    if (!this.map.viewer) {
      return false
    }
    if (!this.graphicLayer) {
      return false
    }
    return true
  }

  destroy() {
    if (this.graphicLayer) {
      this.graphicLayer.clear()
      if (this.map) {
        try {
          this.map.removeLayer(this.graphicLayer)
        } catch (e) {
          console.warn("PipelineManager: 移除图层失败", e)
        }
      }
      this.graphicLayer = null as any
    }
    this.originalPositions = []
    this.finalPositions = []
    this.pipelineGraphic = null
    console.log("PipelineManager: 资源已销毁")
  }

  clear() {
    if (this.graphicLayer) {
      this.graphicLayer.clear()
    }
    this.originalPositions = []
    this.finalPositions = []
    this.pipelineGraphic = null
  }
}
