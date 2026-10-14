<template>
  <div class="btn_container toolbox-container-form toolbox">
    <a-space class="tools_btns">
      <mars-button class="btn" type="primary" @click="drawRectangle">框选范围</mars-button>
      <mars-button class="btn" type="default" @click="drawPolygon">多边形范围</mars-button>
      <mars-button class="btn" type="default" @click="drawCircle">圆形范围</mars-button>
    </a-space>
  </div>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, reactive, watch } from "vue"
import * as mapWork from "./map"
import { eventTarget as fxEvent } from "@mars/widgets/analysis/map"
import { circleToWktByMeter, rectTwoPointsToWktSafe } from "@mars/utils/transWkt"
import { storeToRefs } from "pinia"
import { useLayerStore } from "@mars/stores/layer"
import WKT from "terraformer-wkt-parser"
import { getAroundXZfx } from "@mars/api/csghgl"
import { message } from "ant-design-vue"
import { useAnalysisStore } from "@mars/stores/analysis"
import { $message } from "@mars/components/mars-ui"
const layerStore = useLayerStore()
const store = useAnalysisStore() // 分析数据存储对象
const { selectedLayers } = storeToRefs(layerStore)
const state = reactive({
  hzcs: null,
  areaData: [],
  wktData: "",
  isLoading: false,
  analysisData: null,
  showAnalysisModal: false
})
const drawRectangle = () => {
  mapWork.drawRectangle()
}
const drawPolygon = () => {
  mapWork.drawPolygon()
}
const drawCircle = () => {
  mapWork.drawCircle()
}

onMounted(() => {
  // 注册绘制结束事件
  registerDrawEndEvent()
  // 使用 watch 监听 selectedLayers 的变化
  watch(
    selectedLayers,
    (newVal, oldVal) => {
      // 当选中的图层发生变化时,清除之前的分析结果
      // 这样用户在重新绘制时会使用最新的图层选择
      console.log("选中的图层发生变化:", newVal)
    },
    { deep: true }
  )
})

// 组件卸载前清理
onBeforeUnmount(() => {
  // 清除绘制结束事件
  unregisterDrawEndEvent()
  // 清除鼠标绑定事件
  mapWork.cancelDrawing()
})

// 注册绘制结束事件
const registerDrawEndEvent = () => {
  fxEvent.on("drawEnd", handleDrawEnd)
}

// 清除绘制结束事件
const unregisterDrawEndEvent = () => {
  fxEvent.off("drawEnd", handleDrawEnd)
}

// 绘制结束事件处理函数
const handleDrawEnd = (event: any) => {
  const data = JSON.parse(event.value)
  const pointPositions = data.graphic.positions
  // 获取当前的业态分析结果
  let newdata = []
  let wktData = ""
  const graphicType = data.graphic.type
  if (graphicType === "circle") {
    // 圆形
    newdata = [...data.graphic.position, data.graphic.style.radius]
    wktData = circleToWktByMeter(data.graphic.position[0], data.graphic.position[1], data.graphic.style.radius)
  } else if (graphicType === "rectangle") {
    newdata = pointPositions
    // 矩形
    wktData = rectTwoPointsToWktSafe(pointPositions)
  } else {
    // 多边形
    const newdata1 = pointPositions.map((item: any) => {
      return [item[0], item[1]]
    })
    newdata = [...newdata1, newdata1[0]]
    wktData = WKT.convert({ type: "Polygon", coordinates: [newdata] })
  }

  Object.assign(state, { hzcs: { ...JSON.parse(event.value) }, areaData: [...newdata, newdata[0]], wktData })
  // 请求接口获取分析结果
  getXZFXjg()
}

// 获取分析结果
const getXZFXjg = async () => {
  let loadingMsg
  // 从store中获取选中的layer的id数组
  const selectedLayerIds = selectedLayers.value.map((layer: any) => layer.id)
  try {
    state.isLoading = true
    // 在这里显示 loading 提示
    loadingMsg = message.loading("正在分析，请稍候...", 0)

    // todo: 调用后台接口获取分析结果
    const res = await getAroundXZfx({
      polygonwkt: state.wktData,
      shapefileid: selectedLayerIds
    })
    const { point, line, area } = res.data.result
    if (point.list || line.list || area.list) {
      state.analysisData = res.data.result
      // store.setCurrentAnalysisData()
      // 显示弹出层
      state.showAnalysisModal = true

      // 向历史数据添加内容
      store.saveAnalysisDataToSelected({
        analysisType: "zbdkxzx_analysis",
        data: {
          data: JSON.parse(JSON.stringify(state.hzcs.graphic)),
          analysisType: "zbdkxzx_analysis",
          shijiao: state.hzcs.shijiao,
          analysisData: JSON.parse(JSON.stringify(state.analysisData))
        }
      })

      message.success("分析完成")
    } else {
      $message("您未勾选图层或者勾选的图层暂无限制区域")
    }

    // 关闭 loading 提示

  } catch (e) {
    console.error("分析失败:", e)
    // message.error("分析失败，请重试")
  } finally {
    loadingMsg()
    state.isLoading = false
  }
}

// 关闭弹出层
const closeModal = () => {
  state.showAnalysisModal = false
}
</script>
<style scoped>
/* .btn_container {
  display: flex;
 gap: 10px;
} */
</style>
