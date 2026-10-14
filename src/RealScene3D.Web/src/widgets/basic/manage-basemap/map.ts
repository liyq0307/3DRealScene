/**
 * 底图控制
 * @copyright 火星科技 mars3d.cn
 * @author 火星渣渣灰 2022-01-10
 */
import * as mars3d from "mars3d"

let map: mars3d.Map | null

export function onMounted(mapInstance: mars3d.Map): void {
  map = mapInstance
}

export function onUnmounted(): void {
  map = null
}

export function changeBaseMaps(id: string) {
  if (map) {
    map.basemap = id
  }
}

export function changeTerrain(value: boolean) {
  if (map) {
    map.hasTerrain = value
  }
}

export function getLayers() {
  if (!map) {
    return { baseMaps: [], hasTerrain: false }
  }
  return {
    baseMaps: map.getBasemaps(true),
    hasTerrain: map.hasTerrain
  }
}
