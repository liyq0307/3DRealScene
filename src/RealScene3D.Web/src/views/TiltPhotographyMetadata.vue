<template>
  <div class="tilt-photography-page">
    <!-- 页面头部 -->
    <div class="page-header">
      <div class="header-content">
        <h1 class="page-title">📸 倾斜摄影元数据管理</h1>
        <p class="page-description">管理倾斜摄影数据，支持空间查询和覆盖分析</p>
      </div>
      <button @click="showCreateDialog = true" class="btn btn-primary">
        ➕ 添加倾斜摄影数据
      </button>
    </div>

    <!-- 搜索和筛选区域 -->
    <div class="filters-section">
      <div class="search-box">
        <input
          v-model="searchQuery"
          type="text"
          placeholder="🔍 搜索项目名称..."
          class="search-input"
          @input="handleSearch"
        />
      </div>

      <div class="filter-grid">
        <select v-model="selectedSceneFilter" @change="handleFilterChange" class="filter-select">
          <option value="">全部场景</option>
          <option v-for="scene in scenes" :key="scene.id" :value="scene.id">
            {{ scene.name }}
          </option>
        </select>

        <select v-model="selectedFormat" @change="handleFilterChange" class="filter-select">
          <option value="">全部格式</option>
          <option value="OSGB">OSGB</option>
          <option value="3DTiles">3D Tiles</option>
          <option value="S3M">S3M</option>
          <option value="OBJ">OBJ</option>
        </select>

        <div class="date-filter">
          <input
            v-model="dateRange.start"
            type="date"
            class="filter-input"
            placeholder="开始日期"
          />
          <span>至</span>
          <input
            v-model="dateRange.end"
            type="date"
            class="filter-input"
            placeholder="结束日期"
          />
          <button @click="applyDateFilter" class="btn-filter">筛选</button>
        </div>
      </div>

      <div class="advanced-filters">
        <div class="area-filter">
          <label>最小覆盖面积 (km²):</label>
          <input
            v-model.number="minArea"
            type="number"
            step="0.1"
            class="filter-input"
            placeholder="例如: 1.5"
          />
          <button @click="applyAreaFilter" class="btn-filter">应用</button>
        </div>
      </div>
    </div>

    <!-- 统计信息 -->
    <div class="stats-section">
      <div class="stat-card">
        <div class="stat-icon">📊</div>
        <div class="stat-content">
          <div class="stat-label">总数据集</div>
          <div class="stat-value">{{ totalCount }}</div>
        </div>
      </div>
      <div class="stat-card">
        <div class="stat-icon">📐</div>
        <div class="stat-content">
          <div class="stat-label">总覆盖面积</div>
          <div class="stat-value">{{ totalCoverageArea }} km²</div>
        </div>
      </div>
      <div class="stat-card">
        <div class="stat-icon">📷</div>
        <div class="stat-content">
          <div class="stat-label">总照片数</div>
          <div class="stat-value">{{ totalPhotoCount }}</div>
        </div>
      </div>
    </div>

    <!-- 加载状态 -->
    <div v-if="loading" class="loading-container">
      <LoadingSpinner :loading="loading" text="加载倾斜摄影数据..." />
      <p>加载中...</p>
    </div>

    <!-- 错误提示 -->
    <ErrorDisplay v-if="error" :error="error" @retry="loadData" />

    <!-- 倾斜摄影列表 -->
    <div v-if="!loading && !error" class="data-grid">
      <div
        v-for="data in dataList"
        :key="data.id"
        class="data-card"
      >
        <div class="data-card-header">
          <div class="project-icon">🌍</div>
          <div class="project-info">
            <h3 class="project-title">{{ data.projectName }}</h3>
            <p class="project-meta">{{ data.outputFormat }} · {{ formatDate(data.captureDate) }}</p>
          </div>
          <div class="format-badge" :class="`format-${data.outputFormat?.toLowerCase()}`">
            {{ data.outputFormat }}
          </div>
        </div>

        <div class="data-card-body">
          <div class="data-detail">
            <span class="detail-label">覆盖面积:</span>
            <span class="detail-value">{{ data.coverageAreaKm2 }} km²</span>
          </div>
          <div class="data-detail">
            <span class="detail-label">照片数量:</span>
            <span class="detail-value">{{ data.photoCount || 'N/A' }}</span>
          </div>
          <div class="data-detail">
            <span class="detail-label">分辨率:</span>
            <span class="detail-value">{{ data.groundResolution || 'N/A' }} cm</span>
          </div>
          <div class="data-detail">
            <span class="detail-label">采集设备:</span>
            <span class="detail-value">{{ data.equipment || 'N/A' }}</span>
          </div>

          <!-- 地理范围信息 -->
          <div v-if="data.geographicBounds" class="bounds-info">
            <div class="bounds-title">地理范围:</div>
            <div class="bounds-grid">
              <span>经度: {{ data.geographicBounds.minLon?.toFixed(4) }} ~ {{ data.geographicBounds.maxLon?.toFixed(4) }}</span>
              <span>纬度: {{ data.geographicBounds.minLat?.toFixed(4) }} ~ {{ data.geographicBounds.maxLat?.toFixed(4) }}</span>
            </div>
          </div>
        </div>

        <div class="data-card-footer">
          <button @click="viewOnMap(data)" class="btn-action btn-map">地图</button>
          <button @click="editData(data)" class="btn-action btn-edit">编辑</button>
          <button @click="deleteData(data)" class="btn-action btn-delete">删除</button>
        </div>
      </div>

      <!-- 空状态 -->
      <div v-if="dataList.length === 0" class="empty-state">
        <div class="empty-icon">📸</div>
        <h3>暂无倾斜摄影数据</h3>
        <p>点击右上角"添加倾斜摄影数据"按钮创建第一个数据集</p>
      </div>
    </div>

    <!-- 创建/编辑对话框 -->
    <Modal v-if="showCreateDialog || showEditDialog" @close="closeDialog">
      <template #header>
        <h2>{{ editingData ? '编辑倾斜摄影数据' : '添加倾斜摄影数据' }}</h2>
      </template>
      <template #body>
        <form @submit.prevent="submitForm" class="data-form">
          <div class="form-group">
            <label>项目名称 *</label>
            <input
              v-model="formData.projectName"
              type="text"
              required
              placeholder="请输入项目名称"
              class="form-input"
            />
          </div>

          <div class="form-row">
            <div class="form-group">
              <label>输出格式 *</label>
              <select v-model="formData.outputFormat" required class="form-input">
                <option value="">请选择</option>
                <option value="OSGB">OSGB</option>
                <option value="3DTiles">3D Tiles</option>
                <option value="S3M">S3M</option>
                <option value="OBJ">OBJ</option>
              </select>
            </div>

            <div class="form-group">
              <label>采集日期 *</label>
              <input
                v-model="formData.captureDate"
                type="date"
                required
                class="form-input"
              />
            </div>
          </div>

          <div class="form-row">
            <div class="form-group">
              <label>覆盖面积 (km²) *</label>
              <input
                v-model.number="formData.coverageAreaKm2"
                type="number"
                step="0.01"
                required
                class="form-input"
              />
            </div>

            <div class="form-group">
              <label>照片数量</label>
              <input
                v-model.number="formData.photoCount"
                type="number"
                class="form-input"
              />
            </div>
          </div>

          <div class="form-row">
            <div class="form-group">
              <label>地面分辨率 (cm)</label>
              <input
                v-model.number="formData.groundResolution"
                type="number"
                step="0.1"
                class="form-input"
              />
            </div>

            <div class="form-group">
              <label>采集设备</label>
              <input
                v-model="formData.equipment"
                type="text"
                placeholder="例如: DJI Phantom 4 RTK"
                class="form-input"
              />
            </div>
          </div>

          <div class="form-section-title">地理范围</div>
          <div class="form-row">
            <div class="form-group">
              <label>最小经度 *</label>
              <input
                v-model.number="formData.minLon"
                type="number"
                step="0.000001"
                required
                class="form-input"
              />
            </div>

            <div class="form-group">
              <label>最大经度 *</label>
              <input
                v-model.number="formData.maxLon"
                type="number"
                step="0.000001"
                required
                class="form-input"
              />
            </div>
          </div>

          <div class="form-row">
            <div class="form-group">
              <label>最小纬度 *</label>
              <input
                v-model.number="formData.minLat"
                type="number"
                step="0.000001"
                required
                class="form-input"
              />
            </div>

            <div class="form-group">
              <label>最大纬度 *</label>
              <input
                v-model.number="formData.maxLat"
                type="number"
                step="0.000001"
                required
                class="form-input"
              />
            </div>
          </div>

          <div class="form-group">
            <label>数据路径 *</label>
            <input
              v-model="formData.dataPath"
              type="text"
              required
              placeholder="倾斜摄影数据存储路径"
              class="form-input"
            />
          </div>

          <div class="form-group">
            <label>关联场景</label>
            <select v-model="formData.sceneId" class="form-input">
              <option value="">无关联</option>
              <option v-for="scene in scenes" :key="scene.id" :value="scene.id">
                {{ scene.name }}
              </option>
            </select>
          </div>

          <div class="form-actions">
            <button type="button" @click="closeDialog" class="btn btn-secondary">
              取消
            </button>
            <button type="submit" class="btn btn-primary" :disabled="submitting">
              {{ submitting ? '提交中...' : '提交' }}
            </button>
          </div>
        </form>
      </template>
    </Modal>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { tiltPhotographyMetadataService, sceneService } from '@/services/api'
