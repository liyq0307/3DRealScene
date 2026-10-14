<template>
  <div class="rjl toolbox-container-form toolbox">
    <a-form ref="formRef" :model="state.formData" :label-col="{ span: 12 }" :wrapper-col="{ span: 12 }" :rules="rules">
      <a-form-item label="建筑面积(m²)" name="area" style="margin: 0.5rem 0">
        <mars-input-number v-model:value="state.formData.area"  suffix="m²" min="0" :disabled="state.isDrawing"></mars-input-number>
      </a-form-item>
      <a-form-item label="层数" name="cs" style="margin: 0.5rem 0">
        <mars-input-number v-model:value="state.formData.cs"  suffix="层" min="0" :disabled="state.isDrawing"></mars-input-number>
      </a-form-item>
    </a-form>
    <div class="tools_btns">
      <mars-button class="btn" @click="draw">绘制</mars-button>
    </div>
  </div>
</template>
<script lang="ts" setup>
import { reactive, ref, onBeforeUnmount, onMounted } from "vue"
import * as mars3d from "mars3d"

import * as mapWork from "./map"
import { eventTarget as fxEvent } from "@mars/widgets/analysis/map"
import { useAnalysisStore } from "@mars/stores/analysis"

const analysisStore = useAnalysisStore()
const formRef = ref()
const state = reactive({
  formData: {
    area: undefined,
    cs: undefined
  },
  rjl: "",
  hzjg: null,
  hzArea: null,
  rectGraphic: null,
  isDrawing: false
})

const rules = {
  area: [{ required: true, message: "请填写建筑面积", trigger: "blur" }],
  cs: [{ required: true, message: "请填写层数", trigger: "blur" }]
}

// 监听绘制结束事件，获取矩形的几何信息
const handleDrawEnd = (data: any) => {
  state.isDrawing = false
  if (data.extent?.type !== "rjl") {
    return
  }
  // 绘制矩形结束
  state.hzjg = data.extent
  state.hzArea = data.extent?.graphic?.measured.area
  state.rectGraphic = data.extent?.graphicInstance
  getRjl()
}
onMounted(() => {

  fxEvent.on("drawEnd", handleDrawEnd)
})

onBeforeUnmount(() => {
  // 清除鼠标绑定事件
  mapWork.cancelDraw()
  fxEvent.off("drawEnd")
  // 移除矩形图形及其点击事件

  // 清空状态数据
  state.hzjg = null
  state.hzArea = null
  state.rjl = ""
  state.formData.area = undefined
  state.formData.cs = undefined
  state.isDrawing = false
})
// 计算容积率
async function getRjl() {
  if (!state.hzjg || !state.hzArea) {
    return
  }
  state.rjl = String(((Number(state.formData.area) * Number(state.formData.cs)) / Number(state.hzArea)).toFixed(2))
  console.log(state)
  const pointGraphic = await mapWork.addPoint(state.hzjg?.graphic.positions[0], state.rjl)
  console.log(pointGraphic)
  // 在矩形上绑定点击事件，触发关联点的 Popup
  if (state.rectGraphic && pointGraphic) {
    state.rectGraphic.on(mars3d.EventType.click, () => {
      pointGraphic.openPopup()
    })
  }
  // 将数据保存在store中
  analysisStore.saveAnalysisDataToSelected({
    analysisType: "rjl_analysis",
    data: {
      data: JSON.parse(JSON.stringify(state.hzjg.graphic)),
      pointData: pointGraphic.toJSON(),
      analysisType: "rjl_analysis",
      shijiao: state.hzjg.shijiao,
      rjl: state.rjl
    }
  })
}

const draw = () => {
  formRef.value
    .validate()
    .then(() => {
      state.isDrawing = true
      mapWork.startDraw()
    })
    .catch(() => {
      console.log("验证失败")
    })
}
</script>
<style>
.rjl .mars3d-popup {
  z-index: 998 !important;
}
</style>
