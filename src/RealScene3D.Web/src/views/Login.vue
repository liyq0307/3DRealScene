<template>
  <div class="login-page">
    <!-- 左侧装饰区域 -->
    <div class="login-left">
      <div class="decoration-content">
        <div class="logo-section">
          <h1 class="brand-title">实景三维</h1>
          <p class="brand-subtitle">Real Scene 3D Platform</p>
        </div>

        <!-- 3D科技感地球效果 -->
        <div ref="buildingCanvas" class="building-canvas"></div>
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
          <div v-if="errorMessage" class="alert alert-error" id="email-error">
            <svg class="alert-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor">
              <circle cx="12" cy="12" r="10" stroke-width="2"/>
              <line x1="12" y1="8" x2="12" y2="12" stroke-width="2" stroke-linecap="round"/>
              <circle cx="12" cy="16" r="1" fill="currentColor"/>
            </svg>
            {{ errorMessage }}
          </div>
        </transition>

        <!-- 实时验证提示 -->
        <transition name="slide-fade">
          <div v-if="!isLogin && registerForm.email && !isValidRegisterEmail" class="alert alert-warning" id="register-email-error">
            <svg class="alert-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor">
              <path d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L3.732 16.5c-.77.833.192 2.5 1.732 2.5z" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
            请输入有效的邮箱地址
          </div>
        </transition>

        <transition name="slide-fade">
          <div v-if="!isLogin && registerForm.password && !isValidPassword" class="alert alert-warning" id="password-error">
            <svg class="alert-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor">
              <path d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L3.732 16.5c-.77.833.192 2.5 1.732 2.5z" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
            密码长度至少为6位
          </div>
        </transition>

        <transition name="slide-fade">
          <div v-if="!isLogin && registerForm.confirmPassword && !passwordsMatch" class="alert alert-warning" id="confirm-password-error">
            <svg class="alert-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor">
              <path d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L3.732 16.5c-.77.833.192 2.5 1.732 2.5z" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
            两次输入的密码不一致
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
                :class="{ 'input-error': loginForm.email && !isValidEmail }"
                placeholder="your@email.com"
                required
                aria-label="邮箱地址"
                :aria-invalid="(loginForm.email && !isValidEmail) ? 'true' : undefined"
                :aria-describedby="loginForm.email && !isValidEmail ? 'email-error' : undefined"
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
                :type="showLoginPassword ? 'text' : 'password'"
                class="form-input"
                placeholder="••••••••"
                required
                aria-label="密码"
              />
              <button
                type="button"
                class="password-toggle"
                @click="showLoginPassword = !showLoginPassword"
                :aria-label="showLoginPassword ? '隐藏密码' : '显示密码'"
              >
                <svg v-if="showLoginPassword" viewBox="0 0 24 24" fill="none" stroke="currentColor">
                  <path d="M17.94 17.94A10.07 10.07 0 0112 20c-7 0-11-8-11-8a18.45 18.45 0 015.06-5.94M9.9 4.24A9.12 9.12 0 0112 4c7 0 11 8 11 8a18.5 18.5 0 01-2.16 3.19m-6.72-1.07a3 3 0 11-4.24-4.24M1 1l22 22" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
                </svg>
                <svg v-else viewBox="0 0 24 24" fill="none" stroke="currentColor">
                  <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
                  <circle cx="12" cy="12" r="3" stroke-width="2"/>
                </svg>
              </button>
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
                aria-label="用户名"
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
                :class="{ 'input-error': registerForm.email && !isValidRegisterEmail }"
                placeholder="your@email.com"
                required
                aria-label="邮箱地址"
                :aria-invalid="(registerForm.email && !isValidRegisterEmail) ? 'true' : undefined"
                :aria-describedby="registerForm.email && !isValidRegisterEmail ? 'register-email-error' : undefined"
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
                :type="showRegisterPassword ? 'text' : 'password'"
                class="form-input"
                :class="{ 'input-error': registerForm.password && !isValidPassword }"
                placeholder="至少6位字符"
                required
                minlength="6"
                aria-label="密码"
                :aria-invalid="(registerForm.password && !isValidPassword) ? 'true' : undefined"
                :aria-describedby="registerForm.password && !isValidPassword ? 'password-error' : undefined"
              />
              <button
                type="button"
                class="password-toggle"
                @click="showRegisterPassword = !showRegisterPassword"
                :aria-label="showRegisterPassword ? '隐藏密码' : '显示密码'"
              >
                <svg v-if="showRegisterPassword" viewBox="0 0 24 24" fill="none" stroke="currentColor">
                  <path d="M17.94 17.94A10.07 10.07 0 0112 20c-7 0-11-8-11-8a18.45 18.45 0 015.06-5.94M9.9 4.24A9.12 9.12 0 0112 4c7 0 11 8 11 8a18.5 18.5 0 01-2.16 3.19m-6.72-1.07a3 3 0 11-4.24-4.24M1 1l22 22" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
                </svg>
                <svg v-else viewBox="0 0 24 24" fill="none" stroke="currentColor">
                  <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
                  <circle cx="12" cy="12" r="3" stroke-width="2"/>
                </svg>
              </button>
            </div>
            <!-- 密码强度指示器 -->
            <div v-if="registerForm.password" class="password-strength">
              <div class="strength-bar">
                <div
                  class="strength-fill"
                  :class="passwordStrengthClass"
                  :style="{ width: passwordStrengthWidth }"
                ></div>
              </div>
              <span class="strength-text">{{ passwordStrengthText }}</span>
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
                :type="showConfirmPassword ? 'text' : 'password'"
                class="form-input"
                :class="{ 'input-error': registerForm.confirmPassword && !passwordsMatch }"
                placeholder="再次输入密码"
                required
                aria-label="确认密码"
                :aria-invalid="(registerForm.confirmPassword && !passwordsMatch) ? 'true' : undefined"
                :aria-describedby="registerForm.confirmPassword && !passwordsMatch ? 'confirm-password-error' : undefined"
              />
              <button
                type="button"
                class="password-toggle"
                @click="showConfirmPassword = !showConfirmPassword"
                :aria-label="showConfirmPassword ? '隐藏密码' : '显示密码'"
              >
                <svg v-if="showConfirmPassword" viewBox="0 0 24 24" fill="none" stroke="currentColor">
                  <path d="M17.94 17.94A10.07 10.07 0 0112 20c-7 0-11-8-11-8a18.45 18.45 0 015.06-5.94M9.9 4.24A9.12 9.12 0 0112 4c7 0 11 8 11 8a18.5 18.5 0 01-2.16 3.19m-6.72-1.07a3 3 0 11-4.24-4.24M1 1l22 22" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
                </svg>
                <svg v-else viewBox="0 0 24 24" fill="none" stroke="currentColor">
                  <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
                  <circle cx="12" cy="12" r="3" stroke-width="2"/>
                </svg>
              </button>
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
import { ref, nextTick, computed, onMounted, onUnmounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import authStore from '@/stores/auth'
import * as THREE from 'three'

const router = useRouter()
const route = useRoute()

// Three.js相关
const buildingCanvas = ref<HTMLDivElement | null>(null)
let scene: THREE.Scene | null = null
let camera: THREE.PerspectiveCamera | null = null
let renderer: THREE.WebGLRenderer | null = null
let animationFrameId: number | null = null
let geometryGroup: THREE.Group | null = null
let particles: THREE.Points | null = null

// 状态
const isLogin = ref(true)
const errorMessage = ref('')
const showLoginPassword = ref(false)
const showRegisterPassword = ref(false)
const showConfirmPassword = ref(false)

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

// 密码强度计算
const passwordStrength = computed(() => {
  const password = registerForm.value.password
  if (!password) return 0

  let strength = 0
  if (password.length >= 6) strength += 1
  if (password.length >= 8) strength += 1
  if (/[A-Z]/.test(password)) strength += 1
  if (/[a-z]/.test(password)) strength += 1
  if (/[0-9]/.test(password)) strength += 1
  if (/[^A-Za-z0-9]/.test(password)) strength += 1

  return Math.min(strength, 5)
})

const passwordStrengthWidth = computed(() => `${(passwordStrength.value / 5) * 100}%`)

const passwordStrengthClass = computed(() => {
  const strength = passwordStrength.value
  if (strength <= 1) return 'weak'
  if (strength <= 3) return 'medium'
  return 'strong'
})

const passwordStrengthText = computed(() => {
  const strength = passwordStrength.value
  if (strength <= 1) return '弱'
  if (strength <= 3) return '中等'
  return '强'
})

// 表单验证
const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

const isValidEmail = computed(() => {
  return emailRegex.test(loginForm.value.email) || loginForm.value.email === ''
})

const isValidRegisterEmail = computed(() => {
  return emailRegex.test(registerForm.value.email) || registerForm.value.email === ''
})

const passwordsMatch = computed(() => {
  return registerForm.value.password === registerForm.value.confirmPassword ||
         registerForm.value.confirmPassword === ''
})

const isValidPassword = computed(() => {
  return registerForm.value.password.length >= 6 || registerForm.value.password === ''
})

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

// 生命周期钩子
onMounted(() => {
  init3DScene()
})

onUnmounted(() => {
  cleanup3DScene()
})

// 初始化3D场景
const init3DScene = () => {
  if (!buildingCanvas.value) return

  // 创建场景
  scene = new THREE.Scene()

  // 创建相机
  camera = new THREE.PerspectiveCamera(75, buildingCanvas.value.clientWidth / buildingCanvas.value.clientHeight, 0.1, 1000)
  camera.position.set(0, 0, 5)
  camera.lookAt(0, 0, 0)

  // 创建渲染器
  renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true })
  renderer.setSize(buildingCanvas.value.clientWidth, buildingCanvas.value.clientHeight)
  renderer.setClearColor(0x000000, 0)
  buildingCanvas.value.appendChild(renderer.domElement)

  // 添加光照 - 模拟地球的日照效果
  const ambientLight = new THREE.AmbientLight(0x606060, 0.5)
  scene.add(ambientLight)

  // 主光源 - 模拟太阳光
  const directionalLight = new THREE.DirectionalLight(0xffffff, 1.8)
  directionalLight.position.set(5, 3, 5)
  directionalLight.castShadow = true
  scene.add(directionalLight)

  // 次光源 - 模拟地球的反光
  const secondaryLight = new THREE.DirectionalLight(0x4488ff, 0.5)
  secondaryLight.position.set(-3, -2, -3)
  scene.add(secondaryLight)

  // 创建几何体组
  geometryGroup = new THREE.Group()
  createGeometryObjects()
  scene.add(geometryGroup)

  // 开始动画
  animate3DScene()
}

