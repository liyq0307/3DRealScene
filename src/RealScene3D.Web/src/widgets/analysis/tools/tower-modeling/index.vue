<template>
  <div class="toolbox-container-form toolbox">
    <a-space class="f-mb tools_btns">
      <mars-button type="primary" @click="handleDownloadTemplate">下载excel模板</mars-button>
      <a-upload
        name="file"
        :customRequest="handleExcelUpload"
        :showUploadList="false"
        accept=".xlsx,.xls"
        :beforeUpload="beforeExcelUpload"
        :disabled="isUploading"
      >
        <mars-button :loading="isUploading">{{ isUploading ? "上传中..." : "导入excel" }}</mars-button>
      </a-upload>
    </a-space>
    <div class="pole-setting">
      <span>线杆高度(m) :</span>
      <mars-input-number v-model:value="poleHeight" :disabled="drawDataList.length > 0" :min="10" :max="200" :step="1" />
    </div>
    <div class="pole-setting">
      <span>线杆半径(m) :</span>
      <mars-input-number v-model:value="poleRadius" :disabled="drawDataList.length > 0" :min="0.1" :max="5" :step="0.1" />
    </div>

    <div class="f-mt tools_btns">
      <a-space>
        <mars-button @click="startDrawPole">拾取点位</mars-button>
        <mars-button type="primary" @click="saveData">保存({{drawDataList.length}})</mars-button>
        <mars-button type="primary" danger @click="clearAll">清除</mars-button>
      </a-space>
    </div>
    <div class="tips">
      <p>提示：点击"拾取点位"后，在地图上点击选择位置，将自动生成高压线杆</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from "vue"
import * as mapWork from "./map.js"
import { eventTarget as fxEvent, map } from "@mars/widgets/analysis/map.js"
import { useAnalysisStore } from "@mars/stores/analysis.js"
import { $message } from "@mars/components/mars-ui/index.js"
import { downloadExcelTemplate, readExcel } from "@mars/utils/file-util.js"

const store = useAnalysisStore()
const poleHeight = ref(20)
const poleRadius = ref(0.5)
const isUploading = ref(false)

/**
 * 本地当前绘制数据（借鉴 tsbh 模式）
 * 绘制/导入完成后在此累积，点击"保存"后提交到 store 并清空本数组
 */
interface PoleItem {
  analysisType: string
  data: any
}
const localDrawData = ref<PoleItem[]>([])

// 使用 computed 获取本地暂存数据
const drawDataList = computed(() => localDrawData.value)

// 开始绘制线杆
const startDrawPole = () => {
  // // 检查本地是否有暂存的未保存数据，有则清除
  // if (localDrawData.value.length > 0) {
  //   console.log("🗑️ 清除未保存的线杆数据")
  //   mapWork.clearAll()
  //   localDrawData.value = []
  // }

  // // 检查是否已有保存过的数据，有则全量清除后重新绘制（避免与旧线杆错误连线）
  // if (store.analysisSyncArray.some((item: any) => item.analysisType === "tjjm_pole")) {
  //   console.log("🗑️ 清除已保存的线杆数据，开始全新绘制")
  //   store.clearAnalysisDataByType("tjjm_pole")
  //   mapWork.clearAllGraphics()
  // }

  if (!poleHeight.value) {
    $message("请设置线杆高度")
    return
  }
  mapWork.startDrawPole({
    height: poleHeight.value,
    radius: poleRadius.value
  })
}

// 保存数据（借鉴 tsbh 的 saveToHistory 模式）
const saveData = () => {
  if (localDrawData.value.length === 0) {
    $message("请先绘制线杆或导入Excel")
    return
  }
  console.log(localDrawData.value)

  // 将当前绘制数据提交到 store
  store.saveAnalysisDataToSelected({
    analysisType: "tjjm_pole",
    data: {
      data: JSON.parse(JSON.stringify(localDrawData.value)),
      analysisType: "tjjm_pole",
      shijiao: map?.getCameraView() || {},
      count: localDrawData.value.length
    }
  })

  // 保存成功后清空本地暂存数据，以及 map.ts 中的本地数组（不清除图上图形）
  mapWork.clearLocalArrays()
  localDrawData.value = []
}

// 清除所有未保存的内容
const clearAll = () => {
  // 只清除地图上未保存的图形
  mapWork.clearAll()
  // 清空本地暂存数据
  localDrawData.value = []
}

// 组件挂载时注册事件监听器
onMounted(() => {
  fxEvent.on("drawPoleEnd", (event) => {
    // 将数据暂存到本地（借鉴 tsbh 模式）
    localDrawData.value.push(event)
  })
})

// 组件卸载时移除事件监听器并清理未保存数据
onUnmounted(() => {
  // 清除鼠标绑定事件
  mapWork.cancelDrawPole()
  fxEvent.off("drawPoleEnd")
  mapWork.clearAll()
  localDrawData.value = []
})
//  ----------导入Excel文件转geojson---------------
//  导入Excel文件
const handleDownloadTemplate = () => {
  downloadExcelTemplate("塔基坐标模板")
}
// 解析Excel文件并设置管线位置
const handleExcelUpload = async (options: any) => {
  const { file } = options
  isUploading.value = true
  try {
    const coordinates = await readExcel(file)
    console.log("Excel coordinates:", coordinates)
    if (coordinates.length < 2) {
      $message("坐标点必须大于等于2个")
      return
    }

    await mapWork.drawTj(coordinates, {
      height: poleHeight.value,
      radius: poleRadius.value
    })

    $message("导入成功")
  } catch (err: any) {
    console.error(err)
    $message("导入解析失败: " + err.message)
  } finally {
    isUploading.value = false
  }
}
// 验证上传文件格式
const beforeExcelUpload = (file: any) => {
  clearAll()
  const fileName = file.name
  const fileType = fileName.substring(fileName.lastIndexOf(".") + 1, fileName.length).toLowerCase()
  const isExcel = fileType === "xlsx" || fileType === "xls"
  if (!isExcel) {
    $message("请上传.xlsx或.xls格式的文件！")
  }
  return isExcel
}


</script>

<style scoped lang="less">
.ant-slider {
  width: 140px;
}

.mars-gui-form {
  padding: 0 !important;
}

:deep(.ant-radio-group) {
  width: 101%
}

:deep(.ant-radio-wrapper-in-form-item) {
  margin: 0px;
}


.pole-setting {
  display: flex;
  gap: 10px;
  margin-top: 8px;
  color: var(--mars-sub-title-color);

  span {
    display: inline-block;
    text-align: right;
    width: 157px;
    margin-top: 5px;
    font-size: 12px;
  }
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
