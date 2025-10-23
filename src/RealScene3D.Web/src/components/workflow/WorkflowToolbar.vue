<template>
  <!-- 工作流设计工具栏 -->
  <div class="workflow-toolbar">
    <div class="toolbar-section">
      <h3>节点工具</h3>
      <div class="node-palette">
        <div
          v-for="nodeType in nodeTypes"
          :key="nodeType.type"
          class="node-item"
          :class="`node-type-${nodeType.type}`"
          draggable="true"
          @dragstart="handleDragStart($event, nodeType)"
          @dragend="handleDragEnd"
        >
          <div class="node-icon">{{ nodeType.icon }}</div>
          <div class="node-info">
            <div class="node-label">{{ nodeType.label }}</div>
            <div class="node-description">{{ nodeType.description }}</div>
          </div>
        </div>
      </div>
    </div>

    <div class="toolbar-section">
      <h3>画布工具</h3>
      <div class="canvas-tools">
        <button @click="handleZoomIn" class="tool-button">
          <span>🔍+</span>
          放大
        </button>
        <button @click="handleZoomOut" class="tool-button">
          <span>🔍-</span>
          缩小
        </button>
        <button @click="handleFitView" class="tool-button">
          <span>📐</span>
          适应视图
        </button>
        <button @click="handleCenterView" class="tool-button">
          <span>🎯</span>
          居中视图
        </button>
      </div>
    </div>

    <div class="toolbar-section">
      <h3>编辑工具</h3>
      <div class="edit-tools">
        <button @click="handleUndo" :disabled="!canUndo" class="tool-button">
          <span>↶</span>
          撤销
        </button>
        <button @click="handleRedo" :disabled="!canRedo" class="tool-button">
          <span>↷</span>
          重做
        </button>
        <button @click="handleDelete" :disabled="!hasSelection" class="tool-button danger">
          <span>🗑️</span>
          删除
        </button>
      </div>
    </div>

    <div class="toolbar-section">
      <h3>工作流操作</h3>
      <div class="workflow-actions">
        <button @click="handleSave" class="tool-button primary">
          <span>💾</span>
          保存
        </button>
        <button @click="handleLoad" class="tool-button">
          <span>📁</span>
          加载
        </button>
        <button @click="handleNew" class="tool-button">
          <span>🆕</span>
          新建
        </button>
        <button @click="handleExport" class="tool-button">
          <span>📤</span>
          导出
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { NodeTypeConfig } from '@/types/workflow'

// Props定义
interface Props {
  canUndo?: boolean
  canRedo?: boolean
  hasSelection?: boolean
}

const props = defineProps<Props>()
const { canUndo = false, canRedo = false, hasSelection = false } = props

// Emits定义
const emit = defineEmits<{
  'node-drag-start': [nodeType: NodeTypeConfig, event: DragEvent]
  'zoom-in': []
  'zoom-out': []
  'fit-view': []
  'center-view': []
  'undo': []
  'redo': []
  'delete': []
  'save': []
  'load': []
  'new': []
  'export': []
}>()

// 节点类型配置
const nodeTypes = computed<NodeTypeConfig[]>(() => [
  {
    type: 'start',
    label: '开始节点',
    category: 'flow',
    icon: '▶',
    color: '#4caf50',
    description: '工作流开始节点'
  },
  {
    type: 'end',
    label: '结束节点',
    category: 'flow',
    icon: '⏹',
    color: '#f44336',
    description: '工作流结束节点'
  },
  {
    type: 'delay',
    label: '延迟节点',
    category: 'action',
    icon: '⏱',
    color: '#ff9800',
    inputs: ['input'],
    outputs: ['output'],
    defaultConfig: { delayMs: 1000 },
    description: '延迟执行指定时间'
  },
  {
    type: 'condition',
    label: '条件节点',
    category: 'logic',
    icon: '❓',
    color: '#2196f3',
    inputs: ['input'],
    outputs: ['true', 'false'],
    defaultConfig: { condition: 'true' },
    description: '条件判断分支'
  },
  {
    type: 'task',
    label: '任务节点',
    category: 'action',
    icon: '⚙',
    color: '#9c27b0',
    inputs: ['input'],
    outputs: ['output', 'error'],
    defaultConfig: { taskType: 'custom' },
    description: '执行自定义任务'
  }
])

// 事件处理
const handleDragStart = (event: DragEvent, nodeType: NodeTypeConfig) => {
  emit('node-drag-start', nodeType, event)
}

const handleDragEnd = () => {
  // 拖拽结束处理
}

const handleZoomIn = () => {
  emit('zoom-in')
}

const handleZoomOut = () => {
  emit('zoom-out')
}

const handleFitView = () => {
  emit('fit-view')
}

const handleCenterView = () => {
  emit('center-view')
}

const handleUndo = () => {
  emit('undo')
}

const handleRedo = () => {
  emit('redo')
}

const handleDelete = () => {
  emit('delete')
}

const handleSave = () => {
  emit('save')
}

const handleLoad = () => {
  emit('load')
}

const handleNew = () => {
  emit('new')
}

const handleExport = () => {
  emit('export')
}
</script>

<style scoped>
.workflow-toolbar {
  width: 280px;
  height: 100%;
  background: #f8f9fa;
  border-right: 1px solid #e1e5e9;
  display: flex;
  flex-direction: column;
  overflow-y: auto;
}

.toolbar-section {
  padding: 16px;
  border-bottom: 1px solid #e1e5e9;
}

.toolbar-section:last-child {
  border-bottom: none;
}

.toolbar-section h3 {
  margin: 0 0 12px 0;
  font-size: 14px;
  font-weight: 600;
  color: #333;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.node-palette {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.node-item {
  display: flex;
  align-items: center;
  padding: 8px 12px;
  background: white;
  border: 1px solid #e1e5e9;
  border-radius: 6px;
  cursor: grab;
  transition: all 0.2s ease;
  user-select: none;
}

.node-item:hover {
  border-color: #007acc;
  box-shadow: 0 2px 4px rgba(0, 122, 204, 0.1);
}

.node-item:active {
  cursor: grabbing;
}

.node-icon {
  font-size: 16px;
  margin-right: 8px;
  width: 20px;
  text-align: center;
}

.node-info {
  flex: 1;
}

.node-label {
  font-weight: 500;
  font-size: 13px;
  color: #333;
  margin-bottom: 2px;
}

.node-description {
  font-size: 11px;
  color: #666;
}

.canvas-tools,
.edit-tools,
.workflow-actions {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.tool-button {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  background: white;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  font-size: 13px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.tool-button:hover:not(:disabled) {
  background: #007acc;
  color: white;
  border-color: #007acc;
}

.tool-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.tool-button.primary {
  background: #007acc;
  color: white;
  border-color: #007acc;
}

.tool-button.danger:hover:not(:disabled) {
  background: #ff4757;
  border-color: #ff4757;
}

.tool-button span {
  font-size: 14px;
  width: 16px;
  text-align: center;
}
</style>