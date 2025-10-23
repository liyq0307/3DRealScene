<template>
  <!-- 工作流设计器视图 - 全新设计 -->
  <div class="workflow-designer">
    <!-- 顶部导航栏 -->
    <header class="designer-header">
      <div class="header-left">
        <div class="logo-section">
          <span class="logo-icon">⚙️</span>
          <h1>工作流设计器</h1>
        </div>
        <div class="workflow-info">
          <input
            v-model="workflow.name"
            class="workflow-name-input"
            placeholder="未命名工作流"
            @blur="handleWorkflowNameChange"
          />
          <span class="workflow-status" :class="workflowStatus">
            {{ workflowStatusText }}
          </span>
        </div>
      </div>

      <div class="header-center">
        <!-- 快速操作工具 -->
        <div class="quick-tools">
          <button
            @click="handleUndo"
            :disabled="!canUndo"
            class="tool-btn"
            title="撤销 (Ctrl+Z)"
          >
            <span class="icon">↶</span>
          </button>
          <button
            @click="handleRedo"
            :disabled="!canRedo"
            class="tool-btn"
            title="重做 (Ctrl+Y)"
          >
            <span class="icon">↷</span>
          </button>
          <div class="divider"></div>
          <button
            @click="handleZoomOut"
            class="tool-btn"
            title="缩小 (Ctrl+-)"
          >
            <span class="icon">🔍-</span>
          </button>
          <span class="zoom-level">{{ Math.round(canvasState.scale * 100) }}%</span>
          <button
            @click="handleZoomIn"
            class="tool-btn"
            title="放大 (Ctrl++)"
          >
            <span class="icon">🔍+</span>
          </button>
          <button
            @click="handleFitView"
            class="tool-btn"
            title="适应视图"
          >
            <span class="icon">📐</span>
          </button>
        </div>
      </div>

      <div class="header-right">
        <button @click="handleValidate" class="header-btn" title="验证工作流">
          <span class="icon">✓</span>
          验证
        </button>
        <button @click="handleRun" class="header-btn success" :disabled="isRunning">
          <span class="icon">{{ isRunning ? '⏳' : '▶' }}</span>
          {{ isRunning ? '运行中...' : '运行' }}
        </button>
        <button @click="handleSave" class="header-btn primary">
          <span class="icon">💾</span>
          保存
        </button>
        <div class="dropdown">
          <button @click="toggleMoreMenu" class="header-btn">
            <span class="icon">⋮</span>
            更多
          </button>
          <div v-if="showMoreMenu" class="dropdown-menu">
            <button @click="handleLoad" class="dropdown-item">
              <span class="icon">📁</span>
              打开工作流
            </button>
            <button @click="handleNewWorkflow" class="dropdown-item">
              <span class="icon">🆕</span>
              新建工作流
            </button>
            <div class="dropdown-divider"></div>
            <button @click="handleExport" class="dropdown-item">
              <span class="icon">📤</span>
              导出JSON
            </button>
            <button @click="handleImport" class="dropdown-item">
              <span class="icon">📥</span>
              导入JSON
            </button>
            <div class="dropdown-divider"></div>
            <button @click="handleDuplicate" class="dropdown-item">
              <span class="icon">📋</span>
              复制工作流
            </button>
            <button @click="handleSettings" class="dropdown-item">
              <span class="icon">⚙</span>
              设置
            </button>
          </div>
        </div>
      </div>
    </header>

    <!-- 主设计区域 -->
    <div class="designer-main">
      <!-- 左侧节点工具面板 -->
      <aside class="left-panel" :class="{ collapsed: leftPanelCollapsed }">
        <div class="panel-header">
          <h3 v-if="!leftPanelCollapsed">节点库</h3>
          <button @click="toggleLeftPanel" class="toggle-btn">
            {{ leftPanelCollapsed ? '▶' : '◀' }}
          </button>
        </div>
        <div v-if="!leftPanelCollapsed" class="panel-content">
          <WorkflowToolbar
            :can-undo="canUndo"
            :can-redo="canRedo"
            :has-selection="!!canvasState.selectedNodeId"
            @node-drag-start="handleNodeDragStart"
            @zoom-in="handleZoomIn"
            @zoom-out="handleZoomOut"
            @fit-view="handleFitView"
            @center-view="handleCenterView"
            @undo="handleUndo"
            @redo="handleRedo"
            @delete="handleDelete"
            @save="handleSave"
            @load="handleLoad"
            @new="handleNewWorkflow"
            @export="handleExport"
          />
        </div>
      </aside>

      <!-- 中央画布区域 -->
      <div class="canvas-area">
        <!-- 画布工具栏 -->
        <div class="canvas-toolbar">
          <div class="canvas-toolbar-left">
            <button
              v-for="view in canvasViews"
              :key="view.id"
              @click="switchView(view.id)"
              :class="['view-tab', { active: currentView === view.id }]"
            >
              <span class="icon">{{ view.icon }}</span>
              {{ view.label }}
            </button>
          </div>
          <div class="canvas-toolbar-right">
            <button @click="toggleMinimap" class="canvas-tool-btn" :class="{ active: showMinimap }">
              <span class="icon">🗺</span>
              小地图
            </button>
            <button @click="toggleGrid" class="canvas-tool-btn" :class="{ active: showGrid }">
              <span class="icon">📏</span>
              网格
            </button>
          </div>
        </div>

        <!-- 画布主体 -->
        <WorkflowCanvas
          :workflow="workflow"
          :canvas-state="canvasState"
          :show-grid="showGrid"
          @update:canvas-state="handleCanvasStateUpdate"
          @node-select="handleNodeSelect"
          @node-add="handleNodeAdd"
          @node-delete="handleNodeDelete"
          @node-update="handleNodeUpdate"
          @connection-add="handleConnectionAdd"
          @connection-delete="handleConnectionDelete"
          @node-context-menu="handleNodeContextMenu"
        />

        <!-- 小地图 -->
        <div v-if="showMinimap" class="minimap">
          <div class="minimap-content">
            <svg class="minimap-svg" width="200" height="150">
              <rect
                v-for="node in workflow.nodes"
                :key="node.id"
                :x="node.position.x / 10"
                :y="node.position.y / 10"
                width="12"
                height="8"
                :fill="getNodeColor(node.type)"
                :class="{ selected: canvasState.selectedNodeId === node.id }"
              />
            </svg>
          </div>
        </div>
      </div>

      <!-- 右侧属性面板 -->
      <aside class="right-panel" :class="{ collapsed: rightPanelCollapsed }">
        <div class="panel-header">
          <button @click="toggleRightPanel" class="toggle-btn">
            {{ rightPanelCollapsed ? '◀' : '▶' }}
          </button>
          <h3 v-if="!rightPanelCollapsed">属性面板</h3>
        </div>
        <div v-if="!rightPanelCollapsed" class="panel-content">
          <WorkflowProperties
            :selected-node="selectedNode"
            @update:node="handlePropertyUpdate"
            @close="handlePropertiesClose"
          />
        </div>
      </aside>
    </div>

    <!-- 增强状态栏 -->
    <footer class="designer-footer">
      <div class="status-left">
        <div class="status-item">
          <span class="status-label">节点:</span>
          <span class="status-value">{{ workflow.nodes.length }}</span>
        </div>
        <div class="status-item">
          <span class="status-label">连接:</span>
          <span class="status-value">{{ workflow.connections.length }}</span>
        </div>
        <div class="status-item">
          <span class="status-label">已选中:</span>
          <span class="status-value">{{ canvasState.selectedNodeId ? '1个节点' : '无' }}</span>
        </div>
        <div class="status-item" v-if="lastSaveTime">
          <span class="status-label">最后保存:</span>
          <span class="status-value">{{ lastSaveTime }}</span>
        </div>
      </div>
      <div class="status-center">
        <div v-if="statusMessage" class="status-message" :class="statusMessageType">
          <span class="status-icon">{{ statusMessageIcon }}</span>
          {{ statusMessage }}
        </div>
      </div>
      <div class="status-right">
        <div class="status-item">
          <span class="status-label">版本:</span>
          <span class="status-value">{{ workflow.version || '1.0.0' }}</span>
        </div>
        <div class="status-item">
          <span class="status-label">缩放:</span>
          <span class="status-value">{{ Math.round(canvasState.scale * 100) }}%</span>
        </div>
      </div>
    </footer>

    <!-- 模态框 -->
    <!-- 保存对话框 -->
    <div v-if="showSaveDialog" class="modal-overlay" @click="closeSaveDialog">
      <div class="modal-content" @click.stop>
        <h3>保存工作流</h3>
        <div class="form-group">
          <label for="workflow-name">工作流名称</label>
          <input
            id="workflow-name"
            v-model="workflow.name"
            type="text"
            class="modal-input"
            placeholder="输入工作流名称"
          />
        </div>
        <div class="form-group">
          <label for="workflow-description">描述</label>
          <textarea
            id="workflow-description"
            v-model="workflow.description"
            class="modal-textarea"
            placeholder="输入工作流描述"
          ></textarea>
        </div>
        <div class="modal-actions">
          <button @click="closeSaveDialog" class="btn secondary">取消</button>
          <button @click="confirmSave" class="btn primary">保存</button>
        </div>
      </div>
    </div>

    <!-- 加载对话框 -->
    <div v-if="showLoadDialog" class="modal-overlay" @click="closeLoadDialog">
      <div class="modal-content" @click.stop>
        <h3>加载工作流</h3>
        <div class="workflow-list">
          <div
            v-for="wf in workflowList"
            :key="wf.id"
            class="workflow-item"
            @click="loadWorkflow(wf.id!)"
          >
            <h4>{{ wf.name }}</h4>
            <p>{{ wf.description }}</p>
            <small>更新时间: {{ formatDate(wf.updatedAt) }}</small>
          </div>
        </div>
        <div class="modal-actions">
          <button @click="closeLoadDialog" class="btn secondary">关闭</button>
        </div>
      </div>
    </div>

    <!-- 设置对话框 -->
    <div v-if="showSettingsDialog" class="modal-overlay" @click="closeSettingsDialog">
      <div class="modal-content" @click.stop>
        <h3>⚙ 工作流设计器设置</h3>

        <div class="settings-section">
          <h4>画布设置</h4>
          <div class="form-group">
            <label class="checkbox-label">
              <input type="checkbox" v-model="settings.showGrid" />
              <span>显示网格</span>
            </label>
            <p class="setting-description">在画布上显示网格线以便对齐节点</p>
          </div>

          <div class="form-group">
            <label class="checkbox-label">
              <input type="checkbox" v-model="settings.snapToGrid" />
              <span>对齐网格</span>
            </label>
            <p class="setting-description">移动节点时自动对齐到网格</p>
          </div>

          <div class="form-group">
            <label for="grid-size">网格大小</label>
            <input
              id="grid-size"
              v-model.number="settings.gridSize"
              type="number"
              min="10"
              max="50"
              step="5"
              class="modal-input"
            />
            <p class="setting-description">网格单元格的像素大小 (10-50)</p>
          </div>
        </div>

        <div class="settings-section">
          <h4>显示设置</h4>
          <div class="form-group">
            <label class="checkbox-label">
              <input type="checkbox" v-model="settings.showMinimap" />
              <span>显示小地图</span>
            </label>
            <p class="setting-description">在画布右下角显示工作流缩略图</p>
          </div>

          <div class="form-group">
            <label for="theme">主题</label>
            <select id="theme" v-model="settings.theme" class="modal-input">
              <option value="light">浅色主题</option>
              <option value="dark">深色主题</option>
              <option value="auto">跟随系统</option>
            </select>
            <p class="setting-description">选择界面主题外观</p>
          </div>
        </div>

        <div class="settings-section">
          <h4>自动保存</h4>
          <div class="form-group">
            <label class="checkbox-label">
              <input type="checkbox" v-model="settings.autoSave" />
              <span>启用自动保存</span>
            </label>
            <p class="setting-description">定期自动保存工作流</p>
          </div>

          <div class="form-group" v-if="settings.autoSave">
            <label for="auto-save-interval">自动保存间隔 (秒)</label>
            <input
              id="auto-save-interval"
              v-model.number="settings.autoSaveInterval"
              type="number"
              min="10"
              max="300"
              step="10"
              class="modal-input"
            />
            <p class="setting-description">自动保存的时间间隔 (10-300秒)</p>
          </div>
        </div>

        <div class="modal-actions">
          <button @click="closeSettingsDialog" class="btn secondary">取消</button>
          <button @click="confirmSettings" class="btn primary">保存设置</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import type { WorkflowDefinition, WorkflowNode, WorkflowConnection, CanvasState, Position, NodeTypeConfig } from '@/types/workflow'