import { useMessage } from '@/composables/useMessage'
import LoadingSpinner from '@/components/LoadingSpinner.vue'
import ErrorDisplay from '@/components/ErrorDisplay.vue'
import Modal from '@/components/Modal.vue'

const { success, error: showError } = useMessage()

// 状态
const loading = ref(false)
const error = ref('')
const dataList = ref<any[]>([])
const scenes = ref<any[]>([])
const totalCount = ref(0)
const searchQuery = ref('')
const selectedSceneFilter = ref('')
const selectedFormat = ref('')
const dateRange = ref({ start: '', end: '' })
const minArea = ref<number | null>(null)

// 对话框状态
const showCreateDialog = ref(false)
const showEditDialog = ref(false)
const editingData = ref<any>(null)
const submitting = ref(false)

// 表单数据
const formData = ref({
  projectName: '',
  outputFormat: '',
  captureDate: '',
  coverageAreaKm2: 0,
  photoCount: 0,
  groundResolution: 0,
  equipment: '',
  minLon: 0,
  maxLon: 0,
  minLat: 0,
  maxLat: 0,
  dataPath: '',
  sceneId: ''
})

// 计算统计数据
const totalCoverageArea = computed(() => {
  return dataList.value.reduce((sum, item) => sum + (item.coverageAreaKm2 || 0), 0).toFixed(2)
})

