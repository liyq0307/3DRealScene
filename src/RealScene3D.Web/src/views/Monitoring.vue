<template>
  <div class="monitoring">
    <!-- 页面标题和快捷操作 -->
    <header class="page-header">
      <div class="header-left">
        <h1>系统监控</h1>
        <p class="subtitle">实时监控系统状态和业务指标</p>
      </div>
      <div class="header-right">
        <button @click="refreshData" class="btn btn-primary">
          <span class="icon">🔄</span>
          刷新数据
        </button>
        <button @click="openDashboardDialog" class="btn btn-secondary">
          <span class="icon">➕</span>
          新建仪表板
        </button>
      </div>
    </header>

    <!-- 选项卡导航 -->
    <div class="tabs">
      <button
        v-for="tab in tabs"
        :key="tab.id"
        @click="activeTab = tab.id"
        :class="['tab', { active: activeTab === tab.id }]"
      >
        <span class="icon">{{ tab.icon }}</span>
        {{ tab.label }}
      </button>
    </div>

    <!-- 系统指标视图 -->
    <div v-if="activeTab === 'metrics'" class="tab-content">
      <div class="metrics-section">
        <h2>系统指标</h2>

        <!-- 快照数据卡片 -->
        <div class="metrics-grid">
          <div
            v-for="(metric, name) in systemMetrics"
            :key="name"
            class="metric-card"
          >
            <div class="metric-header">
              <h3>{{ name }}</h3>
              <span class="metric-unit">{{ metric.unit }}</span>
            </div>
            <div class="metric-value">{{ formatValue(metric.value) }}</div>
            <div class="metric-footer">
              <span class="metric-category">{{ metric.category }}</span>
              <span class="metric-time">{{ formatTime(metric.timestamp) }}</span>
            </div>
          </div>
        </div>

        <!-- 查询历史数据 -->
        <div class="history-query">
          <h3>查询历史数据</h3>
          <div class="query-form">
            <input
              v-model="metricQuery.name"
              type="text"
              placeholder="指标名称"
              class="form-input"
            />
            <input
              v-model="metricQuery.category"
              type="text"
              placeholder="分类 (可选)"
              class="form-input"
            />
            <input
              v-model="metricQuery.startTime"
              type="datetime-local"
              class="form-input"
            />
            <input
              v-model="metricQuery.endTime"
              type="datetime-local"
              class="form-input"
            />
            <button @click="queryMetricHistory" class="btn btn-primary">
              查询
            </button>
          </div>

          <!-- 历史数据列表 -->
          <div v-if="metricHistory.length > 0" class="history-list">
            <table class="data-table">
              <thead>
                <tr>
                  <th>时间</th>
                  <th>值</th>
                  <th>单位</th>
                  <th>分类</th>
                  <th>主机</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="record in metricHistory" :key="record.id">
                  <td>{{ formatDateTime(record.timestamp) }}</td>
                  <td>{{ formatValue(record.value) }}</td>
                  <td>{{ record.unit }}</td>
                  <td>{{ record.category }}</td>
                  <td>{{ record.host }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>

    <!-- 告警管理视图 -->
    <div v-if="activeTab === 'alerts'" class="tab-content">
      <div class="alerts-section">
        <div class="section-header">
          <h2>告警管理</h2>
          <button @click="openAlertRuleDialog" class="btn btn-primary">
            <span class="icon">➕</span>
            创建告警规则
          </button>
        </div>

        <!-- 活跃告警 -->
        <div class="active-alerts">
          <h3>活跃告警</h3>
          <div v-if="activeAlerts.length === 0" class="empty-state">
            <p>✅ 当前没有活跃告警</p>
          </div>
          <div v-else class="alert-list">
            <div
              v-for="alert in activeAlerts"
              :key="alert.id"
              :class="['alert-item', `level-${alert.level}`]"
            >
              <div class="alert-header">
                <span class="alert-level">{{ getAlertLevelText(alert.level) }}</span>
                <span class="alert-time">{{ formatDateTime(alert.triggeredAt) }}</span>
              </div>
              <div class="alert-body">
                <h4>{{ alert.ruleName }}</h4>
                <p>{{ alert.message }}</p>
                <div class="alert-details">
                  <span>指标: {{ alert.metricName }}</span>
                  <span>当前值: {{ formatValue(alert.currentValue) }}</span>
                  <span>阈值: {{ formatValue(alert.threshold) }}</span>
                </div>
              </div>
              <div class="alert-actions">
                <button
                  @click="acknowledgeAlert(alert.id)"
                  class="btn btn-sm"
                  :disabled="alert.acknowledgedAt !== null"
                >
                  {{ alert.acknowledgedAt ? '已确认' : '确认' }}
                </button>
                <button @click="resolveAlert(alert.id)" class="btn btn-sm btn-success">
                  解决
                </button>
              </div>
            </div>
          </div>
        </div>

        <!-- 告警规则列表 -->
        <div class="alert-rules">
          <h3>告警规则</h3>
          <div class="rules-list">
            <div
              v-for="rule in alertRules"
              :key="rule.id"
              class="rule-item"
            >
              <div class="rule-header">
                <h4>{{ rule.name }}</h4>
                <span :class="['rule-status', { enabled: rule.isEnabled }]">
                  {{ rule.isEnabled ? '启用' : '禁用' }}
                </span>
              </div>
              <p>{{ rule.description }}</p>
              <div class="rule-details">
                <span>指标: {{ rule.metricName }}</span>
                <span>条件: {{ getConditionText(rule.condition) }}</span>
                <span>级别: {{ getAlertLevelText(rule.level) }}</span>
              </div>
              <div class="rule-actions">
                <button @click="editAlertRule(rule.id)" class="btn btn-sm">
                  编辑
                </button>
                <button @click="deleteAlertRule(rule.id)" class="btn btn-sm btn-danger">
                  删除
                </button>
              </div>
            </div>
          </div>
        </div>

        <!-- 告警历史 -->
        <div class="alert-history">
          <h3>告警历史</h3>
          <div class="history-filters">
            <input
              v-model="alertHistoryQuery.startTime"
              type="datetime-local"
              class="form-input"
            />
            <input
              v-model="alertHistoryQuery.endTime"
              type="datetime-local"
              class="form-input"
            />
            <select v-model="alertHistoryQuery.level" class="form-select">
              <option value="">所有级别</option>
              <option value="0">信息</option>
              <option value="1">警告</option>
              <option value="2">错误</option>
              <option value="3">严重</option>
            </select>
            <button @click="queryAlertHistory" class="btn btn-primary">
              查询
            </button>
          </div>
          <div v-if="alertHistory.length > 0" class="history-table">
            <table class="data-table">
              <thead>
                <tr>
                  <th>触发时间</th>
                  <th>规则名称</th>
                  <th>级别</th>
                  <th>消息</th>
                  <th>解决时间</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="alert in alertHistory" :key="alert.id">
                  <td>{{ formatDateTime(alert.triggeredAt) }}</td>
                  <td>{{ alert.ruleName }}</td>
                  <td>
                    <span :class="['badge', `level-${alert.level}`]">
                      {{ getAlertLevelText(alert.level) }}
                    </span>
                  </td>
                  <td>{{ alert.message }}</td>
                  <td>{{ alert.resolvedAt ? formatDateTime(alert.resolvedAt) : '-' }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>

    <!-- 仪表板视图 -->
    <div v-if="activeTab === 'dashboards'" class="tab-content">
      <div class="dashboards-section">
        <div class="section-header">
          <h2>监控仪表板</h2>
        </div>

        <div class="dashboards-grid">
          <div
            v-for="dashboard in dashboards"
            :key="dashboard.id"
            class="dashboard-card"
            @click="viewDashboard(dashboard.id)"
          >
            <h3>{{ dashboard.name }}</h3>
            <p>{{ dashboard.description }}</p>
            <div class="dashboard-footer">
              <span>图表数量: {{ dashboard.config.charts?.length || 0 }}</span>
              <span>刷新间隔: {{ dashboard.config.refreshInterval }}s</span>
            </div>
            <div class="dashboard-actions">
              <button
                @click.stop="editDashboard(dashboard.id)"
                class="btn btn-sm"
              >
                编辑
              </button>
              <button
                @click.stop="deleteDashboard(dashboard.id)"
                class="btn btn-sm btn-danger"
              >
                删除
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 创建告警规则对话框 -->
    <div v-if="showAlertRuleDialog" class="modal-overlay" @click="closeAlertRuleDialog">
      <div class="modal-content" @click.stop>
        <h3>{{ editingAlertRule ? '编辑告警规则' : '创建告警规则' }}</h3>
        <div class="form-group">
          <label>规则名称</label>
          <input
            v-model="alertRuleForm.name"
            type="text"
            class="form-input"
            placeholder="输入规则名称"
          />
        </div>
        <div class="form-group">
          <label>描述</label>
          <textarea
            v-model="alertRuleForm.description"
            class="form-textarea"
            placeholder="输入规则描述"
          ></textarea>
        </div>
        <div class="form-group">
          <label>指标名称</label>
          <input
            v-model="alertRuleForm.metricName"
            type="text"
            class="form-input"
            placeholder="输入指标名称"
          />
        </div>
        <div class="form-group">
          <label>告警级别</label>
          <select v-model="alertRuleForm.level" class="form-select">
            <option :value="0">信息</option>
            <option :value="1">警告</option>
            <option :value="2">错误</option>
            <option :value="3">严重</option>
          </select>
        </div>
        <div class="form-group">
          <label>比较运算符</label>
          <select v-model="alertRuleForm.condition.operator" class="form-select">
            <option :value="0">大于</option>
            <option :value="1">大于等于</option>
            <option :value="2">等于</option>
            <option :value="3">小于等于</option>
            <option :value="4">小于</option>
          </select>
        </div>
        <div class="form-group">
          <label>阈值</label>
          <input
            v-model.number="alertRuleForm.condition.threshold"
            type="number"
            class="form-input"
            placeholder="输入阈值"
          />
        </div>
        <div class="form-group">
          <label>持续时间 (秒)</label>
          <input
            v-model.number="alertRuleForm.condition.durationSeconds"
            type="number"
            class="form-input"
            placeholder="默认300秒"
          />
        </div>
        <div class="modal-actions">
          <button @click="closeAlertRuleDialog" class="btn btn-secondary">
            取消
          </button>
          <button @click="saveAlertRule" class="btn btn-primary">
            保存
          </button>
        </div>
      </div>
    </div>

    <!-- 创建仪表板对话框 -->
    <div v-if="showDashboardDialog" class="modal-overlay" @click="closeDashboardDialog">
      <div class="modal-content" @click.stop>
        <h3>{{ editingDashboard ? '编辑仪表板' : '创建仪表板' }}</h3>
        <div class="form-group">
          <label>仪表板名称</label>
          <input
            v-model="dashboardForm.name"
            type="text"
            class="form-input"
            placeholder="输入仪表板名称"
          />
        </div>
        <div class="form-group">
          <label>描述</label>
          <textarea
            v-model="dashboardForm.description"
            class="form-textarea"
            placeholder="输入仪表板描述"
          ></textarea>
        </div>
        <div class="form-group">
          <label>刷新间隔 (秒)</label>
          <input
            v-model.number="dashboardForm.refreshInterval"
            type="number"
            class="form-input"
            placeholder="默认60秒"
          />
        </div>
        <div class="form-group">
          <label>时间范围 (分钟)</label>
          <input
            v-model.number="dashboardForm.timeRange"
            type="number"
            class="form-input"
            placeholder="默认60分钟"
          />
        </div>
        <div class="modal-actions">
          <button @click="closeDashboardDialog" class="btn btn-secondary">
            取消
          </button>
          <button @click="saveDashboard" class="btn btn-primary">
            保存
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { monitoringService } from '@/services/api'

// 选项卡
const tabs = [
  { id: 'metrics', label: '系统指标', icon: '📊' },
  { id: 'alerts', label: '告警管理', icon: '🔔' },
  { id: 'dashboards', label: '仪表板', icon: '📈' }
]
const activeTab = ref('metrics')

// 系统指标数据
const systemMetrics = ref<any>({})
const metricHistory = ref<any[]>([])
const metricQuery = ref({
  name: '',
  category: '',
  startTime: '',
  endTime: ''
})

// 告警数据
const activeAlerts = ref<any[]>([])
const alertRules = ref<any[]>([])
const alertHistory = ref<any[]>([])
const alertHistoryQuery = ref({
  startTime: '',
  endTime: '',
  level: ''
})

// 仪表板数据
const dashboards = ref<any[]>([])

// 对话框状态
const showAlertRuleDialog = ref(false)
const editingAlertRule = ref<any>(null)
const alertRuleForm = ref({
  name: '',
  description: '',
  metricName: '',
  level: 1,
  condition: {
    operator: 0,
    threshold: 0,
    durationSeconds: 300,
    aggregation: 0,
    timeWindowSeconds: 300
  }
})

const showDashboardDialog = ref(false)
const editingDashboard = ref<any>(null)
const dashboardForm = ref({
  name: '',
  description: '',
  refreshInterval: 60,
  timeRange: 60,
  charts: [],
  layout: {}
})

// 格式化方法
const formatValue = (value: number) => {
  return value.toFixed(2)
}

const formatTime = (timestamp: string) => {
  return new Date(timestamp).toLocaleTimeString('zh-CN')
}

const formatDateTime = (timestamp: string) => {
  return new Date(timestamp).toLocaleString('zh-CN')
}

const getAlertLevelText = (level: number) => {
  const levelMap: Record<number, string> = {
    0: '信息',
    1: '警告',
    2: '错误',
    3: '严重'
  }
  return levelMap[level] || '未知'
}

const getConditionText = (condition: any) => {
  const operatorMap: Record<number, string> = {
    0: '>',
    1: '>=',
    2: '=',
    3: '<=',
    4: '<'
  }
  return `${operatorMap[condition.operator]} ${condition.threshold}`
}

// 数据加载方法
const loadSystemMetrics = async () => {
  try {
    systemMetrics.value = await monitoringService.getLatestSystemMetrics()
  } catch (error) {
    console.error('加载系统指标失败:', error)
  }
}

const loadActiveAlerts = async () => {
  try {
    activeAlerts.value = await monitoringService.getActiveAlerts()
  } catch (error) {
    console.error('加载活跃告警失败:', error)
  }
}

const loadAlertRules = async () => {
  try {
    alertRules.value = await monitoringService.getAlertRules()
  } catch (error) {
    console.error('加载告警规则失败:', error)
  }
}

const loadDashboards = async () => {
  try {
    // 假设用户ID为默认GUID
    dashboards.value = await monitoringService.getDashboards('00000000-0000-0000-0000-000000000001')
  } catch (error) {
    console.error('加载仪表板失败:', error)
  }
}

const refreshData = async () => {
  await Promise.all([
    loadSystemMetrics(),
    loadActiveAlerts(),
    loadAlertRules(),
    loadDashboards()
  ])
}

const queryMetricHistory = async () => {
  try {
    if (!metricQuery.value.name || !metricQuery.value.startTime || !metricQuery.value.endTime) {
      alert('请填写完整的查询条件')
      return
    }
    metricHistory.value = await monitoringService.getSystemMetrics(
      metricQuery.value.name,
      new Date(metricQuery.value.startTime),
      new Date(metricQuery.value.endTime),
      metricQuery.value.category
    )
  } catch (error) {
    console.error('查询指标历史失败:', error)
  }
}

const queryAlertHistory = async () => {
  try {
    if (!alertHistoryQuery.value.startTime || !alertHistoryQuery.value.endTime) {
      alert('请选择时间范围')
      return
    }
    alertHistory.value = await monitoringService.getAlertHistory(
      new Date(alertHistoryQuery.value.startTime),
      new Date(alertHistoryQuery.value.endTime),
      alertHistoryQuery.value.level ? parseInt(alertHistoryQuery.value.level) : undefined
    )
  } catch (error) {
    console.error('查询告警历史失败:', error)
  }
}

// 告警操作
const acknowledgeAlert = async (alertId: string) => {
  try {
    await monitoringService.acknowledgeAlert(alertId, '00000000-0000-0000-0000-000000000001')
    await loadActiveAlerts()
  } catch (error) {
    console.error('确认告警失败:', error)
  }
}

const resolveAlert = async (alertId: string) => {
  try {
    await monitoringService.resolveAlert(alertId, '00000000-0000-0000-0000-000000000001')
    await loadActiveAlerts()
  } catch (error) {
    console.error('解决告警失败:', error)
  }
}

// 告警规则对话框
const openAlertRuleDialog = () => {
  editingAlertRule.value = null
  alertRuleForm.value = {
    name: '',
    description: '',
    metricName: '',
    level: 1,
    condition: {
      operator: 0,
      threshold: 0,
      durationSeconds: 300,
      aggregation: 0,
      timeWindowSeconds: 300
    }
  }
  showAlertRuleDialog.value = true
}

const closeAlertRuleDialog = () => {
  showAlertRuleDialog.value = false
}

const saveAlertRule = async () => {
  try {
    if (editingAlertRule.value) {
      await monitoringService.updateAlertRule(editingAlertRule.value, alertRuleForm.value, '00000000-0000-0000-0000-000000000001')
    } else {
      await monitoringService.createAlertRule(alertRuleForm.value, '00000000-0000-0000-0000-000000000001')
    }
    await loadAlertRules()
    closeAlertRuleDialog()
  } catch (error) {
    console.error('保存告警规则失败:', error)
  }
}

const editAlertRule = async (ruleId: string) => {
  const rule = alertRules.value.find(r => r.id === ruleId)
  if (rule) {
    editingAlertRule.value = ruleId
    alertRuleForm.value = {
      name: rule.name,
      description: rule.description,
      metricName: rule.metricName,
      level: rule.level,
      condition: { ...rule.condition }
    }
    showAlertRuleDialog.value = true
  }
}

const deleteAlertRule = async (ruleId: string) => {
  if (confirm('确定要删除此告警规则吗?')) {
    try {
      await monitoringService.deleteAlertRule(ruleId, '00000000-0000-0000-0000-000000000001')
      await loadAlertRules()
    } catch (error) {
      console.error('删除告警规则失败:', error)
    }
  }
}

// 仪表板对话框
const openDashboardDialog = () => {
  editingDashboard.value = null
  dashboardForm.value = {
    name: '',
    description: '',
    refreshInterval: 60,
    timeRange: 60,
    charts: [],
    layout: {}
  }
  showDashboardDialog.value = true
}

const closeDashboardDialog = () => {
  showDashboardDialog.value = false
}

const saveDashboard = async () => {
  try {
    if (editingDashboard.value) {
      await monitoringService.updateDashboard(editingDashboard.value, dashboardForm.value, '00000000-0000-0000-0000-000000000001')
    } else {
      await monitoringService.createDashboard(dashboardForm.value, '00000000-0000-0000-0000-000000000001')
    }
    await loadDashboards()
    closeDashboardDialog()
  } catch (error) {
    console.error('保存仪表板失败:', error)
  }
}

const viewDashboard = (dashboardId: string) => {
  console.log('查看仪表板:', dashboardId)
  // TODO: 导航到仪表板详情页面
}

const editDashboard = async (dashboardId: string) => {
  const dashboard = dashboards.value.find(d => d.id === dashboardId)
  if (dashboard) {
    editingDashboard.value = dashboardId
    dashboardForm.value = {
      name: dashboard.name,
      description: dashboard.description,
      refreshInterval: dashboard.config.refreshInterval,
      timeRange: dashboard.config.timeRange,
      charts: dashboard.config.charts || [],
      layout: dashboard.config.layout || {}
    }
    showDashboardDialog.value = true
  }
}

const deleteDashboard = async (dashboardId: string) => {
  if (confirm('确定要删除此仪表板吗?')) {
    try {
      await monitoringService.deleteDashboard(dashboardId, '00000000-0000-0000-0000-000000000001')
      await loadDashboards()
    } catch (error) {
      console.error('删除仪表板失败:', error)
    }
  }
}

// 生命周期
onMounted(async () => {
  await refreshData()
})
</script>

<style scoped>
.monitoring {
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

.tabs {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 2rem;
  background: white;
  padding: 0.5rem;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.tab {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem 1.5rem;
  border: none;
  background: transparent;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 0.95rem;
  color: #666;
}

.tab:hover {
  background: #f8f9fa;
  color: #333;
}

.tab.active {
  background: #007acc;
  color: white;
}

.tab-content {
  background: white;
  padding: 2rem;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

/* 系统指标样式 */
.metrics-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
  gap: 1.5rem;
  margin: 2rem 0;
}

.metric-card {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  padding: 1.5rem;
  border-radius: 8px;
  box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
}

.metric-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}

.metric-header h3 {
  margin: 0;
  font-size: 0.9rem;
  opacity: 0.9;
}

.metric-unit {
  font-size: 0.8rem;
  opacity: 0.8;
}

.metric-value {
  font-size: 2.5rem;
  font-weight: bold;
  margin-bottom: 1rem;
}

.metric-footer {
  display: flex;
  justify-content: space-between;
  font-size: 0.8rem;
  opacity: 0.8;
}

.history-query {
  margin-top: 3rem;
}

.history-query h3 {
  margin-bottom: 1rem;
}

.query-form {
  display: flex;
  gap: 1rem;
  margin-bottom: 2rem;
}

/* 告警样式 */
.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
}

.active-alerts {
  margin-bottom: 3rem;
}

.empty-state {
  text-align: center;
  padding: 3rem;
  color: #666;
  font-size: 1.1rem;
}

.alert-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.alert-item {
  padding: 1.5rem;
  border-radius: 8px;
  border-left: 4px solid;
}

.alert-item.level-0 {
  background: #e3f2fd;
  border-color: #2196f3;
}

.alert-item.level-1 {
  background: #fff3e0;
  border-color: #ff9800;
}

.alert-item.level-2 {
  background: #ffebee;
  border-color: #f44336;
}

.alert-item.level-3 {
  background: #fce4ec;
  border-color: #e91e63;
}

.alert-header {
  display: flex;
  justify-content: space-between;
  margin-bottom: 0.5rem;
}

.alert-level {
  font-weight: bold;
  font-size: 0.9rem;
}

.alert-time {
  color: #666;
  font-size: 0.85rem;
}

.alert-body h4 {
  margin: 0 0 0.5rem 0;
}

.alert-body p {
  margin: 0 0 1rem 0;
  color: #555;
}

.alert-details {
  display: flex;
  gap: 1.5rem;
  font-size: 0.9rem;
  color: #666;
}

.alert-actions {
  display: flex;
  gap: 0.5rem;
  margin-top: 1rem;
}

.rules-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  margin-bottom: 3rem;
}

.rule-item {
  padding: 1.5rem;
  border: 1px solid #e1e5e9;
  border-radius: 8px;
}

.rule-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.5rem;
}

