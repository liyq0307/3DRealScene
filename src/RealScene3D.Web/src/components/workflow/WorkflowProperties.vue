<template>
  <!-- 工作流属性面板 -->
  <div class="workflow-properties">
    <div class="properties-header">
      <h3>属性面板</h3>
      <button v-if="selectedNode" @click="closeProperties" class="close-btn">×</button>
    </div>

    <div v-if="!selectedNode" class="no-selection">
      <div class="no-selection-icon">📋</div>
      <p>选择一个节点来查看和编辑其属性</p>
    </div>

    <div v-else-if="nodeModel" class="properties-content">
      <!-- 节点基本信息 -->
      <div class="property-section">
        <h4>基本信息</h4>
        <div class="property-group">
          <label for="node-label">节点名称</label>
          <input
            id="node-label"
            v-model="nodeModel.label"
            type="text"
            class="property-input"
            @input="handlePropertyChange"
          />
        </div>
        <div class="property-group">
          <label>节点类型</label>
          <div class="property-value">{{ selectedNode.type }}</div>
        </div>
        <div class="property-group">
          <label>位置</label>
          <div class="position-inputs">
            <div class="position-input">
              <label for="node-x">X</label>
              <input
                id="node-x"
                v-model.number="nodeModel.position.x"
                type="number"
                class="property-input small"
                @input="handlePropertyChange"
              />
            </div>
            <div class="position-input">
              <label for="node-y">Y</label>
              <input
                id="node-y"
                v-model.number="nodeModel.position.y"
                type="number"
                class="property-input small"
                @input="handlePropertyChange"
              />
            </div>
          </div>
        </div>
      </div>

      <!-- 节点配置 -->
      <div v-if="hasConfig" class="property-section">
        <h4>节点配置</h4>
        <div v-if="selectedNode.type === 'delay'" class="property-group">
          <label for="delay-ms">延迟时间 (毫秒)</label>
          <input
            id="delay-ms"
            v-model="delayMs"
            type="number"
            min="0"
            class="property-input"
            @input="handleDelayMsChange"
          />
        </div>

        <div v-else-if="selectedNode.type === 'condition'" class="property-group">
          <label for="condition">条件表达式</label>
          <textarea
            id="condition"
            v-model="condition"
            class="property-textarea"
            placeholder="输入条件表达式，如: data.status === 'completed'"
            @input="handleConditionChange"
          ></textarea>
        </div>

        <div v-else-if="selectedNode.type === 'task'" class="property-group">
          <label for="task-type">任务类型</label>
          <select
            id="task-type"
            v-model="taskType"
            class="property-select"
            @change="handleTaskTypeChange"
          >
            <option value="custom">自定义任务</option>
            <option value="http">HTTP请求</option>
            <option value="database">数据库操作</option>
            <option value="file">文件操作</option>
          </select>

          <label v-if="nodeModel.config?.taskType === 'http'" for="http-url">请求URL</label>
          <input
            v-if="taskType === 'http'"
            id="http-url"
            v-model="httpUrl"
            type="text"
            class="property-input"
            placeholder="https://api.example.com"
            @input="handleHttpUrlChange"
          />

          <label v-if="nodeModel.config?.taskType === 'http'" for="http-method">请求方法</label>
          <select
            v-if="taskType === 'http'"
            id="http-method"
            v-model="httpMethod"
            class="property-select"
            @change="handleHttpMethodChange"
          >
            <option value="GET">GET</option>
            <option value="POST">POST</option>
            <option value="PUT">PUT</option>
            <option value="DELETE">DELETE</option>
          </select>
        </div>

        <!-- 通用配置编辑器 -->
        <div v-else class="property-group">
          <label>配置 (JSON)</label>
          <textarea
            v-model="configJson"
            class="property-textarea"
            @input="handleJsonConfigChange"
            :class="{ 'error': jsonError }"
          ></textarea>
          <div v-if="jsonError" class="error-message">{{ jsonError }}</div>
        </div>
      </div>

      <!-- 节点端口 -->
      <div class="property-section">
        <h4>端口配置</h4>
        <div class="property-group">
          <label>输入端口</label>
          <div class="ports-list">
            <span v-for="input in selectedNode.inputs" :key="input" class="port-tag">
              {{ input }}
            </span>
            <span v-if="!selectedNode.inputs || selectedNode.inputs.length === 0" class="no-ports">
              无输入端口
            </span>
          </div>
        </div>
        <div class="property-group">
          <label>输出端口</label>
          <div class="ports-list">
            <span v-for="output in selectedNode.outputs" :key="output" class="port-tag">
              {{ output }}
            </span>
            <span v-if="!selectedNode.outputs || selectedNode.outputs.length === 0" class="no-ports">
              无输出端口
            </span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { WorkflowNode } from '@/types/workflow'

// Props定义
interface Props {
  selectedNode?: WorkflowNode | null
}

const props = withDefaults(defineProps<Props>(), {
  selectedNode: null
})

// Emits定义
const emit = defineEmits<{
  'update:node': [nodeId: string, updates: Partial<WorkflowNode>]
  'close': []
}>()

// 响应式状态
const nodeModel = ref<WorkflowNode | null>(null)
const configJson = ref('')
const jsonError = ref('')
const delayMs = ref(0)
const condition = ref('')
const taskType = ref('custom')
const httpUrl = ref('')
const httpMethod = ref('GET')