const totalPhotoCount = computed(() => {
  return dataList.value.reduce((sum, item) => sum + (item.photoCount || 0), 0)
})

// 加载数据
const loadData = async () => {
  loading.value = true
  error.value = ''

  try {
    if (selectedSceneFilter.value) {
      dataList.value = await tiltPhotographyMetadataService.getTiltPhotographyBySceneId(selectedSceneFilter.value)
    } else if (selectedFormat.value) {
      dataList.value = await tiltPhotographyMetadataService.getTiltPhotographyByOutputFormat(selectedFormat.value)
    } else {
      dataList.value = await tiltPhotographyMetadataService.getAllTiltPhotography()
    }

    totalCount.value = dataList.value.length
  } catch (err: any) {
    error.value = err.message || '加载倾斜摄影数据失败'
    showError(error.value)
  } finally {
    loading.value = false
  }
}

// 加载场景列表
const loadScenes = async () => {
  try {
    scenes.value = await sceneService.getAllScenes()
  } catch (err: any) {
    console.error('加载场景列表失败:', err)
  }
}

// 搜索处理
const handleSearch = async () => {
  if (searchQuery.value.trim()) {
    try {
      dataList.value = await tiltPhotographyMetadataService.searchTiltPhotographyByProjectName(searchQuery.value)
      totalCount.value = dataList.value.length
    } catch (err: any) {
      showError('搜索失败: ' + err.message)
    }
  } else {
    await loadData()
  }
}

