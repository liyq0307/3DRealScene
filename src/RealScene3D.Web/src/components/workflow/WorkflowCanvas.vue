<template>
  <!-- 工作流画布组件 -->
  <div class="workflow-canvas">
    <!-- SVG画布用于绘制连接线 -->
    <svg
      ref="svgRef"
      class="canvas-svg"
      :width="canvasWidth"
      :height="canvasHeight"
      @mousedown="handleCanvasMouseDown"
      @mousemove="handleCanvasMouseMove"
      @mouseup="handleCanvasMouseUp"
    >
      <!-- 网格背景 -->
      <defs>
        <pattern
          id="grid"
          width="20"
          height="20"
          patternUnits="userSpaceOnUse"
        >
          <path
            d="M 20 0 L 0 0 0 20"
            fill="none"
            stroke="#e1e5e9"
            stroke-width="1"
          />
        </pattern>
      </defs>
      <rect v-if="props.showGrid" width="100%" height="100%" fill="url(#grid)" />

      <!-- 渲染连接线 -->
      <g v-for="connection in workflow.connections" :key="connection.id">
        <WorkflowConnection
          :connection="connection"
          :source-node="getNodeById(connection.source)"
          :target-node="getNodeById(connection.target)"
          @delete="handleDeleteConnection(connection.id)"
        />
      </g>

      <!-- 临时连接线（正在绘制中） -->
      <g v-if="connectionState.tempConnection">
        <line
          :x1="connectionState.tempConnection.start.x"
          :y1="connectionState.tempConnection.start.y"
          :x2="connectionState.tempConnection.end.x"
          :y2="connectionState.tempConnection.end.y"
          stroke="#007acc"
          stroke-width="2"
          stroke-dasharray="5,5"
          marker-end="url(#arrowhead)"
        />
      </g>

      <!-- 箭头定义 -->
      <defs>
        <marker
          id="arrowhead"
          markerWidth="10"
          markerHeight="7"
          refX="9"
          refY="3.5"
          orient="auto"
        >
          <polygon
            points="0 0, 10 3.5, 0 7"
            fill="#007acc"
          />
        </marker>
      </defs>
    </svg>

    <!-- 节点容器 -->
    <div
      ref="nodesContainerRef"
      class="nodes-container"
      @drop="handleDrop"
      @dragover="handleDragOver"
    >
      <WorkflowNode
        v-for="node in workflow.nodes"
        :key="node.id"
        :node="node"
        :is-selected="selectedNodeId === node.id"
        @node-select="handleNodeSelect"
        @node-delete="handleNodeDelete"
        @node-dblclick="handleNodeDoubleClick"
        @port-connect-start="handlePortConnectStart"
        @node-drag-start="handleNodeDragStart"
      />
    </div>

    <!-- 右键菜单 -->
    <div
      v-if="contextMenu.visible"
      class="context-menu"
      :style="{ left: contextMenu.x + 'px', top: contextMenu.y + 'px' }"
      @click.stop
    >
      <div class="context-menu-item" @click="handleContextMenuAction('delete')">
        删除节点
      </div>
      <div class="context-menu-item" @click="handleContextMenuAction('duplicate')">
        复制节点
      </div>
      <div class="context-menu-item" @click="handleContextMenuAction('properties')">
        属性设置
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted, nextTick } from 'vue'
import panzoom from '@panzoom/panzoom'
import type { WorkflowDefinition, WorkflowNode as WFNode, WorkflowConnection as WFConnection, Position, CanvasState, DragState, ConnectionState } from '@/types/workflow'
import WorkflowNode from './WorkflowNode.vue'
import WorkflowConnection from './WorkflowConnection.vue'

// Props定义
interface Props {
  workflow: WorkflowDefinition
  canvasState: CanvasState
  showGrid?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  showGrid: true
})

// Emits定义
const emit = defineEmits<{
  'update:canvas-state': [state: CanvasState]
  'node-select': [nodeId: string | undefined]
  'node-add': [node: WFNode, position: Position]
  'node-delete': [nodeId: string]
  'node-update': [nodeId: string, updates: Partial<WFNode>]
  'connection-add': [connection: Omit<WFConnection, 'id'>]
  'connection-delete': [connectionId: string]
  'node-context-menu': [nodeId: string, position: Position]
}>()

