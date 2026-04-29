<template>
  <div class="error-page">
    <!-- 3D背景效果 -->
    <div class="error-background">
      <div class="grid-lines"></div>
      <div class="scan-line"></div>
    </div>
    
    <!-- 错误内容 -->
    <div class="error-content">
      <!-- 错误代码 -->
      <div class="error-code">
        <span class="code-digit">4</span>
        <span class="code-digit code-zero">0</span>
        <span class="code-digit">4</span>
      </div>
      
      <!-- 错误信息 -->
      <h1 class="error-title">页面未找到</h1>
      <p class="error-description">
        抱歉,您访问的页面在数字孪生世界中迷失了方向
      </p>
      
      <!-- 操作按钮 -->
      <div class="error-actions">
        <router-link to="/" class="btn btn-primary">
          <IconHome class="btn-icon" size="sm" />
          <span>返回首页</span>
        </router-link>
        <button @click="goBack" class="btn btn-secondary">
          <IconLayer class="btn-icon" size="sm" />
          <span>返回上页</span>
        </button>
      </div>
      
      <!-- 提示信息 -->
      <div class="error-tips">
        <p>可能的原因:</p>
        <ul>
          <li>页面地址输入错误</li>
          <li>页面已被移动或删除</li>
          <li>链接已过期</li>
        </ul>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'
import { IconHome, IconLayer } from '@/components/icons'

const router = useRouter()

const goBack = () => {
  if (window.history.length > 1) {
    router.go(-1)
  } else {
    router.push('/')
  }
}
</script>

<style scoped>
.error-page {
  position: relative;
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(180deg, #001447 0%, #002b7a 50%, #001447 100%);
  overflow: hidden;
}

/* 背景效果 */
.error-background {
  position: absolute;
  inset: 0;
  z-index: 0;
}

.grid-lines {
  position: absolute;
  inset: 0;
  background-image:
    linear-gradient(rgba(34, 211, 238, 0.1) 1px, transparent 1px),
    linear-gradient(90deg, rgba(34, 211, 238, 0.1) 1px, transparent 1px);
  background-size: 50px 50px;
  opacity: 0.3;
}

.scan-line {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 2px;
  background: linear-gradient(90deg, transparent, var(--neon-cyan), transparent);
  animation: scan 4s linear infinite;
  box-shadow: var(--glow-cyan);
}

@keyframes scan {
  0% { transform: translateY(0); opacity: 0; }
  50% { opacity: 1; }
  100% { transform: translateY(100vh); opacity: 0; }
}

/* 内容区域 */
.error-content {
  position: relative;
  z-index: 1;
  text-align: center;
  padding: var(--spacing-xl);
  max-width: 600px;
}

/* 错误代码 */
.error-code {
  display: flex;
  justify-content: center;
  gap: 1rem;
  margin-bottom: 2rem;
}

.code-digit {
  font-size: clamp(4rem, 15vw, 10rem);
  font-weight: 900;
  color: var(--neon-cyan);
  text-shadow: var(--glow-cyan);
  animation: float 3s ease-in-out infinite;
}

.code-digit:nth-child(1) { animation-delay: 0s; }
.code-digit:nth-child(2) { animation-delay: 0.2s; }
.code-digit:nth-child(3) { animation-delay: 0.4s; }

.code-zero {
  position: relative;
}

.code-zero::before {
  content: '';
  position: absolute;
  inset: 10%;
  border: 4px solid var(--neon-purple);
  border-radius: 50%;
  animation: rotate 4s linear infinite;
}

@keyframes float {
  0%, 100% { transform: translateY(0); }
  50% { transform: translateY(-20px); }
}

@keyframes rotate {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

/* 标题 */
.error-title {
  font-size: clamp(1.5rem, 4vw, 2.5rem);
  font-weight: 700;
  color: white;
  margin-bottom: 1rem;
  letter-spacing: 0.05em;
}

.error-description {
  font-size: clamp(1rem, 2vw, 1.25rem);
  color: var(--gray-300);
  margin-bottom: 2.5rem;
  line-height: 1.6;
}

/* 按钮 */
.error-actions {
  display: flex;
  justify-content: center;
  gap: 1rem;
  margin-bottom: 3rem;
  flex-wrap: wrap;
}

.btn {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.875rem 2rem;
  font-size: 1rem;
  font-weight: 600;
  text-decoration: none;
  border-radius: var(--radius-lg);
  cursor: pointer;
  transition: all var(--transition-base);
  border: none;
}

.btn-primary {
  background: var(--gradient-neon-primary);
  color: white;
  box-shadow: var(--glow-cyan);
}

.btn-primary:hover {
  transform: translateY(-2px);
  box-shadow: var(--glow-strong-cyan);
}

.btn-secondary {
  background: transparent;
  color: var(--neon-cyan);
  border: 2px solid rgba(34, 211, 238, 0.5);
}

.btn-secondary:hover {
  background: rgba(34, 211, 238, 0.1);
  border-color: var(--neon-cyan);
}

.btn-icon {
  display: flex;
}

/* 提示信息 */
.error-tips {
  background: var(--glass-bg);
  backdrop-filter: blur(var(--glass-blur));
  border: 1px solid var(--glass-border);
  border-radius: var(--radius-lg);
  padding: var(--spacing-lg);
  text-align: left;
}

.error-tips p {
  color: var(--neon-cyan);
  font-weight: 600;
  margin-bottom: 0.75rem;
}

.error-tips ul {
  color: var(--gray-300);
  margin: 0;
  padding-left: 1.5rem;
}

.error-tips li {
  margin-bottom: 0.5rem;
  line-height: 1.5;
}

/* 响应式 */
@media (max-width: 640px) {
  .error-actions {
    flex-direction: column;
  }
  
  .btn {
    width: 100%;
    justify-content: center;
  }
}
</style>