// 筛选处理
const handleFilterChange = async () => {
  await loadData()
}

// 日期范围筛选
const applyDateFilter = async () => {
  if (dateRange.value.start && dateRange.value.end) {
    try {
      const startDate = new Date(dateRange.value.start)
      const endDate = new Date(dateRange.value.end)
      dataList.value = await tiltPhotographyMetadataService.getTiltPhotographyByDateRange(startDate, endDate)
      totalCount.value = dataList.value.length
      success('日期筛选已应用')
    } catch (err: any) {
      showError('日期筛选失败: ' + err.message)
    }
  } else {
    showError('请选择完整的日期范围')
  }
}

// 面积筛选
const applyAreaFilter = async () => {
  if (minArea.value !== null && minArea.value > 0) {
    try {
      dataList.value = await tiltPhotographyMetadataService.getTiltPhotographyByCoverageArea(minArea.value)
      totalCount.value = dataList.value.length
      success('面积筛选已应用')
    } catch (err: any) {
      showError('面积筛选失败: ' + err.message)
    }
  } else {
    showError('请输入有效的最小面积')
  }
}

// 在地图上查看
const viewOnMap = (data: any) => {
  // TODO: 实现地图查看功能
  console.log('在地图上查看:', data)
  success('地图查看功能开发中')
}

// 编辑数据
const editData = (data: any) => {
  editingData.value = data
  formData.value = {
    projectName: data.projectName,
    outputFormat: data.outputFormat,
    captureDate: data.captureDate?.split('T')[0] || '',
    coverageAreaKm2: data.coverageAreaKm2,
    photoCount: data.photoCount || 0,
    groundResolution: data.groundResolution || 0,
    equipment: data.equipment || '',
    minLon: data.geographicBounds?.minLon || 0,
    maxLon: data.geographicBounds?.maxLon || 0,
    minLat: data.geographicBounds?.minLat || 0,
    maxLat: data.geographicBounds?.maxLat || 0,
    dataPath: data.dataPath || '',
    sceneId: data.sceneId || ''
  }
  showEditDialog.value = true
}

// 删除数据
const deleteData = async (data: any) => {
  if (!confirm(`确定要删除倾斜摄影数据"${data.projectName}"吗？`)) {
    return
  }

  try {
    await tiltPhotographyMetadataService.deleteTiltPhotography(data.id)
    success('删除成功')
    await loadData()
  } catch (err: any) {
    showError('删除失败: ' + err.message)
  }
}

// 提交表单
const submitForm = async () => {
  submitting.value = true

  try {
    const tiltData = {
      projectName: formData.value.projectName,
      outputFormat: formData.value.outputFormat,
      captureDate: new Date(formData.value.captureDate).toISOString(),
      coverageAreaKm2: formData.value.coverageAreaKm2,
      photoCount: formData.value.photoCount,
      groundResolution: formData.value.groundResolution,
      equipment: formData.value.equipment,
      geographicBounds: {
        minLon: formData.value.minLon,
        maxLon: formData.value.maxLon,
        minLat: formData.value.minLat,
        maxLat: formData.value.maxLat
      },
      dataPath: formData.value.dataPath,
      sceneId: formData.value.sceneId || null
    }

    if (editingData.value) {
      await tiltPhotographyMetadataService.updateTiltPhotography(editingData.value.id, tiltData)
      success('更新成功')
    } else {
      await tiltPhotographyMetadataService.createTiltPhotography(tiltData)
      success('创建成功')
    }

    closeDialog()
    await loadData()
  } catch (err: any) {
    showError('提交失败: ' + err.message)
  } finally {
    submitting.value = false
  }
}

