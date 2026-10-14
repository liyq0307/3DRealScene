import * as mars3d from "mars3d"

export interface RoamOptions {
  map: mars3d.Map
  speed?: number
  onStop?: () => void
}

export class RoamManager {
  private map: mars3d.Map
  private speed: number
  private onStop?: () => void
  private fixedRoute: mars3d.graphic.FixedRoute | null = null
  private graphicLayer: mars3d.layer.GraphicLayer

  constructor(options: RoamOptions) {
    this.map = options.map
    this.speed = options.speed || 100
    this.onStop = options.onStop
    this.graphicLayer = new mars3d.layer.GraphicLayer()
    this.map.addLayer(this.graphicLayer)
  }

  setSpeed(speed: number) {
    this.speed = speed
    if (this.fixedRoute) {
      ; (this.fixedRoute as any).speed = this.speed
    }
  }

  startRoam(positions: mars3d.Cesium.Cartesian3[]) {
    this.stopRoam()

    if (!positions || positions.length < 2) {
      console.warn("RoamManager: Not enough positions to start roam")
      return
    }

    this.fixedRoute = new mars3d.graphic.FixedRoute({
      positions,
      speed: this.speed,
      camera: {
        type: "dy",
        followedX: 10,
        followedZ: 1000,
        offsetZ: -10
      }
    } as any)

    console.log("RoamManager: FixedRoute created, adding to layer")
    this.graphicLayer.addGraphic(this.fixedRoute)

    const handleEnd = () => {
      if (this.onStop) {
        this.onStop()
      }
    }
    this.fixedRoute.on(mars3d.EventType.end, handleEnd)

    const viewer = this.map.viewer
    viewer.clock.shouldAnimate = true
    viewer.clock.multiplier = 1.0
    viewer.clock.clockStep = mars3d.Cesium.ClockStep.SYSTEM_CLOCK_MULTIPLIER

    this.fixedRoute.start()
  }

  stopRoam() {
    if (this.fixedRoute) {
      this.fixedRoute.stop()
      this.graphicLayer.removeGraphic(this.fixedRoute)
      this.fixedRoute = null
    }
  }

  pauseRoam() {
    this.fixedRoute?.pause()
  }

  resumeRoam() {
    this.fixedRoute?.proceed()
  }
}
