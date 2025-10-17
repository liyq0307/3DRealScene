<template>
  <div class="auth-page">
    <div class="auth-container">
      <!-- Logo和标题 -->
      <div class="auth-header">
        <h1>实景三维管理系统</h1>
        <p class="subtitle">{{ isLogin ? '登录到您的账户' : '创建新账户' }}</p>
      </div>

      <!-- 登录/注册表单 -->
      <div class="auth-form">
        <!-- 选项卡 -->
        <div class="tabs">
          <button
            @click="isLogin = true"
            :class="['tab', { active: isLogin }]"
          >
            登录
          </button>
          <button
            @click="isLogin = false"
            :class="['tab', { active: !isLogin }]"
          >
            注册
          </button>
        </div>

        <!-- 错误提示 -->
        <div v-if="errorMessage" class="error-message">
          <span class="icon">⚠</span>
          {{ errorMessage }}
        </div>

        <!-- 登录表单 -->
        <form v-if="isLogin" @submit.prevent="handleLogin" class="form">
          <div class="form-group">
            <label>邮箱</label>
            <input
              v-model="loginForm.email"
              type="email"
              class="form-input"
              placeholder="请输入邮箱"
              required
            />
          </div>

          <div class="form-group">
            <label>密码</label>
            <input
              v-model="loginForm.password"
              type="password"
              class="form-input"
              placeholder="请输入密码"
              required
            />
          </div>

          <div class="form-group checkbox-group">
            <label class="checkbox-label">
              <input
                v-model="loginForm.rememberMe"
                type="checkbox"
              />
              <span>记住我</span>
            </label>
            <a href="#" class="link">忘记密码?</a>
          </div>

          <button
            type="submit"
            class="btn btn-primary btn-block"
            :disabled="authLoading"
          >
            {{ authLoading ? '登录中...' : '登录' }}
          </button>
        </form>

        <!-- 注册表单 -->
        <form v-else @submit.prevent="handleRegister" class="form">
          <div class="form-group">
            <label>用户名</label>
            <input
              v-model="registerForm.username"
              type="text"
              class="form-input"
              placeholder="请输入用户名"
              required
            />
          </div>

          <div class="form-group">
            <label>邮箱</label>
            <input
              v-model="registerForm.email"
              type="email"
              class="form-input"
              placeholder="请输入邮箱"
              required
            />
          </div>

          <div class="form-group">
            <label>密码</label>
            <input
              v-model="registerForm.password"
              type="password"
              class="form-input"
              placeholder="请输入密码（至少6位）"
              required
              minlength="6"
            />
          </div>

          <div class="form-group">
            <label>确认密码</label>
            <input
              v-model="registerForm.confirmPassword"
              type="password"
              class="form-input"
              placeholder="请再次输入密码"
              required
            />
          </div>

          <div class="form-group checkbox-group">
            <label class="checkbox-label">
              <input
                v-model="registerForm.agreeTerms"
                type="checkbox"
                required
              />
              <span>我同意<a href="#" class="link">服务条款</a>和<a href="#" class="link">隐私政策</a></span>
            </label>
          </div>

          <button
            type="submit"
            class="btn btn-primary btn-block"
            :disabled="authLoading"
          >
            {{ authLoading ? '注册中...' : '注册' }}
          </button>
        </form>

        <!-- 社交登录 -->
        <div class="social-login">
          <div class="divider">
            <span>或者使用</span>
          </div>
          <div class="social-buttons">
            <button class="social-btn github">
              <span class="icon">🐙</span>
              GitHub
            </button>
            <button class="social-btn google">
              <span class="icon">🔍</span>
              Google
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, nextTick } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import authStore from '@/stores/auth'

const router = useRouter()
const route = useRoute()

// 状态
const isLogin = ref(true)
const errorMessage = ref('')

// 登录表单
const loginForm = ref({
  email: '',
  password: '',
  rememberMe: false
})

// 注册表单
const registerForm = ref({
  username: '',
  email: '',
  password: '',
  confirmPassword: '',
  agreeTerms: false
})

// 从store获取状态 - authStore.loading已经是ref,直接使用
const authLoading = authStore.loading

// 处理登录
const handleLogin = async () => {
  errorMessage.value = ''

  if (!loginForm.value.email || !loginForm.value.password) {
    errorMessage.value = '请填写完整的登录信息'
    return
  }

  const success = await authStore.login(
    loginForm.value.email,
    loginForm.value.password,
    loginForm.value.rememberMe
  )

  if (success) {
    // 登录成功，等待DOM更新后跳转
    await nextTick()
    const redirect = route.query.redirect as string
    await router.push(redirect || '/')
  } else {
    errorMessage.value = authStore.error.value || '登录失败'
  }
}

