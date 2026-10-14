 <template>
  <a-modal
    v-model:open="modalVisible"
    title="保存分析数据"
    :closeable="true"
    :mask-closable="false"
    cancel-text="取消"
    width="40%"
    ok-text="确定"
    @ok="handleSubmit"
    @cancel="handleCancel"
  >
    <div class="save-dialog-content">
      <!-- 项目选择 -->
      <a-form layout="horizontal" :model="state.queryParams" ref="formRef" :rules="rules" :label-col="{ span: 4 }" :wrapper-col="{ span: 20 }" >
        <a-form-item label="年份" name="year">
           <mars-date-picker v-model:value="state.queryParams.year" picker="year" format="YYYY" placeholder="请选择年份" @change="handleFilterChange" />
        </a-form-item>
        <a-form-item label="项目名称" name="projectId" >
          <mars-select
            v-model:value="state.queryParams.projectId"
            :options="state.projectList"
            placeholder="请选择项目名称"
          />
        </a-form-item>
        <a-form-item label="场景名称" name="sceneName" required>
          <mars-input v-model:value="state.queryParams.sceneName" @input="state.queryParams.sceneName = state.queryParams.sceneName.replace(/\s+/g, '')" @keydown.space.prevent placeholder="请输入场景名称" />
        </a-form-item>
        <a-form-item label="备注" name="bz">
          <mars-textarea v-model:value="state.queryParams.bz" placeholder="请输入备注" />
        </a-form-item>
      </a-form>
    </div>


  </a-modal>
</template>

<script setup lang="ts">
import { ref, reactive } from "vue"
import { $message } from "@mars/components/mars-ui"
import { useAnalysisStore } from "@mars/stores/analysis"
import { getXmSelectWithYear } from "@mars/api/xmgl"
import dayjs from "dayjs"
import { useLayerStore } from "@mars/stores/layer"
import { saveAnalysisDataApi } from "@mars/api/cjgl"
import { map } from "@mars/widgets/analysis/map"
import { pipelineManager } from "@mars/widgets/analysis/tools/pipeline/map"
import { destroyControl } from "@mars/widgets/analysis/tools/building-comparison/map"
import { removeModel } from "../../tools/site-selection/map"
import { clearAllModels } from "../../model-tree/map"
const formRef = ref<any>()
const modalVisible = ref(false)
const rules = {
  sceneName: [
    {
      required: true,
      message: "请输入场景名称",
      trigger: "change"
    },
    {
      pattern: /^\S+$/,
      message: "场景名称不能包含空格",
      trigger: "change"
    }
  ]
}
// 定义事件
const emit = defineEmits(["handleSaveSuccess", "handleSaveCancel"])

const state = reactive({
  queryParams: {
    year: undefined,
    projectId: undefined,
    sceneName: "",
    bz: ""
  },
  projectList: [],
  yearList: []
})

// 响应式数据
const saving = ref(false)

const handleFilterChange = (value: any) => {
  console.log("年份变化:", value)
  // 清空项目选择
  state.queryParams.projectId = ""
  state.projectList = []
  // 重新请求项目列表
  getProjectListData()
}

const openDialog = () => {
  modalVisible.value = true
  // 初始化
  getProjectListData()

}


// 处理取消
const handleCancel = () => {
  formRef.value.resetFields()
  modalVisible.value = false
  emit("handleSaveCancel")
}


