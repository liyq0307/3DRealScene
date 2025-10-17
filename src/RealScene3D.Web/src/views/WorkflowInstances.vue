<template>
  <div class="workflow-instances">
    <!-- 页面标题和快捷操作 -->
    <header class="page-header">
      <div class="header-left">
        <h1>工作流实例管理</h1>
        <p class="subtitle">查看和管理工作流执行实例</p>
      </div>
      <div class="header-right">
        <button @click="refreshInstances" class="btn btn-primary">
          <span class="icon">🔄</span>
          刷新
        </button>
        <button @click="openStartDialog" class="btn btn-success">
          <span class="icon">▶</span>
          启动新实例
        </button>
      </div>
    </header>

    <!-- 搜索和筛选器 -->
    <SearchFilter
      v-model:searchText="searchKeyword"
      :filters="filterConfigs"
      placeholder="搜索实例ID或工作流名称..."
      @search="(text, filters) => { searchKeyword = text; filterWorkflowId = filters.workflowId || ''; filterStatus = filters.status || ''; currentPage = 1 }"
    />

    <!-- 实例列表 -->
    <div class="instances-section">
      <div class="instances-grid">
        <div
          v-for="instance in paginatedInstances"
          :key="instance.id"
          class="instance-card"
          @click="viewInstance(instance)"
        >
          <div class="instance-header">
            <div class="instance-title">
              <h3>{{ instance.workflowName }}</h3>
              <Badge
                :variant="getStatusVariant(instance.status)"
                :label="getStatusText(instance.status)"
              />
            </div>
            <div class="instance-id">ID: {{ instance.id.substring(0, 8) }}</div>
          </div>

          <div class="instance-body">
            <div class="info-grid">
              <div class="info-item">
                <span class="label">开始时间:</span>
                <span class="value">{{ formatDateTime(instance.startedAt) }}</span>
              </div>
              <div class="info-item" v-if="instance.completedAt">
                <span class="label">完成时间:</span>
                <span class="value">{{ formatDateTime(instance.completedAt) }}</span>
              </div>
              <div class="info-item" v-if="instance.completedAt">
                <span class="label">耗时:</span>
                <span class="value">{{ formatDuration(instance.startedAt, instance.completedAt) }}</span>
              </div>
              <div class="info-item" v-if="instance.status === 'Running'">
                <span class="label">已运行:</span>
                <span class="value">{{ formatDuration(instance.startedAt, new Date().toISOString()) }}</span>
              </div>
            </div>

            <!-- 进度条 -->
            <div v-if="instance.status === 'Running' && instance.progress" class="progress-section">
              <div class="progress-bar">
                <div
                  class="progress-fill"
                  :style="{ width: `${instance.progress}%` }"
                ></div>
              </div>
              <span class="progress-text">{{ instance.progress }}%</span>
            </div>

            <!-- 错误信息 -->
            <div v-if="instance.status === 'Failed' && instance.error" class="error-section">
              <span class="error-icon">⚠️</span>
              <span class="error-text">{{ instance.error }}</span>
            </div>
          </div>

          <div class="instance-actions" @click.stop>
            <button
              v-if="instance.status === 'Running'"
              @click="suspendInstance(instance.id)"
              class="btn-sm btn-warning"
            >
              暂停
            </button>
            <button
              v-if="instance.status === 'Suspended'"
              @click="resumeInstance(instance.id)"
              class="btn-sm btn-success"
            >
              恢复
            </button>
            <button
              v-if="instance.status === 'Running' || instance.status === 'Suspended'"
              @click="cancelInstance(instance.id)"
              class="btn-sm btn-danger"
            >
              取消
            </button>
            <button
              @click="viewHistory(instance.id)"
              class="btn-sm btn-primary"
            >
              查看历史
            </button>
          </div>
        </div>
      </div>

      <!-- 空状态 -->
      <div v-if="filteredInstances.length === 0" class="empty-state">
        <p>{{ searchKeyword || filterStatus ? '没有符合条件的实例' : '暂无工作流实例' }}</p>
        <button v-if="!searchKeyword && !filterStatus" @click="openStartDialog" class="btn btn-primary">
          启动第一个实例
        </button>
      </div>

      <!-- 分页组件 -->
      <Pagination
        v-if="filteredInstances.length > 0"
        v-model:currentPage="currentPage"
        v-model:pageSize="pageSize"
        :total="filteredInstances.length"
      />
    </div>

    <!-- 启动工作流对话框 -->
    <div v-if="showStartDialog" class="modal-overlay" @click="closeStartDialog">
      <div class="modal-content" @click.stop>
        <h3>启动工作流实例</h3>
        <div class="form-group">
          <label>选择工作流 *</label>
          <select v-model="startForm.workflowId" class="form-select">
            <option value="">请选择</option>
            <option v-for="wf in workflows" :key="wf.id" :value="wf.id">
              {{ wf.name }}
            </option>
          </select>
        </div>
        <div class="form-group">
          <label>输入参数 (JSON格式)</label>
          <textarea
            v-model="startForm.inputData"
            class="form-textarea"
            placeholder='{ "key": "value" }'
          ></textarea>
        </div>
        <div class="modal-actions">
          <button @click="closeStartDialog" class="btn btn-secondary">
            取消
          </button>
          <button @click="startWorkflow" class="btn btn-primary" :disabled="!startForm.workflowId">
            启动
          </button>
        </div>
      </div>
    </div>

    <!-- 执行历史对话框 -->
    <div v-if="showHistoryDialog" class="modal-overlay" @click="closeHistoryDialog">
      <div class="modal-content large" @click.stop>
        <h3>执行历史</h3>
        <div v-if="currentHistory.length > 0" class="history-timeline">
          <div
            v-for="(record, index) in currentHistory"
            :key="index"
            class="timeline-item"
          >
            <div class="timeline-marker"></div>
            <div class="timeline-content">
              <div class="timeline-header">
                <h4>{{ record.nodeName }}</h4>
                <span :class="['timeline-status', `status-${record.status.toLowerCase()}`]">
                  {{ record.status }}
                </span>
              </div>
              <div class="timeline-body">
                <div class="timeline-info">
                  <span>开始: {{ formatDateTime(record.startedAt) }}</span>
                  <span v-if="record.completedAt">
                    完成: {{ formatDateTime(record.completedAt) }}
                  </span>
                  <span v-if="record.completedAt">
                    耗时: {{ formatDuration(record.startedAt, record.completedAt) }}
                  </span>
                </div>
                <div v-if="record.output" class="timeline-output">
                  <strong>输出:</strong>
                  <pre>{{ formatJson(record.output) }}</pre>
                </div>
                <div v-if="record.error" class="timeline-error">
                  <strong>错误:</strong>
                  <span>{{ record.error }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>
        <div v-else class="empty-state">
          <p>暂无执行历史</p>
        </div>
        <div class="modal-actions">
          <button @click="closeHistoryDialog" class="btn btn-secondary">
            关闭
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { workflowService } from '@/services/api'
import authStore from '@/stores/auth'
import { useMessage } from '@/composables/useMessage'
import SearchFilter from '@/components/SearchFilter.vue'
import Badge from '@/components/Badge.vue'
import Pagination from '@/components/Pagination.vue'
import type { Filter } from '@/components/SearchFilter.vue'

const { success: showSuccess, error: showError } = useMessage()

// 数据状态
const workflows = ref<any[]>([])
const instances = ref<any[]>([])
const currentHistory = ref<any[]>([])

// 筛选状态
const filterWorkflowId = ref('')
const filterStatus = ref('')
const searchKeyword = ref('')

// 分页状态
const currentPage = ref(1)
const pageSize = ref(20)

// 搜索筛选器配置
const filterConfigs: Filter[] = [
  {
    key: 'workflowId',
    label: '工作流',
    options: workflows.value.map(wf => ({ label: wf.name, value: wf.id }))
  },
  {
    key: 'status',
    label: '状态',
    options: [
      { label: '运行中', value: 'Running' },
      { label: '已暂停', value: 'Suspended' },
      { label: '已完成', value: 'Completed' },
      { label: '失败', value: 'Failed' },
      { label: '已取消', value: 'Cancelled' }
    ]
  }
]

// 对话框状态
const showStartDialog = ref(false)
const showHistoryDialog = ref(false)

// 表单数据
const startForm = ref({
  workflowId: '',
  inputData: '{}'
})

// 计算属性
const filteredInstances = computed(() => {
  let result = instances.value

  if (filterWorkflowId.value) {
    result = result.filter(i => i.workflowId === filterWorkflowId.value)
  }

  if (filterStatus.value) {
    result = result.filter(i => i.status === filterStatus.value)
  }

  if (searchKeyword.value) {
    const keyword = searchKeyword.value.toLowerCase()
    result = result.filter(i =>
      i.workflowName.toLowerCase().includes(keyword) ||
      i.id.toLowerCase().includes(keyword)
    )
  }

  return result.sort((a, b) =>
    new Date(b.startedAt).getTime() - new Date(a.startedAt).getTime()
  )
})

const paginatedInstances = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value
  const end = start + pageSize.value
  return filteredInstances.value.slice(start, end)
})