// 创建科技感地球
const createGeometryObjects = () => {
  if (!geometryGroup) return

  // 创建地球
  const earthGeometry = new THREE.SphereGeometry(1.8, 64, 64)

  // 加载地球纹理
  const textureLoader = new THREE.TextureLoader()
  const earthTexture = textureLoader.load('/src/image/earth.jpg')

  const earthMaterial = new THREE.MeshPhongMaterial({
    map: earthTexture,
    transparent: true,
    opacity: 0.95,
    shininess: 30,
    emissive: new THREE.Color(0x001122),
    emissiveIntensity: 0.1
  })
  const earth = new THREE.Mesh(earthGeometry, earthMaterial)
  geometryGroup.add(earth)

  // 添加云层
  const cloudGeometry = new THREE.SphereGeometry(1.9, 32, 32)
  const cloudMaterial = new THREE.MeshLambertMaterial({
    color: 0xffffff,
    transparent: true,
    opacity: 0.4,
    alphaMap: textureLoader.load('/src/image/earth.jpg'), // 复用地球纹理作为云层alpha
  })
  const clouds = new THREE.Mesh(cloudGeometry, cloudMaterial)
  geometryGroup.add(clouds)

  // 添加地球表面纹理（网格线）
  const wireframeGeometry = new THREE.SphereGeometry(1.82, 32, 32)
  const wireframeMaterial = new THREE.MeshBasicMaterial({
    color: 0x00ffff,
    wireframe: true,
    transparent: true,
    opacity: 0.2
  })
  const wireframe = new THREE.Mesh(wireframeGeometry, wireframeMaterial)
  geometryGroup.add(wireframe)

  // 添加经纬线
  addLatLongLines(geometryGroup)

  // 添加卫星
  addSatellites(geometryGroup)

  // 添加光环效果
  addAtmosphere(geometryGroup)

  // 添加数据流效果
  addDataStreams(geometryGroup)

  // 添加建筑
  addBuildings(geometryGroup)
}