// 模板引用
const svgRef = ref<SVGElement>()
const nodesContainerRef = ref<HTMLDivElement>()

// 响应式状态
const canvasWidth = ref(2000)
const canvasHeight = ref(2000)
const panzoomInstance = ref<any>(null)
const dragState = ref<DragState>({
  isDragging: false,
  draggedNode: undefined,
  dragOffset: undefined,
  dragStartPosition: undefined
})
const connectionState = ref<ConnectionState>({
  isConnecting: false,
  connectionStart: undefined,
  tempConnection: undefined
})
const contextMenu = ref({
  visible: false,
  x: 0,
  y: 0,
  targetNodeId: ''
})

// 计算属性
const selectedNodeId = computed(() => props.canvasState.selectedNodeId)

// 方法
const getNodeById = (id: string): WFNode | undefined => {
  return props.workflow.nodes.find(node => node.id === id)
}

const handleCanvasMouseDown = (event: MouseEvent) => {
  if (event.button === 0) { // 左键
    emit('node-select', undefined)
    contextMenu.value.visible = false
  } else if (event.button === 2) { // 右键
    event.preventDefault()
    handleCanvasContextMenu(event)
  }
}

const handleCanvasMouseMove = (event: MouseEvent) => {
  // 处理拖拽
  if (dragState.value.isDragging && dragState.value.draggedNode) {
    const rect = nodesContainerRef.value?.getBoundingClientRect()
    if (!rect) return

    // 获取panzoom的当前变换
    const scale = panzoomInstance.value?.getScale() || 1
    const pan = panzoomInstance.value?.getPan() || { x: 0, y: 0 }

    // 计算节点新位置（考虑缩放和平移）
    const x = (event.clientX - rect.left - pan.x) / scale - (dragState.value.dragOffset?.x || 0)
    const y = (event.clientY - rect.top - pan.y) / scale - (dragState.value.dragOffset?.y || 0)

    emit('node-update', dragState.value.draggedNode.id, {
      position: { x, y }
    })
  }

  // 处理连接线绘制
  if (connectionState.value.isConnecting && connectionState.value.tempConnection) {
    const rect = svgRef.value?.getBoundingClientRect()
    if (!rect) return

    connectionState.value.tempConnection.end = {
      x: event.clientX - rect.left,
      y: event.clientY - rect.top
    }
  }
}

const handleCanvasMouseUp = (event: MouseEvent) => {
  // 结束拖拽
  if (dragState.value.isDragging) {
    dragState.value.isDragging = false
    dragState.value.draggedNode = undefined

    // 重新启用panzoom
    if (panzoomInstance.value) {
      panzoomInstance.value.setOptions({ disablePan: false })
    }
  }

  // 结束连接
  if (connectionState.value.isConnecting) {
    connectionState.value.isConnecting = false
    connectionState.value.tempConnection = undefined
  }
}

const handleCanvasContextMenu = (event: MouseEvent) => {
  const rect = svgRef.value?.getBoundingClientRect()
  if (!rect) return

  contextMenu.value.x = event.clientX - rect.left
  contextMenu.value.y = event.clientY - rect.top
  contextMenu.value.visible = true
}

const handleNodeSelect = (node: WFNode) => {
  emit('node-select', node.id)
  contextMenu.value.targetNodeId = node.id
}

const handleNodeDelete = (nodeId: string) => {
  emit('node-delete', nodeId)
}

const handleNodeDoubleClick = (node: WFNode) => {
  // 双击节点可以编辑属性
  emit('node-context-menu', node.id, node.position)
}