.rule-status {
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-size: 0.8rem;
  background: #f5f5f5;
  color: #999;
}

.rule-status.enabled {
  background: #e8f5e8;
  color: #2e7d32;
}

.rule-details {
  display: flex;
  gap: 1.5rem;
  margin: 1rem 0;
  font-size: 0.9rem;
  color: #666;
}

.rule-actions {
  display: flex;
  gap: 0.5rem;
}

/* 仪表板样式 */
.dashboards-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 1.5rem;
}

.dashboard-card {
  padding: 1.5rem;
  border: 1px solid #e1e5e9;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.dashboard-card:hover {
  border-color: #007acc;
  box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
}

.dashboard-footer {
  display: flex;
  gap: 1.5rem;
  margin: 1rem 0;
  font-size: 0.9rem;
  color: #666;
}

.dashboard-actions {
  display: flex;
  gap: 0.5rem;
}

/* 表格样式 */
.data-table {
  width: 100%;
  border-collapse: collapse;
  margin-top: 1rem;
}

.data-table th,
.data-table td {
  padding: 0.75rem;
  text-align: left;
  border-bottom: 1px solid #e1e5e9;
}

.data-table th {
  background: #f8f9fa;
  font-weight: 600;
  color: #333;
}

.badge {
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-size: 0.8rem;
  font-weight: 500;
}