import WorkflowToolbar from '@/components/workflow/WorkflowToolbar.vue'
import WorkflowCanvas from '@/components/workflow/WorkflowCanvas.vue'
import WorkflowProperties from '@/components/workflow/WorkflowProperties.vue'
import { workflowService } from '@/services/api'
import { useWorkflowHistory } from '@/composables/useWorkflowHistory'
import { useKeyboardShortcuts, COMMON_SHORTCUTS } from '@/composables/useKeyboardShortcuts'

// 使用Vue Router
const router = useRouter()

// 响应式状态
const workflow = ref<WorkflowDefinition>({
  name: '',
  nodes: [],
  connections: [],
  version: '1.0.0'
})

const canvasState = ref<CanvasState>({
  scale: 1,
  offsetX: 0,
  offsetY: 0
})

// 初始化历史记录管理
const history = useWorkflowHistory(workflow)

// UI状态
const leftPanelCollapsed = ref(false)
const rightPanelCollapsed = ref(false)
const showMoreMenu = ref(false)
const showMinimap = ref(true)
const showGrid = ref(true)
const currentView = ref('design')

// 工作流状态 - 使用history的canUndo和canRedo
const canUndo = history.canUndo
const canRedo = history.canRedo
const isRunning = ref(false)
const workflowStatus = ref('draft') // draft, valid, invalid, running
const statusMessage = ref('')
const statusMessageType = ref('info') // info, success, warning, error
const lastSaveTime = ref('')

