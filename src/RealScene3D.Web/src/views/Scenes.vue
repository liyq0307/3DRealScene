<template>
  <!--
    场景列表视图模板
    展示系统中所有可用的3D场景，以卡片形式呈现
    支持场景浏览、详情查看和删除功能
  -->

  <div class="scenes">
    <!-- 页面标题和操作按钮 -->
    <div class="page-header">
      <h1>3D场景管理</h1>
      <button @click="openCreateDialog" class="btn btn-primary">
        <span class="icon">➕</span>
        创建新场景
      </button>
    </div>

    <!-- 搜索和筛选组件 -->
    <SearchFilter
      v-model:searchText="searchText"
      placeholder="搜索场景名称或描述..."
      @search="currentPage = 1"
    />

    <!-- 场景列表容器 -->
    <div class="scene-list">
      <!--
        场景卡片列表
        使用SceneCard组件展示场景
        采用现代化图像背景卡片设计
      -->
      <SceneCard
        v-for="scene in paginatedScenes"
        :key="scene.id"
        :id="scene.id"
        :name="scene.name"
        :description="scene.description"
        :created-at="scene.createdAt"
        :updated-at="scene.updatedAt"
        :preview-image="scene.previewImage"
        @view="viewScene"
        @edit="editScene"
        @delete="deleteScene"
      />
    </div>

    <!-- 空状态 -->
    <div v-if="filteredScenes.length === 0" class="empty-state">
      <p>{{ searchText ? '没有找到匹配的场景' : '暂无场景数据' }}</p>
      <button v-if="!searchText" @click="openCreateDialog" class="btn btn-primary">
        创建第一个场景
      </button>
    </div>

    <!-- 分页组件 -->
    <Pagination
      v-if="filteredScenes.length > 0"
      v-model:currentPage="currentPage"
      v-model:pageSize="pageSize"
      :total="filteredScenes.length"
    />
  </div>
</template>

<script setup lang="ts">
/**
 * 场景列表视图组件 - Vue 3 组合式API实现
 *
 * 功能说明：
 * - 展示系统中所有公开的3D场景列表
 * - 提供场景卡片式浏览界面
 * - 支持场景详情查看、创建、编辑和删除
 * - 集成错误处理和加载状态管理
 * - 响应式网格布局，适配不同屏幕尺寸
 *
 * 技术栈：Vue 3 + TypeScript + CSS Grid
 * 作者：liyq
 * 创建时间：2025-10-13
 */

import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { sceneService } from '../services/api'
import { useMessage } from '@/composables/useMessage'
import authStore from '@/stores/auth'
import SearchFilter from '@/components/SearchFilter.vue'
import Pagination from '@/components/Pagination.vue'
import SceneCard from '@/components/SceneCard.vue'

const { success: showSuccess, error: showError } = useMessage()
const router = useRouter()

// ==================== 响应式数据 ====================

/**
 * 场景列表响应式数据
 * 使用any[]类型临时处理，实际项目中应定义具体的Scene接口
 * TODO: 定义Scene接口类型,提供更好的类型安全
 */
const scenes = ref<any[]>([])

// 搜索和筛选状态
const searchText = ref('')
const currentPage = ref(1)
const pageSize = ref(20)

// 计算过滤后的场景列表
const filteredScenes = computed(() => {
  let result = scenes.value

  // 搜索过滤
  if (searchText.value) {
    const keyword = searchText.value.toLowerCase()
    result = result.filter(scene =>
      scene.name.toLowerCase().includes(keyword) ||
      (scene.description && scene.description.toLowerCase().includes(keyword))
    )
  }

  return result
})

// 计算分页后的场景列表
const paginatedScenes = computed(() => {
  const start = (currentPage.value - 1) * pageSize.value
  const end = start + pageSize.value
  return filteredScenes.value.slice(start, end)
})

// ==================== 业务逻辑方法 ====================

/**
 * 加载场景列表数据
 */
const loadScenes = async () => {
  console.log('[Scenes] loadScenes() called')
  try {
    // 调用场景服务获取公开场景列表
    console.log('[Scenes] Fetching scenes from API...')
    scenes.value = await sceneService.getAllScenes()
    console.log('[Scenes] Scenes loaded successfully:', scenes.value.length, 'scenes')
  } catch (error) {
    // 记录错误日志，便于调试和监控
    console.error('[Scenes] 加载场景列表失败:', error)
    showError('加载场景列表失败，请稍后重试')
  }
}

/**
 * 查看场景详情（点击卡片）
 *
 * @param id 场景唯一标识符
 */