// 计算属性
const hasConfig = computed(() => {
  return props.selectedNode && (
    props.selectedNode.type !== 'start' &&
    props.selectedNode.type !== 'end'
  )
})

// 监听selectedNode变化，初始化模型
watch(() => props.selectedNode, (newNode) => {
  if (newNode) {
    nodeModel.value = { ...newNode }
    configJson.value = JSON.stringify(newNode.config || {}, null, 2)
    jsonError.value = ''

    // 初始化各个字段
    delayMs.value = (newNode.config?.delayMs as number) || 0
    condition.value = (newNode.config?.condition as string) || ''
    taskType.value = (newNode.config?.taskType as string) || 'custom'
    httpUrl.value = (newNode.config?.url as string) || ''
    httpMethod.value = (newNode.config?.method as string) || 'GET'
  } else {
    nodeModel.value = null
    configJson.value = ''
    jsonError.value = ''
    delayMs.value = 0
    condition.value = ''
    taskType.value = 'custom'
    httpUrl.value = ''
    httpMethod.value = 'GET'
  }
}, { immediate: true })

// 事件处理
const handlePropertyChange = () => {
  if (nodeModel.value && props.selectedNode) {
    emit('update:node', props.selectedNode.id, {
      label: nodeModel.value.label,
      position: { ...nodeModel.value.position },
      config: { ...nodeModel.value.config }
    })
  }
}

const handleDelayMsChange = (event: Event) => {
  const target = event.target as HTMLInputElement
  const value = parseInt(target.value) || 0
  delayMs.value = value
  if (nodeModel.value) {
    nodeModel.value.config = { ...nodeModel.value.config, delayMs: value }
    handlePropertyChange()
  }
}

const handleConditionChange = (event: Event) => {
  const target = event.target as HTMLTextAreaElement
  const value = target.value
  condition.value = value
  if (nodeModel.value) {
    nodeModel.value.config = { ...nodeModel.value.config, condition: value }
    handlePropertyChange()
  }
}

const handleTaskTypeChange = (event: Event) => {
  const target = event.target as HTMLSelectElement
  const value = target.value
  taskType.value = value
  if (nodeModel.value) {
    nodeModel.value.config = { ...nodeModel.value.config, taskType: value }
    handlePropertyChange()
  }
}

const handleHttpUrlChange = (event: Event) => {
  const target = event.target as HTMLInputElement
  const value = target.value
  httpUrl.value = value
  if (nodeModel.value) {
    nodeModel.value.config = { ...nodeModel.value.config, url: value }
    handlePropertyChange()
  }
}

const handleHttpMethodChange = (event: Event) => {
  const target = event.target as HTMLSelectElement
  const value = target.value
  httpMethod.value = value
  if (nodeModel.value) {
    nodeModel.value.config = { ...nodeModel.value.config, method: value }
    handlePropertyChange()
  }
}

const handleJsonConfigChange = () => {
  if (!configJson.value.trim()) {
    jsonError.value = ''
    if (nodeModel.value) {
      nodeModel.value.config = {}
    }
    handlePropertyChange()
    return
  }

  try {
    const parsed = JSON.parse(configJson.value)
    jsonError.value = ''
    if (nodeModel.value) {
      nodeModel.value.config = parsed
    }
    handlePropertyChange()
  } catch (error) {
    jsonError.value = 'JSON格式错误'
  }
}

const closeProperties = () => {
  emit('close')
}
</script>

<style scoped>
.workflow-properties {
  width: 300px;
  height: 100%;
  background: #f8f9fa;
  border-left: 1px solid #e1e5e9;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.properties-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px;
  border-bottom: 1px solid #e1e5e9;
  background: white;
}

.properties-header h3 {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  color: #333;
}

.close-btn {
  background: none;
  border: none;
  font-size: 20px;
  color: #999;
  cursor: pointer;
  padding: 0;
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  transition: all 0.2s ease;
}

.close-btn:hover {
  background: #ff4757;
  color: white;
}

.properties-content {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
}

.no-selection {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 200px;
  color: #666;
  text-align: center;
}

.no-selection-icon {
  font-size: 48px;
  margin-bottom: 16px;
  opacity: 0.5;
}

.property-section {
  margin-bottom: 24px;
}

.property-section h4 {
  margin: 0 0 12px 0;
  font-size: 14px;
  font-weight: 600;
  color: #555;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.property-group {
  margin-bottom: 16px;
}

.property-group label {
  display: block;
  margin-bottom: 4px;
  font-size: 13px;
  font-weight: 500;
  color: #333;
}

.property-input,
.property-textarea,
.property-select {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 13px;
  transition: border-color 0.2s ease;
}

.property-input:focus,
.property-textarea:focus,
.property-select:focus {
  outline: none;
  border-color: #007acc;
}

.property-input.small {
  width: 60px;
}

.property-textarea {
  min-height: 80px;
  resize: vertical;
}

.property-select {
  background: white;
}

.position-inputs {
  display: flex;
  gap: 8px;
}

.position-input {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.ports-list {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.port-tag {
  padding: 2px 6px;
  background: #007acc;
  color: white;
  border-radius: 3px;
  font-size: 11px;
  font-weight: 500;
}

.no-ports {
  color: #999;
  font-style: italic;
  font-size: 12px;
}

.error {
  border-color: #ff4757 !important;
}

.error-message {
  color: #ff4757;
  font-size: 12px;
  margin-top: 4px;
}
</style>