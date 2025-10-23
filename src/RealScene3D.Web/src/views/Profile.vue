<template>
  <div class="profile-page">
    <div class="profile-container">
      <!-- 用户信息卡片 -->
      <Card variant="primary" class="profile-card">
        <template #header>
          <div class="profile-header">
            <h2>个人资料</h2>
            <button v-if="!isEditing" class="btn btn-edit" @click="startEdit">
              编辑资料
            </button>
          </div>
        </template>

        <div class="profile-content">
          <!-- 头像区域 -->
          <div class="avatar-section">
            <div class="avatar">
              <img v-if="userInfo?.avatarUrl" :src="userInfo.avatarUrl" alt="头像" />
              <div v-else class="avatar-placeholder">
                {{ userInfo?.username?.charAt(0).toUpperCase() || 'U' }}
              </div>
            </div>
            <button v-if="isEditing" class="btn-upload-avatar" @click="openAvatarUpload">
              上传头像
            </button>
          </div>

          <!-- 用户信息表单 -->
          <div class="info-section">
            <div class="form-group">
              <label>用户名</label>
              <input
                v-model="formData.username"
                type="text"
                class="form-input"
                :disabled="!isEditing"
              />
            </div>

            <div class="form-group">
              <label>邮箱</label>
              <input
                v-model="formData.email"
                type="email"
                class="form-input"
                :disabled="!isEditing"
              />
            </div>

            <div class="form-group">
              <label>角色</label>
              <input
                :value="userInfo?.role || ''"
                type="text"
                class="form-input"
                disabled
              />
            </div>

            <div class="form-group">
              <label>用户ID</label>
              <input
                :value="userInfo?.id || ''"
                type="text"
                class="form-input"
                disabled
              />
            </div>
          </div>
        </div>

        <template v-if="isEditing" #footer>
          <button class="btn btn-secondary" @click="cancelEdit">取消</button>
          <button class="btn btn-primary" @click="saveProfile">保存</button>
        </template>
      </Card>

      <!-- 修改密码卡片 -->
      <Card variant="info" class="password-card">
        <template #header>
          <h2>修改密码</h2>
        </template>

        <div class="password-form">
          <div class="form-group">
            <label>当前密码</label>
            <input
              v-model="passwordForm.currentPassword"
              type="password"
              class="form-input"
              placeholder="请输入当前密码"
            />
          </div>

          <div class="form-group">
            <label>新密码</label>
            <input
              v-model="passwordForm.newPassword"
              type="password"
              class="form-input"
              placeholder="请输入新密码(至少6位)"
            />
          </div>

          <div class="form-group">
            <label>确认新密码</label>
            <input
              v-model="passwordForm.confirmPassword"
              type="password"
              class="form-input"
              placeholder="请再次输入新密码"
            />
          </div>
        </div>

        <template #footer>
          <button class="btn btn-primary" @click="changePassword">
            修改密码
          </button>
        </template>
      </Card>

      <!-- 统计信息卡片 -->
      <div class="stats-grid">
        <Card hoverable class="stat-card">
          <div class="stat-content">
            <div class="stat-icon gradient-bg-primary">🎨</div>
            <div class="stat-details">
              <div class="stat-label">创建场景</div>
              <div class="stat-value">{{ userStats.scenesCount }}</div>
            </div>
          </div>
        </Card>

        <Card hoverable class="stat-card">
          <div class="stat-content">
            <div class="stat-icon gradient-bg-success">📊</div>
            <div class="stat-details">
              <div class="stat-label">工作流</div>
              <div class="stat-value">{{ userStats.workflowsCount }}</div>
            </div>
          </div>
        </Card>

        <Card hoverable class="stat-card">
          <div class="stat-content">
            <div class="stat-icon gradient-bg-info">🗂️</div>
            <div class="stat-details">
              <div class="stat-label">切片任务</div>
              <div class="stat-value">{{ userStats.slicingTasksCount }}</div>
            </div>
          </div>
        </Card>
      </div>
    </div>

    <!-- 头像上传对话框 -->
    <Modal
      v-model="showAvatarUpload"
      title="上传头像"
      size="md"
      :show-footer="false"
    >
      <FileUpload
        v-model="avatarFile"
        accept="image/*"
        :max-size="5"
        hint="支持JPG、PNG、WebP格式,单个文件不超过5MB"
        :auto-upload="false"
        @upload="handleAvatarFileUpload"
      />
    </Modal>
  </div>
</template>

