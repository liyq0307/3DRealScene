<template>
  <mars-dialog ref="toolbarRef" :visible="isToolboxVisible" width="274" :max-width="274" right="34" top="0" bottom="30"
    customClass="tool-dialog" :nopadding="true" :closeButton="false" :handles="false" :animation="false"
    :defaultFold="false" :thumbnail="{ icon: 'tool' }">
    <!-- <template #title>
      <div class="title">
        工具箱
        <mars-icon class="closeBth" icon="bookmark-one" :width="24" @click="toggleBtn" />
      </div>
    </template> -->
    <div class="toolbox-container">
      <div class="module" v-for="module in moduleData" :key="module.key">
        <div class="module-header" @click="toggleModule(module.key)">
          <span class="module-title">{{ module.title }}</span>
          <span class="arrow" :class="{ expanded: modules[module.key] }">
            <DownOutlined />
          </span>
        </div>
        <div class="module-content" v-show="modules[module.key]">
          <div class="button-grid">
            <mars-button class="tool-button" v-show="action.show"
              :class="{ 'full-width': action?.fullWidth, active: moduleActions[module.key] === action.analysisType }"
              v-for="action in module.children" :key="action.name"
              @click="handleAction(module.key, action.name, action.analysisType)">
              <template #icon>
                <mars-icon :icon="action.icon" :width="18" color="#e2e8f0" class="tool-icon" />
              </template>
              {{ action.name }}
            </mars-button>
          </div>
          <div class="component-container" v-if="moduleActions[module.key]">
            <div>
              <!-- 坐标定位 -->
              <zbdw v-if="moduleActions[module.key] === 'zbdw_point'" />
              <!-- 压平功能 -->
              <ypgn v-if="moduleActions[module.key] === 'yp_ground'" />
              <!-- 在线选址 -->
              <zxxz v-if="moduleActions[module.key] === 'zxxz_online'" />
              <!-- 周边地块限制性分析 -->
              <zbdkxzx v-if="moduleActions[module.key] === 'zbdkxzx_analysis'" />
              <!-- 业态分析 -->
              <ytfx v-if="moduleActions[module.key] === 'yt_analysis'" />
              <!-- 特种设备分析 -->
              <!-- 通视分析 -->
              <tsfx v-if="moduleActions[module.key] === 'ts_analysis'" />
              <!-- 天际线分析 -->
              <tjxfx v-if="moduleActions[module.key] === 'tjx_analysis'" />

              <!-- 日照分析 -->
              <rzfx v-if="moduleActions[module.key] === 'rx_analysis'" />
              <!-- 等高线分析 -->
              <dgxfx v-if="moduleActions[module.key] === 'dgx_analysis'" />
              <!-- 建筑布局分析 -->
              <jzbjfx v-if="moduleActions[module.key] === 'jzbj_analysis'" />
              <!-- 容积率分析 -->
              <rjlfx v-if="moduleActions[module.key] === 'rjl_analysis'" />
              <!-- 剖面分析 -->
              <pmfx v-if="moduleActions[module.key] === 'pmfx_analysis'" />
              <!-- 挖方填方分析 -->
              <wftffx v-if="moduleActions[module.key] === 'wftffx_analysis'" />
              <!-- 卷帘对比 -->
              <jldb v-if="moduleActions[module.key] === 'jlzd_comparison'" />
              <!-- 市政三维设计 -->
              <szswsj v-if="moduleActions[module.key] === 'szs3d_design'" />
              <!-- 塔基建模 -->
              <tjjm v-if="moduleActions[module.key] === 'tjjm_pole'" />
              <!-- 管线分析 -->
              <gxfx v-if="moduleActions[module.key] === 'gxfx_pipe'" />
              <!-- 图上标绘 -->
              <tsbh v-if="moduleActions[module.key] === 'basic_draw'" />
              <!-- 多建筑间距分析 -->
              <djzjjfx v-if="moduleActions[module.key] === 'djzjx_analysis'" />
              <!-- 观测台 -->
              <gctfx v-if="moduleActions[module.key] === 'gct_point'" />
            </div>
          </div>
        </div>
      </div>
      <div class="btn-container">
        <mars-button class="ant-btn-zdy" @click="downloadMapImg">场景出图</mars-button>
        <!-- <mars-button danger @click ="clearDrawings">清除</mars-button> -->
        <mars-button @click="saveMapData" v-auth="'csgh_kshfx/add'">场景保存</mars-button>
      </div>
    </div>

    <!-- 保存弹窗 -->
    <SaveDialog ref="saveDialogRef" @handleSaveSuccess="handleSaveSuccess" @handleSaveCancel="handleSaveCancel" />
  </mars-dialog>
