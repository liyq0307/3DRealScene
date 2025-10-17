<template>
  <!-- 工作流节点组件 -->
  <div
    :class="[
      'workflow-node',
      `node-type-${node.type}`,
      { 'selected': isSelected }
    ]"
    :style="nodeStyle"
    @mousedown="handleMouseDown"
    @dblclick="handleDoubleClick"
  >
    <!-- 节点标题栏 -->
    <div class="node-header">
      <div class="node-icon">
        <span>{{ nodeIcon }}</span>
      </div>
      <div class="node-label">{{ node.label }}</div>
      <div class="node-actions">
        <button @click.stop="handleDelete" class="delete-btn">×</button>
      </div>
    </div>

    <!-- 节点输入端口 -->
    <div v-if="node.inputs && node.inputs.length > 0" class="node-inputs">
      <div
        v-for="input in node.inputs"
        :key="input"
        :class="['node-port', 'input-port']"
        :data-port="input"
        @mousedown.stop="handlePortMouseDown($event, input, 'input')"
      >
        <span class="port-label">{{ input }}</span>
        <div class="port-connector"></div>
      </div>
    </div>

    <!-- 节点输出端口 -->
    <div v-if="node.outputs && node.outputs.length > 0" class="node-outputs">
      <div
        v-for="output in node.outputs"
        :key="output"
        :class="['node-port', 'output-port']"
        :data-port="output"
        @mousedown.stop="handlePortMouseDown($event, output, 'output')"
      >
        <div class="port-connector"></div>
        <span class="port-label">{{ output }}</span>
      </div>
    </div>

    <!-- 节点配置内容 -->
    <div v-if="showConfig" class="node-config">
      <slot name="config" :node="node" :config="node.config"></slot>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { WorkflowNode } from '@/types/workflow'

// Props定义
interface Props {
  node: WorkflowNode
  isSelected?: boolean
  showConfig?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  isSelected: false,
  showConfig: false
})

// Emits定义
const emit = defineEmits<{
  'node-select': [node: WorkflowNode]
  'node-delete': [nodeId: string]
  'node-move': [nodeId: string, deltaX: number, deltaY: number]
  'port-connect-start': [nodeId: string, port: string, portType: 'input' | 'output', event: MouseEvent]
  'node-dblclick': [node: WorkflowNode]
  'node-drag-start': [node: WorkflowNode, event: MouseEvent]
}>()

// 计算属性
const nodeStyle = computed(() => ({
  left: `${props.node.position.x}px`,
  top: `${props.node.position.y}px`
}))

const nodeIcon = computed(() => {
  const icons: Record<string, string> = {
    start: '▶',
    end: '⏹',
    delay: '⏱',
    condition: '❓',
    task: '⚙',
    default: '⬜'
  }
  return icons[props.node.type] || icons.default
})

// 事件处理
const handleMouseDown = (event: MouseEvent) => {
  if (event.button === 0) { // 左键
    emit('node-select', props.node)
    emit('node-drag-start', props.node, event)
  }
}

const handleDoubleClick = () => {
  emit('node-dblclick', props.node)
}

const handleDelete = () => {
  emit('node-delete', props.node.id)
}

const handlePortMouseDown = (event: MouseEvent, port: string, portType: 'input' | 'output') => {
  emit('port-connect-start', props.node.id, port, portType, event)
}
</script>

<style scoped>
.workflow-node {
  position: absolute;
  min-width: 120px;
  background: white;
  border: 2px solid #e1e5e9;
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
  cursor: move;
  user-select: none;
  transition: all 0.2s ease;
}

.workflow-node:hover {
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
}

.workflow-node.selected {
  border-color: #007acc;
  box-shadow: 0 4px 16px rgba(0, 122, 204, 0.3);
}

.node-header {
  display: flex;
  align-items: center;
  padding: 8px 12px;
  background: #f8f9fa;
  border-bottom: 1px solid #e1e5e9;
  border-radius: 6px 6px 0 0;
}

.node-icon {
  font-size: 14px;
  margin-right: 8px;
  color: #666;
}

.node-label {
  flex: 1;
  font-weight: 500;
  font-size: 14px;
  color: #333;
}

.node-actions {
  display: flex;
  gap: 4px;
}

.delete-btn {
  background: none;
  border: none;
  color: #999;
  font-size: 16px;
  cursor: pointer;
  padding: 0;
  width: 20px;
  height: 20px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  transition: all 0.2s ease;
}

.delete-btn:hover {
  background: #ff4757;
  color: white;
}

.node-inputs,
.node-outputs {
  padding: 4px 8px;
}

.node-port {
  display: flex;
  align-items: center;
  margin: 2px 0;
  font-size: 12px;
  color: #666;
}

.input-port {
  justify-content: flex-start;
}

.output-port {
  justify-content: flex-end;
}

.port-label {
  margin: 0 4px;
}

.port-connector {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #007acc;
  border: 2px solid white;
  box-shadow: 0 0 0 1px #007acc;
  cursor: crosshair;
  transition: all 0.2s ease;
}

.port-connector:hover {
  background: #005999;
  transform: scale(1.2);
}

.node-config {
  padding: 8px;
  border-top: 1px solid #e1e5e9;
}

/* 不同节点类型的颜色主题 */
.node-type-start .node-header { background: #e8f5e8; }
.node-type-start .node-label { color: #2e7d32; }
.node-type-start .port-connector { background: #4caf50; }

.node-type-end .node-header { background: #ffebee; }
.node-type-end .node-label { color: #c62828; }
.node-type-end .port-connector { background: #f44336; }

.node-type-delay .node-header { background: #fff3e0; }
.node-type-delay .node-label { color: #ef6c00; }
.node-type-delay .port-connector { background: #ff9800; }

.node-type-condition .node-header { background: #e3f2fd; }
.node-type-condition .node-label { color: #1976d2; }
.node-type-condition .port-connector { background: #2196f3; }

.node-type-task .node-header { background: #f3e5f5; }
.node-type-task .node-label { color: #7b1fa2; }
.node-type-task .port-connector { background: #9c27b0; }
</style>