// 添加经纬线
const addLatLongLines = (group: THREE.Group) => {
  // 经线
  for (let i = 0; i < 12; i++) {
    const angle = (i / 12) * Math.PI * 2
    const points = []
    for (let j = 0; j <= 64; j++) {
      const lat = (j / 64 - 0.5) * Math.PI
      const x = Math.cos(lat) * Math.cos(angle) * 1.82
      const y = Math.sin(lat) * 1.82
      const z = Math.cos(lat) * Math.sin(angle) * 1.82
      points.push(new THREE.Vector3(x, y, z))
    }
    const geometry = new THREE.BufferGeometry().setFromPoints(points)
    const material = new THREE.LineBasicMaterial({
      color: 0x00ffff,
      transparent: true,
      opacity: 0.6
    })
    const line = new THREE.Line(geometry, material)
    group.add(line)
  }

  // 纬线
  for (let i = 0; i < 6; i++) {
    const lat = (i / 6 - 0.5) * Math.PI
    const radius = Math.cos(lat) * 1.82
    const points = []
    for (let j = 0; j <= 64; j++) {
      const angle = (j / 64) * Math.PI * 2
      const x = radius * Math.cos(angle)
      const y = Math.sin(lat) * 1.82
      const z = radius * Math.sin(angle)
      points.push(new THREE.Vector3(x, y, z))
    }
    const geometry = new THREE.BufferGeometry().setFromPoints(points)
    const material = new THREE.LineBasicMaterial({
      color: 0x00ffff,
      transparent: true,
      opacity: 0.4
    })
    const line = new THREE.Line(geometry, material)
    group.add(line)
  }
}