// 对话框状态
const showSaveDialog = ref(false)
const showLoadDialog = ref(false)
const showSettingsDialog = ref(false)
const workflowList = ref<WorkflowDefinition[]>([])

// 设置选项
const settings = ref({
  autoSave: true,
  autoSaveInterval: 30,
  gridSize: 20,
  snapToGrid: true,
  showMinimap: true,
  showGrid: true,
  theme: 'light'
})

// 画布视图
const canvasViews = [
  { id: 'design', label: '设计视图', icon: '✏' },
  { id: 'preview', label: '预览', icon: '👁' },
  { id: 'data', label: '数据流', icon: '📊' }
]

// 计算属性
const selectedNode = computed(() => {
  if (!canvasState.value.selectedNodeId) return null
  return workflow.value.nodes.find(node => node.id === canvasState.value.selectedNodeId) || null
})

const workflowStatusText = computed(() => {
  const statusMap: Record<string, string> = {
    draft: '草稿',
    valid: '有效',
    invalid: '无效',
    running: '运行中'
  }
  return statusMap[workflowStatus.value] || '未知'
})

const statusMessageIcon = computed(() => {
  const iconMap: Record<string, string> = {
    info: 'ℹ',
    success: '✓',
    warning: '⚠',
    error: '✗'
  }
  return iconMap[statusMessageType.value] || 'ℹ'
})

