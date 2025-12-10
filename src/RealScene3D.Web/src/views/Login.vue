<template>
  <div class="login-page">
    <!-- 左侧装饰区域 -->
    <div class="login-left">
      <div class="decoration-content">
        <div class="logo-section">
          <div class="logo-circle">
            <svg viewBox="0 0 100 100" class="logo-svg">
              <defs>
                <linearGradient id="logoGradient" x1="0%" y1="0%" x2="100%" y2="100%">
                  <stop offset="0%" style="stop-color:#667eea;stop-opacity:1" />
                  <stop offset="100%" style="stop-color:#764ba2;stop-opacity:1" />
                </linearGradient>
              </defs>
              <path d="M 30 50 L 50 30 L 70 50 L 50 70 Z M 50 20 L 50 40 M 50 60 L 50 80 M 20 50 L 40 50 M 60 50 L 80 50"
                    stroke="url(#logoGradient)"
                    stroke-width="4"
                    fill="none"
                    stroke-linecap="round"/>
            </svg>
          </div>
          <h1 class="brand-title">实景三维管理系统</h1>
          <p class="brand-subtitle">Real Scene 3D Management Platform</p>
        </div>

        <div class="features">
          <div class="feature-item" v-for="(feature, index) in features" :key="index">
            <div class="feature-icon">{{ feature.icon }}</div>
            <div class="feature-text">
              <h3>{{ feature.title }}</h3>
              <p>{{ feature.desc }}</p>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 右侧登录表单 -->
    <div class="login-right">
      <div class="form-container">
        <!-- 欢迎标题 -->
        <div class="form-header">
          <h2>{{ isLogin ? '欢迎回来' : '创建新账户' }}</h2>
          <p>{{ isLogin ? '登录以继续访问您的账户' : '填写信息以创建新账户' }}</p>
        </div>

        <!-- 标签切换 -->
        <div class="auth-tabs">
          <button
            @click="isLogin = true"
            :class="['auth-tab', { active: isLogin }]"
          >
            登录
          </button>
          <button
            @click="isLogin = false"
            :class="['auth-tab', { active: !isLogin }]"
          >
            注册
          </button>
        </div>

        <!-- 错误提示 -->
        <transition name="slide-fade">
          <div v-if="errorMessage" class="alert alert-error">
            <svg class="alert-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor">
              <circle cx="12" cy="12" r="10" stroke-width="2"/>
              <line x1="12" y1="8" x2="12" y2="12" stroke-width="2" stroke-linecap="round"/>
              <circle cx="12" cy="16" r="1" fill="currentColor"/>
            </svg>
            {{ errorMessage }}
          </div>
        </transition>

        <!-- 登录表单 -->
        <form v-if="isLogin" @submit.prevent="handleLogin" class="auth-form">
          <div class="input-group">
            <label class="input-label">邮箱地址</label>
            <div class="input-wrapper">
              <svg class="input-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor">
                <path d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" stroke-width="2" stroke-linecap="round"/>
              </svg>
              <input
                v-model="loginForm.email"
                type="email"
                class="form-input"
                placeholder="your@email.com"
                required
              />
            </div>
          </div>

          <div class="input-group">
            <label class="input-label">密码</label>
            <div class="input-wrapper">
              <svg class="input-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor">
                <rect x="5" y="11" width="14" height="10" rx="2" stroke-width="2"/>
                <path d="M12 15v2M7 11V7a5 5 0 0110 0v4" stroke-width="2" stroke-linecap="round"/>
              </svg>
              <input
                v-model="loginForm.password"
                type="password"
                class="form-input"
                placeholder="••••••••"
                required
              />
            </div>
          </div>

          <div class="form-options">
            <label class="checkbox-wrapper">
              <input v-model="loginForm.rememberMe" type="checkbox" />
              <span class="checkbox-label">记住我</span>
            </label>
            <a href="#" class="text-link">忘记密码？</a>
          </div>

          <button
            type="submit"
            class="btn btn-primary btn-full"
            :disabled="authLoading"
          >
            <span v-if="!authLoading">登录</span>
            <span v-else class="loading-text">
              <svg class="spinner" viewBox="0 0 24 24">
                <circle cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" fill="none" opacity="0.25"/>
                <path d="M12 2a10 10 0 0110 10" stroke="currentColor" stroke-width="4" fill="none" stroke-linecap="round"/>
              </svg>
              登录中...
            </span>
          </button>
        </form>

        <!-- 注册表单 -->
        <form v-else @submit.prevent="handleRegister" class="auth-form">
          <div class="input-group">
            <label class="input-label">用户名</label>
            <div class="input-wrapper">
              <svg class="input-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor">
                <path d="M20 21v-2a4 4 0 00-4-4H8a4 4 0 00-4 4v2M12 11a4 4 0 100-8 4 4 0 000 8z" stroke-width="2" stroke-linecap="round"/>
              </svg>
              <input
                v-model="registerForm.username"
                type="text"
                class="form-input"
                placeholder="您的用户名"
                required
              />
            </div>
          </div>

          <div class="input-group">
            <label class="input-label">邮箱地址</label>
            <div class="input-wrapper">
              <svg class="input-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor">
                <path d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" stroke-width="2" stroke-linecap="round"/>
              </svg>
              <input
                v-model="registerForm.email"
                type="email"
                class="form-input"
                placeholder="your@email.com"
                required
              />
            </div>
          </div>

          <div class="input-group">
            <label class="input-label">密码</label>
            <div class="input-wrapper">
              <svg class="input-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor">
                <rect x="5" y="11" width="14" height="10" rx="2" stroke-width="2"/>
                <path d="M12 15v2M7 11V7a5 5 0 0110 0v4" stroke-width="2" stroke-linecap="round"/>
              </svg>
              <input
                v-model="registerForm.password"
                type="password"
                class="form-input"
                placeholder="至少6位字符"
                required
                minlength="6"
              />
            </div>
          </div>

          <div class="input-group">
            <label class="input-label">确认密码</label>
            <div class="input-wrapper">
              <svg class="input-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor">
                <rect x="5" y="11" width="14" height="10" rx="2" stroke-width="2"/>
                <path d="M12 15v2" stroke-width="2" stroke-linecap="round"/>
                <circle cx="12" cy="7" r="2" stroke-width="2"/>
              </svg>
              <input
                v-model="registerForm.confirmPassword"
                type="password"
                class="form-input"
                placeholder="再次输入密码"
                required
              />
            </div>
          </div>

          <div class="checkbox-group">
            <label class="checkbox-wrapper">
              <input v-model="registerForm.agreeTerms" type="checkbox" required />
              <span class="checkbox-label">
                我同意 <a href="#" class="text-link">服务条款</a> 和 <a href="#" class="text-link">隐私政策</a>
              </span>
            </label>
          </div>

          <button
            type="submit"
            class="btn btn-primary btn-full"
            :disabled="authLoading"
          >
            <span v-if="!authLoading">创建账户</span>
            <span v-else class="loading-text">
              <svg class="spinner" viewBox="0 0 24 24">
                <circle cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" fill="none" opacity="0.25"/>
                <path d="M12 2a10 10 0 0110 10" stroke="currentColor" stroke-width="4" fill="none" stroke-linecap="round"/>
              </svg>
              注册中...
            </span>
          </button>
        </form>

        <!-- 底部提示 -->
        <div class="form-footer">
          <p v-if="isLogin">
            还没有账户？ <a @click.prevent="isLogin = false" class="text-link-bold">立即注册</a>
          </p>
          <p v-else>
            已有账户？ <a @click.prevent="isLogin = true" class="text-link-bold">立即登录</a>
          </p>
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

