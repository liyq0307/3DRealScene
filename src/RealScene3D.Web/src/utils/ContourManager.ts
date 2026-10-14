import { $message } from "@mars/components/mars-ui/index"
import * as mars3d from "mars3d"
import * as turf from "@turf/turf"

const Cesium = mars3d.Cesium

export class ContourManager {
  map: any
  interval: number // 等高距
  showLabels: boolean // 是否显示标注
  show: boolean // 是否显示等高线
  width: number // 线宽
  color: any // 线颜色，Cesium.Color 对象
  currentBbox: number[] | null // 当前边界框 [minLng, minLat, maxLng, maxLat]
  terrainPoints: any // 采样的地形点数据，Turf FeatureCollection
  dataSource: any // Cesium CustomDataSource 用于存储等高线实体
  labelCollection: any // Cesium LabelCollection 用于存储等高线标注
  currentGeojson: any | null // 存储当前渲染的等高线数据
  smoothness: number // 平滑度

  constructor(options: any) {
    this.map = options.map
    this.interval = options.interval || 10
    this.showLabels = options.showLabels !== false
    this.show = options.show !== false
    this.width = options.width || 2
    this.color = options.color || Cesium.Color.fromCssColorString("#FF0000")

    this.currentBbox = null
    this.terrainPoints = null
    this.currentGeojson = null

    this.dataSource = new Cesium.CustomDataSource("ContourLines")
    this.map.viewer.dataSources.add(this.dataSource)
    this.dataSource.show = this.show

    this.labelCollection = this.map.viewer.scene.primitives.add(
      new Cesium.LabelCollection({
        scene: this.map.viewer.scene
      })
    )
    this.labelCollection.show = this.showLabels
  }

  setInterval(interval: number) {
    this.interval = interval
    if (this.terrainPoints) {
      this.generateContours()
    }
  }

  setWidth(width: number) {
    this.width = width
    if (this.currentGeojson) {
      this.refreshRendering()
    }
  }

  setColor(color: any) {
    this.color = color
    if (this.currentGeojson) {
      this.refreshRendering()
    }
  }

  setShowLabels(show: boolean) {
    this.showLabels = show
    this.labelCollection.show = this.showLabels
  }

  setShow(show: boolean) {
    this.show = show
    if (this.dataSource) {
      this.dataSource.show = this.show
    }
  }

  clear() {
    if (this.dataSource) {
      this.dataSource.entities.removeAll()
    }
    if (this.labelCollection) {
      this.labelCollection.removeAll()
    }
    this.terrainPoints = null
    this.currentBbox = null
    this.currentGeojson = null
  }

  destroy() {
    this.clear()
    if (this.map?.viewer) {
      if (this.dataSource) {
        this.map.viewer.dataSources.remove(this.dataSource, true)
      }
      if (this.labelCollection) {
        this.map.viewer.scene.primitives.remove(this.labelCollection)
      }
    }
    this.dataSource = null
    this.labelCollection = null
    this.map = null
  }

  refreshRendering() {
    if (this.currentGeojson) {
      this.dataSource.entities.removeAll()
      this.labelCollection.removeAll()
      this.renderContours(this.currentGeojson)
    }
  }

  getGeojson() {
    return this.currentGeojson
  }

  async createContoursForBbox(bbox: number[], polygon: any = null, cellSide: number = 0.01, graphic: any = null) {
    this.clear()
    this.currentBbox = bbox

    let grid = turf.pointGrid(bbox as any, cellSide, { units: "kilometers" })

    if (grid.features.length > 50000) {
      $message("绘制区域过大或采样点过多(" + grid.features.length + ")，请缩小范围或增大采样间距")
      if (this.map?.graphicLayer && graphic) {
        this.map.graphicLayer.removeGraphic(graphic)
      }
      this.clear()
      return
    }

    if (polygon) {
      try {
        const polyFeature = polygon
        if (polyFeature.type === "Polygon") {
          const coords = polyFeature.coordinates
          coords.forEach((ring: any) => {
            if (ring.length > 0) {
              const first = ring[0]
              const last = ring[ring.length - 1]
              if (first[0] !== last[0] || first[1] !== last[1]) {
                ring.push([first[0], first[1]])
              }
            }
          })
        }
        grid = turf.pointsWithinPolygon(grid as any, polyFeature) as any
      } catch (e) {
        // ignore
      }
    }

    this.terrainPoints = await this.sampleElevations(grid)
    this.generateContours()
  }

  async sampleElevations(grid: any) {
    if (!this.map?.viewer) {
      throw new Error("Cesium viewer is not initialized or has been destroyed.")
    }
    const terrainProvider = this.map.viewer.terrainProvider
    const positions = grid.features.map((f: any) => {
      const [lng, lat] = f.geometry.coordinates
      return Cesium.Cartographic.fromDegrees(lng, lat)
    })

    const updatedPositions = await Cesium.sampleTerrainMostDetailed(terrainProvider, positions)

    grid.features.forEach((f: any, i: number) => {
      f.properties.elevation = updatedPositions[i].height || 0
    })

    return grid
  }