const handlePortConnectStart = (nodeId: string, port: string, portType: 'input' | 'output', event: MouseEvent) => {
  event.stopPropagation()

  if (portType === 'output') {
    connectionState.value.isConnecting = true
    connectionState.value.connectionStart = { nodeId, output: port }

    const rect = svgRef.value?.getBoundingClientRect()
    if (rect) {
      connectionState.value.tempConnection = {
        start: { x: event.clientX - rect.left, y: event.clientY - rect.top },
        end: { x: event.clientX - rect.left, y: event.clientY - rect.top }
      }
    }
  } else if (portType === 'input') {
    // 如果正在连接且点击了输入端口，完成连接
    if (connectionState.value.isConnecting && connectionState.value.connectionStart) {
      const sourceNodeId = connectionState.value.connectionStart.nodeId
      const sourceOutput = connectionState.value.connectionStart.output

      // 检查是否形成有效的连接（不能连接到同一个节点，不能重复连接）
      if (sourceNodeId !== nodeId) {
        const existingConnection = props.workflow.connections.find(
          conn => conn.source === sourceNodeId && conn.target === nodeId &&
                  conn.sourceOutput === sourceOutput && conn.targetInput === port
        )

        if (!existingConnection) {
          emit('connection-add', {
            source: sourceNodeId,
            target: nodeId,
            sourceOutput: sourceOutput,
            targetInput: port
          })
        }
      }

      // 重置连接状态
      connectionState.value.isConnecting = false
      connectionState.value.tempConnection = undefined
      connectionState.value.connectionStart = undefined
    }
  }
}

const handleDeleteConnection = (connectionId: string) => {
  emit('connection-delete', connectionId)
}

const handleNodeDragStart = (node: WFNode, event: MouseEvent) => {
  event.stopPropagation()
  event.preventDefault()

  dragState.value.isDragging = true
  dragState.value.draggedNode = node

  // 禁用panzoom以避免冲突
  if (panzoomInstance.value) {
    panzoomInstance.value.setOptions({ disablePan: true })
  }

  // 计算鼠标相对于节点左上角的偏移
  const rect = nodesContainerRef.value?.getBoundingClientRect()
  if (rect) {
    const scale = panzoomInstance.value?.getScale() || 1
    const pan = panzoomInstance.value?.getPan() || { x: 0, y: 0 }

    const nodeX = node.position.x
    const nodeY = node.position.y
    const mouseX = (event.clientX - rect.left - pan.x) / scale
    const mouseY = (event.clientY - rect.top - pan.y) / scale

    dragState.value.dragOffset = {
      x: mouseX - nodeX,
      y: mouseY - nodeY
    }
    dragState.value.dragStartPosition = { x: nodeX, y: nodeY }
  }

  // 在 document 上添加鼠标移动和释放监听器
  document.addEventListener('mousemove', handleDocumentMouseMove)
  document.addEventListener('mouseup', handleDocumentMouseUp)
}

// 处理 document 级别的鼠标移动（用于拖拽）
const handleDocumentMouseMove = (event: MouseEvent) => {
  if (dragState.value.isDragging && dragState.value.draggedNode) {
    const rect = nodesContainerRef.value?.getBoundingClientRect()
    if (!rect) return

    // 获取panzoom的当前变换
    const scale = panzoomInstance.value?.getScale() || 1
    const pan = panzoomInstance.value?.getPan() || { x: 0, y: 0 }

    // 计算节点新位置（考虑缩放和平移）
    const x = (event.clientX - rect.left - pan.x) / scale - (dragState.value.dragOffset?.x || 0)
    const y = (event.clientY - rect.top - pan.y) / scale - (dragState.value.dragOffset?.y || 0)

    emit('node-update', dragState.value.draggedNode.id, {
      position: { x, y }
    })
  }
}

// 处理 document 级别的鼠标释放（用于结束拖拽）
const handleDocumentMouseUp = () => {
  if (dragState.value.isDragging) {
    dragState.value.isDragging = false
    dragState.value.draggedNode = undefined

    // 重新启用panzoom
    if (panzoomInstance.value) {
      panzoomInstance.value.setOptions({ disablePan: false })
    }

    // 移除 document 级别的监听器
    document.removeEventListener('mousemove', handleDocumentMouseMove)
    document.removeEventListener('mouseup', handleDocumentMouseUp)
  }
}

const handleContextMenuAction = (action: string) => {
  contextMenu.value.visible = false

  switch (action) {
    case 'delete':
      if (contextMenu.value.targetNodeId) {
        emit('node-delete', contextMenu.value.targetNodeId)
      }
      break
    case 'duplicate':
      // TODO: 实现复制节点
      break
    case 'properties':
      // TODO: 实现属性编辑
      break
  }
}