// 数据加载方法
const loadWorkflows = async () => {
  try {
    const userId = authStore.currentUser.value?.id || '00000000-0000-0000-0000-000000000001'
    workflows.value = await workflowService.getUserWorkflows(userId)
  } catch (error) {
    console.error('加载工作流列表失败:', error)
    showError('加载工作流列表失败')
  }
}

const loadInstances = async () => {
  try {
    const userId = authStore.currentUser.value?.id || '00000000-0000-0000-0000-000000000001'
    instances.value = await workflowService.getWorkflowInstances(
      filterWorkflowId.value || undefined,
      userId
    )
  } catch (error) {
    console.error('加载实例列表失败:', error)
    showError('加载实例列表失败')
  }
}

const refreshInstances = async () => {
  await loadInstances()
  showSuccess('刷新成功')
}

// 实例操作方法
const viewInstance = (instance: any) => {
  console.log('查看实例详情:', instance)
}

const openStartDialog = () => {
  startForm.value = {
    workflowId: '',
    inputData: '{}'
  }
  showStartDialog.value = true
}

const closeStartDialog = () => {
  showStartDialog.value = false
}

const startWorkflow = async () => {
  try {
    if (!startForm.value.workflowId) {
      showError('请选择工作流')
      return
    }

    let inputData = {}
    try {
      inputData = JSON.parse(startForm.value.inputData)
    } catch (e) {
      showError('输入参数格式错误，请使用有效的JSON格式')
      return
    }

    const userId = authStore.currentUser.value?.id || '00000000-0000-0000-0000-000000000001'
    await workflowService.startWorkflowInstance(
      startForm.value.workflowId,
      { inputData },
      userId
    )

    showSuccess('工作流实例启动成功')
    closeStartDialog()
    await loadInstances()
  } catch (error) {
    console.error('启动工作流失败:', error)
    showError('启动工作流失败')
  }
}