</template>

<script lang="ts" setup>
import { reactive, computed, ref, onMounted, onUnmounted } from "vue"
import zbdw from "@mars/widgets/analysis/tools/location/index.vue"
import ypgn from "@mars/widgets/analysis/tools/swipe/index.vue"
import zxxz from "@mars/widgets/analysis/tools/site-selection/index.vue"
import zbdkxzx from "@mars/widgets/analysis/tools/restriction/index.vue"
import ytfx from "@mars/widgets/analysis/tools/format/index.vue"
import tsfx from "@mars/widgets/analysis/tools/visibility/index.vue"
import tjxfx from "@mars/widgets/analysis/tools/skyline/index.vue"
import rzfx from "@mars/widgets/analysis/tools/sunlight/index.vue"
import dgxfx from "@mars/widgets/analysis/tools/contour/index.vue"
import jzbjfx from "@mars/widgets/analysis/tools/building-layout/index.vue"
import rjlfx from "@mars/widgets/analysis/tools/floor-area-ratio/index.vue"
import pmfx from "@mars/widgets/analysis/tools/profile/index.vue"
import wftffx from "@mars/widgets/analysis/tools/cut-fill/index.vue"
import jldb from "@mars/widgets/analysis/tools/building-comparison/index.vue"
import szswsj from "@mars/widgets/analysis/tools/civic-3d/index.vue"
import tjjm from "@mars/widgets/analysis/tools/tower-modeling/index.vue"
import gxfx from "@mars/widgets/analysis/tools/pipeline/index.vue"
import tsbh from "@mars/widgets/analysis/tools/drawing/index.vue"
import djzjjfx from "@mars/widgets/analysis/tools/building-spacing/index.vue"
import gctfx from "@mars/widgets/analysis/tools/viewpoint/index.vue"
import SaveDialog from "./components/SaveDialog.vue"

import { DownOutlined } from "@ant-design/icons-vue"
import { moduleData } from "./moduleData"
import * as mapWork from "./map"
import {
  measureAngle,
  measureArea,
  measureHeight,
  measureLength,
  measurePoint,
  measureSurfaceeArea,
  measureSurfaceLength,
  measureTriangleHeight
} from "@mars/widgets/analysis/tools/measurement/map"
import { useToolboxStore } from "@mars/stores/toolbox"
import { useAnalysisStore } from "@mars/stores/analysis"
import { $message } from "@mars/components/mars-ui"
import { eventTarget as fxEvent, map, measure } from "@mars/widgets/analysis/map"
import { useWidget } from "@mars/common/store/widget"

const { disable } = useWidget()
const toolbarRef = ref()

const store = useAnalysisStore()
// 保存弹窗可见性
const saveDialogRef = ref<any>(null)

// 备份保存前的模块选中状态（用于取消时恢复）
const backupModuleActions = ref<Record<string, string> | null>(null)
// 备份保存前地图上打开popup的graphic列表（用于取消时恢复）
const backupPopupGraphics = ref<any[] | null>(null)

// 测量是否正在进行中
const isMeasuring = ref(false)

// 使用工具箱store
const toolboxStore = useToolboxStore()

// 工具箱可见状态
const isToolboxVisible = computed(() => toolboxStore.isToolboxVisible)

// 切换工具箱显示/隐藏
const toggleToolbox = () => {
  toolboxStore.toggleToolbox()
}
// 弹框展开关闭
const toggleBtn = () => {
  toolbarRef.value?.toggle()
}
// 每个模块当前激活的操作
const moduleActions = reactive({
  basicTools: "",
  measurement: "",
  sitePlanning: "",
  spatialEnvironment: "",
  engineeringAnalysis: "",
  designComparison: ""
})

// 模块展开/收起状态
const modules = reactive({
  basicTools: true,
  measurement: true,
  sitePlanning: true,
  spatialEnvironment: true,
  engineeringAnalysis: true,
  designComparison: true
})

// 切换模块展开/收起
const toggleModule = (moduleName: string) => {
  modules[moduleName as keyof typeof modules] = !modules[moduleName as keyof typeof modules]
}

// 清除所有鼠标绑定事件（不清除已绘制的内容）
const clearAllMouseEvents = () => {
  // 停止测量工具当前正在进行的绘制操作
  if (measure?.graphicLayer) {
    measure.graphicLayer.clearDrawing()
  }
  // 停止 graphicLayer 当前正在进行的绘制操作
  if (map?.graphicLayer) {
    map.graphicLayer.clearDrawing()
  }
}

