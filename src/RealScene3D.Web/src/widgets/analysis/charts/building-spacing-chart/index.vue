<template>
  <div class="data-display mars-main-view">
    <div class="btn" v-if="hasAnyData">
      <mars-button class="export-btn" @click="handleExportExcel">导出 excel</mars-button>
    </div>
    <div class="tab-content-scrollable">
      <div class="table-section">
        <a-table bordered :columns="columns" :dataSource="data.analysisData" :pagination="false" :scroll="{y: 400}" size="small" />
      </div>
    </div>
  </div>
</template>

<script lang="ts" setup>
import { ref, computed } from "vue"
import * as XLSX from "xlsx"

interface AnalysisDataItem {
  sourceZRZH: string
  nearestZRZH: string
  distance?: number
}

interface Props {
  data: { analysisData: AnalysisDataItem[] }
}

const props = withDefaults(defineProps<Props>(), { data: () => ({ analysisData: [] }) })

const hasAnyData = computed(() => props.data.analysisData && props.data.analysisData.length > 0)

const columns = ref([
  { title: "序号", width: 60, key: "index", align: "center", customRender: ({ index }: any) => index + 1 },
  { title: "楼栋号", dataIndex: "sourceZRZH", align: "center", key: "sourceZRZH" },
  { title: "临近楼栋号", dataIndex: "nearestZRZH", key: "nearestZRZH", align: "center" },
  { title: "间距 (m)", dataIndex: "distance", width: 100, key: "distance", align: "center" }
])

const handleExportExcel = () => {
  const data = [["序号", "楼栋号", "临近楼栋号", "距离 (m)"], ...props.data.analysisData?.map((item, i) => [i + 1, item.sourceZRZH, item.nearestZRZH, item?.distance ?? ""])]
  if (data.length === 0) return
  const wb: XLSX.WorkBook = XLSX.utils.book_new()
  if (data.length > 1) {
    XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(data), "sheet1")
  }
  XLSX.writeFile(wb, "间距分析.xlsx")
}
</script>

<style lang="less" scoped>
.data-display { width: 100%; max-height: 500px; display: flex; flex-direction: column; overflow: hidden; }
.btn { display: flex; justify-content: flex-end; margin-bottom: 10px; }
</style>