// UI方法
const toggleLeftPanel = () => {
  leftPanelCollapsed.value = !leftPanelCollapsed.value
}

const toggleRightPanel = () => {
  rightPanelCollapsed.value = !rightPanelCollapsed.value
}

const toggleMoreMenu = () => {
  showMoreMenu.value = !showMoreMenu.value
}

const toggleMinimap = () => {
  showMinimap.value = !showMinimap.value
}

const toggleGrid = () => {
  showGrid.value = !showGrid.value
}

const switchView = (viewId: string) => {
  currentView.value = viewId
  showStatusMessage(`切换到${canvasViews.find(v => v.id === viewId)?.label}`, 'info')
}

const getNodeColor = (nodeType: string): string => {
  const colorMap: Record<string, string> = {
    start: '#4caf50',
    end: '#f44336',
    delay: '#ff9800',
    condition: '#2196f3',
    task: '#9c27b0',
    default: '#757575'
  }
  return colorMap[nodeType] || colorMap.default
}

// 消息显示
const showStatusMessage = (message: string, type: 'info' | 'success' | 'warning' | 'error' = 'info', duration = 3000) => {
  statusMessage.value = message
  statusMessageType.value = type
  if (duration > 0) {
    setTimeout(() => {
      statusMessage.value = ''
    }, duration)
  }
}

// 方法
const handleCanvasStateUpdate = (newState: CanvasState) => {
  canvasState.value = newState
}

const handleNodeSelect = (nodeId: string | undefined) => {
  canvasState.value.selectedNodeId = nodeId
  if (nodeId && rightPanelCollapsed.value) {
    rightPanelCollapsed.value = false
  }
}

const handleWorkflowNameChange = () => {
  showStatusMessage('工作流名称已更新', 'info')
}

const handleValidate = () => {
  const result = validateWorkflow()
  if (result.valid) {
    showStatusMessage('工作流验证通过!', 'success')
  } else {
    showStatusMessage(`工作流验证失败: ${result.errors.join(', ')}`, 'error', 5000)
  }
}

const validateWorkflow = (): { valid: boolean; errors: string[] } => {
  const errors: string[] = []

  // 检查是否有开始节点
  const hasStartNode = workflow.value.nodes.some(node => node.type === 'start')
  if (!hasStartNode) {
    errors.push('缺少开始节点')
  }

  // 检查是否有结束节点
  const hasEndNode = workflow.value.nodes.some(node => node.type === 'end')
  if (!hasEndNode) {
    errors.push('缺少结束节点')
  }

  // 检查孤立节点
  const connectedNodes = new Set<string>()
  workflow.value.connections.forEach(conn => {
    connectedNodes.add(conn.source)
    connectedNodes.add(conn.target)
  })
  const isolatedNodes = workflow.value.nodes.filter(
    node => !connectedNodes.has(node.id) && node.type !== 'start' && node.type !== 'end'
  )
  if (isolatedNodes.length > 0) {
    errors.push(`${isolatedNodes.length}个孤立节点`)
  }

  const valid = errors.length === 0
  workflowStatus.value = valid ? 'valid' : 'invalid'
  return { valid, errors }
}

