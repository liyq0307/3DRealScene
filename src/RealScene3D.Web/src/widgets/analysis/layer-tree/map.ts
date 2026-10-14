import * as mars3d from "mars3d"

export let map
export let tilesetLayer: mars3d.layer.TilesetLayer | undefined

export function onMounted(mapInstance) {
  map = mapInstance
}

export function onUnmounted() {
  map = null
  tilesetLayer = undefined
}

export function removeLayer(layer: any) {
  if (map && layer) {
    map.removeLayer(layer)
  }
}

export function addLayer(layer: mars3d.layer.BaseLayer) {
  if (map && layer) {
    map.addLayer(layer)
  }
}

export function setTilesetLayer(layer: mars3d.layer.TilesetLayer | undefined) {
  tilesetLayer = layer
  if (tilesetLayer?.flat) {
    tilesetLayer.flat.raise = false
  }
}