// 添加卫星
const addSatellites = (group: THREE.Group) => {
  for (let i = 0; i < 8; i++) {
    const satelliteGeometry = new THREE.OctahedronGeometry(0.05)
    const satelliteMaterial = new THREE.MeshPhongMaterial({
      color: 0xff6600,
      emissive: 0x331100,
      shininess: 100
    })
    const satellite = new THREE.Mesh(satelliteGeometry, satelliteMaterial)

    // 随机轨道位置
    const distance = 2.6 + Math.random() * 0.5
    const angle = (i / 8) * Math.PI * 2
    const height = (Math.random() - 0.5) * 0.8

    satellite.position.set(
      Math.cos(angle) * distance,
      height,
      Math.sin(angle) * distance
    )

    satellite.userData.orbitRadius = distance
    satellite.userData.orbitAngle = angle
    satellite.userData.orbitSpeed = 0.005 + Math.random() * 0.01

    group.add(satellite)
  }
}

// 添加大气层光环
const addAtmosphere = (group: THREE.Group) => {
  const atmosphereGeometry = new THREE.SphereGeometry(2.1, 32, 32)
  const atmosphereMaterial = new THREE.MeshBasicMaterial({
    color: 0x0088ff,
    transparent: true,
    opacity: 0.1,
    side: THREE.BackSide
  })
  const atmosphere = new THREE.Mesh(atmosphereGeometry, atmosphereMaterial)
  group.add(atmosphere)
}

