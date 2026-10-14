<template>
  <div class="toolbox-container-form toolbox">
    <a-form ref="formRef" layout="horizontal" :label-col="{ span: 8 }" :wrapper-col="{ span: 16 }">
      <a-form-item label="基准面高(m)" style="margin: 0.5rem 0">
        <div style="display: flex; align-items: center; gap: 8px">
          <mars-input-number @blur="baseHeight" id="inputNumber" v-model:value="baseValue" step="0.1" />
          <mars-button class="tools_btns" @click="selHeight">点选高度</mars-button>
        </div>
      </a-form-item>
      <a-form-item label="底部高度(m)" style="margin: 0.5rem 0">
        <mars-input-number @blur="txtMinHeight" id="inputNumber" v-model:value="bottomValue" step="0.1" />
      </a-form-item>
      <a-form-item label="最高高度(m)" style="margin: 0.5rem 0">
        <mars-input-number @blur="txtMaxHeight" id="inputNumber" v-model:value="topValue" step="0.1" />
      </a-form-item>
      <!-- <a-form-item label="显示选项" style="margin: 0.5rem 0">
        <a-checkbox @change="showResult" v-model:checked="checked">显示面内三角网</a-checkbox>
      </a-form-item> -->
      <a-form-item :wrapper-col="{ offset: 0 }">
        <a-space>
        <mars-button class="tools_btns" @click="analysisMeasure">绘制分析区域</mars-button>
        <!-- <mars-button class="tools_btns" @click="saveHistory">保存</mars-button> -->
        </a-space>

      </a-form-item>
    </a-form>
  </div>
</template>
<script lang="ts" setup>
import { onUnmounted, ref, reactive } from "vue"
import * as mapWork from "./map.js"
import { eventTarget as fxEvent } from "@mars/widgets/analysis/map"
import { useAnalysisStore } from "@mars/stores/analysis.js"

const store = useAnalysisStore()

const baseValue = ref<number>()

const bottomValue = ref<number>()

const topValue = ref<number>()

// const checked = ref(true)

const state = reactive({
  graphic: null,
  shijiao: null,
  analysisData: null
})

// 同步更新store中的分析数据
const updateStoreData = () => {
  const item = store.analysisSyncArray.find(
    (i: any) => i.analysisType === "wftffx_analysis"
  )
  if (item) {
    item.data.baseHeight = baseValue.value
    item.data.minHeight = bottomValue.value
    item.data.maxHeight = topValue.value
    // item.data.checked = checked.value
  }
}

// 方量分析
const analysisMeasure = () => {

  mapWork.analysisMeasure()
}

// 复选框显示结果
// const showResult = () => {
//   const isShow = mapWork.showResult(checked.value)
//   checked.value = isShow
//   updateStoreData()
// }

// 基础高度修改
const baseHeight = () => {
  mapWork.baseHeight(baseValue.value)
  updateStoreData()
}

// 修改底高
const txtMinHeight = () => {
  mapWork.txtMinHeight(bottomValue.value)
  updateStoreData()
}

// 修改顶高
const txtMaxHeight = () => {
  mapWork.txtMaxHeight(topValue.value)
  updateStoreData()
}

// 点选高度
const selHeight = () => {
  mapWork.selHeight()
  updateStoreData()
}

// 重置围墙底高为原值
const resetMinHeightHandler = (event: any) => {
  bottomValue.value = Math.ceil(event.value * 10) / 10
}

// 重置围墙顶高为原值
const resetMaxHeightHandler = (event: any) => {
  topValue.value = Math.ceil(event.value * 10) / 10
}

const drawEndHandler = async(event: any) => {
  // showResult()
  baseValue.value = event.baseHeight
  bottomValue.value = event.minHeight
  topValue.value = event.maxHeight
  state.analysisData = event.analysisData
  state.analysisData = event.analysisData
  state.shijiao = event.shijiao

  // 向store中存储数据
  await store.clearAnalysisDataByType("wftffx_analysis")
  await store.saveAnalysisDataToSelected({
    analysisType: "wftffx_analysis",
    data: {
      analysisType: "wftffx_analysis",
      data: {
        ...event.graphic
      },
      baseHeight: baseValue.value,
      minHeight: bottomValue.value,
      maxHeight: topValue.value,
      // checked: checked.value,
      shijiao: state.shijiao,
      analysisData: state.analysisData,
      depth: event.depth === true
    }
  })

  state.graphic = null
  state.shijiao = null
  state.analysisData = null


}
fxEvent.on("heightVal", drawEndHandler)
fxEvent.on("resetMinHeight", resetMinHeightHandler)
fxEvent.on("resetMaxHeight", resetMaxHeightHandler)


onUnmounted(() => {
  // 清除鼠标绑定事件
  mapWork.cancelSelHeightDraw()
  fxEvent.off("heightVal", drawEndHandler)
  fxEvent.off("resetMinHeight", resetMinHeightHandler)
  fxEvent.off("resetMaxHeight", resetMaxHeightHandler)
  if (state.analysisData) {
    mapWork.clearAll()
  }
})
</script>
<style scoped lang="less">
:deep(.ant-checkbox-wrapper span){
  font-size: 12px !important;
}
</style>
