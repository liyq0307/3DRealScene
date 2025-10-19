<template>
  <div class="video-metadata-page">
    <!-- 页面头部 -->
    <div class="page-header">
      <div class="header-content">
        <h1 class="page-title">📹 视频元数据管理</h1>
        <p class="page-description">管理和浏览系统中的所有视频资源元数据</p>
      </div>
      <button @click="showCreateDialog = true" class="btn btn-primary">
        ➕ 添加视频
      </button>
    </div>

    <!-- 搜索和筛选区域 -->
    <div class="filters-section">
      <div class="search-box">
        <input
          v-model="searchQuery"
          type="text"
          placeholder="🔍 搜索视频文件名..."
          class="search-input"
          @input="handleSearch"
        />
      </div>

      <div class="filter-controls">
        <select v-model="selectedSceneFilter" @change="handleFilterChange" class="filter-select">
          <option value="">全部场景</option>
          <option v-for="scene in scenes" :key="scene.id" :value="scene.id">
            {{ scene.name }}
          </option>
        </select>

        <div class="filter-tags">
          <input
            v-model="tagInput"
            type="text"
            placeholder="按标签筛选 (回车添加)"
            class="tag-input"
            @keyup.enter="addTagFilter"
          />
          <div v-if="tagFilters.length > 0" class="active-tags">
            <span v-for="(tag, index) in tagFilters" :key="index" class="tag">
              {{ tag }}
              <button @click="removeTagFilter(index)" class="tag-remove">×</button>
            </span>
          </div>
        </div>
      </div>
    </div>

    <!-- 统计信息 -->
    <div class="stats-section">
      <div class="stat-card">
        <div class="stat-icon">📊</div>
        <div class="stat-content">
          <div class="stat-label">总视频数</div>
          <div class="stat-value">{{ totalCount }}</div>
        </div>
      </div>
      <div class="stat-card">
        <div class="stat-icon">🎬</div>
        <div class="stat-content">
          <div class="stat-label">当前页</div>
          <div class="stat-value">{{ videos.length }}</div>
        </div>
      </div>
    </div>

    <!-- 加载状态 -->
    <div v-if="loading" class="loading-container">
      <LoadingSpinner :loading="loading" text="加载视频数据..." />
      <p>加载中...</p>
    </div>

    <!-- 错误提示 -->
    <ErrorDisplay v-if="error" :error="error" @retry="loadVideos" />

    <!-- 视频列表 -->
    <div v-if="!loading && !error" class="videos-grid">
      <div
        v-for="video in videos"
        :key="video.id"
        class="video-card"
      >
        <div class="video-card-header">
          <div class="video-icon">🎥</div>
          <div class="video-info">
            <h3 class="video-title">{{ video.fileName }}</h3>
            <p class="video-meta">
              {{ formatFileSize(video.fileSize) }} · {{ video.duration }}s
            </p>
          </div>
        </div>

        <div class="video-card-body">
          <div class="video-detail">
            <span class="detail-label">分辨率:</span>
            <span class="detail-value">{{ video.resolution?.width }}x{{ video.resolution?.height }}</span>
          </div>
          <div class="video-detail">
            <span class="detail-label">编码:</span>
            <span class="detail-value">{{ video.codec || 'N/A' }}</span>
          </div>
          <div class="video-detail">
            <span class="detail-label">帧率:</span>
            <span class="detail-value">{{ video.frameRate || 'N/A' }} fps</span>
          </div>
          <div v-if="video.tags && video.tags.length > 0" class="video-tags">
            <span v-for="tag in video.tags" :key="tag" class="video-tag">{{ tag }}</span>
          </div>
        </div>

        <div class="video-card-footer">
          <button @click="viewVideo(video)" class="btn-action btn-view">查看</button>
          <button @click="editVideo(video)" class="btn-action btn-edit">编辑</button>
          <button @click="deleteVideo(video)" class="btn-action btn-delete">删除</button>
        </div>
      </div>

      <!-- 空状态 -->
      <div v-if="videos.length === 0" class="empty-state">
        <div class="empty-icon">📹</div>
        <h3>暂无视频数据</h3>
        <p>点击右上角"添加视频"按钮创建第一个视频元数据</p>
      </div>
    </div>

    <!-- 分页 -->
    <Pagination
      v-if="!loading && videos.length > 0"
      :current-page="currentPage"
      :total-pages="totalPages"
      :page-size="pageSize"
      :total-count="totalCount"
      @page-change="handlePageChange"
      @page-size-change="handlePageSizeChange"
    />

    <!-- 创建/编辑对话框 -->
    <Modal v-if="showCreateDialog || showEditDialog" @close="closeDialog">
      <template #header>
        <h2>{{ editingVideo ? '编辑视频元数据' : '添加视频元数据' }}</h2>
      </template>
      <template #body>
        <form @submit.prevent="submitForm" class="video-form">
          <div class="form-group">
            <label>文件名 *</label>
            <input
              v-model="formData.fileName"
              type="text"
              required
              placeholder="请输入视频文件名"
              class="form-input"
            />
          </div>

          <div class="form-group">
            <label>文件路径 *</label>
            <input
              v-model="formData.filePath"
              type="text"
              required
              placeholder="请输入文件存储路径"
              class="form-input"
            />
          </div>

          <div class="form-row">
            <div class="form-group">
              <label>文件大小 (字节) *</label>
              <input
                v-model.number="formData.fileSize"
                type="number"
                required
                class="form-input"
              />
            </div>

            <div class="form-group">
              <label>时长 (秒) *</label>
              <input
                v-model.number="formData.duration"
                type="number"
                required
                class="form-input"
              />
            </div>
          </div>

          <div class="form-row">
            <div class="form-group">
              <label>宽度 (像素)</label>
              <input
                v-model.number="formData.width"
                type="number"
                class="form-input"
              />
            </div>

            <div class="form-group">
              <label>高度 (像素)</label>
              <input
                v-model.number="formData.height"
                type="number"
                class="form-input"
              />
            </div>
          </div>

          <div class="form-row">
            <div class="form-group">
              <label>编码格式</label>
              <input
                v-model="formData.codec"
                type="text"
                placeholder="例如: H.264"
                class="form-input"
              />
            </div>

            <div class="form-group">
              <label>帧率 (fps)</label>
              <input
                v-model.number="formData.frameRate"
                type="number"
                class="form-input"
              />
            </div>
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

          <div class="form-group">
            <label>标签 (逗号分隔)</label>
            <input
              v-model="formData.tagsInput"
              type="text"
              placeholder="例如: 教学,演示,会议"
              class="form-input"
            />
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
import { videoMetadataService, sceneService } from '@/services/api'
import { useMessage } from '@/composables/useMessage'
import LoadingSpinner from '@/components/LoadingSpinner.vue'
import ErrorDisplay from '@/components/ErrorDisplay.vue'
import Pagination from '@/components/Pagination.vue'
import Modal from '@/components/Modal.vue'