// 添加数据流效果
const addDataStreams = (group: THREE.Group) => {
  for (let i = 0; i < 20; i++) {
    const points = []
    const startAngle = Math.random() * Math.PI * 2
    const endAngle = startAngle + (Math.random() - 0.5) * Math.PI

    for (let j = 0; j < 10; j++) {
      const t = j / 9
      const angle = startAngle + (endAngle - startAngle) * t
      const radius = 1.83 + Math.random() * 0.1
      const x = Math.cos(angle) * radius
      const y = (Math.random() - 0.5) * 0.5
      const z = Math.sin(angle) * radius
      points.push(new THREE.Vector3(x, y, z))
    }

    const geometry = new THREE.BufferGeometry().setFromPoints(points)
    const material = new THREE.LineBasicMaterial({
      color: 0x00ff88,
      transparent: true,
      opacity: 0.8
    })
    const stream = new THREE.Line(geometry, material)
    group.add(stream)
  }
}

// 添加建筑
const addBuildings = (group: THREE.Group) => {
  for (let i = 0; i < 20; i++) {
    // 随机位置（经纬度）
    const longitude = (Math.random() - 0.5) * Math.PI * 2 // -π 到 π
    const latitude = (Math.random() - 0.5) * Math.PI * 0.8 // -0.4π 到 0.4π，避免极地

    // 建筑高度随机
    const height = 0.15 + Math.random() * 0.25

    // 创建建筑几何体（更大的建筑）
    const buildingGeometry = new THREE.BoxGeometry(0.03, height, 0.03)
    const buildingMaterial = new THREE.MeshPhongMaterial({
      color: new THREE.Color().setHSL(Math.random(), 0.9, 0.6 + Math.random() * 0.4),
      transparent: false,
      opacity: 1.0,
      shininess: 100,
      emissive: new THREE.Color(0x222222)
    })

    const building = new THREE.Mesh(buildingGeometry, buildingMaterial)

    // 将建筑放置在地球表面上方
    const earthRadius = 1.8 // 地球半径
    const buildingBaseRadius = earthRadius + height / 2 // 建筑底部在地球表面上方
    const x = Math.cos(latitude) * Math.cos(longitude) * buildingBaseRadius
    const y = Math.sin(latitude) * buildingBaseRadius
    const z = Math.cos(latitude) * Math.sin(longitude) * buildingBaseRadius

    building.position.set(x, y, z)

    // 计算建筑的朝向（法线方向）
    const normal = new THREE.Vector3(x, y, z).normalize()
    const up = new THREE.Vector3(0, 1, 0)

    // 创建旋转矩阵
    const quaternion = new THREE.Quaternion()
    quaternion.setFromUnitVectors(up, normal)
    building.setRotationFromQuaternion(quaternion)

    group.add(building)
  }
}

// 3D场景动画
const animate3DScene = () => {
  animationFrameId = requestAnimationFrame(animate3DScene)

  const time = Date.now() * 0.001

  // 地球和科技元素动画
  if (geometryGroup) {
    geometryGroup.children.forEach((child, index) => {
      if (child instanceof THREE.Mesh) {
        if (child.geometry.type === 'SphereGeometry' && !child.material.wireframe) {
          // 检查是否是地球（有纹理）还是云层（透明材质）
          if (child.material instanceof THREE.MeshPhongMaterial && child.material.map) {
            // 地球自转
            child.rotation.y += 0.001

            // 地球发光脉动
            const emissiveIntensity = 0.1 + Math.sin(time * 0.5) * 0.05
            child.material.emissive.setHSL(0.6, 0.8, emissiveIntensity)
          } else if (child.material instanceof THREE.MeshLambertMaterial) {
            // 云层旋转（稍慢于地球）
            child.rotation.y += 0.0008
            // 云层轻微脉动
            child.material.opacity = 0.3 + Math.sin(time * 0.3) * 0.1
          }
        } else if (child.userData.orbitRadius) {
          // 卫星轨道运动
          child.userData.orbitAngle += child.userData.orbitSpeed
          child.position.x = Math.cos(child.userData.orbitAngle) * child.userData.orbitRadius
          child.position.z = Math.sin(child.userData.orbitAngle) * child.userData.orbitRadius

          // 卫星自转
          child.rotation.x += 0.05
          child.rotation.y += 0.03
        }
      } else if (child instanceof THREE.Line) {
        // 数据流闪烁效果
        if (child.material instanceof THREE.LineBasicMaterial) {
          child.material.opacity = 0.4 + Math.sin(time * 2 + index * 0.5) * 0.3
        }
      }
    })

    // 整体旋转
    geometryGroup.rotation.y += 0.001
  }

  // 粒子动画
  if (particles) {
    particles.rotation.y += 0.001
    particles.rotation.x += 0.0005

    // 让粒子闪烁
    const material = particles.material as THREE.PointsMaterial
    material.opacity = 0.6 + Math.sin(time * 2) * 0.2
  }

  if (scene && camera && renderer) {
    renderer.render(scene, camera)
  }
}

