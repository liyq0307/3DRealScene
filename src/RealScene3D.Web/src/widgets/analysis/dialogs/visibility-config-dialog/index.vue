<template>
  <mars-dialog
    :nopadding="true"
    title="属性编辑"
    width="270"
    :top="541"
    bottom="26"
    :left="dialogLeft"
    :minWidth="200"
    :animation="false"
    :visible="video.selectedGraphic"
    :closeable="true"
    :beforeClose="
      () => {
        video.selectedGraphic = false
      }
    "
  >
    <div class="f-mb first_row">
      <a-space>
        <span class="mars-pannel-item-label">水平张角:</span>
        <mars-slider @change="onChangeAngle" v-model:value="video.cameraAngle" :min="1" :max="60" :step="0.1" />
      </a-space>
    </div>

    <div class="f-mb">
      <a-space>
        <span class="mars-pannel-item-label">垂直张角:</span>
        <mars-slider @change="onChangeAngle2" v-model:value="video.cameraAngle2" :min="10" :max="30" :step="0.1" />
      </a-space>
    </div>

    <div class="f-mb">
      <a-space>
        <span class="mars-pannel-item-label">投射距离:</span>
        <mars-slider @change="onChangeDistance" v-model:value="video.distanceValue" :min="1" :max="5000" :step="1" />
      </a-space>
    </div>

    <div class="f-mb">
      <a-space>
        <span class="mars-pannel-item-label">四周方向:</span>
        <mars-slider @change="onChangeHeading" class="head" v-model:value="video.heading" :min="0" :max="360" :step="0.1" />
        <mars-button @click="onClickSelView">图上选点</mars-button>
      </a-space>
    </div>

    <div class="f-mb">
      <a-space>
        <span class="mars-pannel-item-label">俯仰角度:</span>
        <mars-slider @change="onChangePitch" v-model:value="video.pitchValue" :min="-180" :max="180" :step="0.1" />
      </a-space>
    </div>

    <div class="f-mb">
      <a-space>
        <span class="mars-pannel-item-label">视椎框线:</span>
        <a-checkbox @change="showFrustum" v-model:checked="video.showFrustum">是否显示</a-checkbox>
      </a-space>
    </div>

    <div class="f-mb">
      <a-space>
        <span class="mars-pannel-item-label">透明度:</span>
        <mars-slider @change="onChangeOpacity" v-model:value="video.opacity" :min="0" :max="1" :step="0.1" />
      </a-space>
    </div>

    <div class="f-mb f-tac">
      <a-space>
        <mars-button @click="selCamera">点选相机位置</mars-button>
      </a-space>
    </div>
  </mars-dialog>
</template>

<script lang="ts" setup>
import { nextTick, onUnmounted, ref, watch } from "vue"
import * as mapWork from "./map"

import type { TsfxVideoData } from "@mars/stores/analysis"
import { useAnalysisStore } from "@mars/stores/analysis"

// 监听 video 内容变化（由子组件 slider 等通过 v-model 触发）
// 手动更新 modelValue 以触发父组件的 setter
// 实际上 v-model:value 在 slider 上运行，而 video 是 defineModel，
// 只要我们在子组件里修改了 video 的属性，vue 会通知并更新父组件。
// 但由于我们希望在父组件 setter 中逻辑更通用，我们在属性编辑框内操作后需要显式触发同步。

const video = defineModel<TsfxVideoData>({ required: true })
const store = useAnalysisStore()
// 对话框左侧位置
const dialogLeft = ref(0)
let mutationObserver: MutationObserver | null = null

// 初始检查图层树可见性
nextTick(() => {
  checkLayerTreeVisibility()
})

// 使用 MutationObserver 监听图层树的显示状态变化
mutationObserver = new MutationObserver(() => {
  checkLayerTreeVisibility()
})

// 监听 body 的子元素变化
mutationObserver.observe(document.body, {
  attributes: true,
  subtree: true,
  attributeFilter: ["style", "class"]
})
onUnmounted(() => {
  if (mutationObserver) {
    mutationObserver.disconnect()
    mutationObserver = null
  }
})

// 图层树显示状态
const isLayerTreeVisible = ref(false)
// 检查图层树是否可见
function checkLayerTreeVisibility() {
  const layerDialog = document.querySelector(".layer-tree-dialog.mars-dialog") as HTMLElement
  if (layerDialog) {
    const isVisible =
      layerDialog.offsetParent !== null && getComputedStyle(layerDialog).display !== "none" && getComputedStyle(layerDialog).visibility !== "hidden"
    isLayerTreeVisible.value = isVisible
    dialogLeft.value = isVisible ? 270 : 0
  } else {
    isLayerTreeVisible.value = false
    dialogLeft.value = 0
  }
}

// 相机位置选择
const selCamera = () => {
  mapWork.selCamera()
}
// 修改水平张角
const onChangeAngle = () => {
  mapWork.onChangeAngle(video.value.cameraAngle)
  store.updateTsfxVideoData({ cameraAngle: video.value.cameraAngle })
}
// 修改垂直张角
const onChangeAngle2 = () => {
  mapWork.onChangeAngle2(video.value.cameraAngle2)
  store.updateTsfxVideoData({ cameraAngle2: video.value.cameraAngle2 })
}
// 修改投射距离
const onChangeDistance = () => {
  mapWork.onChangeDistance(video.value.distanceValue)
  store.updateTsfxVideoData({ distanceValue: video.value.distanceValue })
}
// 修改四周方向
const onChangeHeading = () => {
  mapWork.onChangeHeading(video.value.heading)
  store.updateTsfxVideoData({ heading: video.value.heading })
}
// 图上选点
const onClickSelView = () => {
  mapWork.onClickSelView()
}
// 修改俯仰角度
const onChangePitch = () => {
  mapWork.onChangePitch(video.value.pitchValue)
  store.updateTsfxVideoData({ pitchValue: video.value.pitchValue })
}

// 视锥体显示控制
const showFrustum = () => {
  mapWork.showFrustum(video.value.showFrustum)
  store.updateTsfxVideoData({ showFrustum: video.value.showFrustum })
}

// 修改透明度
const onChangeOpacity = () => {
  mapWork.onChangeOpacity(video.value.opacity)
  store.updateTsfxVideoData({ opacity: video.value.opacity })
}
watch(
  () => ({ ...video.value }),
  async (newVal, oldVal) => {
    // 过滤掉控制属性
    if (newVal.selectedGraphic && oldVal.selectedGraphic) {
      video.value = { ...newVal } // 重新赋值给 model 触发父组件 setter
    }
  },
  { deep: true }
)
</script>
<style scoped lang="less">
.f-mb.first_row {
  margin-top: 10px;
}
.ant-slider {
  width: 170px;
}
.head {
  width: 80px;
}
.mars-pannel-item-label {
  width: 68px;
}
</style>
