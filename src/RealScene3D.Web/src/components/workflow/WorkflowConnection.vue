<template>
  <!-- 工作流连接线组件 -->
  <g class="workflow-connection" @click="handleClick">
    <!-- 连接线 -->
    <path
      :d="pathData"
      :stroke="isSelected ? '#007acc' : '#999'"
      stroke-width="2"
      fill="none"
      marker-end="url(#arrowhead)"
      class="connection-path"
    />

    <!-- 删除按钮 -->
    <circle
      v-if="showDeleteButton"
      :cx="deleteButtonPosition.x"
      :cy="deleteButtonPosition.y"
      r="8"
      fill="white"
      stroke="#ff4757"
      stroke-width="2"
      class="delete-button"
      @click.stop="handleDelete"
    />
    <text
      v-if="showDeleteButton"
      :x="deleteButtonPosition.x"
      :y="deleteButtonPosition.y + 3"
      text-anchor="middle"
      font-size="12"
      fill="#ff4757"
      class="delete-button-text"
      @click.stop="handleDelete"
    >
      ×
    </text>
  </g>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import type { WorkflowConnection as WFConnection, WorkflowNode, Position } from '@/types/workflow'

// Props定义
interface Props {
  connection: WFConnection
  sourceNode?: WorkflowNode
  targetNode?: WorkflowNode
  isSelected?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  isSelected: false
})

// Emits定义
const emit = defineEmits<{
  delete: [connectionId: string]
  select: [connectionId: string]
}>()

// 响应式状态
const showDeleteButton = ref(false)

// 计算属性
const pathData = computed(() => {
  if (!props.sourceNode || !props.targetNode) return ''

  const sourcePos = getNodePortPosition(props.sourceNode, 'output', props.connection.sourceOutput)
  const targetPos = getNodePortPosition(props.targetNode, 'input', props.connection.targetInput)

  // 计算贝塞尔曲线控制点
  const dx = targetPos.x - sourcePos.x
  const dy = targetPos.y - sourcePos.y

  // 根据连接方向调整控制点
  const cp1x = sourcePos.x + Math.abs(dx) * 0.5
  const cp1y = sourcePos.y
  const cp2x = targetPos.x - Math.abs(dx) * 0.5
  const cp2y = targetPos.y

  return `M ${sourcePos.x} ${sourcePos.y} C ${cp1x} ${cp1y}, ${cp2x} ${cp2y}, ${targetPos.x} ${targetPos.y}`
})

const deleteButtonPosition = computed(() => {
  if (!props.sourceNode || !props.targetNode) return { x: 0, y: 0 }

  const sourcePos = getNodePortPosition(props.sourceNode, 'output', props.connection.sourceOutput)
  const targetPos = getNodePortPosition(props.targetNode, 'input', props.connection.targetInput)

  return {
    x: (sourcePos.x + targetPos.x) / 2,
    y: (sourcePos.y + targetPos.y) / 2
  }
})

// 方法
const getNodePortPosition = (node: WorkflowNode, portType: 'input' | 'output', portName?: string): Position => {
  const baseX = node.position.x
  const baseY = node.position.y
  const nodeHeight = 80 // 假设节点高度
  const portOffset = 20 // 端口间距

  if (portType === 'output') {
    // 输出端口在右侧
    const portIndex = node.outputs?.indexOf(portName || '') ?? 0
    return {
      x: baseX + 120, // 节点宽度
      y: baseY + 40 + portIndex * portOffset // 节点顶部 + 端口偏移
    }
  } else {
    // 输入端口在左侧
    const portIndex = node.inputs?.indexOf(portName || '') ?? 0
    return {
      x: baseX,
      y: baseY + 40 + portIndex * portOffset
    }
  }
}

const handleClick = () => {
  emit('select', props.connection.id)
  showDeleteButton.value = true

  // 3秒后自动隐藏删除按钮
  setTimeout(() => {
    showDeleteButton.value = false
  }, 3000)
}

const handleDelete = () => {
  emit('delete', props.connection.id)
}
</script>

<style scoped>
.workflow-connection {
  cursor: pointer;
}

.workflow-connection:hover .connection-path {
  stroke: #007acc;
  stroke-width: 3;
}

.delete-button {
  cursor: pointer;
  transition: all 0.2s ease;
}

.delete-button:hover {
  fill: #ff4757;
}

.delete-button-text {
  cursor: pointer;
  pointer-events: none;
  user-select: none;
}
</style>