const { success, error: showError } = useMessage()

// 状态
const loading = ref(false)
const error = ref('')
const videos = ref<any[]>([])
const scenes = ref<any[]>([])
const totalCount = ref(0)
const currentPage = ref(1)
const pageSize = ref(12)
const searchQuery = ref('')
const selectedSceneFilter = ref('')
const tagFilters = ref<string[]>([])
const tagInput = ref('')

// 对话框状态
const showCreateDialog = ref(false)
const showEditDialog = ref(false)
const editingVideo = ref<any>(null)
const submitting = ref(false)

// 表单数据
const formData = ref({
  fileName: '',
  filePath: '',
  fileSize: 0,
  duration: 0,
  width: 1920,
  height: 1080,
  codec: '',
  frameRate: 30,
  sceneId: '',
  tagsInput: ''
})

// 计算属性
const totalPages = computed(() => Math.ceil(totalCount.value / pageSize.value))

// 加载视频列表
const loadVideos = async () => {
  loading.value = true
  error.value = ''

  try {
    const result = await videoMetadataService.getVideosPaged(
      selectedSceneFilter.value || undefined,
      currentPage.value,
      pageSize.value
    )

    videos.value = result.items || []
    totalCount.value = result.totalCount || 0
  } catch (err: any) {
    error.value = err.message || '加载视频列表失败'
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
      videos.value = await videoMetadataService.searchVideosByFileName(searchQuery.value)
      totalCount.value = videos.value.length
    } catch (err: any) {
      showError('搜索失败: ' + err.message)
    }
  } else {
    await loadVideos()
  }
}

// 筛选处理
const handleFilterChange = async () => {
  currentPage.value = 1
  await loadVideos()
}

// 标签筛选
const addTagFilter = () => {
  if (tagInput.value.trim() && !tagFilters.value.includes(tagInput.value.trim())) {
    tagFilters.value.push(tagInput.value.trim())
    tagInput.value = ''
    applyTagFilter()
  }
}

const removeTagFilter = (index: number) => {
  tagFilters.value.splice(index, 1)
  applyTagFilter()
}

const applyTagFilter = async () => {
  if (tagFilters.value.length > 0) {
    try {
      videos.value = await videoMetadataService.getVideosByTags(tagFilters.value)
      totalCount.value = videos.value.length
    } catch (err: any) {
      showError('标签筛选失败: ' + err.message)
    }
  } else {
    await loadVideos()
  }
}

// 分页处理
const handlePageChange = (page: number) => {
  currentPage.value = page
  loadVideos()
}

const handlePageSizeChange = (size: number) => {
  pageSize.value = size
  currentPage.value = 1
  loadVideos()
}