<script setup lang="ts">
/**
 * 用户个人中心页面
 *
 * 功能特性:
 * - 查看和编辑个人资料
 * - 修改密码
 * - 查看用户统计数据
 * - 上传头像
 *
 * @author liyq
 * @date 2025-10-15
 */

import { ref, reactive, computed, onMounted } from 'vue'
import { useMessage } from '@/composables/useMessage'
import authStore from '@/stores/auth'
import Card from '@/components/Card.vue'
import Modal from '@/components/Modal.vue'
import FileUpload from '@/components/FileUpload.vue'
import { sceneService, workflowService, slicingService, fileService } from '@/services/api'

const { success, error: showError } = useMessage()

const isEditing = ref(false)
const showAvatarUpload = ref(false)
const avatarFile = ref<File | null>(null)
const userInfo = computed(() => authStore.currentUser.value)

const formData = reactive({
  username: '',
  email: ''
})

const passwordForm = reactive({
  currentPassword: '',
  newPassword: '',
  confirmPassword: ''
})

const userStats = reactive({
  scenesCount: 0,
  workflowsCount: 0,
  slicingTasksCount: 0
})

// 开始编辑
const startEdit = () => {
  if (userInfo.value) {
    formData.username = userInfo.value.username
    formData.email = userInfo.value.email
  }
  isEditing.value = true
}

// 取消编辑
const cancelEdit = () => {
  isEditing.value = false
}

// 保存资料
const saveProfile = async () => {
  try {
    // 验证
    if (!formData.username.trim()) {
      showError('用户名不能为空')
      return
    }

    if (!formData.email.trim()) {
      showError('邮箱不能为空')
      return
    }

    // TODO: 调用API更新用户信息
    authStore.updateUserInfo({
      username: formData.username,
      email: formData.email
    })

    success('个人资料更新成功')
    isEditing.value = false
  } catch (err) {
    showError('更新失败,请稍后重试')
  }
}

// 修改密码
const changePassword = async () => {
  try {
    // 验证
    if (!passwordForm.currentPassword) {
      showError('请输入当前密码')
      return
    }

    if (!passwordForm.newPassword || passwordForm.newPassword.length < 6) {
      showError('新密码至少需要6位')
      return
    }

    if (passwordForm.newPassword !== passwordForm.confirmPassword) {
      showError('两次输入的密码不一致')
      return
    }

    // TODO: 调用API修改密码

    success('密码修改成功')
    // 清空表单
    passwordForm.currentPassword = ''
    passwordForm.newPassword = ''
    passwordForm.confirmPassword = ''
  } catch (err) {
    showError('修改密码失败,请稍后重试')
  }
}

// 打开头像上传对话框
const openAvatarUpload = () => {
  showAvatarUpload.value = true
}

// 头像上传处理 - 当文件被选择并点击上传时
const handleAvatarFileUpload = async (file: File) => {
  try {
    // 调用头像专用API上传
    const response = await fileService.uploadAvatar(file, (percent) => {
      console.log(`上传进度: ${percent}%`)
    })

    // 更新本地用户信息和存储
    if (userInfo.value && response.avatarUrl) {
      userInfo.value.avatarUrl = response.avatarUrl
      authStore.updateUserInfo({ avatarUrl: response.avatarUrl })
    }

    success('头像上传成功')
    showAvatarUpload.value = false
    avatarFile.value = null
  } catch (err) {
    showError('头像上传失败,请稍后重试')
  }
}

// 加载用户统计数据
const loadUserStats = async () => {
  try {
    if (!userInfo.value?.id) {
      console.warn('用户信息不完整,无法加载统计数据')
      return
    }

    const userId = userInfo.value.id

    // 并发调用三个API获取用户统计数据
    const [scenes, workflows, slicingTasks] = await Promise.all([
      sceneService.getUserScenes(userId),
      workflowService.getUserWorkflows(userId),
      slicingService.getUserSlicingTasks(userId, 1, 1000) // 获取所有任务
    ])

    // 更新统计数据
    userStats.scenesCount = Array.isArray(scenes) ? scenes.length : 0
    userStats.workflowsCount = Array.isArray(workflows) ? workflows.length : 0
    userStats.slicingTasksCount = Array.isArray(slicingTasks) ? slicingTasks.length : 0
  } catch (err) {
    console.error('加载统计数据失败:', err)
    // 出错时保持为0
    userStats.scenesCount = 0
    userStats.workflowsCount = 0
    userStats.slicingTasksCount = 0
  }
}