const handleImport = () => {
  const input = document.createElement('input')
  input.type = 'file'
  input.accept = '.json'
  input.onchange = (e: Event) => {
    const file = (e.target as HTMLInputElement).files?.[0]
    if (file) {
      const reader = new FileReader()
      reader.onload = (event) => {
        try {
          const importedWorkflow = JSON.parse(event.target?.result as string)
          workflow.value = importedWorkflow
          showStatusMessage(`导入工作流: ${importedWorkflow.name}`, 'success')
          validateWorkflow()
        } catch (error) {
          showStatusMessage('导入失败: JSON格式错误', 'error')
        }
      }
      reader.readAsText(file)
    }
  }
  input.click()
  showMoreMenu.value = false
}

const handleDuplicate = () => {
  const duplicated = JSON.parse(JSON.stringify(workflow.value))
  duplicated.name = `${duplicated.name} (副本)`
  duplicated.id = undefined
  workflow.value = duplicated
  showStatusMessage('工作流已复制', 'success')
  showMoreMenu.value = false
}

const handleSettings = () => {
  showSettingsDialog.value = true
  showMoreMenu.value = false
}

const handleNodeAdd = (node: WorkflowNode, position: Position) => {
  const newNode = {
    ...node,
    id: generateId(),
    position
  }
  workflow.value.nodes.push(newNode)
  showStatusMessage(`添加节点: ${newNode.label}`, 'success')
  validateWorkflow()
}

const handleNodeDelete = (nodeId: string) => {
  const index = workflow.value.nodes.findIndex(node => node.id === nodeId)
  if (index !== -1) {
    const deletedNode = workflow.value.nodes[index]
    workflow.value.nodes.splice(index, 1)
    // 删除相关连接
    workflow.value.connections = workflow.value.connections.filter(
      conn => conn.source !== nodeId && conn.target !== nodeId
    )
    statusMessage.value = `删除节点: ${deletedNode.label}`
  }
}

const handleNodeUpdate = (nodeId: string, updates: Partial<WorkflowNode>) => {
  const node = workflow.value.nodes.find(n => n.id === nodeId)
  if (node) {
    Object.assign(node, updates)
  }
}

const handleConnectionAdd = (connection: Omit<WorkflowConnection, 'id'>) => {
  const newConnection = {
    ...connection,
    id: generateId()
  }
  workflow.value.connections.push(newConnection)
  statusMessage.value = '添加连接'
}

const handleConnectionDelete = (connectionId: string) => {
  const index = workflow.value.connections.findIndex(conn => conn.id === connectionId)
  if (index !== -1) {
    workflow.value.connections.splice(index, 1)
    statusMessage.value = '删除连接'
  }
}

const handleNodeContextMenu = (nodeId: string, position: Position) => {
  // 处理节点右键菜单
  console.log('Node context menu:', nodeId, position)
}

const handlePropertyUpdate = (nodeId: string, updates: Partial<WorkflowNode>) => {
  handleNodeUpdate(nodeId, updates)
}

const handlePropertiesClose = () => {
  canvasState.value.selectedNodeId = undefined
}

const handleNodeDragStart = (nodeType: NodeTypeConfig, event: DragEvent) => {
  // 处理从工具栏拖拽节点到画布
  event.dataTransfer!.setData('application/json', JSON.stringify(nodeType))
}

const handleZoomIn = () => {
  canvasState.value.scale = Math.min(canvasState.value.scale * 1.2, 2)
}

const handleZoomOut = () => {
  canvasState.value.scale = Math.max(canvasState.value.scale / 1.2, 0.1)
}

const handleFitView = () => {
  // 适应视图逻辑
  if (workflow.value.nodes.length === 0) return

  const nodes = workflow.value.nodes
  const minX = Math.min(...nodes.map(n => n.position.x))
  const maxX = Math.max(...nodes.map(n => n.position.x + 120)) // 节点宽度
  const minY = Math.min(...nodes.map(n => n.position.y))
  const maxY = Math.max(...nodes.map(n => n.position.y + 80)) // 节点高度

  const centerX = (minX + maxX) / 2
  const centerY = (minY + maxY) / 2

  canvasState.value.offsetX = -centerX + 400 // 画布宽度的一半
  canvasState.value.offsetY = -centerY + 300 // 画布高度的一半
  canvasState.value.scale = 1
}

const handleCenterView = () => {
  canvasState.value.offsetX = 0
  canvasState.value.offsetY = 0
  canvasState.value.scale = 1
}

const handleUndo = () => {
  const previousState = history.undo()
  if (previousState) {
    workflow.value = JSON.parse(JSON.stringify(previousState))
    showStatusMessage('已撤销', 'info')
  }
}

const handleRedo = () => {
  const nextState = history.redo()
  if (nextState) {
    workflow.value = JSON.parse(JSON.stringify(nextState))
    showStatusMessage('已重做', 'info')
  }
}

const handleDelete = () => {
  if (canvasState.value.selectedNodeId) {
    handleNodeDelete(canvasState.value.selectedNodeId)
  }
}