const suspendInstance = async (instanceId: string) => {
  if (confirm('确定要暂停此实例吗?')) {
    try {
      const userId = authStore.currentUser.value?.id || '00000000-0000-0000-0000-000000000001'
      await workflowService.suspendWorkflowInstance(instanceId, userId)
      showSuccess('实例已暂停')
      await loadInstances()
    } catch (error) {
      console.error('暂停实例失败:', error)
      showError('暂停实例失败')
    }
  }
}

const resumeInstance = async (instanceId: string) => {
  try {
    const userId = authStore.currentUser.value?.id || '00000000-0000-0000-0000-000000000001'
    await workflowService.resumeWorkflowInstance(instanceId, userId)
    showSuccess('实例已恢复')
    await loadInstances()
  } catch (error) {
    console.error('恢复实例失败:', error)
    showError('恢复实例失败')
  }
}

const cancelInstance = async (instanceId: string) => {
  if (confirm('确定要取消此实例吗? 此操作不可撤销。')) {
    try {
      const userId = authStore.currentUser.value?.id || '00000000-0000-0000-0000-000000000001'
      await workflowService.cancelWorkflowInstance(instanceId, userId)
      showSuccess('实例已取消')
      await loadInstances()
    } catch (error) {
      console.error('取消实例失败:', error)
      showError('取消实例失败')
    }
  }
}

const viewHistory = async (instanceId: string) => {
  try {
    currentHistory.value = await workflowService.getWorkflowExecutionHistory(instanceId)
    showHistoryDialog.value = true
  } catch (error) {
    console.error('加载执行历史失败:', error)
    showError('加载执行历史失败')
  }
}

