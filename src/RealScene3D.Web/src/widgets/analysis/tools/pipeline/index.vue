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
        :disabled="isUploading || isRoaming"
      >
        <mars-button :disabled="isRoaming" :loading="isUploading">{{ isUploading ? "上传中..." : (isRoaming ? "正在漫游中..." : "导入excel") }}</mars-button>
      </a-upload>
    </a-space>
    <div class="pipe-setting">
      <span>管线内径 (m):</span>
      <mars-input-number v-model:value="pipeRadius" :min="0.1" :max="50" :step="0.1" />
    </div>

    <div class="pipe-setting">
      <span>路径平滑度 (0-1):</span>
      <mars-input-number v-model:value="smoothness" :min="0" :max="1" :step="0.1" />
    </div>

    <div class="pipe-setting">
      <span>漫游速度 (km/h):</span>
      <mars-input-number v-model:value="roamSpeed" :min="1" :max="500" :step="10" />
    </div>

    <div class="draw-tools tools_btns">
      <mars-button class="btn" @click="startDrawPipe">绘制管线</mars-button>
      <div class="btn-group">
        <mars-button class="btn btn-blue" @click="startRoam">
          {{ isRoaming ? "停止漫游" : "开始漫游" }}
        </mars-button>
        <mars-button class="btn btn-green" @click="toggleRecording">
          {{ isRecording ? "停止录制" : "开始录制" }}
        </mars-button>
        <!-- <mars-button class="btn btn-danger" @click="clearAll">清除</mars-button> -->
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted, ref } from "vue"
import * as mapWork from "./map"
import { downloadExcelTemplate, readExcel } from "@mars/utils/file-util"
import { $message } from "@mars/components/mars-ui"
import { eventTarget as fxEvent, map } from "@mars/widgets/analysis/map"
import { useAnalysisStore } from "@mars/stores/analysis"

const isUploading = ref(false)

const pipeRadius = ref(5)
const smoothness = ref(0.1)
const roamSpeed = ref(100)

const isRoaming = ref(false)
const isRecording = ref(false)
const analysisStore = useAnalysisStore()
onMounted(() => {
  mapWork.onMounted()
})

onUnmounted(() => {
  mapWork.onUnmounted()
})

//  ----------导入Excel文件转geojson---------------
//  导入Excel文件
const handleDownloadTemplate = () => {
  downloadExcelTemplate("管线坐标模板")
}
// 解析Excel文件并设置管线位置
const handleExcelUpload = async (options: any) => {
  const { file } = options
  isUploading.value = true
  try {
    const coordinates = await readExcel(file)
    // console.log("Excel coordinates:", coordinates)
    if (coordinates.length < 2) {
      $message("坐标点必须大于等于2个")
      return
    }

    await mapWork.setPipelinePositions(coordinates, {
      radius: pipeRadius.value,
      smoothness: smoothness.value
    })
    analysisStore.clearAnalysisDataByType("gxfx_pipe")

    analysisStore.saveAnalysisDataToSelected({
      analysisType: "gxfx_pipe",
      data: {
        data: {
          positions: coordinates,
          radius: pipeRadius.value,
          smoothness: smoothness.value
        },
        shijiao: {
          lat: coordinates[0][1],
          lng: coordinates[0][0],
          radius: 1000,
          heading: 0,
          pitch: -90
        },
        analysisType: "gxfx_pipe"
      }
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
  if (isRoaming.value) {
    $message("正在漫游中，请先停止漫游后再上传！")
    return false
  }
  const fileName = file.name
  const fileType = fileName.substring(fileName.lastIndexOf(".") + 1, fileName.length).toLowerCase()
  const isExcel = fileType === "xlsx" || fileType === "xls"
  if (!isExcel) {
    $message("请上传.xlsx或.xls格式的文件！")
  }
  return isExcel
}

/**
 * 开始绘制管线
 */
const startDrawPipe = () => {
  isRoaming.value = false
  analysisStore.clearAnalysisDataByType("gxfx_pipe")
  mapWork.startDrawPipe({
    radius: pipeRadius.value,
    smoothness: smoothness.value
  })
}

fxEvent.on("drawPipe", (data: any) => {
  // 存store中
  analysisStore.clearAnalysisDataByType("gxfx_pipe")
  analysisStore.saveAnalysisDataToSelected({
    analysisType: "gxfx_pipe",
    data: {
      data: {
        ...data.graphic,
        radius: pipeRadius.value,
        smoothness: smoothness.value
      },
      shijiao: map.getCameraView(),
      analysisType: "gxfx_pipe"
    }
  })
})

// 监听漫游结束事件
fxEvent.on("roamEnd", () => {
  isRoaming.value = false
})
/**
 * 切换漫游状态
 */
const startRoam = () => {
  if (isRoaming.value) {
    mapWork.stopRoam()
    isRoaming.value = false
  } else {
    const value = mapWork.startRoam(roamSpeed.value)
    isRoaming.value = value
  }
}

/**
 * 切换录制状态
 */
const toggleRecording = async () => {
  if (isRecording.value) {
    await mapWork.stopRecording()
    isRecording.value = false
  } else {
    mapWork.startRecording()
    isRecording.value = true
  }
}

/**
 * 清空所有数据和图形
 */
const clearAll = () => {
  mapWork.clearAll()
  isRoaming.value = false
  isRecording.value = false

}
</script>

<style scoped lang="less">
.toolbox-container-form {
  padding: 10px;
}

.pipe-setting {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 10px;

  span {
    color: #fff;
    font-size: 12px;
  }

  :deep(.ant-input-number) {
    width: 140px;
  }
}

.draw-tools {
  margin-top: 15px;
  display: flex;
  flex-direction: column;
  gap: 10px;

  .btn {
    width: 100%;
  }

  .btn-group {
    display: flex;
    gap: 8px;
    width: 100%;

    .btn {
      flex: 1;
      padding: 0 4px;
    }
  }

  .btn-blue {
    background-color: #00c2ff;
    border-color: #00c2ff;
  }

  .btn-green {
    background-color: #1eb980;
    border-color: #1eb980;
  }

  .btn-danger {
    background-color: var(--mars-dangerous-color);
    border-color: var(--mars-dangerous-color);
  }
  .btn-green:hover {
    background: #1eb980;
    opacity: 0.8;
  }
  .btn-blue:hover {
    background: #00c2ff;
    opacity: 0.8;
  }
  .btn-danger:hover {
    background: var(--mars-dangerous-color);
    opacity: 0.8;
  }
}
</style>