const handleSave = () => {
  showSaveDialog.value = true
}

const handleRun = () => {
  // 运行工作流逻辑
  statusMessage.value = '正在运行工作流...'
  setTimeout(() => {
    statusMessage.value = '工作流运行完成'
  }, 2000)
}

const handleExport = () => {
  const dataStr = JSON.stringify(workflow.value, null, 2)
  const dataUri = 'data:application/json;charset=utf-8,'+ encodeURIComponent(dataStr)

  const exportFileDefaultName = `${workflow.value.name || 'workflow'}.json`

  const linkElement = document.createElement('a')
  linkElement.setAttribute('href', dataUri)
  linkElement.setAttribute('download', exportFileDefaultName)
  linkElement.click()

  statusMessage.value = '工作流已导出'
}

const handleLoad = async () => {
  try {
    // 假设用户ID为1，实际应该从认证信息中获取
    const userId = '1'
    workflowList.value = await workflowService.getUserWorkflows(userId)
    showLoadDialog.value = true
  } catch (error) {
    console.error('加载工作流列表失败:', error)
    statusMessage.value = '加载工作流列表失败'
  }
}

const handleNewWorkflow = () => {
  workflow.value = {
    name: '',
    nodes: [],
    connections: [],
    version: '1.0.0'
  }
  canvasState.value = {
    scale: 1,
    offsetX: 0,
    offsetY: 0
  }
  statusMessage.value = '新建工作流'
}

const closeSaveDialog = () => {
  showSaveDialog.value = false
}

const confirmSave = async () => {
  try {
    // 假设用户ID为1，实际应该从认证信息中获取
    const userId = '1'

    const savedWorkflow = await workflowService.saveWorkflow(workflow.value, userId)
    workflow.value = savedWorkflow
    statusMessage.value = '工作流保存成功'
    closeSaveDialog()
  } catch (error) {
    console.error('保存工作流失败:', error)
    statusMessage.value = '工作流保存失败'
  }
}

const closeLoadDialog = () => {
  showLoadDialog.value = false
}

const closeSettingsDialog = () => {
  showSettingsDialog.value = false
}

const confirmSettings = () => {
  // 应用设置
  showMinimap.value = settings.value.showMinimap
  showGrid.value = settings.value.showGrid
  showStatusMessage('设置已保存', 'success')
  closeSettingsDialog()
}

const loadWorkflow = async (workflowId: string) => {
  try {
    const loadedWorkflow = await workflowService.getWorkflow(workflowId)
    workflow.value = loadedWorkflow
    canvasState.value = {
      scale: 1,
      offsetX: 0,
      offsetY: 0
    }
    closeLoadDialog()
    statusMessage.value = `加载工作流: ${loadedWorkflow.name}`
  } catch (error) {
    console.error('加载工作流失败:', error)
    statusMessage.value = '加载工作流失败'
  }
}

const generateId = (): string => {
  return Date.now().toString(36) + Math.random().toString(36).substr(2)
}

const formatDate = (dateStr?: string): string => {
  if (!dateStr) return ''
  return new Date(dateStr).toLocaleString('zh-CN')
}

// 监听工作流变化,保存历史记录
watch(
  () => workflow.value,
  (newWorkflow) => {
    // 保存到历史记录
    history.saveState(newWorkflow)
  },
  { deep: true }
)

// 设置键盘快捷键
useKeyboardShortcuts([
  {
    ...COMMON_SHORTCUTS.UNDO,
    handler: handleUndo
  },
  {
    ...COMMON_SHORTCUTS.REDO,
    handler: handleRedo
  },
  {
    ...COMMON_SHORTCUTS.SAVE,
    handler: handleSave
  },
  {
    ...COMMON_SHORTCUTS.DELETE,
    handler: handleDelete
  },
  {
    ...COMMON_SHORTCUTS.ZOOM_IN,
    handler: handleZoomIn
  },
  {
    ...COMMON_SHORTCUTS.ZOOM_OUT,
    handler: handleZoomOut
  },
  {
    ...COMMON_SHORTCUTS.DUPLICATE,
    handler: handleDuplicate
  }
])

// 生命周期
onMounted(async () => {
  console.log('[WorkflowDesigner] Component mounted')
  // 初始化历史记录
  history.init(workflow.value)

  // 初始化逻辑
  // 检查URL参数中是否有工作流ID
  const urlParams = new URLSearchParams(window.location.search)
  const workflowId = urlParams.get('id')

  if (workflowId) {
    try {
      const loadedWorkflow = await workflowService.getWorkflow(workflowId)
      workflow.value = loadedWorkflow
      history.init(loadedWorkflow)
      statusMessage.value = `加载工作流: ${loadedWorkflow.name}`
    } catch (error) {
      console.error('加载工作流失败:', error)
      statusMessage.value = '加载工作流失败，返回空工作流'
    }
  }
})
</script>

