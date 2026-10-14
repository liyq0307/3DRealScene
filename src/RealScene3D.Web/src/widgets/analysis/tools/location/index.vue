<template>
  <a-form>
    <div class="toolbox-container-form toolbox">
      <a-form-item label="类型">
        <a-radio-group v-model:value="formState.radioFanwei" @change="changeFanwei">
          <a-radio value="1">十进制</a-radio>
          <a-radio value="2">度分秒</a-radio>
        </a-radio-group>
      </a-form-item>

      <!-- 十进制的面板 -->
      <div v-show="formState.radioFanwei == '1'">
        <a-form-item label="经度" class="shijingzhi" @change="changeJWD">
          <mars-input class="point_Trans_input" v-model:value="formState.jd"> </mars-input>
        </a-form-item>
        <a-form-item label="纬度" class="shijingzhi">
          <mars-input class="point_Trans_input" v-model:value="formState.wd" @change="changeJWD"> </mars-input>
        </a-form-item>
      </div>
      <!-- 度分秒的面板 -->
      <div v-show="formState.radioFanwei == '2'">
        <a-form-item label="经度">
          <a-space>
            <mars-input v-model:value="formState.jdDegree" class="dufenmiao" @change="changeDMS"> </mars-input>°
            <mars-input v-model:value="formState.jdMinute" class="dufenmiao" @change="changeDMS"> </mars-input>'
            <mars-input v-model:value="formState.jdSecond" class="dufenmiao" @change="changeDMS"> </mars-input>"
          </a-space>
        </a-form-item>
        <a-form-item label="纬度">
          <a-space>
            <mars-input v-model:value="formState.wdDegree" class="dufenmiao" @change="changeDMS"> </mars-input>°
            <mars-input v-model:value="formState.wdMinute" class="dufenmiao" @change="changeDMS"> </mars-input>'
            <mars-input v-model:value="formState.wdSecond" class="dufenmiao" @change="changeDMS"> </mars-input>"
          </a-space>
        </a-form-item>
      </div>
      <a-form-item class="tools_btns">
        <a-space>
          <mars-button class="btn" @click="bindMourseClick">图上拾取</mars-button>
          <mars-button class="btn" @click="submitCenter">坐标定位</mars-button>
          <mars-button class="btn reset-btn" @click="registerTransform">重置</mars-button>
        </a-space>
      </a-form-item>
    </div>
  </a-form>
</template>

<script setup lang="ts">
import { computed, reactive, onMounted, onUnmounted } from "vue"
import * as mapWork from "./map.js"
import * as mapWork1 from "../../map.js"
import { $alert } from "@mars/components/mars-ui/index"
import { useAnalysisStore } from "@mars/stores/analysis.js"

const analysiaStore = useAnalysisStore()
const formState = reactive({
  radioFanwei: "1",
  radioFendai: "2",
  jd: 0,
  wd: 0,
  alt: 0,
  jdDegree: 0,
  jdMinute: 0,
  jdSecond: 0,
  wdDegree: 0,
  wdMinute: 0,
  wdSecond: 0
})

// 全局中间变量
let currJD: number
let currWD: number
let currGD: number

// 处理点击地图事件的函数
const handleClickMap = (event: any) => {
  currJD = event.point.lng
  currWD = event.point.lat
  currGD = event.point.alt

  // 控制台打印从图上拾取事件中获取到的点坐标

  formState.jd = mapWork.marsUtilFormtNum(currJD, 6)
  formState.wd = mapWork.marsUtilFormtNum(currWD, 6)
  formState.alt = mapWork.marsUtilFormtNum(currGD, 6)
  changeFanwei()
  // 更新面板
  mapWork.updateMarker(false, currJD, currWD, currGD)
}

// 处理加载中心点事件的函数
const handleLoadCenterPoint = (event: any) => {
  const point = event.point
  currJD = point.lng
  currWD = point.lat
  currGD = point.alt
  formState.jd = mapWork.marsUtilFormtNum(currJD, 6)
  formState.wd = mapWork.marsUtilFormtNum(currWD, 6)
  formState.alt = mapWork.marsUtilFormtNum(currGD, 6)
}

// 组件挂载时注册事件监听器
onMounted(() => {
  mapWork.eventTarget.on("loadCenterPoint", handleLoadCenterPoint)
  mapWork.eventTarget.on("clickMap", handleClickMap)
})