// 关闭对话框
const closeDialog = () => {
  showCreateDialog.value = false
  showEditDialog.value = false
  editingData.value = null
  formData.value = {
    projectName: '',
    outputFormat: '',
    captureDate: '',
    coverageAreaKm2: 0,
    photoCount: 0,
    groundResolution: 0,
    equipment: '',
    minLon: 0,
    maxLon: 0,
    minLat: 0,
    maxLat: 0,
    dataPath: '',
    sceneId: ''
  }
}

// 格式化日期
const formatDate = (date: string) => {
  if (!date) return 'N/A'
  return new Date(date).toLocaleDateString('zh-CN')
}

// 初始化
onMounted(() => {
  loadData()
  loadScenes()
})
</script>

<style scoped>
.tilt-photography-page {
  padding: 2rem;
  max-width: 1400px;
  margin: 0 auto;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
  gap: 1rem;
}

.header-content h1 {
  margin: 0;
  font-size: 1.75rem;
  color: var(--gray-900);
}

.page-description {
  margin: 0.5rem 0 0 0;
  color: var(--gray-600);
  font-size: 0.95rem;
}

.filters-section {
  background: white;
  padding: 1.5rem;
  border-radius: var(--border-radius);
  box-shadow: var(--shadow-sm);
  margin-bottom: 1.5rem;
}

.search-box {
  margin-bottom: 1rem;
}

.search-input {
  width: 100%;
  padding: 0.75rem 1rem;
  border: 1px solid var(--gray-300);
  border-radius: var(--border-radius);
  font-size: 0.95rem;
}

.filter-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
  margin-bottom: 1rem;
}

.filter-select,
.filter-input {
  padding: 0.6rem 1rem;
  border: 1px solid var(--gray-300);
  border-radius: var(--border-radius);
  font-size: 0.9rem;
}

.date-filter {
  grid-column: 1 / -1;
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.date-filter span {
  color: var(--gray-600);
}

.advanced-filters {
  margin-top: 1rem;
  padding-top: 1rem;
  border-top: 1px solid var(--gray-200);
}

.area-filter {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.area-filter label {
  font-weight: 500;
  color: var(--gray-700);
}

.btn-filter {
  padding: 0.6rem 1.25rem;
  border: none;
  border-radius: var(--border-radius);
  background: var(--primary-color);
  color: white;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 500;
  transition: all var(--transition-base);
}

.btn-filter:hover {
  background: var(--primary-hover);
}

.stats-section {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
  margin-bottom: 1.5rem;
}

.stat-card {
  display: flex;
  align-items: center;
  gap: 1rem;
  background: white;
  padding: 1.5rem;
  border-radius: var(--border-radius);
  box-shadow: var(--shadow-sm);
}

.stat-icon {
  font-size: 2rem;
}

.stat-label {
  font-size: 0.85rem;
  color: var(--gray-600);
  margin-bottom: 0.25rem;
}

.stat-value {
  font-size: 1.5rem;
  font-weight: 700;
  color: var(--gray-900);
}

.data-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(350px, 1fr));
  gap: 1.5rem;
  margin-bottom: 2rem;
}

.data-card {
  background: white;
  border-radius: var(--border-radius);
  box-shadow: var(--shadow-sm);
  transition: all var(--transition-base);
  overflow: hidden;
}

.data-card:hover {
  box-shadow: var(--shadow-md);
  transform: translateY(-4px);
}

.data-card-header {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1.25rem;
  border-bottom: 1px solid var(--gray-200);
  position: relative;
}

.project-icon {
  font-size: 2rem;
  flex-shrink: 0;
}

.project-info {
  flex: 1;
  min-width: 0;
}

.project-title {
  margin: 0;
  font-size: 1rem;
  font-weight: 600;
  color: var(--gray-900);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.project-meta {
  margin: 0.25rem 0 0 0;
  font-size: 0.85rem;
  color: var(--gray-600);
}

.format-badge {
  padding: 0.3rem 0.75rem;
  border-radius: var(--border-radius-full);
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
}

.format-osgb {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
}

.format-3dtiles {
  background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
  color: white;
}

.format-s3m {
  background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%);
  color: white;
}

