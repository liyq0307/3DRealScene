/**
 * 图上选点
 * @copyright 火星科技 mars3d.cn
 */
import * as mars3d from "mars3d"
let map: mars3d.Map | null

export const eventTarget = new mars3d.BaseClass()

export function onMounted(mapInstance: mars3d.Map): void {
  map = mapInstance
}

export function onUnmounted(): void {
  if (pointEntity) {
    pointEntity.remove()
    pointEntity = null
  }
  eventTarget.off()
  map = null
}

export function defultPoint() {
  if (!map) { return { lng: 0, lat: 0, alt: 0 } }
  const point: mars3d.LngLatPoint = map.getCenter()
  point.format()
  return { lng: point.lng, lat: point.lat, alt: point.alt }
}

export function marsUtilFormtNum(item: number, num: number) {
  return mars3d.Util.formatNum(item, num)
}

export function marsPointTrans(item: number) {
  return mars3d.PointTrans.degree2dms(item)
}

export function marsProj4Trans(JD: number, WD: number, radio: string) {
  if (radio === "2") {
    return mars3d.PointTrans.proj4Trans([JD, WD], mars3d.CRS.EPSG4326, mars3d.CRS.CGCS2000_GK_Zone_6)
  } else {
    return mars3d.PointTrans.proj4Trans([JD, WD], mars3d.CRS.EPSG4326, mars3d.CRS.CGCS2000_GK_Zone_3)
  }
}

export function marsDms2degree(du: number, fen: number, miao: number) {
  return mars3d.PointTrans.dms2degree(du, fen, miao)
}

export function marsZONEtoCRS(jd: number, wd: number, radio: string) {
  if (radio === "2") {
    return mars3d.PointTrans.proj4Trans([jd, wd], mars3d.CRS.CGCS2000_GK_Zone_6, mars3d.CRS.EPSG4326)
  } else {
    return mars3d.PointTrans.proj4Trans([jd, wd], mars3d.CRS.CGCS2000_GK_Zone_3, mars3d.CRS.EPSG4326)
  }
}

export function bindMourseClick() {
  if (!map) { return }
  map.setCursor("crosshair")
  map.once(mars3d.EventType.click, function (event: any) {
    if (!map) { return }
    map.setCursor("")
    const cartesian = event.cartesian
    const point = mars3d.LngLatPoint.fromCartesian(cartesian)
    point.format()
    eventTarget.fire("clickMap", { point })
    updateMarker(false, point.lng, point.lat, point.alt)
  })
}

let pointEntity: any
export function updateMarker(hasCenter: boolean, jd: number, wd: number, alt: number) {
  if (!map) { return }
  const position = [jd, wd, alt]

  if (pointEntity == null) {
    pointEntity = new mars3d.graphic.PointEntity({
      position,
      style: {
        color: "rgba(51, 136, 255, 1)",
        pixelSize: 10,
        outlineColor: "rgba(255, 255, 255, 1)",
        outlineWidth: 2
      }
    })
    map.graphicLayer.addGraphic(pointEntity)
  } else {
    pointEntity.position = position
  }

  if (hasCenter) {
    pointEntity.flyTo({ radius: 1000 })
  }
}
