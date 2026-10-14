<template>
  <div class="toolbox-container-form toolbox">
    <a-form :model="state.formData">
      <a-form-item label="模型高度(m)" name="height" style="margin: 0.5rem 0" :label-col="{ span: 8 }" :wrapper-col="{ span: 16 }" >
        <div style="display: flex; align-items: center;">
          <a-space>
          <mars-input-number v-model:value="state.formData.height" @blur="handleChangeHeight"></mars-input-number>
          <div class="tools_btns" style="margin-left: auto;">
            <mars-button class="btn sd_draw" @click="draw"> 手动绘制 </mars-button>
          </div>
        </a-space>
        </div>
      </a-form-item>
      <a-form-item label="模型目录选择" :label-col="{ span: 8 }" :wrapper-col="{ span: 16 }">
        <mars-select
          v-model:value="state.formData.folderid"
          :options="state.categoryTreeData"
          tree-default-expand-all
          placeholder="请选择模型目录"
          allow-clear
          :fieldNames="{ label: 'name', value: 'id', children: 'children' }"
          style="width: 100%"
          @change="handleCategoryChange"
        />
      </a-form-item>
    </a-form>

    <!-- 模型列表区域 -->
    <div class="model-list-section">
      <a-tabs v-model:activeKey="state.activeTab" @change="handleTabChange">
        <a-tab-pane key="built-in" tab="内置模型">
          <div class="model-grid" v-if="state.builtInModels.length > 0">
            <div
              class="model-card"
              v-for="model in state.builtInModels"
              :key="model.id"

            >
              <div class="model-card__img-wrap">
                <img :src="model.thumburl ? (state.apiurl + '/' + model.thumburl) : defaultThumb" :alt="model.modeltitle" class="model-card__img" />
                <div class="model-card__actions">
                  <mars-button type="primary" size="small" @click.stop="handleDefaultView(model)">默认视角</mars-button>
                  <mars-button size="small" @click.stop="rePickModel(model)">重新拾取</mars-button>
                </div>
              </div>
              <div class="model-card__name" :title="model.modeltitle">
                {{ model.modeltitle }}
              </div>
            </div>
          </div>
          <div v-else class="model-grid__empty">暂无模型数据</div>
        </a-tab-pane>
        <a-tab-pane key="uploaded" tab="自定义模型">
          <div class="model-grid" v-if="state.uploadedModels.length > 0">
            <div
              class="model-card"
              v-for="model in state.uploadedModels"
              :key="model.id"
            >
              <div class="model-card__img-wrap">
                <img :src="model.thumburl ? (state.apiurl + '/' + model.thumburl) : defaultThumb" :alt="model.modeltitle" class="model-card__img" />
                <div class="model-card__actions">
                  <mars-button type="primary" size="small" @click.stop="handleDefaultView(model)">默认视角</mars-button>
                  <mars-button size="small" @click.stop="rePickModel(model)">重新拾取</mars-button>
                </div>
              </div>
              <div class="model-card__name" :title="model.modeltitle">{{ model.modeltitle }}</div>
            </div>
          </div>
          <div v-else class="model-grid__empty">
            暂无自定义模型
          </div>
        </a-tab-pane>
      </a-tabs>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted, reactive } from "vue"
import * as mapWork from "./map"
import { useWidget } from "@mars/common/store/widget"
import { useAnalysisStore } from "@mars/stores/analysis"
import { $message } from "@mars/components/mars-ui"
import { fetchBuiltInModels, getModelFolderTree } from "@mars/api/models"
import defaultThumb from "@mars/assets/icons/slt_loudong.png"


const { activate, disable, isActivate } = useWidget()
const store = useAnalysisStore()


// TODO: 定义模型数据类型接口
interface ModelItem {
  id: string
  modeltitle: string
  thumburl: string
  modelurl: string
  latitude?: number
  longitude?: number
  elevation?: number
  pitchangle?: number
  direction?: number
  sightheight?: number
}


const state = reactive({
  formData: {
    height: 20,
    folderid: undefined as string | undefined,
    isinside: true
  },
  apiurl: import.meta.env.VITE_API_URL,
  activeTab: "built-in",
  // TODO: 从接口获取树形分类数据
  categoryTreeData: [] as any[],
  // TODO: 从接口获取内置模型列表
  builtInModels: [] as ModelItem[],
  // TODO: 根据选中的分类获取上传模型列表
  uploadedModels: [] as ModelItem[],
  selectedModel: null as ModelItem | null
})