<style src="@/styles/workflow-enhanced.css"></style>

<style scoped>
.workflow-designer {
  height: 100vh;
  display: flex;
  flex-direction: column;
  background: #f5f5f5;
}

.designer-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 20px;
  background: white;
  border-bottom: 1px solid #e1e5e9;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.header-left {
  display: flex;
  align-items: center;
  gap: 16px;
}

.header-left h1 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: #333;
}

.workflow-title {
  color: #666;
  font-size: 14px;
}

.header-right {
  display: flex;
  gap: 8px;
}

.header-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 8px 16px;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  background: white;
  font-size: 13px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.header-btn:hover {
  background: #f8f9fa;
  border-color: #007acc;
}

.header-btn.primary {
  background: #007acc;
  color: white;
  border-color: #007acc;
}

.header-btn.primary:hover {
  background: #005999;
}

.designer-main {
  flex: 1;
  display: flex;
  overflow: hidden;
}

/* 左侧面板样式 */
.left-panel {
  width: 280px;
  min-width: 280px;
  background: white;
  border-right: 1px solid #e1e5e9;
  display: flex;
  flex-direction: column;
  transition: all 0.3s ease;
}

.left-panel.collapsed {
  width: 40px;
  min-width: 40px;
}

/* 右侧面板样式 */
.right-panel {
  width: 320px;
  min-width: 320px;
  background: white;
  border-left: 1px solid #e1e5e9;
  display: flex;
  flex-direction: column;
  transition: all 0.3s ease;
  overflow: hidden;
}

.right-panel.collapsed {
  width: 40px;
  min-width: 40px;
}

/* 面板头部 */
.panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-bottom: 1px solid #e1e5e9;
  background: #f8f9fa;
}

.panel-header h3 {
  margin: 0;
  font-size: 14px;
  font-weight: 600;
  color: #333;
}

.toggle-btn {
  background: none;
  border: none;
  color: #666;
  cursor: pointer;
  padding: 4px 8px;
  border-radius: 4px;
  transition: all 0.2s ease;
  font-size: 14px;
}

.toggle-btn:hover {
  background: #e1e5e9;
  color: #333;
}

/* 面板内容 */
.panel-content {
  flex: 1;
  overflow-y: auto;
  overflow-x: hidden;
}

.canvas-area {
  flex: 1;
  position: relative;
  display: flex;
  flex-direction: column;
}

/* 画布工具栏 */
.canvas-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 16px;
  background: white;
  border-bottom: 1px solid #e1e5e9;
}

.canvas-toolbar-left,
.canvas-toolbar-right {
  display: flex;
  align-items: center;
  gap: 8px;
}

.view-tab,
.canvas-tool-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  background: white;
  font-size: 13px;
  cursor: pointer;
  transition: all 0.2s ease;
  color: #666;
}

.view-tab:hover,
.canvas-tool-btn:hover {
  background: #f8f9fa;
  border-color: #007acc;
  color: #333;
}

.view-tab.active,
.canvas-tool-btn.active {
  background: #007acc;
  color: white;
  border-color: #007acc;
}

/* 小地图 */
.minimap {
  position: absolute;
  bottom: 20px;
  right: 20px;
  background: white;
  border: 1px solid #e1e5e9;
  border-radius: 8px;
  padding: 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
  z-index: 100;
}

.minimap-content {
  position: relative;
}

.minimap-svg {
  display: block;
  background: #fafafa;
  border-radius: 4px;
}

.minimap-svg rect {
  stroke: #999;
  stroke-width: 0.5;
  transition: all 0.2s ease;
}

.minimap-svg rect.selected {
  stroke: #007acc;
  stroke-width: 1;
}

/* 头部其他样式 */
.logo-section {
  display: flex;
  align-items: center;
  gap: 8px;
}

.logo-icon {
  font-size: 20px;
}

.workflow-info {
  display: flex;
  align-items: center;
  gap: 12px;
}

.workflow-name-input {
  padding: 4px 8px;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  font-size: 14px;
  background: #f8f9fa;
  transition: all 0.2s ease;
}

.workflow-name-input:focus {
  outline: none;
  border-color: #007acc;
  background: white;
}

.workflow-status {
  padding: 2px 8px;
  border-radius: 12px;
  font-size: 11px;
  font-weight: 500;
  text-transform: uppercase;
}

.workflow-status.draft {
  background: #e3f2fd;
  color: #1976d2;
}

.workflow-status.valid {
  background: #e8f5e8;
  color: #2e7d32;
}

.workflow-status.invalid {
  background: #ffebee;
  color: #c62828;
}

.workflow-status.running {
  background: #fff3e0;
  color: #ef6c00;
}

