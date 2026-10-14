<template>
  <div class="toolbox-container-form toolbox">
    <mars-button class="btn tools_btns" @click="measureSection">绘制线</mars-button>
  </div>
</template>

<script lang="ts" setup>
import { nextTick, ref, onUnmounted } from "vue"
import * as mapWork from "./map"
import * as fxMapWork from "@mars/widgets/analysis/map"
import { useAnalysisStore } from "@mars/stores/analysis"

const store = useAnalysisStore()
const hzcs = ref(null)
const fxjg = ref(null)
// 添加节流变量,优化tooltip性能
const tooltipUpdateTimer: any = null
const measureSection = () => {
  mapWork.measureSection()
}

const drawEndHandler = (event) => {
  if (typeof event.value === "string") {
    const data = JSON.parse(event.value)
    if (data.analysisType && data.analysisType !== "pmfx_analysis") {
      return
    }
    hzcs.value = data
  }
}

const measureEndHandler = (event: any) => {
  fxjg.value = event

  nextTick(() => {
    // 将数据放置在store中
    store.saveAnalysisDataToSelected({
      analysisType: "pmfx_analysis",
      data: {
        data: hzcs.value?.graphic,
        analysisData: JSON.parse(JSON.stringify(fxjg.value)),
        analysisType: "pmfx_analysis",
        shijiao: hzcs.value?.shijiao
      }
    })
  })
}

fxMapWork.eventTarget.on("drawEnd", drawEndHandler)
fxMapWork.eventTarget.on("measureEnd", measureEndHandler)

// 组件卸载时移除事件监听
onUnmounted(() => {
  // 清除鼠标绑定事件
  mapWork.cancelMeasureSection()
  fxMapWork.eventTarget.off("drawEnd", drawEndHandler)
  fxMapWork.eventTarget.off("measureEnd", measureEndHandler)
})
</script>