// 查看视频
const viewVideo = (video: any) => {
  // TODO: 实现视频预览功能
  console.log('查看视频:', video)
  success('视频预览功能开发中')
}

// 编辑视频
const editVideo = (video: any) => {
  editingVideo.value = video
  formData.value = {
    fileName: video.fileName,
    filePath: video.filePath,
    fileSize: video.fileSize,
    duration: video.duration,
    width: video.resolution?.width || 1920,
    height: video.resolution?.height || 1080,
    codec: video.codec || '',
    frameRate: video.frameRate || 30,
    sceneId: video.sceneId || '',
    tagsInput: video.tags?.join(', ') || ''
  }
  showEditDialog.value = true
}

// 删除视频
const deleteVideo = async (video: any) => {
  if (!confirm(`确定要删除视频"${video.fileName}"吗？`)) {
    return
  }

  try {
    await videoMetadataService.deleteVideo(video.id)
    success('删除成功')
    await loadVideos()
  } catch (err: any) {
    showError('删除失败: ' + err.message)
  }
}

// 提交表单
const submitForm = async () => {
  submitting.value = true

  try {
    const videoData = {
      fileName: formData.value.fileName,
      filePath: formData.value.filePath,
      fileSize: formData.value.fileSize,
      duration: formData.value.duration,
      resolution: {
        width: formData.value.width,
        height: formData.value.height
      },
      codec: formData.value.codec,
      frameRate: formData.value.frameRate,
      sceneId: formData.value.sceneId || null,
      tags: formData.value.tagsInput
        ? formData.value.tagsInput.split(',').map(t => t.trim())
        : []
    }

    if (editingVideo.value) {
      await videoMetadataService.updateVideo(editingVideo.value.id, videoData)
      success('更新成功')
    } else {
      await videoMetadataService.createVideo(videoData)
      success('创建成功')
    }

    closeDialog()
    await loadVideos()
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
  editingVideo.value = null
  formData.value = {
    fileName: '',
    filePath: '',
    fileSize: 0,
    duration: 0,
    width: 1920,
    height: 1080,
    codec: '',
    frameRate: 30,
    sceneId: '',
    tagsInput: ''
  }
}

// 格式化文件大小
const formatFileSize = (bytes: number) => {
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(2) + ' KB'
  if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(2) + ' MB'
  return (bytes / (1024 * 1024 * 1024)).toFixed(2) + ' GB'
}

// 初始化
onMounted(() => {
  loadVideos()
  loadScenes()
})
</script>

<style scoped>
.video-metadata-page {
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

.filter-controls {
  display: flex;
  gap: 1rem;
  flex-wrap: wrap;
}

.filter-select,
.tag-input {
  padding: 0.6rem 1rem;
  border: 1px solid var(--gray-300);
  border-radius: var(--border-radius);
  font-size: 0.9rem;
}

.filter-select {
  min-width: 200px;
}

.filter-tags {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.active-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.tag {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.4rem 0.8rem;
  background: var(--primary-light);
  color: var(--primary-color);
  border-radius: var(--border-radius-full);
  font-size: 0.85rem;
}

.tag-remove {
  background: none;
  border: none;
  color: var(--primary-color);
  cursor: pointer;
  font-size: 1.2rem;
  line-height: 1;
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

.videos-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 1.5rem;
  margin-bottom: 2rem;
}

.video-card {
  background: white;
  border-radius: var(--border-radius);
  box-shadow: var(--shadow-sm);
  transition: all var(--transition-base);
  overflow: hidden;
}

.video-card:hover {
  box-shadow: var(--shadow-md);
  transform: translateY(-4px);
}

.video-card-header {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1.25rem;
  border-bottom: 1px solid var(--gray-200);
}

.video-icon {
  font-size: 2rem;
  flex-shrink: 0;
}

.video-title {
  margin: 0;
  font-size: 1rem;
  font-weight: 600;
  color: var(--gray-900);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.video-meta {
  margin: 0.25rem 0 0 0;
  font-size: 0.85rem;
  color: var(--gray-600);
}

.video-card-body {
  padding: 1.25rem;
}

.video-detail {
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

.video-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-top: 1rem;
}

.video-tag {
  padding: 0.3rem 0.7rem;
  background: var(--gray-100);
  color: var(--gray-700);
  border-radius: var(--border-radius-full);
  font-size: 0.8rem;
}

.video-card-footer {
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

.btn-view {
  background: var(--info-light);
  color: var(--info-color);
}

.btn-view:hover {
  background: var(--info-color);
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

.video-form {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
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
  .video-metadata-page {
    padding: 1rem;
  }

  .page-header {
    flex-direction: column;
    align-items: flex-start;
  }

  .videos-grid {
    grid-template-columns: 1fr;
  }

  .form-row {
    grid-template-columns: 1fr;
  }
}
</style>