// 处理保存
const handleSubmit = () => {
  if (saving.value) {
    return
  }
  // 验证必填项
  formRef.value.validate().then(async () => {
    // 保存数据
    saving.value = true
    const analysisStore = useAnalysisStore()
    const layerStore = useLayerStore()
    console.log("模型数据:", analysisStore.selectedModelIds)
    console.log("图层数据:", layerStore.selectedLayers)
    console.log("视角数据:", analysisStore.gctData)
    console.log("分析同步数组:", analysisStore.analysisSyncArray)
    let modalData, layerData, analysisData, gctData
    // 模型数据
    if (analysisStore.selectedModelIds.length > 0) {
      modalData = analysisStore.selectedModelIds.map(id => {
        return {
          mxlx: "1",
          mxid: id
        }
      })
    }

    // 图层数据
    if (layerStore.selectedLayers.length > 0) {
      layerData = layerStore.selectedLayers.map(item => {
        return {
          mxlx: "0",
          mxid: item.id
        }
      })
    }
    // 分析数据
    if (analysisStore.analysisSyncArray.length === 0) {
      $message("请先进行分析", "warning")
      return
    } else {

      analysisData = analysisStore.analysisSyncArray.map(item => {
        console.log("当前item:", item)
        console.log("item.analysisType:", item.analysisType)
        console.log("item.data:", item.data)

        let jsonjg
        if (item.analysisType === "dgx_analysis") {
          // 对于等高线分析，从 item.data 中提取可序列化的字段
          const { name, data, shijiao, bbox, polygonGeometry, options } = item.data
          jsonjg = {
            name,
            analysisType: item.analysisType,
            shijiao: JSON.parse(JSON.stringify(shijiao)),
            bbox: JSON.parse(JSON.stringify(bbox)),
            polygonGeometry: JSON.parse(JSON.stringify(polygonGeometry)),
            data: JSON.parse(JSON.stringify(data)),
            options: JSON.parse(JSON.stringify(options))
          }
        } else if (item.analysisType === "ts_analysis" || item.analysisType === "basic_draw") {
          // 通视分析：提取可序列化字段，跳过mars3d图形对象
          const { name, analysisType, shijiao, data } = item.data
          console.log(item)
          jsonjg = {
            name,
            analysisType,
            data: JSON.parse(JSON.stringify(data)),
            shijiao: JSON.parse(JSON.stringify(shijiao))
          }
        } else if (item.analysisType === "jzbj_analysis") {
          // 建筑布局：提取可序列化字段，跳过非序列化的对象
          const { name, analysisType, shijiao, plot, data, buildings, buildingCount, totalBaseArea, totalBuildingArea } = item.data
          jsonjg = {
            name,
            analysisType,
            shijiao: JSON.parse(JSON.stringify(shijiao)),
            plot: JSON.parse(JSON.stringify(plot)),
            data,
            buildings,
            buildingCount,
            totalBaseArea,
            totalBuildingArea
          }
        } else {
          console.warn(`未知的分析类型 ${item.analysisType}，尝试直接序列化数据`)
          try {
            jsonjg = JSON.parse(JSON.stringify(item.data))
          } catch (e) {
            console.warn(`${item.analysisType} 数据序列化失败，使用安全字段`, e)
            jsonjg = { name: item.data.name, analysisType: item.analysisType }
          }
        }

        // 安全序列化 jsoncs
        let jsoncs: any = {}
        if (item.analysisType !== "tjjm_pole" && item.analysisType !== "dgx_analysis" && item.analysisType !== "basic_draw") {
          console.log("ssss", item.data.data)
          if (item.data.data) {
            if (typeof item.data.data !== "object") {
              jsoncs = item.data.data
            } else {
              try {
                jsoncs = JSON.parse(JSON.stringify(item.data.data))
              } catch (_e) {
                jsoncs = {}
              }
            }
          }
        }

        return {
          gnmc: item.data.name,
          lx: item.analysisType,
          jsoncs,
          jsonjg
        }
      })
    }

    console.log("analysisStore:", analysisData)
    // 视角数据
    if (analysisStore.gctData && analysisStore.gctData.length > 0) {
      gctData = analysisStore.gctData.map(item => {
        return {
          sjmc: item.name,
          cjsjjson: JSON.parse(JSON.stringify(item.value)),
          sfmr: item.sfmr
        }
      })
    }

    console.log("视角数据处理后:", gctData)

    // 如果视角数据有值且所有的sfmr都是false，则设置第一条数据的sfmr值为true
    if (gctData && gctData.length > 0 && gctData.every(item => item.sfmr === false)) {
      gctData[0].sfmr = true
    }
    try {
    // 构造保存数据
      const saveData = {
        xmid: state.queryParams.projectId || null,
        cjmc: state.queryParams.sceneName,
        bz: state.queryParams.bz,
        cjfxlist: analysisData,
        cjsjlist: gctData && gctData.length > 0
          ? gctData
          : [{
            sjmc: "默认",
            cjsjjson: JSON.parse(JSON.stringify(map.getCameraView())),
            sfmr: true
          }],
        cjmxlist: layerData && modalData ? layerData.concat(modalData) : (layerData || modalData)
      }

      console.log("保存数据:", JSON.stringify(saveData, null, 2))
      console.log("尝试序列化数据...")
      try {
        const serialized = JSON.stringify(saveData)
        console.log("序列化成功，数据长度:", serialized.length)
      } catch (serializeError) {
        console.error("序列化失败:", serializeError)
        $message("数据序列化失败，请检查数据格式", "error")
        return
      }

      // TODO: 调用后端接口保存数据
      console.log("开始调用保存API...")
      const response = await saveAnalysisDataApi(saveData)
      console.log("保存API响应:", response)

      // 模拟接口调用


      $message("保存成功", "success")
      modalVisible.value = false
      emit("handleSaveSuccess")
      analysisStore.clearSelectedModelsAnalysisData()
      analysisStore.clearGctData()
      formRef.value.resetFields()

      // 清除管线数据
      if (pipelineManager) {
        pipelineManager.clear()
        // analysisStore.clearGxfxPipeDataFromStore()
      }

      // 清除卷帘数据
      destroyControl()
      const jlzdIndex = analysisStore.analysisSyncArray.findIndex((item) => item.analysisType === "jlzd_comparison")
      if (jlzdIndex !== -1) {
        analysisStore.analysisSyncArray.splice(jlzdIndex, 1)
      }

      removeModel()


    } catch (error) {
      console.error("保存失败:", error)
    } finally {
      saving.value = false
    }
  }).catch((error) => {
    console.log("表单验证失败:", error)
  })
}
function getProjectListData() {
  if (state.queryParams.year) {
    const year = state.queryParams.year?.format("YYYY")
    getXmSelectWithYear({ year }).then((res) => {
      console.log("项目列表数据:", res)
      const data = res.data.result

      state.projectList = data.map(item => ({
        label: item.xmmc,
        value: item.id
      }))
    }).catch((error) => {
      console.error("获取项目列表失败:", error)
    })
  }
}

defineExpose({
  openDialog
})
</script>

<style scoped lang="less">
.save-dialog-content {
  padding: 10px 0;

  .selected-models-list {
    max-height: 200px;
    overflow-y: auto;
    border: 1px solid var(--mars-base-border);
    border-radius: 4px;
    padding: 8px;

    .model-item {
      padding: 8px;
      border-bottom: 1px solid var(--mars-base-border);

      &:last-child {
        border-bottom: none;
      }

      .model-name {
        font-size: 13px;
        color: var(--mars-text-color);
      }
    }
  }

  .no-models,
  .no-data {
    padding: 20px;
    text-align: center;
    color: var(--mars-text-color);
    background: var(--mars-dropdown-bg);
    border: 1px solid var(--mars-base-border);
    border-radius: 4px;
  }

  .analysis-data-preview {
    .data-summary {
      padding: 12px;
      background: var(--mars-dropdown-bg);
      border: 1px solid var(--mars-base-border);
      border-radius: 4px;
      font-size: 13px;
      color: var(--mars-text-color);
    }
  }
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  padding: 0 14px;
}
</style>