onMounted(() => {
  loadUserStats()
})
</script>

<style scoped>
.profile-page {
  min-height: 100vh;
  padding: 2rem;
  background: linear-gradient(to bottom, #f9fafb 0%, #ffffff 100%);
}

.profile-container {
  max-width: 1200px;
  margin: 0 auto;
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(400px, 1fr));
  gap: 2rem;
}

.profile-card,
.password-card {
  animation: fadeInUp 0.4s ease;
}

.password-card {
  animation-delay: 0.1s;
}

.profile-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.profile-header h2 {
  margin: 0;
  font-size: var(--font-size-xl);
}

.profile-content {
  display: grid;
  grid-template-columns: auto 1fr;
  gap: 2rem;
}

.avatar-section {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1rem;
}

.avatar {
  width: 120px;
  height: 120px;
  border-radius: var(--border-radius-full);
  overflow: hidden;
  box-shadow: var(--shadow-lg);
  border: 4px solid white;
}

.avatar img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.avatar-placeholder {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--gradient-primary-alt);
  color: white;
  font-size: 3rem;
  font-weight: 700;
}

.btn-upload-avatar {
  padding: 0.5rem 1rem;
  background: var(--gray-100);
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius);
  font-size: var(--font-size-sm);
  cursor: pointer;
  transition: all var(--transition-base);
}

.btn-upload-avatar:hover {
  background: var(--primary-light);
  color: var(--primary-color);
  border-color: var(--primary-color);
}

.info-section {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.form-group label {
  font-size: var(--font-size-sm);
  font-weight: 600;
  color: var(--gray-700);
}

.form-input {
  padding: 0.75rem 1rem;
  border: 1.5px solid var(--border-color);
  border-radius: var(--border-radius);
  font-size: var(--font-size-base);
  transition: all var(--transition-base);
  background: var(--gray-50);
}

.form-input:disabled {
  background: var(--gray-100);
  cursor: not-allowed;
}

.form-input:not(:disabled):hover {
  border-color: var(--gray-400);
  background: white;
}

.form-input:focus {
  outline: none;
  border-color: var(--primary-color);
  background: white;
  box-shadow: 0 0 0 3px rgba(99, 102, 241, 0.1);
}

.password-form {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.stats-grid {
  grid-column: 1 / -1;
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
  gap: 1.5rem;
}

.stat-card {
  animation: scaleIn 0.3s ease;
}

.stat-card:nth-child(2) {
  animation-delay: 0.05s;
}

.stat-card:nth-child(3) {
  animation-delay: 0.1s;
}

.stat-content {
  display: flex;
  align-items: center;
  gap: 1.5rem;
  padding: 1rem;
}

.stat-icon {
  width: 64px;
  height: 64px;
  border-radius: var(--border-radius-lg);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 2rem;
}

.gradient-bg-primary {
  background: var(--gradient-primary-alt);
}

.gradient-bg-success {
  background: var(--gradient-success);
}

.gradient-bg-info {
  background: var(--gradient-info);
}

.stat-details {
  flex: 1;
}

.stat-label {
  font-size: var(--font-size-sm);
  color: var(--gray-600);
  margin-bottom: 0.25rem;
}

.stat-value {
  font-size: var(--font-size-3xl);
  font-weight: 700;
  background: var(--gradient-primary-alt);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

.btn {
  padding: 0.6rem 1.4rem;
  border: none;
  border-radius: var(--border-radius);
  font-size: var(--font-size-base);
  font-weight: 600;
  cursor: pointer;
  transition: all var(--transition-base);
}

.btn-edit,
.btn-primary {
  background: var(--gradient-primary-alt);
  color: white;
  box-shadow: var(--shadow-colored);
}

.btn-edit:hover,
.btn-primary:hover {
  box-shadow: var(--shadow-xl);
  transform: translateY(-2px);
}

.btn-secondary {
  background: var(--gray-100);
  color: var(--gray-700);
  border: 1px solid var(--gray-300);
}

.btn-secondary:hover {
  background: var(--gray-200);
  transform: translateY(-2px);
}

/* 响应式设计 */
@media (max-width: 768px) {
  .profile-container {
    grid-template-columns: 1fr;
  }

  .profile-content {
    grid-template-columns: 1fr;
  }

  .avatar-section {
    flex-direction: row;
    justify-content: flex-start;
  }

  .stats-grid {
    grid-template-columns: 1fr;
  }
}
</style>
