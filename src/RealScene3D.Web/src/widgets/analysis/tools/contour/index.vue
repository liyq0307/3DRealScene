<template>
  <div class="toolbox-container-form toolbox dgx">
    <mars-gui :options="options" :labelCol="6" ref="marsGuiRef"></mars-gui>
    <div class="highlight-contour">
      <a-row :gutter="[0, 10]">
        <a-col :span="6">等高标线:</a-col>
        <a-col :span="12">
          <mars-input-number v-model:value="lineHeight" :min="0" :step="1" placeholder="输入高程" />
        </a-col>
        <a-col :span="6" style="text-align: right">
          <mars-button class="tools_btns" @click="handleHighlightLine">绘制</mars-button>
        </a-col>
      </a-row>
    </div>
    <div class="draw-tools tools_btns">
      <a-row :gutter="8">
        <a-col :span="12">
          <mars-button style="width: 100%" @click="btnDrawExtent">添加矩形</mars-button>
        </a-col>
        <a-col :span="12">
          <a-dropdown-button
            class="export-dropdown-btn"
            overlayClassName="export-format-dropdown"
            @click="handleExportClick"
            :trigger="['hover']"
            style="width: 100%"
          >
            {{ exportButtonText }}
            <template #overlay>
              <a-menu :selectedKeys="[currentExportFormat]" @click="handleExportMenuClick">
                <a-menu-item key="dxf">导出DXF</a-menu-item>
                <a-menu-item key="shp">导出SHP</a-menu-item>
              </a-menu>
            </template>
          </a-dropdown-button>
        </a-col>
      </a-row>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, onMounted, onUnmounted } from "vue"
import * as mapWork from "./map"
import type { GuiItem } from "@mars/components/mars-ui/mars-gui"
import { eventTarget as fxEvent } from "@mars/widgets/analysis/map"
import { useAnalysisStore } from "@mars/stores/analysis"
import { $message } from "@mars/components/mars-ui"
import { convertToDXF } from "@mars/utils/geo-export-dxf"
import { convertToSHP } from "@mars/utils/geo-export-shp"
import type { ConvertedFile } from "@mars/utils/geo-export-common"

import initGdalJs from "gdal3.js"

export type ExportFormat = "dxf" | "shp"

const paths = {
  wasm: "/gdal/gdal3WebAssembly.wasm",
  data: "/gdal/gdal3WebAssembly.data"
}

let Gdal: any = null
const store = useAnalysisStore()
const marsGuiRef = ref()
const lineHeight = ref()
const currentExportFormat = ref<ExportFormat>("dxf")
const hasDrawnExtent = ref(false)
const isContourVisible = ref(true)
const options: GuiItem[] = [
  {
    type: "switch",
    field: "showLabel",
    label: "标注",
    value: true,
    change(val) {
      mapWork.showLabels(val)
      if (isContourVisible.value && lineHeight.value !== undefined && lineHeight.value !== null) {
        mapWork.drawHighlightContour(lineHeight.value)
      }
    }
  },
  {
    type: "switch",
    field: "showContour",
    label: "等高线",
    value: true,
    change(val) {
      isContourVisible.value = val
      mapWork.showDengGX(val)
    }
  },
  {
    type: "slider",
    field: "gapValue",
    label: "等高距",
    step: 1,
    min: 10,
    max: 500,
    value: 30,
    change(val) {
      mapWork.changeSpacing(val)
      if (lineHeight.value !== undefined && lineHeight.value !== null) {
        mapWork.drawHighlightContour(lineHeight.value)
      }
    }
  },
  {
    type: "color",
    field: "colorValue",
    label: "颜色",
    value: "rgba(255, 15, 0, 1)",
    change(val) {
      mapWork.changeColor(val)
      if (lineHeight.value !== undefined && lineHeight.value !== null) {
        mapWork.drawHighlightContour(lineHeight.value)
      }
    }
  },
  {
    type: "slider",
    field: "lineWidth",
    label: "线宽(px)",
    step: 1,
    min: 1,
    max: 10,
    value: 2,
    change(val) {
      mapWork.changeWidth(val)
      mapWork.drawHighlightContour(lineHeight.value)
    }
  }
]

const exportButtonText = computed(() => (currentExportFormat.value === "dxf" ? "导出DXF" : "导出SHP"))
onMounted(async () => {
  // 从 options 中提取初始值传递给 mapWork
  const initialOptions = {
    showLabels: options.find((item) => item.field === "showLabel")?.value,
    interval: options.find((item) => item.field === "gapValue")?.value,
    color: options.find((item) => item.field === "colorValue")?.value,
    width: options.find((item) => item.field === "lineWidth")?.value
  }
  mapWork.getContourManager(initialOptions)

  // 从 store 恢复等高标线高度（模块切换后 lineHeight ref 会丢失，需要从 store 中恢复）
  const dgxItem = store.analysisSyncArray.find((item) => item.analysisType === "dgx_analysis")
  if (dgxItem?.data?.options?.lineHeight !== undefined) {
    lineHeight.value = dgxItem.data.options.lineHeight
  }

  try {
    Gdal = await (initGdalJs as any)({
      paths,
      useWorker: false
    })
    // console.log("GDAL loaded successfully", Gdal)
  } catch (e) {
    console.error("GDAL load failed:", e)
  }

  // 监听等高线分析完成事件，保存分析结果到 store
  fxEvent.on("drawEnd", handleDrawEnd)
  fxEvent.on("updateOptions", handleUpdateOptions)
})