const handleChangeHeight = () => {
  disable("graphic-options")
  mapWork.updateModelHeight(+state.formData.height)
}

const draw = () => {
  if (!state.selectedModel) {
    $message("请先选择一个模型", "warning")
    return
  }
  // TODO: 将选中模型信息(state.selectedModel)传递给绘制逻辑，需扩展mapWork.startDraw方法
  mapWork.startDraw(String(state.formData.height))
}

// 模型分类切换
const handleCategoryChange = (value: string) => {
  state.formData.folderid = value
  state.activeTab = "uploaded"
  loadBuiltInModels(value, false)
}

// Tab 切换
const handleTabChange = (key: string) => {
  state.activeTab = key
  if (key === "uploaded") {
    loadBuiltInModels(state.formData.folderid, false)
  } else if (key === "built-in") {
    loadBuiltInModels(state.formData.folderid, true)
  }
}

// 选择模型
const selectModel = (model: any) => {
  // 取消之前的绘制/加载
  mapWork.cancelCurrentDraw()
  state.selectedModel = model
  if (!model?.modelurl) {
    $message("该模型缺少URL信息", "warning")
    return
  }
  // 加载模型到地图的逻辑，需扩展mapWork.startDraw方法以支持传入模型信息
  mapWork.startDrawModel({ url: model.modelurl })
}

// 默认视角：根据模型的默认参数定位加载模型并 flyTo
const handleDefaultView = (model: ModelItem) => {
  if (!model?.modelurl) {
    $message("该模型缺少URL信息", "warning")
    return
  }
  if (!model.latitude || !model.longitude) {
    $message("该模型缺少默认视角参数", "warning")
    return
  }
  state.selectedModel = model
  console.log("加载模型默认视角", model)
  mapWork.loadModelWithDefaultView({
    url: model.modelurl,
    latitude: model.latitude,
    longitude: model.longitude,
    elevation: model.elevation || 0,
    pitchangle: model.pitchangle || -45,
    direction: model.direction || 0,
    sightheight: model.sightheight || 0
  })
}

// 重新拾取：保持原有逻辑不变
const rePickModel = (model: ModelItem) => {
  selectModel(model)
}



// 打开属性面板
const openGraphicPanel = (data: { layerId: string; graphicId: string }) => {
  if (isActivate("graphic-options")) {
    disable("graphic-options")
    setTimeout(() => {
      activate({ name: "graphic-options", data, reload: true })
    }, 100)
  } else {
    activate({ name: "graphic-options", data, reload: true })
  }
}

const drawEndHandler = (event: any) => {
  const eventData = event.data
  // 按 graphic id / layerId 去重（每次绘制产生唯一 id）
  const dataId = eventData?.data?.id || eventData?.data?.layerId
  if (!dataId) {
    console.warn("drawEnd 缺少标识ID", eventData)
    return
  }
  const isExist = store.analysisSyncArray.find(
    (item: any) => item.analysisType === "zxxz_online" && (
      item.data?.data?.id === dataId || item.data?.data?.layerId === dataId
    )
  )
  if (isExist) {
    $message("该数据已加载，请勿重复添加", "warning")
    return
  }
  store.saveAnalysisDataToSelected({
    analysisType: event.analysisType,
    data: eventData
  })
}

// 模型编辑后同步属性到 store 中对应的条目
const graphicEditHandler = (event: any) => {
  const graphic = event.graphic || event.layer
  if (!graphic) { return }

  // 区分 primitive model 和 tileset layer
  const isTilesetLayer = !!event.layer
  const graphicJson = isTilesetLayer ? graphic.toJSON({ full: true }) : graphic.toJSON()
  console.log("模型属性更新", isTilesetLayer ? "3D Tiles" : "单体模型", graphicJson)

  // 在 analysisSyncArray 中找到对应条目并更新 data
  const dataId = graphicJson.id || graphicJson.layerId
  let item = store.analysisSyncArray.find((item: any) =>
    item.analysisType === "zxxz_online" && (
      item.data?.data?.id === dataId ||
      item.data?.data?.layerId === dataId
    )
  )

  if (!item && !isTilesetLayer) {
    // 单体模型备选匹配：通过 position 坐标匹配
    item = store.analysisSyncArray.find((item: any) => {
      if (item.analysisType !== "zxxz_online") { return false }
      const storedData = item?.data?.data
      if (!storedData) { return false }
      return (
        storedData.type === "model" &&
        storedData.position?.lng === graphicJson.position?.lng &&
        storedData.position?.lat === graphicJson.position?.lat
      )
    })
  }

  if (item) {
    item.data.data = graphicJson
  }
  // 同步更新 clickedRow（当前选中行数据）
  if (store.clickedRow?.data?.data?.id === dataId || store.clickedRow?.data?.data?.layerId === dataId) {
    store.setCurrentAnalysisData({
      ...store.clickedRow,
      data: { ...store.clickedRow.data, data: graphicJson }
    })
  } else if (item && store.clickedRow?.id === item.id) {
    store.setCurrentAnalysisData({
      ...store.clickedRow,
      data: { ...store.clickedRow.data, data: graphicJson }
    })
  }

  console.log("更新后 store.analysisSyncArray", store.analysisSyncArray)
}
onMounted(() => {
  mapWork.eventTarget.on("drawCreatedForPanel", openGraphicPanel)
  mapWork.eventTarget.on("drawEnd", drawEndHandler)
  mapWork.eventTarget.on("zxxzGraphicEditorUpdate", graphicEditHandler)
  mapWork.eventTarget.on("zxxzTilesetUpdate", graphicEditHandler)
  // TODO: 初始化加载模型分类树和内置模型列表
  initModelData()
  loadBuiltInModels(state.formData.folderid, true)
})