// 处理注册
const handleRegister = async () => {
  errorMessage.value = ''

  // 验证表单
  if (!registerForm.value.username || !registerForm.value.email || !registerForm.value.password) {
    errorMessage.value = '请填写完整的注册信息'
    return
  }

  if (registerForm.value.password.length < 6) {
    errorMessage.value = '密码长度至少为6位'
    return
  }

  if (registerForm.value.password !== registerForm.value.confirmPassword) {
    errorMessage.value = '两次输入的密码不一致'
    return
  }

  if (!registerForm.value.agreeTerms) {
    errorMessage.value = '请同意服务条款和隐私政策'
    return
  }

  const success = await authStore.register(
    registerForm.value.username,
    registerForm.value.email,
    registerForm.value.password
  )

  if (success) {
    // 注册成功，等待DOM更新后跳转
    await nextTick()
    const redirect = route.query.redirect as string
    await router.push(redirect || '/')
  } else {
    errorMessage.value = authStore.error.value || '注册失败'
  }
}
</script>

<style scoped>
.auth-page {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  padding: 1rem;
  overflow-y: auto;
}

.auth-container {
  width: 100%;
  max-width: 450px;
  background: white;
  border-radius: 12px;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.2);
  overflow: hidden;
  margin: 1rem auto;
  display: flex;
  flex-direction: column;
  max-height: calc(100% - 2rem);
}

.auth-header {
  padding: 1.5rem 2rem 1.25rem;
  text-align: center;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  flex-shrink: 0;
}

.auth-header h1 {
  margin: 0 0 0.5rem 0;
  font-size: 1.4rem;
  line-height: 1.3;
}

.subtitle {
  margin: 0;
  font-size: 0.85rem;
  opacity: 0.9;
}

.auth-form {
  padding: 1.5rem 2rem 2rem;
  overflow-y: auto;
  overflow-x: hidden;
  flex: 1 1 auto;
  min-height: 0;
  /* 确保有明确的滚动条 */
  scrollbar-width: thin;
  scrollbar-color: #667eea #e1e5e9;
}

/* Webkit浏览器滚动条样式 */
.auth-form::-webkit-scrollbar {
  width: 10px;
}

.auth-form::-webkit-scrollbar-track {
  background: #f1f1f1;
  border-radius: 4px;
}

.auth-form::-webkit-scrollbar-thumb {
  background: #667eea;
  border-radius: 4px;
  transition: background 0.2s ease;
}

.auth-form::-webkit-scrollbar-thumb:hover {
  background: #764ba2;
}

.tabs {
  display: flex;
  margin-bottom: 1.5rem;
  border-bottom: 2px solid #e1e5e9;
  flex-shrink: 0;
}

.tab {
  flex: 1;
  padding: 0.75rem;
  border: none;
  background: none;
  cursor: pointer;
  font-size: 1rem;
  font-weight: 500;
  color: #666;
  transition: all 0.2s ease;
  position: relative;
}

.tab.active {
  color: #667eea;
}

.tab.active::after {
  content: '';
  position: absolute;
  bottom: -2px;
  left: 0;
  right: 0;
  height: 2px;
  background: #667eea;
}

.error-message {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.65rem 0.875rem;
  margin-bottom: 1rem;
  background: #ffebee;
  color: #c62828;
  border-radius: 6px;
  font-size: 0.85rem;
}

.form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}

.form-group label {
  font-weight: 500;
  color: #333;
  font-size: 0.85rem;
}

.form-input {
  padding: 0.65rem 0.75rem;
  border: 1px solid #e1e5e9;
  border-radius: 6px;
  font-size: 0.9rem;
  transition: all 0.2s ease;
}

.form-input:focus {
  outline: none;
  border-color: #667eea;
  box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.1);
}

.checkbox-group {
  flex-direction: row;
  align-items: center;
  justify-content: space-between;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  cursor: pointer;
  font-size: 0.9rem;
  color: #666;
}

.checkbox-label input[type="checkbox"] {
  width: auto;
  cursor: pointer;
}

.link {
  color: #667eea;
  text-decoration: none;
  font-size: 0.9rem;
  transition: color 0.2s ease;
}

.link:hover {
  color: #764ba2;
  text-decoration: underline;
}

.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  padding: 0.75rem 1.5rem;
  border: 1px solid #e1e5e9;
  border-radius: 6px;
  background: white;
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 0.95rem;
  font-weight: 500;
}

.btn-primary {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  border: none;
}

.btn-primary:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4);
}

.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
  transform: none;
}

.btn-block {
  width: 100%;
}

.social-login {
  margin-top: 1.5rem;
}

.divider {
  position: relative;
  text-align: center;
  margin: 1rem 0;
}

.divider::before {
  content: '';
  position: absolute;
  top: 50%;
  left: 0;
  right: 0;
  height: 1px;
  background: #e1e5e9;
}

.divider span {
  position: relative;
  background: white;
  padding: 0 1rem;
  color: #999;
  font-size: 0.85rem;
}

.social-buttons {
  display: flex;
  gap: 1rem;
}

.social-btn {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  padding: 0.75rem;
  border: 1px solid #e1e5e9;
  border-radius: 6px;
  background: white;
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 0.9rem;
  font-weight: 500;
}

