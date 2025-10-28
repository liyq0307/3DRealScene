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
        使用v-for指令动态渲染场景集合
        每个卡片展示场景的基本信息和操作按钮
      -->
      <div v-for="scene in paginatedScenes" :key="scene.id" class="scene-card">
        <!-- 场景名称 -->
        <h3>{{ scene.name }}</h3>

        <!-- 场景描述 -->
        <p>{{ scene.description }}</p>

        <!-- 场景信息 -->
        <div class="scene-info">
          <div class="info-item">
            <span class="label">创建时间:</span>
            <span class="value">{{ formatDateTime(scene.createdAt) }}</span>
          </div>
          <div class="info-item">
            <span class="label">更新时间:</span>
            <span class="value">{{ formatDateTime(scene.updatedAt) }}</span>
          </div>
        </div>

        <!-- 操作按钮 -->
        <div class="scene-actions">
          <button class="btn btn-primary" @click="viewScene(scene.id)">
            查看场景
          </button>
          <button class="btn btn-secondary" @click="editScene(scene.id)">
            编辑
          </button>
          <button class="btn btn-danger" @click="deleteScene(scene.id)">
            删除
          </button>
        </div>
      </div>
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

    <!-- 场景详情对话框 -->
    <div v-if="showDetailDialog" class="modal-overlay" @click.self="closeDetailDialog">
      <div class="modal-content large">
        <div class="modal-header">
          <h3>场景详情</h3>
          <button @click="closeDetailDialog" class="modal-close-btn" aria-label="关闭">
            ✕
          </button>
        </div>

        <div class="modal-body">
          <div v-if="currentScene" class="scene-detail">
            <div class="detail-section">
              <h4>基本信息</h4>
              <div class="detail-grid">
                <div class="detail-item">
                  <span class="label">场景名称:</span>
                  <span class="value">{{ currentScene.name }}</span>
                </div>
                <div class="detail-item">
                  <span class="label">场景描述:</span>
                  <span class="value">{{ currentScene.description || '无' }}</span>
                </div>
                <div class="detail-item">
                  <span class="label">创建时间:</span>
                  <span class="value">{{ formatDateTime(currentScene.createdAt) }}</span>
                </div>
                <div class="detail-item">
                  <span class="label">更新时间:</span>
                  <span class="value">{{ formatDateTime(currentScene.updatedAt) }}</span>
                </div>
              </div>
            </div>

            <div class="detail-section" v-if="currentScene.boundaryGeoJson">
              <h4>地理信息</h4>
              <div class="detail-grid">
                <div class="detail-item full-width">
                  <span class="label">边界GeoJSON:</span>
                  <span class="value">{{ currentScene.boundaryGeoJson }}</span>
                </div>
              </div>
            </div>

            <!-- 3D查看器 -->
            <div class="detail-section">
              <h4>Cesium 3D地球</h4>
              <div class="scene-viewer-wrapper">
                <CesiumViewer
                  v-if="showDetailDialog"
                  :show-info="true"
                  :scene-objects="sceneObjects"
                  @ready="onCesiumReady"
                  @error="onCesiumError"
                />
              </div>
            </div>
          </div>
          <div v-else class="loading-state">
            <p>加载场景详情中...</p>
          </div>
        </div>
      </div>
    </div>

    <!-- 创建/编辑场景对话框 -->
    <div v-if="showCreateDialog" class="modal-overlay" @click="closeCreateDialog">
      <div class="modal-content" @click.stop>
        <h3>{{ editingScene ? '编辑场景' : '创建新场景' }}</h3>
        <div class="form-group">
          <label>场景名称 *</label>
          <input
            v-model="sceneForm.name"
            type="text"
            class="form-input"
            placeholder="输入场景名称"
          />
        </div>
        <div class="form-group">
          <label>场景描述</label>
          <textarea
            v-model="sceneForm.description"
            class="form-textarea"
            placeholder="输入场景描述"
          ></textarea>
        </div>
        <div class="form-group">
          <label>边界GeoJSON (可选)</label>
          <input
            v-model="sceneForm.boundaryGeoJson"
            type="text"
            class="form-input"
            placeholder="输入地理边界框 (GeoJSON格式)"
          />
        </div>

        <div class="form-group">
          <label>场景文件 (可选)</label>
          <FileUpload
            v-model="sceneFile"
            accept=".gltf,.glb,.obj,.fbx,.dae"
            :max-size="500"
            :multiple="false"
            hint="支持GLTF、GLB、OBJ、FBX、DAE格式，单个文件不超过500MB"
            :auto-upload="false"
            @upload="handleSceneFileUpload"
            @remove="handleRemoveSceneFile"
          />
        </div>

        <div class="modal-actions">
          <button @click="closeCreateDialog" class="btn btn-secondary">
            取消
          </button>
          <button @click="saveScene" class="btn btn-primary">
            保存
          </button>
        </div>
      </div>
    </div>
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
import { sceneService, sceneObjectService } from '../services/api'
import authStore from '@/stores/auth'
import { useMessage } from '@/composables/useMessage'
import CesiumViewer from '@/components/CesiumViewer.vue'
import SearchFilter from '@/components/SearchFilter.vue'
import Pagination from '@/components/Pagination.vue'
import FileUpload from '@/components/FileUpload.vue'

