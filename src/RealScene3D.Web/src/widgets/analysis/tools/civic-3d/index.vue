<template>
<div class="toolbox-container-form toolbox">
  <a-space class="tools_btns" :size="8">
    <mars-button type="primary" @click="handleDownloadTemplate">下载excel模板</mars-button>
    <a-upload
      :customRequest="handleExcelUpload"
      :showUploadList="false"
      accept=".xlsx,.xls"
      :beforeUpload="beforeExcelUpload"
      :disabled="isUploading"
    >
      <mars-button :loading="isUploading">{{ isUploading ? "上传中..." : "导入excel" }}</mars-button>
    </a-upload>
    <mars-button @click="handleDrawRoad"  type="primary">开始铺设</mars-button>
  </a-space>

</div>
</template>

<script setup lang="ts">
import { $message } from "@mars/components/mars-ui"
import { reactive, ref, onMounted, onUnmounted } from "vue"
import * as mapWork from "./map"
import { map, eventTarget } from "@mars/widgets/analysis/map"
import { useAnalysisStore } from "@mars/stores/analysis"
import { downloadExcelTemplate, readExcel } from "@mars/utils/file-util"
import { useWidget } from "@mars/common/store/widget"
const isUploading = ref(false)

const isDrawing = ref(false)
const store = useAnalysisStore()
const { disable, activate } = useWidget()

// 监听图形编辑更新事件
const onGraphicEditorUpdate = (event: any) => {
  store.clearAnalysisDataByType("dlps")
  mapWork.clearRoad()
  const graphic = event.graphic
  // console.log("收到graphicEditorUpdate事件，graphic:", graphic)
  // 如果是道路铺设的图形，同步更新 store
  if (graphic.type === "road" || graphic.attr?.type === "dlps") {

    store.saveAnalysisDataToSelected({
      analysisType: "dlps",
      data: {
        analysisType: "dlps",
        name: "道路铺设",
        shijiao: map?.getCameraView() || {},
        data: graphic.toJSON()
      }
    })
  }
}

onMounted(() => {
  eventTarget.on("graphicEditorUpdate", onGraphicEditorUpdate)
})

onUnmounted(() => {
  // 清除鼠标绑定事件
  mapWork.cancelDrawRoad()
  eventTarget.off("graphicEditorUpdate", onGraphicEditorUpdate)
})

// 下载模板
const handleDownloadTemplate = () => {
  downloadExcelTemplate("道路铺设模板")
}
// DWG上传前的验证
const beforeExcelUpload = (file: File) => {
  const fileName = file.name
  const fileType = fileName.substring(fileName.lastIndexOf(".") + 1, fileName.length).toLowerCase()
  const isExcel = fileType === "xlsx" || fileType === "xls"
  if (!isExcel) {
    $message("请上传.xlsx或.xls格式的文件！")
  }
  return isExcel
}



// DWG自定义上传逻辑
const handleExcelUpload = async (options: any) => {
  store.clearAnalysisDataByType("dlps")
  mapWork.clearRoad()
  const { file } = options

  isUploading.value = true

  try {
    const coordinates = await readExcel(file)
    console.log("Excel coordinates:", coordinates)
    if (coordinates.length < 2) {
      $message("坐标点必须大于等于2个")
      return
    }

    const roadGraphic = await mapWork.setDlpsPositions(coordinates)

    store.saveAnalysisDataToSelected({
      analysisType: "dlps",
      data: {
        analysisType: "dlps",
        name: "道路铺设",
        shijiao: roadGraphic.sj || {},
        data: roadGraphic.road
      }
    })
    const realGraphic = map.graphicLayer.getGraphicById(roadGraphic.road.id)
    if (realGraphic) {
      activate({ name: "graphic-options", data: { layerId: map.graphicLayer.id, graphicId: roadGraphic.road.id }, reload: true })
    }
    $message("导入成功")
  } catch (err: any) {
    console.error(err)
    $message("导入解析失败: " + err.message)
  } finally {
    isUploading.value = false
  }
}

// 道路铺设
const handleDrawRoad = async () => {
  mapWork.clearRoad()
  disable("graphic-options")
  try {
    const graphic = await mapWork.drawRoad()

    // 向store添加数据
    store.clearAnalysisDataByType("dlps")

    store.saveAnalysisDataToSelected({
      analysisType: "dlps",
      data: {
        analysisType: "dlps",
        name: "道路铺设",
        shijiao: map?.getCameraView() || {},
        data: graphic
      }
    })

    // 绘制完成后，从图层中获取真实的 graphic 对象，激活属性面板
    const realGraphic = map.graphicLayer.getGraphicById(graphic.id)
    if (realGraphic) {
      activate({ name: "graphic-options", data: { layerId: map.graphicLayer.id, graphicId: realGraphic.id }, reload: true })
    }
  } catch (error) {
    console.error("道路铺设失败:", error)
    $message("道路铺设失败")
  }
}
</script>
<style lang="less" scoped>
.tools_btns{
  width: 100%;
  display: flex;
  :deep(.ant-space-item) {
    flex: 1;
  }
  :deep(.ant-upload) {
    width: 100%;
    display: block;
  }
  :deep(.mars-button) {
    padding: 0 4px;
    width: 100%;
  }
}
</style>