// 功能特性列表
const features = [
  {
    icon: '🌍',
    title: '三维场景管理',
    desc: '支持多种3D模型格式，智能场景组织'
  },
  {
    icon: '⚡',
    title: '高性能渲染',
    desc: 'Cesium与Three.js双引擎支持'
  },
  {
    icon: '🔒',
    title: '安全可靠',
    desc: '企业级数据安全保障'
  }
]

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
    await nextTick()
    const redirect = route.query.redirect as string
    await router.push(redirect || '/')
  } else {
    errorMessage.value = authStore.error.value || '登录失败，请检查邮箱和密码'
  }
}

// 处理注册
const handleRegister = async () => {
  errorMessage.value = ''

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
    await nextTick()
    const redirect = route.query.redirect as string
    await router.push(redirect || '/')
  } else {
    errorMessage.value = authStore.error.value || '注册失败，请稍后重试'
  }
}
</script>

<style scoped>
.login-page {
  display: flex;
  min-height: 100vh;
  background: linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%);
}

/* 左侧装饰区域 */
.login-left {
  flex: 1;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  padding: 4rem;
  display: flex;
  align-items: center;
  justify-content: center;
  position: relative;
  overflow: hidden;
}

.login-left::before {
  content: '';
  position: absolute;
  top: -50%;
  right: -50%;
  width: 100%;
  height: 100%;
  background: radial-gradient(circle, rgba(255,255,255,0.1) 0%, transparent 70%);
  animation: float 20s ease-in-out infinite;
}

