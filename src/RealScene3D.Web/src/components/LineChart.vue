<template>
  <div class="line-chart" ref="chartRef"></div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch } from 'vue'

interface ChartProps {
  data: Array<{ time: string; value: number }>
  width?: number
  height?: number
  color?: string
  title?: string
}

const props = withDefaults(defineProps<ChartProps>(), {
  width: 400,
  height: 200,
  color: '#007acc',
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
  const minValue = Math.min(...values)
  const maxValue = Math.max(...values)
  const range = maxValue - minValue || 1

  // 绘制网格线
  ctx.strokeStyle = '#e1e5e9'
  ctx.lineWidth = 1
  const gridLines = 5
  for (let i = 0; i <= gridLines; i++) {
    const y = (props.height / gridLines) * i
    ctx.beginPath()
    ctx.moveTo(0, y)
    ctx.lineTo(props.width, y)
    ctx.stroke()
  }

  // 绘制Y轴标签
  ctx.fillStyle = '#666'
  ctx.font = '10px sans-serif'
  ctx.textAlign = 'right'
  for (let i = 0; i <= gridLines; i++) {
    const value = maxValue - (range / gridLines) * i
    const y = (props.height / gridLines) * i
    ctx.fillText(value.toFixed(1), props.width - 5, y + 3)
  }

  // 绘制数据线
  if (props.data.length > 1) {
    ctx.strokeStyle = props.color
    ctx.lineWidth = 2
    ctx.beginPath()

    const xStep = props.width / (props.data.length - 1)
    props.data.forEach((point, index) => {
      const x = index * xStep
      const y = props.height - ((point.value - minValue) / range) * props.height
      if (index === 0) {
        ctx.moveTo(x, y)
      } else {
        ctx.lineTo(x, y)
      }
    })
    ctx.stroke()

    // 绘制数据点
    ctx.fillStyle = props.color
    props.data.forEach((point, index) => {
      const x = index * xStep
      const y = props.height - ((point.value - minValue) / range) * props.height
      ctx.beginPath()
      ctx.arc(x, y, 3, 0, Math.PI * 2)
      ctx.fill()
    })
  }

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
.line-chart {
  position: relative;
  display: inline-block;
  background: white;
  border-radius: 4px;
  padding: 0.5rem;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.line-chart canvas {
  display: block;
}
</style>