const { success: showSuccess, error: showError } = useMessage()

// ==================== 响应式数据 ====================

/**
 * 场景列表响应式数据
 * 使用any[]类型临时处理，实际项目中应定义具体的Scene接口
 * TODO: 定义Scene接口类型，提供更好的类型安全
 */
const scenes = ref<any[]>([])
const currentScene = ref<any>(null)
const sceneObjects = ref<any[]>([]) // 新增：存储当前场景的场景对象
const editingScene = ref<string | null>(null)

// 对话框状态
const showDetailDialog = ref(false)
const showCreateDialog = ref(false)

// 场景文件上传状态
const sceneFile = ref<File | null>(null)

// 场景表单
const sceneForm = ref({
  name: '',
  description: '',
  boundaryGeoJson: ''
})

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
 * 格式化日期时间
 */
const formatDateTime = (dateStr: string) => {
  if (!dateStr) return '-'
  return new Date(dateStr).toLocaleString('zh-CN')
}

/**
 * 查看场景详情
 *
 * @param id 场景唯一标识符
 *
 * 功能描述：
 * - 加载场景详情数据并显示在对话框中
 */
const viewScene = async (id: string) => {
  console.log('[Scenes] viewScene() called with id:', id)
  try {
    console.log('[Scenes] Fetching scene details...')
    currentScene.value = await sceneService.getScene(id)
    sceneObjects.value = currentScene.value.sceneObjects // 直接从场景数据中获取场景对象
    console.log('[Scenes] Scene details loaded:', currentScene.value)
    console.log('[Scenes] Scene objects loaded:', sceneObjects.value)
    showDetailDialog.value = true
    console.log('[Scenes] Dialog shown')
  } catch (error) {
    console.error('[Scenes] 加载场景详情失败:', error)
    showError('加载场景详情失败')
  }
}

/**
 * 关闭详情对话框
 */
const closeDetailDialog = () => {
  console.log('[Scenes] closeDetailDialog() called')
  showDetailDialog.value = false
  currentScene.value = null
  sceneObjects.value = [] // 清空场景对象
  console.log('[Scenes] Dialog closed, showDetailDialog:', showDetailDialog.value)
}

/**
 * 打开创建场景对话框
 */
const openCreateDialog = () => {
  editingScene.value = null
  sceneForm.value = {
    name: '',
    description: '',
    boundaryGeoJson: ''
  }
  sceneFile.value = null
  showCreateDialog.value = true
}

/**
 * 关闭创建对话框
 */
const closeCreateDialog = () => {
  showCreateDialog.value = false
  sceneFile.value = null
}

/**
 * 处理场景文件上传
 */
const handleSceneFileUpload = async (file: File) => {
  try {
    // TODO: 实现场景文件上传到服务器
    // const formData = new FormData()
    // formData.append('file', file)
    // const response = await sceneService.uploadSceneFile(formData)

    console.log('场景文件已选择:', file.name)
    showSuccess(`场景文件 ${file.name} 已选择，保存时将上传`)
  } catch (error) {
    console.error('场景文件处理失败:', error)
    showError('场景文件处理失败')
  }
}

/**
 * Cesium就绪回调
 */
const onCesiumReady = (viewer: any) => {
  console.log('Cesium地球初始化成功', viewer)
  showSuccess('Cesium 3D地球加载成功')
}

/**
 * Cesium错误回调
 */
const onCesiumError = (error: Error) => {
  console.error('Cesium初始化失败:', error)
  showError('Cesium地球加载失败: ' + error.message)
}

/**
 * 移除场景文件
 */
const handleRemoveSceneFile = () => {
  sceneFile.value = null
  showSuccess('文件已移除')
}

/**
 * 编辑场景
 */
const editScene = async (id: string) => {
  try {
    const scene = await sceneService.getScene(id)
    editingScene.value = id
    sceneForm.value = {
      name: scene.name,
      description: scene.description,
      boundaryGeoJson: scene.boundaryGeoJson || ''
    }
    sceneFile.value = null
    showCreateDialog.value = true
  } catch (error) {
    console.error('加载场景信息失败:', error)
    showError('加载场景信息失败')
  }
}