.badge.level-0 {
  background: #e3f2fd;
  color: #1976d2;
}

.badge.level-1 {
  background: #fff3e0;
  color: #ef6c00;
}

.badge.level-2 {
  background: #ffebee;
  color: #c62828;
}

.badge.level-3 {
  background: #fce4ec;
  color: #c2185b;
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

.btn-primary:hover {
  background: #005999;
}

.btn-secondary {
  background: #6c757d;
  color: white;
  border-color: #6c757d;
}

.btn-secondary:hover {
  background: #5a6268;
}

.btn-success {
  background: #28a745;
  color: white;
  border-color: #28a745;
}

.btn-success:hover {
  background: #218838;
}

.btn-danger {
  background: #dc3545;
  color: white;
  border-color: #dc3545;
}

.btn-danger:hover {
  background: #c82333;
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
.form-input,
.form-select,
.form-textarea {
  padding: 0.5rem;
  border: 1px solid #e1e5e9;
  border-radius: 4px;
  font-size: 0.9rem;
}

.form-input:focus,
.form-select:focus,
.form-textarea:focus {
  outline: none;
  border-color: #007acc;
}

.form-textarea {
  min-height: 80px;
  resize: vertical;
}

.form-group {
  margin-bottom: 1rem;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 500;
  color: #333;
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

.icon {
  font-size: 1.1em;
}
</style>