// 清理3D场景
const cleanup3DScene = () => {
  if (animationFrameId) {
    cancelAnimationFrame(animationFrameId)
    animationFrameId = null
  }

  if (renderer && buildingCanvas.value) {
    buildingCanvas.value.removeChild(renderer.domElement)
    renderer.dispose()
    renderer = null
  }

  scene = null
  camera = null
  geometryGroup = null
  particles = null
}
</script>

<style scoped>
.login-page {
  display: flex;
  min-height: 100vh;
  background: linear-gradient(180deg, #001447 0%, #002b7a 50%, #001447 100%);
}

/* 左侧装饰区域 */
.login-left {
  flex: 1;
  background: linear-gradient(135deg, #002b7a 0%, #001447 100%);
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
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
  justify-content: center;
}

.logo-section {
  text-align: center;
  margin-bottom: 20rem;
  margin-top: 1rem;
  z-index: 3;
  position: relative;
}

.brand-title {
  font-size: 2.5rem;
  font-weight: 900;
  margin: 0 0 0.5rem 0;
  letter-spacing: 0.15em;
  background: linear-gradient(135deg,
    #3498db 0%,
    #52a8e8 25%,
    #74c0f4 50%,
    #52a8e8 75%,
    #3498db 100%);
  background-size: 200% auto;
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
  animation: titleShine 6s linear infinite;
  text-shadow:
    0 0 40px rgba(52, 152, 219, 0.6),
    0 0 80px rgba(41, 128, 185, 0.4);
}

@keyframes titleShine {
  0% { background-position: 0% center; }
  100% { background-position: 200% center; }
}

.brand-subtitle {
  font-size: 1rem;
  font-weight: 300;
  letter-spacing: 0.4em;
  margin: 0;
  color: rgba(116, 192, 244, 0.9);
  text-transform: uppercase;
}

/* 3D地球画布 */
.building-canvas {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  height: 400px;
  z-index: 2;
}



/* 右侧表单区域 */
.login-right {
  flex: 0 0 510px;
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

.alert-warning {
  background: #fffbeb;
  color: #92400e;
  border: 1px solid #fcd34d;
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
  z-index: 1;
}

.form-input {
  width: 100%;
  padding: 0.875rem 1rem 0.875rem 3rem;
  border: 2px solid #e2e8f0;
  border-radius: 12px;
  font-size: 0.9375rem;
  transition: all 0.3s ease;
  background: #f7fafc;
  transform: translateY(0);
}

.form-input:focus {
  outline: none;
  border-color: #667eea;
  background: white;
  box-shadow: 0 0 0 4px rgba(102, 126, 234, 0.1);
  transform: translateY(-1px);
}

.form-input:focus {
  outline: none;
  border-color: #667eea;
  background: white;
  box-shadow: 0 0 0 4px rgba(102, 126, 234, 0.1);
}

.form-input.input-error {
  border-color: #e53e3e;
  background: #fff5f5;
}

.form-input.input-error:focus {
  border-color: #e53e3e;
  box-shadow: 0 0 0 4px rgba(229, 62, 62, 0.1);
}

.form-input::placeholder {
  color: #cbd5e0;
}

.password-toggle {
  position: absolute;
  right: 1rem;
  background: none;
  border: none;
  cursor: pointer;
  padding: 0.25rem;
  border-radius: 4px;
  color: #a0aec0;
  transition: color 0.2s ease;
  display: flex;
  align-items: center;
  justify-content: center;
}

.password-toggle:hover {
  color: #667eea;
  background: rgba(102, 126, 234, 0.1);
}

.password-toggle svg {
  width: 20px;
  height: 20px;
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

.password-strength {
  margin-top: 0.5rem;
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.strength-bar {
  flex: 1;
  height: 4px;
  background: #e2e8f0;
  border-radius: 2px;
  overflow: hidden;
}

.strength-fill {
  height: 100%;
  border-radius: 2px;
  transition: all 0.3s ease;
}

.strength-fill.weak {
  background: #e53e3e;
}

.strength-fill.medium {
  background: #dd6b20;
}

.strength-fill.strong {
  background: #38a169;
}

.strength-text {
  font-size: 0.75rem;
  font-weight: 600;
  color: #4a5568;
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
  background: linear-gradient(135deg, #3498db 0%, #2980b9 100%);
  color: white;
  box-shadow: 0 4px 15px rgba(52, 152, 219, 0.4);
}

.btn-primary:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 6px 20px rgba(52, 152, 219, 0.5);
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

@media (max-width: 768px) {
  .login-page {
    flex-direction: column;
  }

  .login-right {
    flex: 1;
    padding: 2rem 1.5rem;
    min-height: 100vh;
  }

  .form-container {
    max-width: 100%;
  }

  .form-header {
    margin-bottom: 1.5rem;
  }

  .form-header h2 {
    font-size: 1.75rem;
  }

  .auth-tabs {
    margin-bottom: 1.5rem;
  }

  .input-group {
    gap: 0.375rem;
  }

  .form-input {
    padding: 0.75rem 0.875rem 0.75rem 2.75rem;
    font-size: 0.875rem;
  }

  .input-icon {
    left: 0.875rem;
    width: 18px;
    height: 18px;
  }

  .password-toggle {
    right: 0.875rem;
  }

  .btn {
    padding: 0.875rem;
    font-size: 0.9375rem;
  }
}

@media (max-width: 640px) {
  .login-right {
    padding: 1.5rem 1rem;
  }

  .form-header h2 {
    font-size: 1.5rem;
  }

  .brand-title {
    font-size: 2rem;
  }


}

@media (max-width: 480px) {
  .login-right {
    padding: 1rem 0.75rem;
  }

  .form-container {
    padding: 0 1rem;
  }

  .form-header {
    margin-bottom: 1.25rem;
  }

  .form-header h2 {
    font-size: 1.375rem;
  }

  .auth-tabs {
    padding: 0.1875rem;
  }

  .auth-tab {
    padding: 0.625rem;
    font-size: 0.875rem;
  }

  .alert {
    padding: 0.875rem;
    font-size: 0.8125rem;
  }

  .input-group {
    gap: 0.25rem;
  }

  .input-label {
    font-size: 0.8125rem;
  }

  .form-input {
    padding: 0.6875rem 0.75rem 0.6875rem 2.5rem;
    font-size: 0.8125rem;
  }

  .input-icon {
    left: 0.75rem;
    width: 16px;
    height: 16px;
  }

  .password-toggle {
    right: 0.75rem;
    padding: 0.1875rem;
  }

  .password-toggle svg {
    width: 16px;
    height: 16px;
  }

  .form-options {
    flex-direction: column;
    align-items: flex-start;
    gap: 0.75rem;
  }

  .checkbox-wrapper {
    gap: 0.375rem;
  }

  .checkbox-label {
    font-size: 0.8125rem;
  }

  .btn {
    padding: 0.75rem;
    font-size: 0.875rem;
  }

  .form-footer {
    margin-top: 1.5rem;
    font-size: 0.8125rem;
  }
}
</style>
