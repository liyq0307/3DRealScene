<template>
  <div class="toolbox-container-form toolbox">
    <a-space class="tools_btns">
      <mars-button class="btn" type="primary" @click="drawLine">线通视</mars-button>
      <mars-button class="btn" type="primary" @click="drawCircle">圆形通视</mars-button>
      <mars-button class="btn" type="primary" v-if="!formState.isDrawing" @click="drawViewshed">可视域</mars-button>
      <mars-button class="btn" type="primary" v-if="formState.isDrawing" @click="onClickClearDrawing">取消绘制</mars-button>
    </a-space>
    <!-- 可视域属性框 -->
    <tsfxSxDialog v-model="video" />
  </div>
</template>
<script lang="ts" setup>
import { reactive, onUnmounted, computed, onMounted } from "vue"
import { useAnalysisStore } from "@mars/stores/analysis"
import * as mapWork from "./map"
import * as sxMapWork from "@mars/widgets/analysis/dialogs/visibility-config-dialog/map"
import { eventTarget as fxEvent, map } from "@mars/widgets/analysis/map"
import { useWidget } from "@mars/common/store/widget"

import tsfxSxDialog from "@mars/widgets/analysis/dialogs/visibility-config-dialog/index.vue"

const store = useAnalysisStore()
const { disable } = useWidget()
const formState = reactive({
  isDrawing: false
})

// 使用 store 中的可视域分析数据，通过 computed 实现双向绑定
const video = computed({
  get: () => store.getTsfxVideoData,
  set: (val) => {
    store.updateTsfxVideoData(val)
    // 当属性改变时，同步更新分析历史记录中的数据内容，确保只有最后的一条历史
    if (val.selectedGraphic) {
      const graphic = sxMapWork.getGraphic()
      if (graphic) {
        store.clearAnalysisDataByType("ts_analysis")
        store.saveAnalysisDataToSelected({
          analysisType: "ts_analysis",
          data: {
            data: graphic.toJSON(),
            name: "通视分析",
            shijiao: map.getCameraView(),
            analysisType: "ts_analysis"
          }
        })
      }
    }
  }
})
// 线通视分析
const drawLine = () => {
  mapWork.drawLine()
}
// 圆形通视分析
const drawCircle = () => {
  mapWork.drawCircle()
}

// 监听绘制线通视完成事件，接收数据并保存到 store
function onDrawLineEnd(event: any) {
  const data = JSON.parse(event.extent)
  // console.log("drawLineEnd 事件数据:", data)
  disable("graphic-options")
  store.updateTsfxVideoData({ selectedGraphic: false }) // 取消选中状态
  // 优化：绘制新数据前，清空 store 中所有已存在的分析数据（线、圆、可视域），保证唯一存在
  store.clearAnalysisDataByType("ts_analysis")

  store.saveAnalysisDataToSelected({
    analysisType: data.type,
    data: { data: data.graphic, name: "通视分析", shijiao: data.shijiao, sightLineId: data.sightlineId, analysisType: data.type }
  })
}
onMounted(() => {
  fxEvent.on("drawLineEnd", onDrawLineEnd)
})

// 组件卸载时移除监听器，防止反复挂导致累积
onUnmounted(() => {
  // 清除鼠标绑定事件
  mapWork.clearDrawing()
  formState.isDrawing = false
  fxEvent.off("drawLineEnd", onDrawLineEnd)
  store.updateTsfxVideoData({
    selectedGraphic: false // 是否选中图形（控制对话框显示）
  })
  disable("graphic-options")
})

// 可视域分析
const drawViewshed = async () => {
  // 关闭图形属性面板
  disable("graphic-options")

  formState.isDrawing = true
  store.updateTsfxVideoData({ selectedGraphic: false }) // 取消选中状态
  // 绘制新数据前，清空 store 中所有已存在的分析数据（线、圆、可视域），保证唯一存在
  store.clearAnalysisDataByType("ts_analysis")
  try {
    const graphic = await mapWork.drawViewshed()

    // 设置当前选中的可视域图形
    sxMapWork.setGraphic(graphic)

    // 更新属性编辑框的初始值到 store
    store.updateTsfxVideoData({
      graphicName: "属性",
      cameraAngle: graphic.angle || 60,
      cameraAngle2: graphic.angle2 || 45,
      distanceValue: graphic.distance || 80,
      heading: graphic.heading || 44,
      pitchValue: graphic.pitch || -12,
      opacity: graphic.opacity || 1,
      showFrustum: graphic.showFrustum || false,
      selectedGraphic: true
    })
  } catch (error) {
    console.error("可视域分析失败:", error)
  } finally {
    formState.isDrawing = false
  }
}
// 清除绘制
const onClickClearDrawing = () => {
  mapWork.clearDrawing()
  formState.isDrawing = false
}
</script>
<style scoped></style>
