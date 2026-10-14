<template>
  <div class="toolbox-container-form toolbox">
    <mars-button class="tools_btns" @click="handleAddViewer" type="primary">添加视角</mars-button>
  </div>
</template>

<script setup lang="ts">
import { $message } from "@mars/components/mars-ui"
import * as mapWork from "./map"
import { useAnalysisStore } from "@mars/stores/analysis"

const store = useAnalysisStore()

const handleAddViewer = () => {
  const shijiao = mapWork.measureGroundPoint()
  if (shijiao) {
    store.saveGctData({
      analysisType: "gct_point",
      data: {
        id: Date.now(),
        name: "视角" + (store.gctData.length + 1),
        value: shijiao,
        sfmr: false
      }
    })
    $message("添加成功,可在多视角查看!")
  }
}
</script>