@keyframes float {
  0%, 100% { transform: translate(0, 0) rotate(0deg); }
  50% { transform: translate(-20px, 20px) rotate(5deg); }
}

.decoration-content {
  position: relative;
  z-index: 1;
  color: white;
  max-width: 500px;
}

.logo-section {
  text-align: center;
  margin-bottom: 4rem;
}

.logo-circle {
  width: 120px;
  height: 120px;
  margin: 0 auto 2rem;
  background: rgba(255, 255, 255, 0.15);
  backdrop-filter: blur(10px);
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.1);
  transition: transform 0.3s ease;
}

.logo-circle:hover {
  transform: scale(1.05) rotate(5deg);
}

.logo-svg {
  width: 60px;
  height: 60px;
}

.brand-title {
  font-size: 2.5rem;
  font-weight: 700;
  margin: 0 0 0.5rem 0;
  text-shadow: 0 2px 10px rgba(0, 0, 0, 0.2);
}

.brand-subtitle {
  font-size: 1rem;
  opacity: 0.9;
  margin: 0;
  letter-spacing: 1px;
}

.features {
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

.feature-item {
  display: flex;
  gap: 1.5rem;
  align-items: flex-start;
  background: rgba(255, 255, 255, 0.1);
  padding: 1.5rem;
  border-radius: 16px;
  backdrop-filter: blur(10px);
  transition: all 0.3s ease;
}

.feature-item:hover {
  background: rgba(255, 255, 255, 0.15);
  transform: translateX(10px);
}

.feature-icon {
  font-size: 2.5rem;
  flex-shrink: 0;
}

.feature-text h3 {
  margin: 0 0 0.5rem 0;
  font-size: 1.25rem;
  font-weight: 600;
}

.feature-text p {
  margin: 0;
  opacity: 0.9;
  line-height: 1.6;
}

/* 右侧表单区域 */
.login-right {
  flex: 0 0 480px;
  background: white;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 2rem;
  box-shadow: -5px 0 20px rgba(0, 0, 0, 0.05);
}

.form-container {
  width: 100%;
  max-width: 400px;
}

.form-header {
  margin-bottom: 2rem;
}

.form-header h2 {
  font-size: 1.875rem;
  font-weight: 700;
  color: #1a202c;
  margin: 0 0 0.5rem 0;
}

.form-header p {
  color: #718096;
  margin: 0;
  font-size: 0.9375rem;
}

/* 标签切换 */
.auth-tabs {
  display: flex;
  gap: 1rem;
  margin-bottom: 2rem;
  background: #f7fafc;
  padding: 0.25rem;
  border-radius: 12px;
}

.auth-tab {
  flex: 1;
  padding: 0.75rem;
  border: none;
  background: transparent;
  border-radius: 10px;
  font-size: 0.9375rem;
  font-weight: 600;
  color: #718096;
  cursor: pointer;
  transition: all 0.3s ease;
}

.auth-tab.active {
  background: white;
  color: #667eea;
  box-shadow: 0 2px 8px rgba(102, 126, 234, 0.15);
}

/* 警告提示 */
.alert {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 1rem;
  border-radius: 12px;
  margin-bottom: 1.5rem;
  font-size: 0.875rem;
}

.alert-error {
  background: #fff5f5;
  color: #c53030;
  border: 1px solid #feb2b2;
}

.alert-icon {
  width: 20px;
  height: 20px;
  flex-shrink: 0;
}

/* 表单 */
.auth-form {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.input-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.input-label {
  font-size: 0.875rem;
  font-weight: 600;
  color: #2d3748;
}

.input-wrapper {
  position: relative;
  display: flex;
  align-items: center;
}

.input-icon {
  position: absolute;
  left: 1rem;
  width: 20px;
  height: 20px;
  color: #a0aec0;
  pointer-events: none;
}

.form-input {
  width: 100%;
  padding: 0.875rem 1rem 0.875rem 3rem;
  border: 2px solid #e2e8f0;
  border-radius: 12px;
  font-size: 0.9375rem;
  transition: all 0.3s ease;
  background: #f7fafc;
}

.form-input:focus {
  outline: none;
  border-color: #667eea;
  background: white;
  box-shadow: 0 0 0 4px rgba(102, 126, 234, 0.1);
}

.form-input::placeholder {
  color: #cbd5e0;
}

/* 表单选项 */
.form-options {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: -0.5rem;
}

.checkbox-wrapper {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  cursor: pointer;
}

.checkbox-wrapper input[type="checkbox"] {
  width: 18px;
  height: 18px;
  border-radius: 4px;
  border: 2px solid #e2e8f0;
  cursor: pointer;
  accent-color: #667eea;
}

.checkbox-label {
  font-size: 0.875rem;
  color: #4a5568;
  user-select: none;
}

.checkbox-group {
  margin-top: -0.5rem;
}

.text-link {
  font-size: 0.875rem;
  color: #667eea;
  text-decoration: none;
  transition: color 0.2s ease;
}

.text-link:hover {
  color: #764ba2;
}

.text-link-bold {
  font-weight: 600;
  color: #667eea;
  text-decoration: none;
  cursor: pointer;
  transition: color 0.2s ease;
}

.text-link-bold:hover {
  color: #764ba2;
  text-decoration: underline;
}

/* 按钮 */
.btn {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  padding: 1rem;
  border: none;
  border-radius: 12px;
  font-size: 1rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.3s ease;
}

.btn-primary {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  box-shadow: 0 4px 15px rgba(102, 126, 234, 0.4);
}

.btn-primary:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 6px 20px rgba(102, 126, 234, 0.5);
}

.btn-primary:active:not(:disabled) {
  transform: translateY(0);
}

.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
  transform: none;
}

.btn-full {
  width: 100%;
}

.loading-text {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.spinner {
  width: 20px;
  height: 20px;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

/* 表单底部 */
.form-footer {
  margin-top: 2rem;
  text-align: center;
  font-size: 0.875rem;
  color: #718096;
}

.form-footer p {
  margin: 0;
}

/* 过渡动画 */
.slide-fade-enter-active,
.slide-fade-leave-active {
  transition: all 0.3s ease;
}

.slide-fade-enter-from {
  transform: translateY(-10px);
  opacity: 0;
}

.slide-fade-leave-to {
  transform: translateY(-10px);
  opacity: 0;
}

/* 响应式设计 */
@media (max-width: 1024px) {
  .login-left {
    display: none;
  }

  .login-right {
    flex: 1;
  }
}

@media (max-width: 640px) {
  .login-right {
    padding: 1.5rem;
  }

  .form-header h2 {
    font-size: 1.5rem;
  }

  .brand-title {
    font-size: 2rem;
  }

  .features {
    gap: 1.5rem;
  }

  .feature-item {
    padding: 1rem;
  }
}

@media (max-width: 480px) {
  .login-right {
    padding: 1rem;
  }

  .form-container {
    max-width: 100%;
  }
}
</style>