// 组件卸载时移除事件监听器
onUnmounted(() => {
  mapWork.eventTarget.off("loadCenterPoint", handleLoadCenterPoint)
  mapWork.eventTarget.off("clickMap", handleClickMap)
})
// 十进制改变
const changeJWD = () => {
  currJD = Number(formState.jd)
  currWD = Number(formState.wd)
  currGD = Number(formState.alt)
}
// 度分秒改变
const changeDMS = () => {
  currJD = mapWork.marsDms2degree(formState.jdDegree, formState.jdMinute, formState.jdSecond)
  currWD = mapWork.marsDms2degree(formState.wdDegree, formState.wdMinute, formState.wdSecond)
}
// 十进制、度分秒切换
const changeFanwei = () => {
  switch (formState.radioFanwei) {
    case "1":
      formState.jd = mapWork.marsUtilFormtNum(currJD, 6) || 0
      formState.wd = mapWork.marsUtilFormtNum(currWD, 6) || 0
      break
    case "2": // 度分秒
      console.log("当前经度:", currJD, "当前纬度:", currWD)
      if (currJD !== undefined && currWD !== undefined) {
        formState.jdDegree = mapWork.marsPointTrans(currJD).degree
        formState.jdMinute = mapWork.marsPointTrans(currJD).minute
        formState.jdSecond = mapWork.marsPointTrans(currJD).second
        formState.wdDegree = mapWork.marsPointTrans(currWD).degree
        formState.wdMinute = mapWork.marsPointTrans(currWD).minute
        formState.wdSecond = mapWork.marsPointTrans(currWD).second
      } else {
        formState.jdDegree = 0
        formState.jdMinute = 0
        formState.jdSecond = 0

        formState.wdDegree = 0
        formState.wdMinute = 0
        formState.wdSecond = 0
      }

      break
    default:
      // 十进制
      formState.jd = mapWork.marsUtilFormtNum(currJD, 6) || 0
      formState.wd = mapWork.marsUtilFormtNum(currWD, 6) || 0
      break
  }
}
const bindMourseClick = () => {
  mapWork.bindMourseClick()
}

const submitCenter = () => {
  if (formState.jd > 180 || formState.jd < -180) {
    $alert("请输入有效的经度值！")
    return
  }
  if (formState.wd > 90 || formState.wd < -90) {
    $alert("请输入有效的纬度值！")
    return
  }

  // 控制台打印坐标定位时提交的点坐标
  console.log("坐标定位时提交的点坐标:", { lng: currJD, lat: currWD, alt: currGD })

  // 先判断历史记录是否有当前经纬度的值，有的话直接定位，没有的话先添加到历史记录再定位
  const historyPointData = analysiaStore.analysisSyncArray.find((item) => {
    if (item.analysisType === "zbdw_point") {
      const lng = item.data.data.position[0]
      const lat = item.data.data.position[1]
      // 使用容差比较浮点数，避免精度问题
      const tolerance = 0.000001
      if (Math.abs(lng - currJD) < tolerance && Math.abs(lat - currWD) < tolerance) {
        mapWork1.map.flyToPoint({ lng: currJD, lat: currWD, alt: currGD })
        return item
      }
    }
    return undefined
  })

  // const historyData = mapWork1.map.getHistoryData()
  // const historyPoint = historyData.find((item) => item.lng === currJD && item.lat === currWD)
  if (historyPointData) {
    mapWork1.map.flyToPoint(historyPointData.data.data.position)
  } else {
    mapWork1.map.flyToPoint({ lng: currJD, lat: currWD, alt: currGD })
    mapWork.updateMarker(true, currJD, currWD, currGD)
  }
}
const registerTransform = () => {
  Object.assign(formState, {
    radioFanwei: "1",
    radioFendai: "2",
    jd: 0,
    wd: 0,
    alt: 0,
    jdDegree: 0,
    jdMinute: 0,
    jdSecond: 0,
    wdDegree: 0,
    wdMinute: 0,
    wdSecond: 0
  })

  // 重置全局变量
  currJD = 0
  currWD = 0
  currGD = 0
}
</script>
<style lang="less" scoped>
:deep(.ant-radio-wrapper span) {
  font-size: 12px !important;
}
</style>
