<template>
  <!-- <poiSearch></poiSearch> -->
  <div class="toolbox-container-form toolbox">
    <a-form ref="formRef" :label-col="{ span: 6 }" :wrapper-col="{ span: 18 }">
      <a-form-item label="左侧图层" name="leftName" style="margin: 0.5rem 0">
        <a-tree-select
          :fieldNames="{ children: 'children', label: 'layername', value: 'layerurl' }"
          treeDefaultExpandAll
          v-model:value="currentLeftUrl"
          :tree-data="treeData"
          @change="handleLeftChange"
          allow-clear
          placeholder="请选择图层"
        ></a-tree-select>
      </a-form-item>
      <a-form-item label="右侧图层" name="rightName" style="margin: 0.5rem 0">
        <a-tree-select
          :fieldNames="{ children: 'children', label: 'layername', value: 'layerurl' }"
          treeDefaultExpandAll
          v-model:value="currentRightUrl"
          :tree-data="treeData"
          @change="handleRightChange"
          allow-clear
          placeholder="请选择图层"
        ></a-tree-select>
      </a-form-item>
    </a-form>
  </div>
</template>

<script setup lang="ts">
import useLifecycle from "@mars/common/uses/use-lifecycle"
import * as mapWork from "./map"
import { layertreeList } from "@mars/api/admin"

import { onMounted, ref } from "vue"

const treeData = ref<any[]>([])

useLifecycle(mapWork)

// 存储当前左右两侧的URL和节点信息
const currentLeftUrl = ref()
const currentRightUrl = ref()
const currentLeftNode = ref<any>(null)
const currentRightNode = ref<any>(null)

/** 通用处理：任一侧选择变化时，判断是否需要更新或销毁对比控件 */
const handleChange = (side: "left" | "right", url: string | undefined, node?: any) => {
  // 更新对应侧的数据
  if (side === "left") {
    currentLeftUrl.value = url
    if (node) { currentLeftNode.value = node }
  } else {
    currentRightUrl.value = url
    if (node) { currentRightNode.value = node }
  }

  // 任一侧为空 → 销毁对比控件
  if (!currentLeftUrl.value || !currentRightUrl.value) {
    mapWork.destroyControl()
    return
  }

  // 两侧都有值 → 重新创建/更新对比控件（传入当前选中的节点用于获取坐标）
  mapWork.commonControl(currentLeftUrl.value, currentRightUrl.value, currentLeftNode.value || currentRightNode.value)
}

/** @change 触发：选中选项(url, node) 或 清空(undefined) */
const handleLeftChange = (value: any, node: any) => {
  handleChange("left", value, node)
}
const handleRightChange = (value: any, node: any) => {
  handleChange("right", value, node)
}
onMounted(async () => {
  getLayerTree()
})
// 递归筛选datatype为tileset的数据
const filterTilesetData = (nodes: any[]): any[] => {
  return nodes.reduce((acc: any[], node: any) => {
    // 如果当前节点的datatype为tileset,加入结果
    if (node.datatype === "tileset") {
      // 保留节点,并递归处理子节点
      const filteredNode = {
        ...node,
        children: node.children ? filterTilesetData(node.children) : undefined
      }
      // 确保有必要的字段,如果没有则跳过
      if (filteredNode.id && filteredNode.layername && filteredNode.layerurl) {
        acc.push(filteredNode)
      }
    } else if (node.children && node.children.length > 0) {
      // 如果当前节点不符合条件但有子节点,递归处理子节点
      const filteredChildren = filterTilesetData(node.children)
      // 如果子节点中有符合条件的,保留当前节点(但只包含符合条件的子节点)
      if (filteredChildren.length > 0) {
        acc.push({
          ...node,
          layerurl: node.id || node.layername,
          disabled: true,
          children: filteredChildren
        })
      }
    }
    return acc
  }, [])
}

// 右侧树
const getLayerTree = async () => {
  const { data } = await layertreeList()
  // treeData.value = data.result
  treeData.value = filterTilesetData(data.result)

}
</script>

<style lang="less">
/* MapSplit 控件的拖拽图标居中（全局样式，因为控件渲染在地图容器上而非组件内）*/
.mars3d-slider .slider-splitter {
  display: flex;
  align-items: center;
  justify-content: center;

  svg,
  img {
    position: static !important;
  }
}
</style>