const handleDrop = (event: DragEvent) => {
  event.preventDefault()
  const nodeTypeData = event.dataTransfer?.getData('application/json')
  if (nodeTypeData) {
    try {
      const nodeType = JSON.parse(nodeTypeData)
      const rect = nodesContainerRef.value?.getBoundingClientRect()
      if (rect) {
        const scale = panzoomInstance.value?.getScale() || 1
        const pan = panzoomInstance.value?.getPan() || { x: 0, y: 0 }

        const x = (event.clientX - rect.left - pan.x) / scale
        const y = (event.clientY - rect.top - pan.y) / scale

        const newNode: WFNode = {
          id: '',
          type: nodeType.type,
          label: nodeType.label,
          position: { x, y },
          config: { ...nodeType.defaultConfig },
          inputs: nodeType.inputs,
          outputs: nodeType.outputs
        }

        emit('node-add', newNode, { x, y })
      }
    } catch (error) {
      console.error('解析节点类型数据失败:', error)
    }
  }
}

const handleDragOver = (event: DragEvent) => {
  event.preventDefault()
}

// 生命周期
onMounted(async () => {
  await nextTick()

  // 初始化panzoom
  if (nodesContainerRef.value) {
    panzoomInstance.value = panzoom(nodesContainerRef.value, {
      maxZoom: 2,
      minZoom: 0.1,
      contain: 'outside',
      cursor: 'default',
      excludeClass: 'workflow-node', // 排除节点元素,允许节点接收鼠标事件
      startScale: 1,
      startX: 0,
      startY: 0
    })

    // 使用 addEventListener 监听 panzoom 事件
    nodesContainerRef.value.addEventListener('panzoomzoom', (e: any) => {
      const newState = {
        ...props.canvasState,
        scale: e.detail.scale
      }
      emit('update:canvas-state', newState)
    })

    nodesContainerRef.value.addEventListener('panzoompan', (e: any) => {
      const newState = {
        ...props.canvasState,
        offsetX: e.detail.x,
        offsetY: e.detail.y
      }
      emit('update:canvas-state', newState)
    })
  }

  // 监听键盘事件
  document.addEventListener('keydown', handleKeyDown)
  document.addEventListener('contextmenu', (e) => {
    if (svgRef.value?.contains(e.target as Node)) {
      e.preventDefault()
    }
  })
})

// 监听canvasState变化，同步更新panzoom
watch(() => props.canvasState.scale, (newScale) => {
  if (panzoomInstance.value) {
    const currentScale = panzoomInstance.value.getScale()
    // 只有当缩放值真正改变时才更新
    if (Math.abs(currentScale - newScale) > 0.001) {
      panzoomInstance.value.zoom(newScale, { animate: true })
    }
  }
})

watch(() => [props.canvasState.offsetX, props.canvasState.offsetY], ([newX, newY]) => {
  if (panzoomInstance.value && newX !== undefined && newY !== undefined) {
    const currentPan = panzoomInstance.value.getPan()
    // 只有当平移值真正改变时才更新
    if (Math.abs(currentPan.x - newX) > 0.1 || Math.abs(currentPan.y - newY) > 0.1) {
      panzoomInstance.value.pan(newX, newY, { animate: true })
    }
  }
})

onUnmounted(() => {
  document.removeEventListener('keydown', handleKeyDown)
  document.removeEventListener('mousemove', handleDocumentMouseMove)
  document.removeEventListener('mouseup', handleDocumentMouseUp)
  if (panzoomInstance.value) {
    panzoomInstance.value.destroy()
  }
})

const handleKeyDown = (event: KeyboardEvent) => {
  if (event.key === 'Delete' && selectedNodeId.value) {
    emit('node-delete', selectedNodeId.value)
  }
}
</script>

<style scoped>
.workflow-canvas {
  position: relative;
  width: 100%;
  height: 100%;
  overflow: hidden;
  background: #fafafa;
}

.canvas-svg {
  position: absolute;
  top: 0;
  left: 0;
  pointer-events: none;
}

.canvas-svg * {
  pointer-events: auto;
}

.nodes-container {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
}

.context-menu {
  position: absolute;
  background: white;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
  z-index: 1000;
  min-width: 120px;
}

.context-menu-item {
  padding: 8px 12px;
  cursor: pointer;
  font-size: 14px;
  color: #333;
  border-bottom: 1px solid #f0f0f0;
}

.context-menu-item:last-child {
  border-bottom: none;
}

.context-menu-item:hover {
  background: #f8f9fa;
}
</style>