// 初始化模型数据
const initModelData = async () => {
  // 获取目录树
  const modelListData = await getModelFolderTree()
  state.categoryTreeData = modelListData.data.result || []

}

const loadBuiltInModels = async (folderid?: string, isinside?: boolean) => {
  const res = await fetchBuiltInModels({ folderid, isinside })
  if (state.activeTab === "built-in") {
    state.builtInModels = res.data?.result || []
  } else {
    state.uploadedModels = res.data?.result || []
  }
}


onUnmounted(() => {
  // 清除鼠标绑定事件
  mapWork.cancelDraw()
  // 移除属性面板监听
  mapWork.eventTarget.off("drawCreatedForPanel", openGraphicPanel)
  mapWork.eventTarget.off("drawEnd", drawEndHandler)
  mapWork.eventTarget.off("zxxzGraphicEditorUpdate", graphicEditHandler)
  mapWork.eventTarget.off("zxxzTilesetUpdate", graphicEditHandler)
  // 退出模型编辑状态
  // mapWork.stopModelEdit()
})
</script>

<style lang="less" scoped>
.model-list-section {
  margin-top: 0.5rem;

  :deep(.ant-tabs-nav) {
    margin-bottom: 0.5rem;
    width: 100%;
  }

  :deep(.ant-tabs-nav-list) {
    width: 100%;
  }

  :deep(.ant-tabs-tab) {
    flex: 1;
    justify-content: center;
  }
}
.model-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 0.3rem;
  max-height: 320px;
  overflow-y: auto;
  overflow-x: hidden;
  padding: 0.25rem 0.15rem;

  .model-card {
    height: 100px;
  }
}

.model-card {
  border-radius: 4px;
  cursor: pointer;
  border: 1px solid rgba(33, 76, 249, 0.588);
  transition: all 0.2s ease;
  text-align: center;
  position: relative;
  overflow: hidden;
  min-width: 0;
  display: flex;
  flex-direction: column;

  &:has(.model-card__img-wrap:hover) {
    box-shadow: inset 0 0 8px #1890ff, 0 0 8px rgba(24, 144, 255, 0.6);

    .model-card__actions {
      opacity: 1;
      pointer-events: auto;
    }
  }

  &--selected {
    box-shadow: inset 0 0 8px #1890ff, 0 0 8px rgba(24, 144, 255, 0.6);
  }

  &__img-wrap {
    cursor: pointer;
    flex: 1;
    min-height: 0;
    overflow: hidden;
  }

  &__img {
    width: 100%;
    height: 100%;
    display: block;
    border-radius: 3px 3px 0 0;
  }

  &__name {
    font-size: 12px;
    color: #ccc;
    padding: 3px 2px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    flex-shrink: 0;
    border-top: 1px solid rgba(33, 76, 249, 0.588);
  }

  &__actions {
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 4px;
    background: rgba(0, 0, 0, 0.5);
    opacity: 0;
    pointer-events: none;
    transition: opacity 0.2s ease;
    border-radius: 3px;

    :deep(.ant-btn) {
      font-size: 11px;
      padding: 1px 8px;
      height: 22px;
      line-height: 20px;
      border-radius: 3px;
      border-width: 1px;
    }
  }
}

.model-grid__empty {
  text-align: center;
  padding: 2rem 0;
  color: #999;
  font-size: 13px;
}
</style>
