<template>
  <div class="toolbox-container-form toolbox">
    <a-form>
      <!-- <a-form-item label="基准地形高(m)" name="baseTerrainHeight" style="margin: 0.5rem 0">
        <mars-input-number v-model:value="state.formData.baseTerrainHeight" :disabled="true"></mars-input-number>
      </a-form-item> -->
      <a-form-item label="当前地形高(m)" name="currentTerrainHeight" style="margin: 0.5rem 0">
        <mars-input-number v-model:value="state.formData.currentTerrainHeight" :disabled="true"></mars-input-number>
      </a-form-item>
      <a-form-item label="建筑压平高(m)" name="flatHeight" style="margin: 0.5rem 0">
        <mars-input-number v-model:value="state.formData.flatHeight" @blur="handleChangeFlatHeight"></mars-input-number>
      </a-form-item>
        <div class="button-group">
        <mars-button class="btn_mx tools_btns" @click="btnDrawDbx"> 建筑压平 </mars-button>
        <!-- <mars-button class="btn_mx " danger @click="clearBuildYp">
      <template #icon>
        <img :src="qcypIcon" alt="清除压平" class="icon_style" />
      </template>
      清除压平
      </mars-button> -->
      </div>
    </a-form>
  </div>
</template>
<script setup lang="ts">
import * as mapWork from "./map"
// import qcypIcon from "@mars/assets/icons/clearyp.svg"
import { reactive, onUnmounted } from "vue"

// 基准地形高度：压平效果正常时的高地势高度
const BASE_TERRAIN_HEIGHT = import.meta.env.VITE_BASE_TERRAIN_HEIGHT

const state = reactive({
  formData: {
    flatHeight: 0,
    baseTerrainHeight: BASE_TERRAIN_HEIGHT,
    currentTerrainHeight: 0
  }
})

const btnDrawDbx = async () => {
  // 先拾取当前地形高度，再根据与基准高度的差值修正压平高度
  const terrainHeight = await mapWork.pickTerrainHeight()
  if (terrainHeight === null || terrainHeight === undefined) {
    return
  }
  state.formData.currentTerrainHeight = parseFloat(terrainHeight.toFixed(2))
  // 计算地形偏移量：当前地形高 - 基准地形高
  const offset = terrainHeight - BASE_TERRAIN_HEIGHT
  // 实际压平高度 = 用户设置的压平高 + 地形偏移量
  const actualFlatHeight = state.formData.flatHeight + offset
  mapWork.startBuildFlat(actualFlatHeight)
}

// const clearBuildYp = () => {
//   mapWork.clearBuildFlat()
// }

const handleChangeFlatHeight = async () => {
  // 如果已拾取过地形高度，更新时也需考虑偏移量
  const offset = state.formData.currentTerrainHeight - BASE_TERRAIN_HEIGHT
  const actualFlatHeight = state.formData.flatHeight + offset
  await mapWork.updateFlatHeight(actualFlatHeight)
}

// 组件销毁时清除鼠标绑定事件
onUnmounted(() => {
  mapWork.cancelBuildFlat()
})
</script>
<style lang="less" scoped>
</style>