const handleDrawEnd = (event: any) => {
  if (event.analysisType !== "dgx_analysis") {
    return
  }
  // console.log("等高线绘制结束:", event)

  // 历史列表永远只留最新一条数据
  store.clearAnalysisDataByType("dgx_analysis")

  store.saveAnalysisDataToSelected({
    analysisType: "dgx_analysis",
    data: {
      data: event.data,
      analysisType: "dgx_analysis",
      shijiao: event.data?.shijiao,
      ...event.data
    }
  })
}
// 监听分析选项更新事件，实时同步更新 store 中的数据对象
const handleUpdateOptions = (event: any) => {
  if (event.analysisType !== "dgx_analysis") {
    return
  }

  // 统一字段名：将后端传来的 show 映射为 showContour，showLabels 保持不变
  const normalizedOptions = { ...event.options }
  if ("show" in normalizedOptions) {
    normalizedOptions.showContour = normalizedOptions.show
    delete normalizedOptions.show
  }

  // 1. 更新当前选中行（如果正在编辑）
  if (store.clickedRow?.analysisType === "dgx_analysis") {
    store.setCurrentAnalysisData({
      ...store.clickedRow,
      data: {
        ...store.clickedRow.data,
        options: {
          ...store.clickedRow.data.options,
          ...normalizedOptions
        }
      }
    })
  }

  // 2. 同步更新历史列表中的数据对象
  const index = store.analysisSyncArray.findIndex((item) => item.analysisType === "dgx_analysis" && item.data.data?.id === event.id)
  if (index !== -1) {
    const target = store.analysisSyncArray[index]
    target.data = {
      ...target.data,
      options: {
        ...target.data.options,
        ...normalizedOptions
      }
    }
  }
}

onUnmounted(() => {
  fxEvent.off("drawEnd", handleDrawEnd)
  fxEvent.off("updateOptions", handleUpdateOptions)

  // 清除鼠标绑定事件
  mapWork.cancelDrawExtent()

  // 标记离开模块（下次绘制时会恢复默认属性）
  mapWork.markModuleLeft()
})

// 绘制并高亮指定高度的等高线
const handleHighlightLine = () => {
  if (!hasDrawnExtent.value) {
    $message("请先添加矩形")
    return
  }
  if (lineHeight.value === undefined || lineHeight.value === null) {
    $message("请输入有效的高程值")
    return
  }
  mapWork.drawHighlightContour(lineHeight.value)
}

// 添加矩形
const btnDrawExtent = () => {
  store.clearAnalysisDataByType("dgx_analysis") // 先清除之前的等高线分析数据

  // 重置等高标线的输入值，确保能够触发 drawEnd 事件
  lineHeight.value = undefined

  mapWork.btnDrawExtent()
  hasDrawnExtent.value = true
}

// 导出geojson
const downloadGeoJSON = () => {
  mapWork.exportGeojson()
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

// 导出 DXF
const downloadDXF = async () => {
  const geojson = mapWork.getExportGeojson()
  if (!geojson?.features?.length) {
    $message("当前没有可导出的等高线数据")
    return
  }
  if (!Gdal) {
    $message("GDAL 库尚未加载完成")
    return
  }

  try {
    const files = await convertToDXF(Gdal, geojson)
    downloadFiles(files)
    $message("DXF 导出成功")
  } catch (error) {
    console.error("DXF 导出失败:", error)
    $message("DXF 导出失败")
  }
}

// 导出 SHP
const downloadSHP = async () => {
  const geojson = mapWork.getExportGeojson()
  if (!geojson?.features?.length) {
    $message("当前没有可导出的等高线数据")
    return
  }
  if (!Gdal) {
    $message("GDAL 库尚未加载完成")
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

// 兼容按钮组逻辑（如果仍需保留下拉切换）
const downloadByFormat = (format: ExportFormat = "dxf") => {
  if (format === "dxf") {
    downloadDXF()
  } else {
    downloadSHP()
  }
}

// 菜单导出事件，根据选择的格式调用下载函数
const handleExportMenuClick = ({ key }: { key: string }) => {
  currentExportFormat.value = key as ExportFormat
  downloadByFormat(currentExportFormat.value)
}

const handleExportClick = () => {
  downloadByFormat(currentExportFormat.value)
}

// 清除
const clearAll = () => {
  mapWork.clearAll()
  hasDrawnExtent.value = false
}
</script>

<style scoped lang="less">
.toolbox-container-form.dgx {
  margin-bottom: 10px;
}
.draw-tools {
  padding-top: 10px;
  .export-dropdown-btn {
    :deep(.ant-btn) {
      color: var(--mars-text-color);
      border-color: rgba(234, 242, 255, 0.25);
      background: rgba(234, 242, 255, 0.08);
      height: 28px;
      border-radius: 2px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      padding: 0 8px;
      > span {
        font-size: 12px;
        display: inline-block;
        line-height: 1;
      }
    }

    :deep(.ant-btn-group) {
      width: 100%;
      display: flex;
      .ant-btn:first-child {
        flex: 1;
      }
    }

    :deep(.ant-btn:hover),
    :deep(.ant-btn:focus) {
      color: var(--mars-text-color);
      border-color: rgba(234, 242, 255, 0.45);
      background: rgba(234, 242, 255, 0.16);
    }
  }
}

.highlight-contour {
  padding: 0 10px 15px 10px;
  :deep(.ant-row) {
    align-items: center;
    .ant-col {
      font-size: 12px;
    }
  }
}
</style>