// 处理按钮点击事件
const handleAction = (moduleKey: string, actionName: string, analysisType: string) => {
  // 清除所有鼠标绑定事件
  clearAllMouseEvents()

  // 将属性面板关掉
  // 关闭图形属性面板
  disable("graphic-options")

  // 记录之前的选中状态
  const previousAction = moduleActions[moduleKey as keyof typeof moduleActions]
  console.log("moduleKey:", moduleKey, "previousAction:", previousAction, "analysisType:", analysisType)
  // 如果点击的是同一个按钮,则取消选中
  if (previousAction === analysisType) {
    moduleActions[moduleKey as keyof typeof moduleActions] = ""
    // 如果是测量模块，取消选中时清除绘图
    // if (moduleKey === "measurement") {
    //   mapWork.clearDrawings()
    // }
    return
  } else {
    // 图上标绘取消选中时清除绘图
    if (analysisType === "basic_draw") {
      store.clearBasicDrawData()
    }

    // 当从塔基建模切换到其他模块时，清空塔基建模数据
    if (previousAction === "tjjm_pole" && analysisType !== "tjjm_pole") {
      store.clearTjjmPoleData()
    }

    // 清空所有模块的选中状态
    Object.keys(moduleActions).forEach((key) => {
      moduleActions[key as keyof typeof moduleActions] = ""
    })
    // 只选中当前点击的按钮,存储 analysisType
    moduleActions[moduleKey as keyof typeof moduleActions] = analysisType
  }

  if (moduleKey === "measurement") {
    switch (analysisType) {
      // 空间距离
      case "kjjl_distance":
        measureLength()
        break
      // 贴地距离
      case "tdjl_distance":
        measureSurfaceLength()
        break
      // 高度差
      case "gdc_height":
        measureHeight()
        break
      // 水平面积
      case "spmj_area":
        measureArea()
        break
      // 贴地面积
      case "tdmj_area":
        measureSurfaceeArea()
        break
      // 坐标测量
      case "zbcl_point":
        measurePoint()
        break
      // 三角测量
      case "sjcl_triangle":
        measureTriangleHeight()
        break
      // 方位角
      case "fwj_direction":
        measureAngle()
        break
    }
  }
}

// 场景出图
const downloadMapImg = () => {
  mapWork.downloadMapImg()
}

// 场景保存
const saveMapData = () => {
  // 检查是否有正在进行绘制操作（测量工具 或 图上标绘 等），如有则提示先完成

  if (isMeasuring.value) {
    $message("请先完成当前绘制操作")
    return
  }

  try {
    // 检查是否有可保存的分析数据 或 多视角数据
    const hasAnalysisData = store.analysisSyncArray.length > 0
    const hasGctData = store.gctData && store.gctData.length > 0

    if (hasAnalysisData) {
      // 备份当前所有模块的选中状态
      backupModuleActions.value = JSON.parse(JSON.stringify(moduleActions))
      // 备份当前地图上打开popup的graphic（用于取消时恢复）
      if (map.graphicLayer) {
        const graphics = map.graphicLayer.getGraphics()
        backupPopupGraphics.value = graphics.filter((g: any) => g.hasPopup && g.isPopupShow)
      }
      // 关闭地图上所有的popup（包括容积率的 bindPopup 等）
      map.closePopup()
      // 关闭所有分析组件的popup面板
      Object.keys(moduleActions).forEach((key) => {
        moduleActions[key as keyof typeof moduleActions] = ""
      })
      saveDialogRef.value?.openDialog()
    } else {
      $message("没有可保存的数据")
    }
  } catch (error) {
    console.error("图上未标绘完成，请稍等:", error)
    $message("图上未标绘完成，请稍等")
  }
}

// 取消保存回调（恢复原样）
const handleSaveCancel = () => {
  if (backupModuleActions.value) {
    // 恢复之前的模块选中状态
    Object.keys(backupModuleActions.value).forEach((key) => {
      moduleActions[key as keyof typeof moduleActions] = backupModuleActions.value![key]
    })
    backupModuleActions.value = null
  }
  // 恢复地图上之前打开的popup
  if (backupPopupGraphics.value && backupPopupGraphics.value.length > 0) {
    backupPopupGraphics.value.forEach((g: any) => {
      g.openPopup()
    })
    backupPopupGraphics.value = null
  }
}

// 保存成功回调
const handleSaveSuccess = (data: any) => {
  mapWork.clearDrawings()
  backupModuleActions.value = null
  backupPopupGraphics.value = null
}

// 测量开始事件回调
const handleMeasureStart = () => {
  isMeasuring.value = true
}

// 测量完成事件回调（绘制完成后取消选中样式）
const handleMeasureEnd = () => {
  isMeasuring.value = false
  moduleActions.measurement = ""
}