const viewScene = (id: string) => {
  console.log('[Scenes] viewScene() called with id:', id)
  // 跳转到场景预览页面
  router.push({ name: 'ScenePreview', params: { id } })
}

/**
 * 打开创建场景对话框
 */
const openCreateDialog = () => {
  // 从弹窗方式改为路由跳转到创建页面
  router.push({ name: 'ScenesCreate' })
}

/**
 * 编辑场景
 */
const editScene = (id: string) => {
  router.push({ name: 'ScenesEdit', params: { id } })
}

/**
 * 删除场景
 */
const deleteScene = async (id: string) => {
  if (confirm('确定要删除此场景吗?')) {
    try {
      // 获取当前用户ID
      const userId = authStore.currentUser.value?.id || '00000000-0000-0000-0000-000000000001'
      await sceneService.deleteScene(id, userId)
      showSuccess('场景删除成功')
      await loadScenes()
    } catch (error) {
      console.error('删除场景失败:', error)
      showError('删除场景失败，请稍后重试')
    }
  }
}

// ==================== 生命周期钩子 ====================

/**
 * 组件挂载完成后的处理
 */
onMounted(() => {
  console.log('[Scenes] Component mounted, loading scenes...')
  loadScenes()
})
</script>

<style scoped>
/**
 * 场景列表页面样式定义
 * 使用scoped限定样式作用域，确保样式隔离
 */

/**
 * 页面主容器样式
 * 提供页面级别的内边距，营造舒适的浏览空间
 */