const closeHistoryDialog = () => {
  showHistoryDialog.value = false
  currentHistory.value = []
}

// 工具方法
const getStatusText = (status: string): string => {
  const statusMap: Record<string, string> = {
    Running: '运行中',
    Suspended: '已暂停',
    Completed: '已完成',
    Failed: '失败',
    Cancelled: '已取消'
  }
  return statusMap[status] || status
}

const getStatusVariant = (status: string): 'warning' | 'primary' | 'success' | 'danger' | 'gray' => {
  const variantMap: Record<string, 'warning' | 'primary' | 'success' | 'danger' | 'gray'> = {
    Running: 'warning',
    Suspended: 'primary',
    Completed: 'success',
    Failed: 'danger',
    Cancelled: 'gray'
  }
  return variantMap[status] || 'gray'
}

const formatDateTime = (dateStr: string): string => {
  if (!dateStr) return '-'
  return new Date(dateStr).toLocaleString('zh-CN')
}

const formatDuration = (start: string, end: string): string => {
  const startTime = new Date(start).getTime()
  const endTime = new Date(end).getTime()
  const duration = endTime - startTime

  const seconds = Math.floor(duration / 1000)
  const minutes = Math.floor(seconds / 60)
  const hours = Math.floor(minutes / 60)

  if (hours > 0) {
    return `${hours}小时${minutes % 60}分钟`
  } else if (minutes > 0) {
    return `${minutes}分钟${seconds % 60}秒`
  } else {
    return `${seconds}秒`
  }
}

const formatJson = (data: any): string => {
  try {
    return JSON.stringify(typeof data === 'string' ? JSON.parse(data) : data, null, 2)
  } catch {
    return data
  }
}

// 生命周期
onMounted(async () => {
  await Promise.all([
    loadWorkflows(),
    loadInstances()
  ])
})
</script>