/**
 * 保存场景
 */
const saveScene = async () => {
  try {
    if (!sceneForm.value.name) {
      showError('请输入场景名称')
      return
    }

    // 获取当前用户ID (使用已注册的管理员用户ID作为默认值)
    const userId = authStore.currentUser.value?.id || '9055f06c-20d2-4e67-8a89-069887a2c4e8'

    // TODO: 如果有场景文件，先上传文件
    if (sceneFile.value) {
      console.log('需要上传场景文件:', sceneFile.value.name)
      // 实现文件上传逻辑
      // const formData = new FormData()
      // formData.append('file', sceneFile.value)
      // await sceneService.uploadSceneFile(formData)
    }

    if (editingScene.value) {
      // 更新场景
      await sceneService.updateScene(editingScene.value, sceneForm.value, userId)
      showSuccess('场景更新成功')
    } else {
      // 创建场景
      await sceneService.createScene(sceneForm.value, userId)
      showSuccess('场景创建成功')
    }

    await loadScenes()
    closeCreateDialog()
  } catch (error) {
    console.error('保存场景失败:', error)
    showError('保存场景失败，请稍后重试')
  }
}

/**
 * 删除场景
 */
const deleteScene = async (id: string) => {
  if (confirm('确定要删除此场景吗?')) {
    try {
      // 获取当前用户ID (使用已注册的管理员用户ID作为默认值)
      const userId = authStore.currentUser.value?.id || '9055f06c-20d2-4e67-8a89-069887a2c4e8'
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
  padding: 1.75rem 2rem;
  border-radius: var(--border-radius-lg);
  box-shadow: var(--shadow-sm);
  border: 1px solid var(--border-color);
  animation: fadeInDown 0.4s ease;
}

.page-header h1 {
  margin: 0;
  font-size: 2rem;
  font-weight: 700;
  background: var(--gradient-primary-alt);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
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
  grid-template-columns: repeat(auto-fill, minmax(350px, 1fr));
  gap: 1.5rem;
}

/**
 * 场景卡片样式
 * 单个场景的展示容器，采用卡片式设计
 *
 * 视觉设计：
 * - 白色背景，与整体主题保持一致
 * - 圆角边框，提供现代感
 * - 阴影效果，营造浮起感
 */
.scene-card {
  background: white;
  padding: 1.75rem;
  border-radius: var(--border-radius-lg);
  box-shadow: var(--shadow-sm);
  transition: all var(--transition-base);
  border: 1px solid var(--border-color);
  position: relative;
  overflow: hidden;
  animation: scaleIn 0.3s ease;
}

.scene-card::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 4px;
  background: var(--gradient-primary-alt);
  transform: scaleX(0);
  transform-origin: left;
  transition: transform var(--transition-base);
}

.scene-card:hover::before {
  transform: scaleX(1);
}

.scene-card:hover {
  box-shadow: var(--shadow-xl);
  transform: translateY(-6px);
  border-color: var(--primary-light);
}

/**
 * 场景卡片标题样式
 * 场景名称的展示样式
 */
.scene-card h3 {
  margin: 0 0 0.5rem 0;
  font-size: 1.25rem;
  color: #333;
}

/**
 * 场景卡片描述样式
 * 场景描述信息的展示样式
 */
.scene-card p {
  color: #666;
  margin-bottom: 1rem;
  line-height: 1.6;
}

.scene-info {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-bottom: 1rem;
  padding: 1rem;
  background: #f8f9fa;
  border-radius: 4px;
}

.info-item {
  display: flex;
  gap: 0.5rem;
  font-size: 0.9rem;
}

.info-item .label {
  color: #666;
  font-weight: 500;
  min-width: 80px;
}

.info-item .value {
  color: #333;
}

.scene-actions {
  display: flex;
  gap: 0.5rem;
  justify-content: flex-end;
}

/* 按钮样式 */
.btn {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.6rem 1.2rem;
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius);
  background: white;
  cursor: pointer;
  transition: all var(--transition-base);
  font-size: 0.9rem;
  font-weight: 600;
  position: relative;
  overflow: hidden;
}

.btn::before {
  content: '';
  position: absolute;
  inset: 0;
  opacity: 0;
  transition: opacity var(--transition-base);
  z-index: -1;
}

.btn:hover {
  transform: translateY(-2px);
  box-shadow: var(--shadow-md);
}

.btn-primary {
  background: var(--gradient-primary-alt);
  color: white;
  border: none;
  box-shadow: var(--shadow-colored);
}

.btn-primary::before {
  background: var(--gradient-info);
}

.btn-primary:hover {
  box-shadow: var(--shadow-xl);
}

.btn-primary:hover::before {
  opacity: 1;
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