.scenes {
  padding: 2rem;
  background: linear-gradient(to bottom, #f9fafb 0%, #ffffff 100%);
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

.page-header h1 {
  margin: 0;
  font-size: 1.75rem;
  font-weight: 600;
  color: #333;
}

/**
 * 场景列表容器样式
 * 使用CSS Grid实现响应式网格布局
 *
 * 布局特点：
 * - 自适应列数，最小列宽300px
 * - 列间隙1.5rem，提供良好的视觉分离
 */
.scene-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 1.5rem;
}

/* 响应式调整 */
@media (max-width: 767px) {
  .scene-list {
    grid-template-columns: 1fr;
  }
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
  background: var(--secondary-light);
  color: var(--secondary-color);
  border-color: var(--secondary-color);
}

.btn-secondary:hover {
  background: var(--secondary-color);
  color: white;
}

.btn-danger {
  background: var(--danger-light);
  color: var(--danger-color);
  border-color: var(--danger-color);
}

.btn-danger:hover {
  background: var(--danger-color);
  color: white;
  box-shadow: 0 4px 12px rgba(239, 68, 68, 0.3);
}

/* 空状态 */
.empty-state {
  text-align: center;
  padding: 4rem;
  background: white;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.empty-state p {
  margin-bottom: 1.5rem;
  color: #999;
  font-size: 1.1rem;
}

/* 场景详情样式 */
.scene-detail {
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

.detail-section h4 {
  margin: 0 0 1rem 0;
  color: #333;
  font-size: 1.1rem;
  font-weight: 600;
}

.detail-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 1rem;
}

.detail-item {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  padding: 0.75rem;
  background: #f8f9fa;
  border-radius: 6px;
}

.detail-item.full-width {
  grid-column: 1 / -1;
}

.detail-item .label {
  font-size: 0.85rem;
  color: #666;
  font-weight: 500;
}

.detail-item .value {
  font-size: 0.95rem;
  color: #333;
  word-break: break-word;
}

/* 3D查看器容器 */
.scene-viewer-wrapper {
  position: relative;
  width: 100%;
  height: 500px;
  background: #1a1a1a;
  border-radius: 8px;
  overflow: hidden;
}

/* 格式兼容性提示 */
.format-notice {
  display: flex;
  gap: 1rem;
  padding: 1rem 1.25rem;
  margin-bottom: 1rem;
  background: linear-gradient(135deg, #fff3cd 0%, #ffeaa7 100%);
  border: 1px solid #ffc107;
  border-left: 4px solid #ff9800;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(255, 152, 0, 0.1);
}

.notice-icon {
  font-size: 1.5rem;
  flex-shrink: 0;
  line-height: 1;
}

.notice-content {
  flex: 1;
}

.notice-content strong {
  display: block;
  margin-bottom: 0.5rem;
  color: #856404;
  font-size: 1rem;
}

.notice-content p {
  margin: 0 0 0.75rem 0;
  color: #664d03;
  font-size: 0.9rem;
  line-height: 1.6;
}

.btn-sm {
  padding: 0.4rem 0.8rem;
  font-size: 0.85rem;
}

.loading-state {
  text-align: center;
  padding: 3rem;
  color: #999;
}

.loading-state p {
  margin: 0;
  font-size: 1rem;
}

/* 模态框样式 */
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(15, 23, 42, 0.6);
  backdrop-filter: blur(8px);
  -webkit-backdrop-filter: blur(8px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: var(--z-index-modal);
  animation: fadeIn 0.2s ease;
}

.modal-content {
  background: white;
  border-radius: var(--border-radius-lg);
  padding: 2rem;
  width: 500px;
  max-width: 90vw;
  max-height: 85vh;
  overflow-y: auto;
  box-shadow: var(--shadow-2xl);
  animation: scaleIn 0.3s cubic-bezier(0.68, -0.55, 0.265, 1.55);
  border: 1px solid var(--border-color);
}

.modal-content.large {
  width: 850px;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem 2rem;
  border-bottom: 1px solid #e1e5e9;
  margin: -2rem -2rem 0 -2rem;
}

.modal-header h3 {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 700;
  background: var(--gradient-primary-alt);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

.modal-close-btn {
  width: 32px;
  height: 32px;
  border: none;
  background: #f8f9fa;
  color: #666;
  border-radius: 4px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.25rem;
  transition: all 0.2s ease;
  padding: 0;
}

.modal-close-btn:hover {
  background: #ffebee;
  color: #dc3545;
  transform: rotate(90deg);
}

.modal-body {
  padding: 2rem 0;
  max-height: 70vh;
  overflow-y: auto;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 1rem;
  padding: 1.5rem 2rem;
  border-top: 1px solid #e1e5e9;
  margin: 0 -2rem -2rem -2rem;
  background: #f8f9fa;
}

.scene-detail {
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

.detail-section h4 {
  margin: 0 0 1rem 0;
  color: #333;
  font-size: 1.1rem;
}

.detail-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 1rem;
}

.detail-item {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.detail-item.full-width {
  grid-column: 1 / -1;
}

.detail-item .label {
  font-size: 0.85rem;
  color: #999;
}

.detail-item .value {
  font-size: 0.95rem;
  color: #333;
}

.scene-viewer-container {
  position: relative;
  height: 500px;
  background: #1a1a1a;
  border-radius: 8px;
  overflow: hidden;
}

.scene-viewer-placeholder {
  height: 400px;
  background: #f8f9fa;
  border: 2px dashed #e1e5e9;
  border-radius: 8px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: #999;
}

.scene-viewer-placeholder p {
  margin: 0.5rem 0;
}

.scene-viewer-placeholder .hint {
  font-size: 0.9rem;
}

/* 表单样式 */
.form-group {
  margin-bottom: 1.25rem;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 600;
  color: var(--gray-800);
  font-size: 0.95rem;
}

.form-input,
.form-textarea {
  width: 100%;
  padding: 0.75rem 1rem;
  border: 1.5px solid var(--border-color);
  border-radius: var(--border-radius);
  font-size: 0.95rem;
  transition: all var(--transition-base);
  background: var(--gray-50);
}

.form-input:hover,
.form-textarea:hover {
  border-color: var(--gray-400);
  background: white;
}

.form-input:focus,
.form-textarea:focus {
  outline: none;
  border-color: var(--primary-color);
  background: white;
  box-shadow: 0 0 0 3px rgba(99, 102, 241, 0.1);
}

.form-textarea {
  min-height: 120px;
  resize: vertical;
  font-family: inherit;
}

/* 表单提示样式 */
.field-hint {
  margin-top: 0.75rem;
  padding: 1rem;
  background: #f8f9fa;
  border-radius: 6px;
  border-left: 3px solid #667eea;
  font-size: 0.9rem;
  line-height: 1.6;
}

.field-hint strong {
  display: block;
  margin-bottom: 0.5rem;
  color: #333;
  font-size: 0.95rem;
}

.field-hint ul {
  margin: 0.5rem 0 0 0;
  padding-left: 1.5rem;
  list-style: none;
}

.field-hint ul li {
  margin-bottom: 0.4rem;
  color: #555;
  position: relative;
}

.field-hint ul li::before {
  content: '';
  position: absolute;
  left: -1.2rem;
  top: 0.5rem;
  width: 4px;
  height: 4px;
  background: #667eea;
  border-radius: 50%;
}

.field-hint.warning {
  background: #fff3cd;
  border-left-color: #ff9800;
  color: #856404;
}

.form-input:disabled {
  background: #f0f0f0;
  color: #999;
  cursor: not-allowed;
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