<style scoped>
.workflow-instances {
  padding: 2rem;
  background: #f5f5f5;
  min-height: 100vh;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
  background: white;
  padding: 1.5rem;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.header-left h1 {
  margin: 0 0 0.5rem 0;
  font-size: 1.75rem;
  color: #333;
}

.subtitle {
  margin: 0;
  color: #666;
  font-size: 0.9rem;
}

.header-right {
  display: flex;
  gap: 1rem;
}

.filters {
  display: flex;
  gap: 1rem;
  margin-bottom: 2rem;
  background: white;
  padding: 1rem 1.5rem;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.filter-select,
.search-input {
  padding: 0.5rem 1rem;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  font-size: 0.9rem;
}

.search-input {
  flex: 1;
  min-width: 250px;
}

.instances-section {
  background: white;
  padding: 1.5rem;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.instances-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(450px, 1fr));
  gap: 1.5rem;
}

.instance-card {
  border: 2px solid #e1e5e9;
  border-radius: 8px;
  padding: 1.5rem;
  cursor: pointer;
  transition: all 0.2s ease;
  background: white;
}

.instance-card:hover {
  border-color: #007acc;
  box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
}

.instance-header {
  margin-bottom: 1rem;
}

.instance-title {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.5rem;
}

.instance-title h3 {
  margin: 0;
  font-size: 1.1rem;
  color: #333;
}

.status-badge {
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-size: 0.8rem;
  font-weight: 500;
}

.status-badge.status-running {
  background: #fff3e0;
  color: #ef6c00;
}

.status-badge.status-suspended {
  background: #e3f2fd;
  color: #1976d2;
}

.status-badge.status-completed {
  background: #e8f5e8;
  color: #2e7d32;
}

.status-badge.status-failed {
  background: #ffebee;
  color: #c62828;
}

.status-badge.status-cancelled {
  background: #f5f5f5;
  color: #757575;
}

.instance-id {
  font-size: 0.8rem;
  color: #999;
  font-family: monospace;
}

.instance-body {
  margin-bottom: 1rem;
}

.info-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.info-item {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.info-item .label {
  font-size: 0.85rem;
  color: #999;
}

.info-item .value {
  font-size: 0.9rem;
  color: #333;
  font-weight: 500;
}

.progress-section {
  margin: 1rem 0;
}

.progress-bar {
  height: 8px;
  background: #e1e5e9;
  border-radius: 4px;
  overflow: hidden;
  margin-bottom: 0.5rem;
}

.progress-fill {
  height: 100%;
  background: linear-gradient(90deg, #007acc, #00bcd4);
  transition: width 0.3s ease;
}

.progress-text {
  font-size: 0.85rem;
  color: #666;
}

.error-section {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem;
  background: #ffebee;
  border-radius: 4px;
  margin: 1rem 0;
}

.error-icon {
  font-size: 1.2rem;
}

.error-text {
  font-size: 0.85rem;
  color: #c62828;
}

.instance-actions {
  display: flex;
  gap: 0.5rem;
  padding-top: 1rem;
  border-top: 1px solid #e1e5e9;
}

/* 时间线样式 */
.history-timeline {
  position: relative;
  padding-left: 2rem;
}

.timeline-item {
  position: relative;
  padding-bottom: 2rem;
}

.timeline-marker {
  position: absolute;
  left: -2rem;
  width: 12px;
  height: 12px;
  background: #007acc;
  border-radius: 50%;
  border: 3px solid white;
  box-shadow: 0 0 0 2px #e1e5e9;
}

.timeline-item:not(:last-child)::before {
  content: '';
  position: absolute;
  left: -1.45rem;
  top: 12px;
  width: 2px;
  height: calc(100% - 12px);
  background: #e1e5e9;
}

.timeline-content {
  background: #f8f9fa;
  padding: 1rem;
  border-radius: 6px;
  border-left: 3px solid #007acc;
}

.timeline-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.75rem;
}

.timeline-header h4 {
  margin: 0;
  font-size: 1rem;
  color: #333;
}

.timeline-status {
  padding: 0.2rem 0.6rem;
  border-radius: 10px;
  font-size: 0.75rem;
  font-weight: 500;
}

.timeline-info {
  display: flex;
  gap: 1.5rem;
  font-size: 0.85rem;
  color: #666;
  margin-bottom: 0.75rem;
}

.timeline-output,
.timeline-error {
  margin-top: 0.75rem;
  font-size: 0.85rem;
}

.timeline-output pre {
  background: white;
  padding: 0.75rem;
  border-radius: 4px;
  margin: 0.5rem 0 0 0;
  overflow-x: auto;
  font-size: 0.8rem;
}

.timeline-error span {
  color: #c62828;
}

/* 按钮样式 */
.btn {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 1rem;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  background: white;
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 0.9rem;
}

.btn:hover {
  background: #f8f9fa;
}

.btn-primary {
  background: #007acc;
  color: white;
  border-color: #007acc;
}

.btn-primary:hover:not(:disabled) {
  background: #005999;
}

.btn-success {
  background: #28a745;
  color: white;
  border-color: #28a745;
}

.btn-success:hover {
  background: #218838;
}

.btn-warning {
  background: #ffc107;
  color: #333;
  border-color: #ffc107;
}

.btn-warning:hover {
  background: #e0a800;
}

.btn-danger {
  background: #dc3545;
  color: white;
  border-color: #dc3545;
}

.btn-danger:hover {
  background: #c82333;
}

.btn-secondary {
  background: #6c757d;
  color: white;
  border-color: #6c757d;
}

.btn-secondary:hover {
  background: #5a6268;
}

.btn-sm {
  padding: 0.25rem 0.75rem;
  font-size: 0.85rem;
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

/* 表单样式 */
.form-group {
  margin-bottom: 1rem;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 500;
  color: #333;
}

.form-select,
.form-textarea {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  font-size: 0.9rem;
}

.form-textarea {
  min-height: 100px;
  resize: vertical;
  font-family: monospace;
}

.form-select:focus,
.form-textarea:focus {
  outline: none;
  border-color: #007acc;
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
  padding: 2rem;
  width: 500px;
  max-width: 90vw;
  max-height: 80vh;
  overflow-y: auto;
}

.modal-content.large {
  width: 800px;
}

.modal-content h3 {
  margin: 0 0 1.5rem 0;
  font-size: 1.25rem;
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 1rem;
  margin-top: 2rem;
}

/* 空状态 */
.empty-state {
  text-align: center;
  padding: 4rem;
  color: #999;
}

.empty-state p {
  margin-bottom: 1.5rem;
  font-size: 1.1rem;
}

.icon {
  font-size: 1.1em;
}
</style>