// 监听清除所有store数据事件
onMounted(() => {
  fxEvent.on("clearAllStoreData", () => {
    store.clearAllData()
  })
  // 监听测量开始事件
  fxEvent.on("measureStart", handleMeasureStart)
  // 监听测量完成事件
  fxEvent.on("measureEnd", handleMeasureEnd)
})

// 组件销毁时清除所有鼠标绑定事件
onUnmounted(() => {
  clearAllMouseEvents()
})
</script>

<style lang="less" scoped>
.title {
  font-size: 16px;
  position: relative;

  .closeBth {
    position: absolute;
    right: 4px;
    top: 8px;
    cursor: pointer;
  }
}

:deep(.mars-dialog.tool-dialog) {
  border: 1px solid #334155;
  max-height: calc(100vh - 80px);
  overflow-y: auto;
  position: relative;
  background: #0f172a;

  .mars-dialog__content {
    background: transparent;
  }
}

/* 自定义滚动条样式 */
:deep(.mars-dialog.tool-dialog)::-webkit-scrollbar {
  width: 6px;
}

:deep(.mars-dialog.tool-dialog)::-webkit-scrollbar-track {
  background: #1e293b;
  border-radius: 3px;
}

:deep(.mars-dialog.tool-dialog)::-webkit-scrollbar-thumb {
  background: #475569;
  border-radius: 3px;
}

:deep(.mars-dialog.tool-dialog)::-webkit-scrollbar-thumb:hover {
  background: #6366f1;
}

.toolbox-container {
  padding: 0;
  color: #f1f5f9;
  background: transparent;
  padding-bottom: 60px;
}

.module:last-child {
  border-bottom: none;
}

.module-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 16px;
  background: #1e293b;
  border-top: solid 1px #334155;
  cursor: pointer;

  &:hover {
    background: #334155;
  }
}

.module-title {
  font-size: 14px;
  font-weight: 600;
  color: #e2e8f0;
  letter-spacing: 0.3px;
}

.arrow {
  font-size: 12px;
  color: #e2e8f0;
  transition: transform 0.3s ease;
  display: inline-flex;
  align-items: center;
}

.arrow.expanded {
  transform: rotate(180deg);
}

.module-content {
  padding: 8px;
  background-color: #0f172a;
}

.button-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 6px;
}

.tool-button {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 4px;
  padding: 8px 2px !important;
  min-height: 60px;
  height: auto !important;
  background-color: rgba(30, 41, 59, 0.8) !important;
  border: 1px solid #334155;
  border-radius: 6px;
  transition: all 0.2s ease;
  font-size: 13px !important;
  font-weight: 500;
  line-height: 1.3;
  color: #e2e8f0 !important;
  box-shadow: none !important;
}

.tool-button :deep(.anticon) {
  margin: 0 !important;
  padding: 0 !important;
  line-height: 1;
}

.tool-button :deep(.tool-icon),
.tool-button :deep(.mars-icon) {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  line-height: 1;
  padding: 0 !important;
}

.tool-button :deep(.tool-icon svg),
.tool-button :deep(.mars-icon svg) {
  display: block;
}

.mars-button.tool-button:hover {
  background-color: rgba(99, 102, 241, 0.2) !important;
  border-color: #6366f1;
  transform: translateY(-1px);
}

.tool-button:active,
.tool-button.active {
  background-color: #6366f1 !important;
  border-color: #6366f1;
  box-shadow: 0 0 12px rgba(99, 102, 241, 0.4);
  transform: translateY(0);
  color: #ffffff !important;
}

.component-container {
  margin-top: 8px;
  padding: 10px 8px;
  background-color: rgba(30, 41, 59, 0.5);
  border-radius: 6px;
  border: 1px solid #334155;

  :deep(.ant-form-item) {
    margin-bottom: 10px;
  }

  :deep(.ant-form-item-label) {
    overflow: visible;
    white-space: nowrap;
    padding-right: 6px;

    > label {
      color: #e2e8f0 !important;
      font-size: 12px;
      height: auto;
      line-height: 1.4;
      width: 100%;
      justify-content: flex-start;

      &::after {
        display: none;
      }
    }
  }

  :deep(.ant-form-item-control),
  :deep(.ant-form-item-control-input-content),
  :deep(.ant-radio-wrapper),
  :deep(.ant-checkbox-wrapper),
  :deep(.ant-form-text) {
    color: #f1f5f9;
  }
}

.btn-container {
  position: absolute;
  bottom: -15px;
  left: 0;
  right: 0;
  display: flex;
  gap: 12px;
  padding: 12px 16px;
  border-top: 1px solid #334155;
  background: #0f172a;
  z-index: 10;
}

.btn-container .mars-button {
  flex: 1;
}

.anticon svg {
  font-size: 12px;
}
</style>
