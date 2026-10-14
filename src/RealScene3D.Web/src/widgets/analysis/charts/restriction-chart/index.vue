<template>
  <div class="data-display mars-main-view">
    <div class="zb_title" v-if="hasAnyData">
      <div>该分析区域内 <span v-if="data.point?.list.length > 0">限制性点图层数：{{ data.point?.tj?.length }}个，</span><span v-if="data.line?.list.length > 0">限制性线图层数：{{ data.line?.list.length }} 个，</span><span v-if="data.area?.list.length > 0">限制性面图层数：{{ data.area?.list.length }} 个</span></div>
      <mars-button class="export-btn" @click="handleExportExcel">导出 excel</mars-button>
    </div>
    <a-tabs v-model:activeKey="activeTab" @change="handleTabChange">
      <a-tab-pane key="point" tab="点" v-if="data.point?.list.length > 0">
        <div class="tab-content-fixed"><div class="chart-section"><h4 class="section-title">统计图表</h4><div v-if="hasPointData" ref="pointChartRef" class="chart-container"></div><div v-else class="no-data">无数据</div></div></div>
        <div class="tab-content-scrollable"><div class="table-section"><a-table bordered :columns="columns" :dataSource="data.point?.list" :pagination="false" size="small" /></div></div>
      </a-tab-pane>
      <a-tab-pane key="line" tab="线" v-if="data.line?.list.length > 0">
        <div class="tab-content-fixed"><div class="chart-section"><h4 class="section-title">统计图表</h4><div v-if="hasLineData" ref="lineChartRef" class="chart-container"></div><div v-else class="no-data">无数据</div></div></div>
        <div class="tab-content-scrollable"><div class="table-section"><a-table bordered :columns="columns" :dataSource="data.line?.list" :pagination="false" size="small" /></div></div>
      </a-tab-pane>
      <a-tab-pane key="area" tab="面" v-if="data.area?.list.length > 0">
        <div class="tab-content-fixed"><div class="chart-section"><h4 class="section-title">统计图表</h4><div ref="areaChartRef" v-if="hasAreaData" class="chart-container"></div><div v-else class="no-data">无数据</div></div></div>
        <div class="tab-content-scrollable"><div class="table-section"><a-table bordered :columns="columns" :dataSource="data.area.list" :pagination="false" size="small" /></div></div>
      </a-tab-pane>
    </a-tabs>
    <a-empty v-if="!hasAnyData" description="暂无数据" />
  </div>
</template>

<script lang="ts" setup>
import { ref, watch, nextTick, onMounted, onBeforeUnmount, computed } from "vue"
import * as echarts from "echarts"
import * as XLSX from "xlsx"

interface Props { data: { point?: { list: any[]; tj: any[] }; line?: { list: any[]; tj: any[] }; area?: { list: any[]; tj: any[] } } }
const props = withDefaults(defineProps<Props>(), { data: () => ({}) })

const hasPointData = computed(() => props.data.point?.list && props.data.point.list.length > 0)
const hasLineData = computed(() => props.data.line?.list && props.data.line.list.length > 0)
const hasAreaData = computed(() => props.data.area?.list && props.data.area.list.length > 0)
const hasAnyData = computed(() => hasPointData.value || hasLineData.value || hasAreaData.value)

const activeTab = ref()
const pointChartRef = ref<HTMLElement>()
const lineChartRef = ref<HTMLElement>()
const areaChartRef = ref<HTMLElement>()
let pointChart: echarts.ECharts | null = null
let lineChart: echarts.ECharts | null = null
let areaChart: echarts.ECharts | null = null

const columns = ref([{ title: "图层名称", dataIndex: "layerName", key: "layerName", width: 120 }, { title: "数值", dataIndex: "intersectArea", key: "intersectArea", width: 80, customRender: ({ record }: any) => `${record.intersectArea} ${record.unit}` }])

const initChart = (chartInstance: echarts.ECharts | null, container: HTMLElement | undefined, data: any[]) => {
  if (!container) return null
  if (chartInstance) chartInstance.dispose()
  const option: any = { tooltip: { trigger: "item", formatter: "{b}: {c} ({d}%)", backgroundColor: "rgba(0,0,0,0.9)", textStyle: { color: "#fff" } }, series: [{ type: "pie", radius: ["40%", "65%"], center: ["50%", "50%"], label: { color: "#fff", show: true, position: "outside", formatter: "{d}%" }, data: data.map((i: any) => ({ name: i.name, value: i.value })) }] }
  const chart = echarts.init(container)
  chart.setOption(option)
  return chart
}

const updateCharts = async () => { await nextTick(); setTimeout(() => { if (activeTab.value === "point" && props.data.point?.tj) pointChart = initChart(pointChart, pointChartRef.value, props.data.point.tj); else if (activeTab.value === "line" && props.data.line?.tj) lineChart = initChart(lineChart, lineChartRef.value, props.data.line.tj); else if (activeTab.value === "area" && props.data.area?.tj) areaChart = initChart(areaChart, areaChartRef.value, props.data.area.tj) }, 500) }
const handleTabChange = (key: string) => { nextTick(() => setTimeout(() => { if (key === "point") { pointChart?.dispose(); pointChart = null; if (props.data.point?.tj) pointChart = initChart(null, pointChartRef.value, props.data.point.tj) } else if (key === "line") { lineChart?.dispose(); lineChart = null; if (props.data.line?.tj) lineChart = initChart(null, lineChartRef.value, props.data.line.tj) } else if (key === "area") { areaChart?.dispose(); areaChart = null; if (props.data.area?.tj) areaChart = initChart(null, areaChartRef.value, props.data.area.tj) } }, 100)) }
const handleResize = () => { pointChart?.resize(); lineChart?.resize(); areaChart?.resize() }
watch(() => props.data, updateCharts, { deep: true })
onMounted(() => { updateCharts(); window.addEventListener("resize", handleResize) })
onBeforeUnmount(() => { window.removeEventListener("resize", handleResize); pointChart?.dispose(); lineChart?.dispose(); areaChart?.dispose() })

const handleExportExcel = () => {
  const pd = [["图层名称", "数值 (个)"], ...props.data.point?.list.map((i) => [i.layerName, i.intersectArea])] || []
  const ld = [["图层名称", "数值 (m)"], ...props.data.line?.list.map((i) => [i.layerName, i.intersectArea])] || []
  const ad = [["图层名称", "数值 (平方米)"], ...props.data.area?.list.map((i) => [i.layerName, i.intersectArea])] || []
  if (!(pd.length > 1 || ld.length > 1 || ad.length > 1)) return
  const wb: XLSX.WorkBook = XLSX.utils.book_new(), md: (string | number)[][] = []
  if (pd.length > 1) { md.push(...pd); md.push([]) }
  if (ld.length > 1) { md.push(...ld); md.push([]) }
  if (ad.length > 1) { md.push(...ad); md.push([]) }
  XLSX.utils.book_append_sheet(wb, XLSX.utils.aoa_to_sheet(md), "限制性数据")
  XLSX.writeFile(wb, "周边地块限制性分析.xlsx")
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
