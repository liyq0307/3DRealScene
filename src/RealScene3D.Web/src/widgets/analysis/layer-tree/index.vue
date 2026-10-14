<template>
  <mars-dialog
    :visible="visible"
    customClass="layer-tree-dialog"
    ref="dialogRef"
    width="274"
    :max-width="274"
    :top="60"
    :left="0"
    :bottom="30"
    :nopadding="true"
    :animation="false"
    :closeButton="false"
    :defaultFold="false"
    :thumbnail="{ icon: 'layers' }"
  >
    <template #title>
      <div class="title">
        图层
        <mars-icon class="closeBth" icon="layers" :width="24" @click="toggleBtn" />
      </div>
    </template>
    <div class="titles">图层数据</div>
    <div class="tree-container">
      <mars-tree
        class="layer-tree"
        ref="layerTreeRef"
        v-if="treeData.length > 0"
        checkable
        :showIcon="false"
        :tree-data="treeData"
        v-model:checkedKeys="checkedKeys"
        v-model:expandedKeys="expandedKeys"
        :fieldNames="{ children: 'children', title: 'layername', key: 'id' }"
        @check="onCheckTreeItem"
        @select="flyToGraphic"
      >
      </mars-tree>
    </div>
  </mars-dialog>
</template>

<script setup lang="ts">
import { onMounted, ref } from "vue"
import useLifecycle from "@mars/common/uses/use-lifecycle"
import * as mapWork from "./map"
import { useMenuStore } from "@mars/stores/menu"
import { useLayerStore } from "@mars/stores/layer"

useLifecycle(mapWork)

const store = useMenuStore()
const layerStore = useLayerStore()
const visible = ref(false)
const layerTreeRef = ref()
const dialogRef = ref()
const treeData = ref<any[]>([])
const expandedKeys = ref<string[]>([])
const checkedKeys = ref<string[]>([])
const layersObj = ref<any>({})

const props = defineProps({
  title: { type: String, default: "" },
  handleLeftClick: { type: Function, default: null }
})

onMounted(() => {
  getLayerTree()
})

const openDialog = async () => {
  visible.value = true
}

const toggleBtn = () => {
  dialogRef.value?.toggle()
}

const getLayerTree = async () => {
  // TODO: implement layer tree fetch
  treeData.value = []
}

const onCheckTreeItem = (keys: string[], e: any) => {
  console.log("onCheckTreeItem", keys, e)
  store.setCheckedKeys(keys)
  store.setActiveCheckedNodes(e.checkedNodes)
}

const flyToGraphic = (keys: any, item: any) => {
  console.log("flyToGraphic", item)
}

defineExpose({
  openDialog,
  clearAllLayers: () => {
    Object.keys(layersObj.value).forEach((key) => {
      if (layersObj.value[key]) {
        mapWork.removeLayer(layersObj.value[key])
        delete layersObj.value[key]
      }
    })
    checkedKeys.value = []
  }
})
</script>

<style lang="less" scoped>
.title {
  font-size: 16px;
  position: relative;
  .closeBth {
    position: absolute;
    right: 4px;
    top: 8px;
    cursor: pointer;
  }
}
.titles {
  background: var(--mars-dropdown-bg, rgba(1, 11, 50, 1));
  padding: 4px;
  flex-shrink: 0;
}
.tree-container {
  flex: 1;
  overflow: auto;
  padding: 5px;
  min-height: 0;
}
</style>