  generateContours() {
    if (!this.terrainPoints || !this.terrainPoints.features?.length) {
      return
    }

    this.dataSource.entities.removeAll()
    this.labelCollection.removeAll()

    const elevations = this.terrainPoints.features.map((f: any) => f.properties.elevation)

    if (elevations.length > 50000) {
      $message(`绘制点数过多(${elevations.length})，请缩小区域或增大采样间隔`)
      return
    }

    let minElev = elevations[0]
    let maxElev = elevations[0]
    for (let i = 1; i < elevations.length; i++) {
      const h = elevations[i]
      if (h < minElev) {
        minElev = h
      }
      if (h > maxElev) {
        maxElev = h
      }
    }

    minElev = Math.floor(minElev / this.interval) * this.interval
    maxElev = Math.ceil(maxElev / this.interval) * this.interval

    const breaks: number[] = []
    for (let i = minElev; i <= maxElev; i += this.interval) {
      breaks.push(i)
    }

    if (breaks.length === 0) {
      return
    }

    try {
      const lines = turf.isolines(this.terrainPoints, breaks, {
        zProperty: "elevation"
      })

      this.currentGeojson = lines
      this.renderContours(lines)
    } catch (e) {
      console.error("生成等高线失败", e)
    }
  }

  renderContours(geojson: any) {
    if (!geojson || !geojson.features) {
      return
    }

    geojson.features.forEach((feature: any) => {
      const elevation = feature.properties.elevation !== undefined ? feature.properties.elevation : feature.properties.value || 0
      const type = feature.geometry.type
      const coordinates = feature.geometry.coordinates

      if (!coordinates || coordinates.length === 0) {
        return
      }

      let lineSegments: any[] = []
      if (type === "LineString") {
        lineSegments = [coordinates]
      } else if (type === "MultiLineString") {
        lineSegments = coordinates
      }

      lineSegments.forEach((lineCoords: any) => {
        if (!lineCoords || lineCoords.length < 2) {
          return
        }

        const positions = lineCoords.map((coord: any) => Cesium.Cartesian3.fromDegrees(coord[0], coord[1], elevation))

        this.dataSource.entities.add({
          polyline: {
            positions,
            width: this.width,
            material: this.color,
            clampToGround: true
          }
        })

        if (this.showLabels && positions.length >= 2) {
          const midIndex = Math.floor(positions.length / 2)
          const midPos = positions[midIndex]

          this.labelCollection.add({
            position: midPos,
            text: `${elevation.toFixed(1)}m`,
            font: "14px sans-serif",
            fillColor: Cesium.Color.WHITE,
            outlineColor: Cesium.Color.BLACK,
            outlineWidth: 2,
            style: Cesium.LabelStyle.FILL_AND_OUTLINE,
            disableDepthTestDistance: Number.POSITIVE_INFINITY
          })
        }
      })
    })

    this.labelCollection.show = this.showLabels
  }

  addHighlightContour(height: number, color: string = "rgba(255, 255, 0, 1)", width: number = 4) {
    if (!this.terrainPoints) {
      return
    }

    try {
      const line = turf.isolines(this.terrainPoints, [height], {
        zProperty: "elevation"
      })

      if (!line || !line.features || line.features.length === 0) {
        return
      }

      if (this.smoothness > 0) {
        const resolution = 500
        const sharpness = this.smoothness
        line.features.forEach((feature: any) => {
          if (feature.geometry.type === "LineString" && feature.geometry.coordinates.length > 2) {
            try {
              const smoothed = turf.bezierSpline(feature, { resolution, sharpness })
              feature.geometry = smoothed.geometry
            } catch (err) {
              console.warn("Bezier scaling failed for highlight feature", err)
            }
          } else if (feature.geometry.type === "MultiLineString") {
            const newCoords = feature.geometry.coordinates.map((coords: any) => {
              if (coords.length > 2) {
                try {
                  const ls = turf.lineString(coords)
                  const smoothed = turf.bezierSpline(ls, { resolution, sharpness })
                  return smoothed.geometry.coordinates
                } catch (err) {
                  return coords
                }
              }
              return coords
            })
            feature.geometry.coordinates = newCoords
          }
        })
      }

      const highlightColor = Cesium.Color.fromCssColorString(color)

      line.features.forEach((feature: any) => {
        const type = feature.geometry.type
        const coordinates = feature.geometry.coordinates

        let lineSegments: any[] = []
        if (type === "LineString") {
          lineSegments = [coordinates]
        } else if (type === "MultiLineString") {
          lineSegments = coordinates
        }

        lineSegments.forEach((lineCoords: any) => {
          if (!lineCoords || lineCoords.length < 2) {
            return
          }

          const positions = lineCoords.map((coord: any) => Cesium.Cartesian3.fromDegrees(coord[0], coord[1], height))

          this.dataSource.entities.add({
            name: "HighlightContour",
            polyline: {
              positions,
              width,
              material: highlightColor,
              clampToGround: true,
              zIndex: 100
            }
          })
        })
      })
    } catch (e) {
      console.error("生成高亮等高线失败", e)
    }
  }

  removeEntitiesByName(name: string) {
    const entities = this.dataSource.entities.values
    for (let i = entities.length - 1; i >= 0; i--) {
      const entity = entities[i]
      if (entity.name === name) {
        this.dataSource.entities.remove(entity)
      }
    }
  }
}
