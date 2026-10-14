<template>
  <div class="toolbox-container-form toolbox">
    <div class="tools_btns f-mb">
      <a-space :gutter="8" wrap>
        <mars-button class="btn" @click="drawPoint">标记点</mars-button>
        <mars-button class="btn" @click="drawLine">标记线</mars-button>
        <mars-button class="btn" @click="drawPolygon">标记面</mars-button>
        <mars-button class="btn" @click="drawCircle">标记圆</mars-button>
        <mars-button class="btn" danger @click="handleClear">清除</mars-button>
        <mars-button class="btn" type="primary" @click="saveToHistory">保存({{ currentDrawData.length }})</mars-button>
        <mars-button class="btn" type="primary" @click="downloadSHP">导出SHP</mars-button>
      </a-space>
    </div>
    <div class="tips">
      <p>提示：点击标记按钮后，在地图上进行绘制，完成后可保存或导出</p>
    </div>
  </div>
</template>

<script lang="ts" setup>
import { ref, onMounted, onUnmounted } from "vue"
import { $message } from "@mars/components/mars-ui"
import * as mapWork from "./map"
import * as fxMapWork from "@mars/widgets/analysis/map"
import { useAnalysisStore } from "@mars/stores/analysis"
import { convertToSHP } from "@mars/utils/geo-export-shp"
import type { ConvertedFile } from "@mars/utils/geo-export-common"
import initGdalJs from "gdal3.js" // 导出shp需要用到gdal.js

const paths = {
  wasm: "/gdal/gdal3WebAssembly.wasm",
  data: "/gdal/gdal3WebAssembly.data"
}
let Gdal: any = null
const store = useAnalysisStore()

/**
 * 本地当前绘制数据（JSON数组）—— 绘制完成后在此累积
 * 点击"保存"后提交到 store 并清空本数组
 * 离开页面时清空本数组和图上未保存的标绘
 */
interface DrawItem {
  shijiao: any
  graphic: any
}
const currentDrawData = ref<DrawItem[]>([])


onMounted(async () => {
  try {
    Gdal = await (initGdalJs as any)({
      paths,
      useWorker: false
    })
  } catch (e) {
    console.error("GDAL load failed:", e)
  }
})

// 处理绘制结束事件 —— 将绘制内容累积到本地 JSON 数组
const handleDrawEnd = (event: any) => {
  const data = JSON.parse(event.value)
  currentDrawData.value.push(data)
}

// 点绘制
const drawPoint = () => {
  mapWork.drawPoint()
}

// 线绘制
const drawLine = () => {
  mapWork.drawLine()
}

// 多边形绘制
const drawPolygon = () => {
  mapWork.drawPolygon()
}

// 圆形绘制
const drawCircle = () => {
  mapWork.drawCircle()
}

/**
 * 清除按钮逻辑：
 * - 清除图上未保存的当前标绘
 * - 清空本地 currentDrawData 数组
 * - 已保存的内容（store 中）不受影响
 */
const handleClear = () => {
  // 清除地图上未保存的图形
  mapWork.clearAll()
  // 清空本地当前绘制数据
  currentDrawData.value = []
}

// 导出 SHP
const downloadSHP = async () => {
  const geojson = mapWork.exportGeoJson()

  if (!geojson) {
    return
  }

  try {
    const files = await convertToSHP(Gdal, geojson)
    downloadFiles(files)
    $message("SHP 导出成功")
  } catch (error) {
    console.error("SHP 导出失败:", error)
    $message("SHP 导出失败")
  }
}

// 执行文件下载
const downloadFiles = (files: ConvertedFile[]) => {
  files.forEach((item) => {
    const fileBytes = new Uint8Array(item.bytes.byteLength)
    fileBytes.set(item.bytes)
    const blob = new Blob([fileBytes], { type: item.mimeType })
    const url = URL.createObjectURL(blob)
    const link = document.createElement("a")
    link.href = url
    link.download = item.name
    link.click()
    URL.revokeObjectURL(url)
  })
}

/**
 * 保存至 store：
 * 1. 将 currentDrawData 整体写入 store.analysisSyncArray
 * 2. 标记图上图形为已保存（保留在地图上）
 * 3. 清空 currentDrawData 本地数组
 */
const saveToHistory = () => {
  if (currentDrawData.value.length === 0) {
    console.log("没有可保存的绘制内容")
    return
  }
  console.log(currentDrawData.value)
  // 将当前绘制数据提交到 store
  store.saveAnalysisDataToSelected({
    analysisType: "basic_draw",
    data: {
      data: JSON.parse(JSON.stringify(currentDrawData.value)),
      analysisType: "basic_draw",
      shijiao: mapWork.map?.getCameraView() || {},
      count: currentDrawData.value.length
    }
  })

  // 标记地图上的图形为已保存（图上标绘保留）
  mapWork.markGraphicsAsSaved()

  // 保存后清空本地绘制数据（但图上已保存的图形仍在）
  currentDrawData.value = []
}

// 组件挂载时注册事件监听器
onMounted(() => {
  fxMapWork.eventTarget.on("drawEnd", handleDrawEnd)
})

// 组件卸载时移除事件监听器并清理未保存数据
onUnmounted(() => {
  // 清除鼠标绑定事件
  mapWork.cancelDrawing()
  fxMapWork.eventTarget.off("drawEnd", handleDrawEnd)
  // 只清除未保存的图形和本地数据，已保存的保留在地图上
  mapWork.clearAll()
  // 清空本地当前绘制数据
  currentDrawData.value = []
})
</script>

<style scoped lang="less">
:deep(.tools_btns .ant-space){
  gap: 7px !important;
}
.tips {
  margin-top: 15px;
  padding: 10px;
  background: rgba(64, 169, 255, 0.1);
  border-left: 3px solid #40a9ff;
  border-radius: 2px;

  p {
    margin: 0;
    color: var(--mars-sub-title-color);
    font-size: 12px;
    line-height: 1.6;
  }
}
</style>