.header-center {
  flex: 1;
  display: flex;
  justify-content: center;
}

.quick-tools {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 8px;
  background: #f8f9fa;
  border-radius: 6px;
}

.tool-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 6px;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  background: white;
  cursor: pointer;
  transition: all 0.2s ease;
  min-width: 32px;
}

.tool-btn:hover:not(:disabled) {
  background: #007acc;
  color: white;
  border-color: #007acc;
}

.tool-btn:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.zoom-level {
  padding: 0 4px;
  font-size: 12px;
  color: #666;
  min-width: 40px;
  text-align: center;
}

.divider {
  width: 1px;
  height: 20px;
  background: #e1e5e9;
  margin: 0 4px;
}

.dropdown {
  position: relative;
}

.dropdown-menu {
  position: absolute;
  top: 100%;
  right: 0;
  margin-top: 4px;
  background: white;
  border: 1px solid #e1e5e9;
  border-radius: 6px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  min-width: 200px;
  z-index: 1000;
}

.dropdown-item {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  padding: 10px 16px;
  border: none;
  background: none;
  text-align: left;
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 13px;
  color: #333;
}

.dropdown-item:hover {
  background: #f8f9fa;
}

.dropdown-divider {
  height: 1px;
  background: #e1e5e9;
  margin: 4px 0;
}

/* 状态栏样式 */
.status-left,
.status-center,
.status-right {
  display: flex;
  align-items: center;
  gap: 16px;
}

.status-item {
  display: flex;
  align-items: center;
  gap: 4px;
}

.status-label {
  color: #999;
}

.status-value {
  color: #333;
  font-weight: 500;
}

.status-message {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 12px;
  border-radius: 4px;
  font-size: 13px;
}

.status-message.info {
  background: #e3f2fd;
  color: #1976d2;
}

.status-message.success {
  background: #e8f5e8;
  color: #2e7d32;
}

.status-message.warning {
  background: #fff3e0;
  color: #ef6c00;
}

.status-message.error {
  background: #ffebee;
  color: #c62828;
}

.designer-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 20px;
  background: white;
  border-top: 1px solid #e1e5e9;
  font-size: 12px;
  color: #666;
}

.status-info {
  display: flex;
  gap: 16px;
}

.status-message {
  color: #007acc;
  font-weight: 500;
}

/* 模态框样式 */
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-content {
  background: white;
  border-radius: 8px;
  padding: 24px;
  width: 500px;
  max-width: 90vw;
  max-height: 80vh;
  overflow-y: auto;
}

.modal-content h3 {
  margin: 0 0 20px 0;
  font-size: 18px;
  font-weight: 600;
}

.form-group {
  margin-bottom: 16px;
}

.form-group label {
  display: block;
  margin-bottom: 4px;
  font-weight: 500;
  color: #333;
}

.modal-input,
.modal-textarea {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 14px;
}

.modal-input:focus,
.modal-textarea:focus {
  outline: none;
  border-color: #007acc;
}

.modal-textarea {
  min-height: 80px;
  resize: vertical;
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  margin-top: 24px;
}

.btn {
  padding: 8px 16px;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  background: white;
  font-size: 14px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.btn:hover {
  background: #f8f9fa;
}

.btn.primary {
  background: #007acc;
  color: white;
  border-color: #007acc;
}

.btn.primary:hover {
  background: #005999;
}

.btn.secondary:hover {
  border-color: #999;
}

/* 工作流列表 */
.workflow-list {
  max-height: 300px;
  overflow-y: auto;
}

.workflow-item {
  padding: 12px;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  margin-bottom: 8px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.workflow-item:hover {
  border-color: #007acc;
  background: #f8f9fa;
}

.workflow-item h4 {
  margin: 0 0 4px 0;
  font-size: 14px;
  font-weight: 500;
}

.workflow-item p {
  margin: 0 0 4px 0;
  color: #666;
  font-size: 13px;
}

.workflow-item small {
  color: #999;
  font-size: 11px;
}

/* 设置对话框样式 */
.settings-section {
  margin-bottom: 24px;
  padding-bottom: 24px;
  border-bottom: 1px solid #e1e5e9;
}

.settings-section:last-of-type {
  border-bottom: none;
  padding-bottom: 0;
}

.settings-section h4 {
  margin: 0 0 16px 0;
  font-size: 15px;
  font-weight: 600;
  color: #333;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  font-size: 14px;
  color: #333;
}

.checkbox-label input[type="checkbox"] {
  width: 18px;
  height: 18px;
  cursor: pointer;
}

.checkbox-label span {
  font-weight: 500;
}

.setting-description {
  margin: 4px 0 0 26px;
  font-size: 12px;
  color: #666;
  line-height: 1.4;
}

select.modal-input {
  cursor: pointer;
}
</style>