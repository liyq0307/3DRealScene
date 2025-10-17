<template>
  <div class="bar-chart" ref="chartRef"></div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch } from 'vue'

interface ChartProps {
  data: Array<{ label: string; value: number }>
  width?: number
  height?: number
  color?: string
  title?: string
}

const props = withDefaults(defineProps<ChartProps>(), {
  width: 400,
  height: 200,
  color: '#28a745',
  title: ''
})

const chartRef = ref<HTMLDivElement>()

const drawChart = () => {
  if (!chartRef.value || !props.data || props.data.length === 0) return

  const canvas = chartRef.value.querySelector('canvas') || document.createElement('canvas')
  if (!chartRef.value.contains(canvas)) {
    chartRef.value.appendChild(canvas)
  }

  const ctx = canvas.getContext('2d')
  if (!ctx) return

  // 设置画布大小
  const dpr = window.devicePixelRatio || 1
  canvas.width = props.width * dpr
  canvas.height = props.height * dpr
  canvas.style.width = `${props.width}px`
  canvas.style.height = `${props.height}px`
  ctx.scale(dpr, dpr)

  // 清空画布
  ctx.clearRect(0, 0, props.width, props.height)

  // 绘制背景
  ctx.fillStyle = '#f8f9fa'
  ctx.fillRect(0, 0, props.width, props.height)

  // 计算数据范围
  const values = props.data.map(d => d.value)
  const maxValue = Math.max(...values)

  // 绘制Y轴标签和网格线
  ctx.strokeStyle = '#e1e5e9'
  ctx.fillStyle = '#666'
  ctx.font = '10px sans-serif'
  ctx.textAlign = 'right'
  ctx.lineWidth = 1

  const gridLines = 5
  for (let i = 0; i <= gridLines; i++) {
    const y = (props.height / gridLines) * i
    const value = maxValue - (maxValue / gridLines) * i

    // 网格线
    ctx.beginPath()
    ctx.moveTo(40, y)
    ctx.lineTo(props.width, y)
    ctx.stroke()

    // Y轴标签
    ctx.fillText(value.toFixed(0), 35, y + 3)
  }

  // 绘制柱状图
  const barWidth = (props.width - 50) / props.data.length
  const barPadding = barWidth * 0.2

  props.data.forEach((item, index) => {
    const x = 45 + index * barWidth
    const barHeight = (item.value / maxValue) * props.height
    const y = props.height - barHeight

    // 绘制柱子
    ctx.fillStyle = props.color
    ctx.fillRect(x, y, barWidth - barPadding, barHeight)

    // 绘制X轴标签
    ctx.fillStyle = '#666'
    ctx.font = '10px sans-serif'
    ctx.textAlign = 'center'
    ctx.fillText(item.label, x + (barWidth - barPadding) / 2, props.height + 15)

    // 绘制数值
    ctx.fillStyle = '#333'
    ctx.font = 'bold 10px sans-serif'
    ctx.fillText(item.value.toString(), x + (barWidth - barPadding) / 2, y - 5)
  })

  // 绘制标题
  if (props.title) {
    ctx.fillStyle = '#333'
    ctx.font = 'bold 12px sans-serif'
    ctx.textAlign = 'left'
    ctx.fillText(props.title, 10, 15)
  }
}

watch(() => props.data, drawChart, { deep: true })

onMounted(() => {
  drawChart()
  window.addEventListener('resize', drawChart)
})

onUnmounted(() => {
  window.removeEventListener('resize', drawChart)
})
</script>

<style scoped>
.bar-chart {
  position: relative;
  display: inline-block;
  background: white;
  border-radius: 4px;
  padding: 0.5rem;
  padding-bottom: 1.5rem;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.bar-chart canvas {
  display: block;
}
</style>