.format-obj {
  background: linear-gradient(135deg, #43e97b 0%, #38f9d7 100%);
  color: white;
}

.data-card-body {
  padding: 1.25rem;
}

.data-detail {
  display: flex;
  justify-content: space-between;
  margin-bottom: 0.75rem;
  font-size: 0.9rem;
}

.detail-label {
  color: var(--gray-600);
}

.detail-value {
  font-weight: 600;
  color: var(--gray-900);
}

.bounds-info {
  margin-top: 1rem;
  padding-top: 1rem;
  border-top: 1px solid var(--gray-200);
}

.bounds-title {
  font-size: 0.85rem;
  font-weight: 600;
  color: var(--gray-700);
  margin-bottom: 0.5rem;
}

.bounds-grid {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  font-size: 0.85rem;
  color: var(--gray-600);
}

.data-card-footer {
  display: flex;
  gap: 0.5rem;
  padding: 1rem 1.25rem;
  background: var(--gray-50);
  border-top: 1px solid var(--gray-200);
}

.btn-action {
  flex: 1;
  padding: 0.5rem;
  border: none;
  border-radius: var(--border-radius);
  cursor: pointer;
  font-size: 0.85rem;
  font-weight: 500;
  transition: all var(--transition-base);
}

.btn-map {
  background: var(--success-light);
  color: var(--success-color);
}

.btn-map:hover {
  background: var(--success-color);
  color: white;
}

.btn-edit {
  background: var(--warning-light);
  color: var(--warning-color);
}

.btn-edit:hover {
  background: var(--warning-color);
  color: white;
}

.btn-delete {
  background: var(--danger-light);
  color: var(--danger-color);
}

.btn-delete:hover {
  background: var(--danger-color);
  color: white;
}

.empty-state {
  grid-column: 1 / -1;
  text-align: center;
  padding: 4rem 2rem;
}

.empty-icon {
  font-size: 4rem;
  margin-bottom: 1rem;
}

.empty-state h3 {
  margin: 0 0 0.5rem 0;
  color: var(--gray-700);
}

.empty-state p {
  margin: 0;
  color: var(--gray-500);
}

.loading-container {
  text-align: center;
  padding: 4rem 2rem;
}

.data-form {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.form-section-title {
  font-size: 1rem;
  font-weight: 600;
  color: var(--gray-700);
  margin-top: 0.5rem;
  padding-bottom: 0.5rem;
  border-bottom: 2px solid var(--gray-200);
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.form-group label {
  font-weight: 600;
  color: var(--gray-700);
  font-size: 0.9rem;
}

.form-input {
  padding: 0.75rem;
  border: 1px solid var(--gray-300);
  border-radius: var(--border-radius);
  font-size: 0.95rem;
}

.form-input:focus {
  outline: none;
  border-color: var(--primary-color);
  box-shadow: 0 0 0 3px var(--primary-light);
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 1rem;
  margin-top: 1rem;
}

.btn {
  padding: 0.75rem 1.5rem;
  border: none;
  border-radius: var(--border-radius);
  cursor: pointer;
  font-size: 0.95rem;
  font-weight: 600;
  transition: all var(--transition-base);
}

.btn-primary {
  background: var(--primary-color);
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: var(--primary-hover);
  box-shadow: var(--shadow-md);
}

.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-secondary {
  background: var(--gray-200);
  color: var(--gray-700);
}

.btn-secondary:hover {
  background: var(--gray-300);
}

@media (max-width: 768px) {
  .tilt-photography-page {
    padding: 1rem;
  }

  .page-header {
    flex-direction: column;
    align-items: flex-start;
  }

  .data-grid {
    grid-template-columns: 1fr;
  }

  .form-row {
    grid-template-columns: 1fr;
  }

  .date-filter,
  .area-filter {
    flex-wrap: wrap;
  }
}
</style>