.social-btn:hover {
  background: #f8f9fa;
  border-color: #667eea;
}

.social-btn.github {
  color: #333;
}

.social-btn.google {
  color: #4285f4;
}

.icon {
  font-size: 1.1em;
}

/* 响应式优化 */
@media (max-height: 700px) {
  .auth-page {
    padding: 0.5rem;
    align-items: flex-start;
    padding-top: 0.5rem;
    padding-bottom: 0.5rem;
  }

  .auth-container {
    margin: 0.5rem auto;
    max-height: calc(100% - 1rem);
  }

  .auth-header {
    padding: 1rem 1.5rem 0.875rem;
  }

  .auth-header h1 {
    font-size: 1.25rem;
  }

  .subtitle {
    font-size: 0.8rem;
  }

  .auth-form {
    padding: 1rem 1.5rem 1.5rem;
  }

  .tabs {
    margin-bottom: 1rem;
  }

  .form {
    gap: 0.75rem;
  }

  .form-group {
    gap: 0.35rem;
  }

  .social-login {
    margin-top: 1rem;
  }

  .divider {
    margin: 0.75rem 0;
  }
}

@media (max-height: 600px) {
  .auth-header {
    padding: 0.75rem 1.5rem 0.625rem;
  }

  .auth-header h1 {
    font-size: 1.15rem;
  }

  .subtitle {
    font-size: 0.75rem;
  }

  .auth-form {
    padding: 0.75rem 1.5rem 1rem;
  }

  .tabs {
    margin-bottom: 0.75rem;
  }

  .form {
    gap: 0.625rem;
  }

  .form-group label {
    font-size: 0.8rem;
  }

  .form-input {
    padding: 0.5rem 0.65rem;
    font-size: 0.85rem;
  }

  .btn {
    padding: 0.625rem 1.25rem;
    font-size: 0.875rem;
  }

  .social-login {
    margin-top: 0.75rem;
  }

  .divider {
    margin: 0.5rem 0;
  }

  .social-btn {
    padding: 0.625rem;
    font-size: 0.85rem;
  }
}

@media (max-width: 480px) {
  .auth-page {
    padding: 0.5rem;
  }

  .auth-container {
    max-width: 100%;
    border-radius: 8px;
    margin: 0.5rem auto;
  }

  .auth-header {
    padding: 1.25rem 1.5rem 1rem;
  }

  .auth-header h1 {
    font-size: 1.3rem;
  }

  .auth-form {
    padding: 1.25rem 1.5rem 1.5rem;
  }

  .auth-form::-webkit-scrollbar {
    width: 8px;
  }

  .checkbox-group {
    flex-direction: column;
    align-items: flex-start;
    gap: 0.5rem;
  }

  .social-buttons {
    flex-direction: column;
  }
}

@media (max-width: 360px) {
  .auth-header h1 {
    font-size: 1.15rem;
  }

  .auth-form {
    padding: 1rem;
  }

  .form-input {
    padding: 0.6rem;
    font-size: 0.875rem;
  }
}

/* 超低高度屏幕（如横屏手机） */
@media (max-height: 500px) {
  .auth-page {
    align-items: flex-start;
    padding: 0.25rem;
  }

  .auth-container {
    margin: 0.25rem auto;
    max-height: calc(100% - 0.5rem);
  }

  .auth-header {
    padding: 0.5rem 1rem 0.5rem;
  }

  .auth-header h1 {
    font-size: 1rem;
  }

  .subtitle {
    display: none;
  }

  .auth-form {
    padding: 0.5rem 1rem 0.75rem;
  }

  .tabs {
    margin-bottom: 0.5rem;
  }

  .tab {
    padding: 0.5rem;
    font-size: 0.875rem;
  }

  .form {
    gap: 0.5rem;
  }

  .form-group {
    gap: 0.25rem;
  }

  .form-group label {
    font-size: 0.75rem;
  }

  .form-input {
    padding: 0.4rem 0.5rem;
    font-size: 0.8rem;
  }

  .error-message {
    padding: 0.5rem;
    font-size: 0.75rem;
    margin-bottom: 0.5rem;
  }

  .btn {
    padding: 0.5rem 1rem;
    font-size: 0.8rem;
  }

  .social-login {
    margin-top: 0.5rem;
  }

  .divider {
    margin: 0.375rem 0;
  }

  .divider span {
    font-size: 0.75rem;
    padding: 0 0.5rem;
  }

  .social-btn {
    padding: 0.5rem;
    font-size: 0.8rem;
  }

  .checkbox-label {
    font-size: 0.8rem;
  }

  .link {
    font-size: 0.8rem;
  }
}

/* 触摸设备优化 - 确保滚动流畅 */
@media (hover: none) and (pointer: coarse) {
  .auth-form {
    -webkit-overflow-scrolling: touch;
  }

  .auth-form::-webkit-scrollbar {
    width: 6px;
  }
}
</style>
