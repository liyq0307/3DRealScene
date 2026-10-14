<template>
  <div class="echartsView">
    <div class="btn"><mars-button class="export-btn" @click="handleExportExcel">导出 excel</mars-button></div>
    <div id="echartsView" ref="chartContainer" style="width: 100%; height: 300px;"></div>
  </div>
</template>

<script lang="ts" setup>
import { nextTick, onMounted, onUnmounted, watch, ref } from "vue"
import * as echarts from "echarts"
import * as XLSX from "xlsx"

let pmChart: echarts.ECharts | null = null
let isComponentMounted = false
const chartContainer = ref<HTMLElement | null>(null)

interface Props { data?: any }
const props = withDefaults(defineProps<Props>(), { data: () => ({}) })

const initChart = (chartInstance: echarts.ECharts | null, container: HTMLElement | undefined, data: any) => {
  if (!container || !data?.arrPoint) return null
  const { clientWidth, clientHeight } = container
  if (clientWidth === 0 || clientHeight === 0) return null
  if (chartInstance) chartInstance.dispose()
  const existing = echarts.getInstanceByDom(container)
  if (existing && existing !== chartInstance) existing.dispose()
  const option = {
    grid: { left: 10, right: 40, bottom: 10, top: 40, containLabel: true },
    xAxis: [{ name: "行程", type: "category", axisLabel: { color: "#fff", formatter: "{value} 米" }, data: data.arrLen || [] }],
    yAxis: [{ name: "高程", type: "value", axisLabel: { color: "#fff" } }],
    series: [{ name: "高程值", type: "line", smooth: true, symbol: "none", data: data.arrHB || [], itemStyle: { color: "rgb(255,70,131)" } }]
  }
  const chart = echarts.init(container)
  chart.setOption(option)
  return chart
}

const updateCharts = async () => { if (!isComponentMounted || !props.data) return; await nextTick(); const c = chartContainer.value; if (c && props.data) pmChart = initChart(pmChart, c, props.data) }

onMounted(async () => { isComponentMounted = true; await nextTick(); const c = chartContainer.value; if (c && props.data) pmChart = initChart(null, c, props.data) })
onUnmounted(() => { if (pmChart) { pmChart.dispose(); pmChart = null }; isComponentMounted = false; window.removeEventListener("resize", handleResize) })
const handleResize = () => { if (pmChart) pmChart.resize() }
window.addEventListener("resize", handleResize)
watch(() => props.data, updateCharts, { deep: true, immediate: true })

const handleExportExcel = () => {
  if (!props.data) return
  const { arrPoint, arrLen, arrHB } = props.data
  const exportData = [["距起点 (m)", "海拔 (m)", "经度", "纬度"], ...arrPoint.map((p: any, i: number) => [arrLen[i], arrHB[i], p._lng, p._lat])]
  const wb = XLSX.utils.book_new(), ws = XLSX.utils.aoa_to_sheet(exportData)
  XLSX.utils.book_append_sheet(wb, ws, "剖面数据")
  XLSX.writeFile(wb, "剖面分析.xlsx")
}
</script>

<style scoped>
.echartsView { width: 100%; height: 310px; display: flex; flex-direction: column; overflow: hidden; }
.btn { display: flex; justify-content: flex-end; margin-bottom: 10px; }
</style>
