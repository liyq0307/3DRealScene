<template>
  <div class="toolbox-container-form toolbox">
    <div class="f-mb show_skyline">
      <span>开启天际线</span>
      <mars-switch @change="isVChecked" v-model:checked="checked" />
    </div>

    <div class="show_skyline">
      <span class="f-mr">线宽(像素):</span>
      <mars-input-number @change="changeLineWidth" v-model:value="skyLineWidth" :min="1" :max="10" />
      <mars-button class="tools_btns" @click="changeColor">换色</mars-button>
    </div>
  </div>
</template>

<script lang="ts" setup>
import { onMounted, onUnmounted, ref } from "vue"
import * as mapWork from "./map"
import { skyline, map } from "@mars/widgets/analysis/map"
import { useAnalysisStore } from "@mars/stores/analysis"
import { color } from "echarts"

const skyLineWidth = ref(5)
const skyLineColor = ref(skyline.color)
const checked = ref(true)

const store = useAnalysisStore()
const changeLineWidth = (value: number) => {
  skyLineWidth.value = mapWork.lineWidth(value)
}

onMounted(() => {
  mapWork.isVChecked(checked.value)
})
const changeColor = () => {
  skyLineColor.value = mapWork.changeColor()
}

const isVChecked = () => {
  mapWork.isVChecked(checked.value)
}

onUnmounted(() => {
  // 将当前时间保存在store中（只保存一次）
  // 先检查store中是否已有日照分析数据
  const hasExistingData = store.analysisSyncArray.some((item) => item.analysisType === "tjx_analysis")

  if (!hasExistingData) {
    store.saveAnalysisDataToSelected({
      analysisType: "tjx_analysis",
      data: {
        enabled: checked.value,
        analysisType: "tjx_analysis",
        data: {},
        lineWidth: skyLineWidth.value,
        color: skyLineColor.value,
        shijiao: map.getCameraView()
      }
    })
  }
  // shadows.enabled = false
})
</script>
<style scoped>
.toolbox {
}
.btn_container {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.show_skyline {
  display: flex;
  gap: 10px;
  align-items: center;
  justify-content: space-between;
  span {
    font-size: 12px;
  }
}
.f-mr {
  width: 140px;
}
</style>
