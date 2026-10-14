import { defineStore } from 'pinia'
import { ref } from 'vue'

/**
 * 图层选中状态 store
 *
 * P4 阶段从 cityplan/src/stores/layer.ts 对齐迁移。
 * 用法: const { selectedLayers, addSelectedLayer, removeSelectedLayer } = useLayerStore()
 */
export const useLayerStore = defineStore('layer', () => {
  const selectedLayers = ref<any[]>([])

  function setSelectedLayers(layers: any[]) {
    selectedLayers.value = layers
  }

  function addSelectedLayer(layer: any) {
    const idx = selectedLayers.value.findIndex((l: any) => String(l.id) === String(layer.id))
    if (idx === -1) {
      selectedLayers.value.push(layer)
    }
  }

  function removeSelectedLayer(layerId: string | number) {
    const idx = selectedLayers.value.findIndex((l: any) => String(l.id) === String(layerId))
    if (idx !== -1) {
      selectedLayers.value.splice(idx, 1)
    }
  }

  return {
    selectedLayers,
    setSelectedLayers,
    addSelectedLayer,
    removeSelectedLayer
  }
})
