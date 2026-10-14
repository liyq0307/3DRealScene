<template>
  <div class="data-display mars-main-view">
    <div class="zb_title" v-if="hasAnyData">
      <div>共 {{ validAnalysisItems.length }} 个图层的统计数据</div>
      <mars-button class="export-btn" @click="handleExportExcel">导出 excel</mars-button>
    </div>
    <a-tabs v-model:activeKey="activeTab" @change="handleTabChange">
      <a-tab-pane v-for="item in validAnalysisItems" :key="`analysis_${item.layerId}`" :tab="item.layerName">
        <div class="tab-content-fixed"><div class="chart-section"><h4 class="section-title">统计图表</h4><div :ref="(el) => setChartRef(item.layerId, el)" class="chart-container"></div></div></div>
        <div class="tab-content-scrollable"><div class="table-section"><h4 class="section-title">详细数据</h4><a-table bordered :columns="getColumns(item.statisticalResults)" :dataSource="getDataSource(item.statisticalResults)" :pagination="false" :scroll="{ x: 'max-content' }" size="small" /></div></div>
      </a-tab-pane>
    </a-tabs>
    <a-empty v-if="!hasAnyData" description="暂无数据" />
  </div>
</template>

<script lang="ts" setup>
import { ref, watch, nextTick, onMounted, onBeforeUnmount, computed } from "vue"
import * as echarts from "echarts"
import * as XLSX from "xlsx"

interface StatisticalValue { name: string; field: string; statisticalType: string; value: number }
interface StatisticalResult { groupName: string; groupValue: StatisticalValue[] }
interface AnalysisItem { layerId: number; layerName: string; statisticalResults: StatisticalResult[] }
interface Props { data: { analysisData?: AnalysisItem[] } }
const props = withDefaults(defineProps<Props>(), { data: () => ({}) })

const validAnalysisItems = computed(() => props.data.analysisData?.filter((item) => item.statisticalResults && item.statisticalResults.length > 0) || [])
const hasAnyData = computed(() => validAnalysisItems.value.length > 0)
const activeTab = ref("")
const chartInstances = new Map<number, echarts.ECharts>()
const chartRefs = new Map<number, HTMLElement>()
const setChartRef = (layerId: number, el: any) => { if (el) chartRefs.set(layerId, el) }

watch(validAnalysisItems, (newItems) => { if (newItems && newItems.length > 0) { activeTab.value = `analysis_${newItems[0].layerId}`; nextTick(updateCharts) } }, { immediate: true })

const getColumns = (sr: StatisticalResult[]) => [{ title: "序号", dataIndex: "index", key: "index", align: "center", width: 60 }, { title: "分组名称", dataIndex: "groupName", key: "groupName", align: "center" }, ...sr[0]?.groupValue.map((v) => ({ title: v.name, dataIndex: v.name, key: v.name, align: "center", width: 100 })) || []]
const getDataSource = (sr: StatisticalResult[]) => sr.map((item, i) => { const row: any = { key: item.groupName, groupName: item.groupName, index: i + 1 }; item.groupValue.forEach((val) => (row[val.name] = val.value)); return row })

const initChart = (chartInstance: echarts.ECharts | null, container: HTMLElement | undefined, data: StatisticalResult[]) => {
  if (!container) return null
  if (chartInstance) chartInstance.dispose()
  const xData = data.map((i) => i.groupName), seriesMap = new Map<string, number[]>()
  data.forEach((i) => i.groupValue.forEach((v) => { if (!seriesMap.has(v.name)) seriesMap.set(v.name, []); seriesMap.get(v.name)?.push(v.value) }))
  const option: any = { tooltip: { trigger: "axis", backgroundColor: "rgba(0,0,0,0.9)", textStyle: { color: "#fff" } }, legend: { data: Array.from(seriesMap.keys()), textStyle: { color: "#fff" }, top: 0 }, grid: { left: "3%", right: "5%", bottom: "10%", top: "15%" }, xAxis: { type: "category", data: xData, axisLabel: { color: "#fff", rotate: 30 } }, yAxis: { type: "value", axisLabel: { color: "#fff" } }, series: Array.from(seriesMap.entries()).map(([n, v]) => ({ name: n, type: "bar", data: v })) }
  const chart = echarts.init(container)
  chart.setOption(option)
  return chart
}

const updateCharts = async () => { await nextTick(); setTimeout(() => { if (activeTab.value.startsWith("analysis_")) { const lid = parseInt(activeTab.value.replace("analysis_", "")), item = props.data.analysisData?.find((i) => i.layerId === lid); if (item && item.statisticalResults?.length) { const c = chartRefs.get(lid), ec = chartInstances.get(lid); const ch = initChart(ec || null, c, item.statisticalResults); if (ch) chartInstances.set(lid, ch) } } }, 200) }
const handleTabChange = (key: string) => { nextTick(() => setTimeout(() => { if (key.startsWith("analysis_")) { const lid = parseInt(key.replace("analysis_", "")), item = props.data.analysisData?.find((i) => i.layerId === lid); if (item && item.statisticalResults?.length) { chartInstances.get(lid)?.dispose(); chartInstances.delete(lid); const c = chartRefs.get(lid); const ch = initChart(null, c, item.statisticalResults); if (ch) chartInstances.set(lid, ch) } } }, 100)) }
const handleResize = () => chartInstances.forEach((c) => c.resize())
watch(() => props.data, updateCharts, { deep: true })
onMounted(() => { updateCharts(); window.addEventListener("resize", handleResize) })
onBeforeUnmount(() => { window.removeEventListener("resize", handleResize); chartInstances.forEach((c) => c.dispose()); chartInstances.clear() })

const handleExportExcel = () => {
  const wb: XLSX.WorkBook = XLSX.utils.book_new()
  validAnalysisItems.value.forEach((item) => { const h = getColumns(item.statisticalResults).map((c: any) => c.title), r = getDataSource(item.statisticalResults).map((d: any) => getColumns(item.statisticalResults).map((c: any) => d[c.dataIndex || c.key])); XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet([h, ...r]), item.layerName.substring(0, 31)) })
  XLSX.writeFile(wb, "业态分析.xlsx")
}
</script>

<style lang="less" scoped>
.data-display { width: 100%; max-height: 500px; display: flex; flex-direction: column; overflow: hidden; }
.no-data { color: #f1f1f1; text-align: center; }
.tab-content-fixed { flex-shrink: 0; }
.section-title { font-size: 14px; font-weight: bold; margin: 10px 0; color: #c7c1c1; padding-left: 10px; position: relative; &::before { content: ""; position: absolute; display: inline-block; width: 3px; height: 100%; left: 0; top: 0; background: rgb(24 144 255 / 60%); } }
.chart-section { margin-bottom: 15px; }
.chart-container { width: 100%; height: 200px; }
.zb_title { display: flex; justify-content: space-between; margin-bottom: 10px; box-shadow: inset 0 0 8px rgb(24 144 255 / 60%); padding: 10px; align-items: center; background: var(--mars-dropdown-bg); }
</style>
