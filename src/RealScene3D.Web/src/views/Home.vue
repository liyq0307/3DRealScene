<template>
  <!--
    主页视图模板
    提供应用的欢迎界面和导航入口
    采用居中布局，展示品牌信息和简介
  -->

  <div class="home">
    <!-- Hero区域 -->
    <div class="hero-section">
      <div class="hero-content">
        <!-- <div class="badge-new">全新体验</div> -->
        <h1 class="hero-title">
          <span class="gradient-text"></span>
          <br/>实景三维管理系统
        </h1>
        <p class="hero-subtitle">
          现代化的3D场景管理平台，提供强大的可视化工作流设计、实时监控和场景编辑功能
        </p>
        <div class="hero-actions">
          <router-link to="/scenes" class="btn btn-primary">
            <span class="btn-icon">🚀</span>
            开始探索
          </router-link>
          <router-link to="/workflow-designer" class="btn btn-secondary">
            <span class="btn-icon">🎨</span>
            设计工作流
          </router-link>
        </div>
      </div>
    </div>

    <!-- 功能特性卡片 -->
    <div class="features-container">
      <h2 class="section-title">核心功能</h2>
      <div class="features">
        <router-link to="/scenes" class="feature-card-link">
          <div class="feature-card fade-in">
            <div class="feature-icon gradient-bg-primary">🌐</div>
            <h3>3D场景查看</h3>
            <p>支持GLTF、OBJ、FBX等多种3D格式的实时渲染和交互操作</p>
            <div class="feature-tags">
              <span class="tag">实时渲染</span>
              <span class="tag">多格式支持</span>
            </div>
            <div class="feature-link">
              查看更多 →
            </div>
          </div>
        </router-link>

        <router-link to="/scene-objects" class="feature-card-link">
          <div class="feature-card fade-in" style="animation-delay: 0.1s;">
            <div class="feature-icon gradient-bg-success">📊</div>
            <h3>场景对象管理</h3>
            <p>完整的场景对象CRUD操作，支持变换属性编辑和实时预览</p>
            <div class="feature-tags">
              <span class="tag">对象编辑</span>
              <span class="tag">属性管理</span>
            </div>
            <div class="feature-link">
              查看更多 →
            </div>
          </div>
        </router-link>

        <router-link to="/workflow-designer" class="feature-card-link">
          <div class="feature-card fade-in" style="animation-delay: 0.2s;">
            <div class="feature-icon gradient-bg-info">🎨</div>
            <h3>工作流设计器</h3>
            <p>强大的拖拽式工作流可视化设计工具，支持流程建模和执行</p>
            <div class="feature-tags">
              <span class="tag">可视化设计</span>
              <span class="tag">流程编排</span>
            </div>
            <div class="feature-link">
              查看更多 →
            </div>
          </div>
        </router-link>

        <router-link to="/monitoring" class="feature-card-link">
          <div class="feature-card fade-in" style="animation-delay: 0.3s;">
            <div class="feature-icon gradient-bg-warning">📈</div>
            <h3>系统监控</h3>
            <p>实时监控系统状态、性能指标和业务数据，提供可视化图表</p>
            <div class="feature-tags">
              <span class="tag">实时监控</span>
              <span class="tag">数据可视化</span>
            </div>
            <div class="feature-link">
              查看更多 →
            </div>
          </div>
        </router-link>

        <router-link to="/workflow-instances" class="feature-card-link">
          <div class="feature-card fade-in" style="animation-delay: 0.4s;">
            <div class="feature-icon gradient-bg-primary">🔧</div>
            <h3>工作流实例</h3>
            <p>工作流实例管理和执行历史跟踪，支持暂停、恢复、取消等操作</p>
            <div class="feature-tags">
              <span class="tag">实例管理</span>
              <span class="tag">执行跟踪</span>
            </div>
            <div class="feature-link">
              查看更多 →
            </div>
          </div>
        </router-link>

        <router-link to="/slicing" class="feature-card-link">
          <div class="feature-card fade-in" style="animation-delay: 0.5s;">
            <div class="feature-icon gradient-bg-success">🗂️</div>
            <h3>切片管理</h3>
            <p>3D模型切片任务创建、管理和数据浏览，支持批量处理</p>
            <div class="feature-tags">
              <span class="tag">任务管理</span>
              <span class="tag">批量处理</span>
            </div>
            <div class="feature-link">
              查看更多 →
            </div>
          </div>
        </router-link>
      </div>
    </div>

    <!-- 统计数据展示 -->
    <div class="stats-section">
      <div class="stat-card">
        <div class="stat-value">99.9%</div>
        <div class="stat-label">系统可用性</div>
      </div>
      <div class="stat-card">
        <div class="stat-value">&lt;100ms</div>
        <div class="stat-label">平均响应时间</div>
      </div>
      <div class="stat-card">
        <div class="stat-value">10+</div>
        <div class="stat-label">3D格式支持</div>
      </div>
      <div class="stat-card">
        <div class="stat-value">24/7</div>
        <div class="stat-label">实时监控</div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * 主页视图组件 - Vue 3 组合式API实现
 *
 * 功能说明：
 * - 展示应用欢迎信息和品牌标识
 * - 提供简洁明了的界面布局
 * - 为用户提供应用功能概览
 * - 作为应用的默认首页和导航起点
 *
 * 技术栈：Vue 3 + TypeScript
 * 作者：liyq
 * 创建时间：2025-10-13
 *
 * 未来扩展：
 * - 添加快速开始指南
 * - 集成最近使用的场景快捷访问
 * - 添加系统状态指示器
 */

// ==================== 组件逻辑区域 ====================
// 目前该组件主要用于展示，无复杂交互逻辑
// 未来可以添加欢迎动画、快速操作按钮等功能
</script>

<style scoped>
.home {
  width: 100%;
  background: var(--gray-100);
}

/* Hero区域 */
.hero-section {
  min-height: 500px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #6366f1 0%, #8b5cf6 50%, #ec4899 100%);
  position: relative;
  overflow: hidden;
}

.hero-section::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background:
    radial-gradient(circle at 20% 50%, rgba(255, 255, 255, 0.1) 0%, transparent 50%),
    radial-gradient(circle at 80% 50%, rgba(255, 255, 255, 0.1) 0%, transparent 50%);
  animation: float 20s infinite linear;
}

.hero-section::after {
  content: '';
  position: absolute;
  inset: 0;
  background: url('data:image/svg+xml,<svg width="60" height="60" xmlns="http://www.w3.org/2000/svg"><circle cx="30" cy="30" r="1.5" fill="white" opacity="0.15"/></svg>');
  animation: floatReverse 30s infinite linear;
}

@keyframes float {
  from { transform: translateY(0) rotate(0deg); }
  to { transform: translateY(-100px) rotate(360deg); }
}

@keyframes floatReverse {
  from { transform: translateX(0) translateY(0); }
  to { transform: translateX(-60px) translateY(-60px); }
}

.hero-content {
  position: relative;
  z-index: 1;
  text-align: center;
  color: white;
  max-width: 800px;
  padding: 2rem;
  animation: fadeInUp 0.8s ease;
}

@keyframes fadeInUp {
  from {
    opacity: 0;
    transform: translateY(30px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.badge-new {
  display: inline-block;
  padding: 0.5rem 1.2rem;
  background: rgba(255, 255, 255, 0.25);
  backdrop-filter: blur(10px);
  -webkit-backdrop-filter: blur(10px);
  border-radius: var(--border-radius-full);
  font-size: 0.85rem;
  font-weight: 600;
  margin-bottom: 1.5rem;
  border: 1.5px solid rgba(255, 255, 255, 0.3);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
  animation: pulse 2s ease-in-out infinite;
}

.hero-title {
  font-size: 3.5rem;
  font-weight: 800;
  margin: 0 0 1.5rem 0;
  line-height: 1.2;
  letter-spacing: -1.5px;
  text-shadow: 0 2px 20px rgba(0, 0, 0, 0.15);
}

.gradient-text {
  background: linear-gradient(to right, #ffffff, #f0f0f0 50%, #ffffff);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
  background-size: 200% auto;
  animation: shimmerText 3s linear infinite;
}

@keyframes shimmerText {
  to {
    background-position: 200% center;
  }
}

.hero-subtitle {
  font-size: 1.25rem;
  margin: 0 0 2.5rem 0;
  opacity: 0.95;
  line-height: 1.6;
  color: rgba(255, 255, 255, 0.9);
}

.hero-actions {
  display: flex;
  gap: 1rem;
  justify-content: center;
  flex-wrap: wrap;
}

/* 按钮样式 */
.btn {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.875rem 2rem;
  border-radius: var(--border-radius);
  font-size: 1rem;
  font-weight: 600;
  text-decoration: none;
  transition: all var(--transition-base);
  box-shadow: var(--shadow-lg);
  border: none;
  cursor: pointer;
}

.btn-icon {
  font-size: 1.2rem;
}

.btn-primary {
  background: white;
  color: #667eea;
}

.btn-primary:hover {
  transform: translateY(-2px);
  box-shadow: var(--shadow-xl);
}

.btn-secondary {
  background: rgba(255, 255, 255, 0.1);
  color: white;
  border: 2px solid white;
  backdrop-filter: blur(10px);
}

.btn-secondary:hover {
  background: rgba(255, 255, 255, 0.2);
  transform: translateY(-2px);
}

/* 功能区域 */
.features-container {
  max-width: 1200px;
  margin: 0 auto;
  padding: 4rem 2rem;
}

.section-title {
  text-align: center;
  font-size: 2.5rem;
  font-weight: 700;
  margin: 0 0 3rem 0;
  background: var(--gradient-primary);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

.features {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
  gap: 2rem;
}

.feature-card-link {
  text-decoration: none;
  color: inherit;
  display: block;
}

.feature-card {
  background: white;
  border-radius: var(--border-radius-lg);
  padding: 2rem;
  box-shadow: var(--shadow-md);
  transition: all var(--transition-base);
  border: 1px solid var(--border-color);
  position: relative;
  overflow: hidden;
  cursor: pointer;
  height: 100%;
}

.feature-card::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 4px;
  background: var(--gradient-primary-alt);
  transform: scaleX(0);
  transition: transform var(--transition-base);
  transform-origin: left;
}

.feature-card::after {
  content: '';
  position: absolute;
  inset: 0;
  background: radial-gradient(circle at 50% 0%, rgba(99, 102, 241, 0.05), transparent 70%);
  opacity: 0;
  transition: opacity var(--transition-base);
}

.feature-card:hover::before {
  transform: scaleX(1);
}

.feature-card:hover::after {
  opacity: 1;
}

.feature-card:hover {
  transform: translateY(-10px) scale(1.02);
  box-shadow: var(--shadow-2xl);
  border-color: var(--primary-light);
}

.feature-icon {
  width: 64px;
  height: 64px;
  border-radius: var(--border-radius-lg);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 2rem;
  margin-bottom: 1.5rem;
  position: relative;
  transition: all var(--transition-base);
}

.feature-card:hover .feature-icon {
  transform: scale(1.1) rotate(-5deg);
}

.gradient-bg-primary {
  background: var(--gradient-primary);
}

.gradient-bg-success {
  background: var(--gradient-success);
}

.gradient-bg-info {
  background: var(--gradient-info);
}

.gradient-bg-warning {
  background: var(--gradient-warning);
}

.feature-card h3 {
  margin: 0 0 1rem 0;
  color: var(--gray-900);
  font-size: 1.5rem;
  font-weight: 700;
}

.feature-card p {
  margin: 0 0 1.5rem 0;
  color: var(--gray-600);
  font-size: 1rem;
  line-height: 1.6;
}

.feature-tags {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
  margin-bottom: 1.5rem;
}

.tag {
  padding: 0.35rem 0.75rem;
  background: var(--gray-100);
  color: var(--gray-700);
  font-size: 0.8rem;
  font-weight: 600;
  border-radius: 50px;
}

.feature-link {
  color: var(--primary-color);
  font-weight: 600;
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  transition: all var(--transition-base);
}

.feature-card:hover .feature-link {
  gap: 0.5rem;
  color: var(--primary-hover);
}

/* 统计区域 */
.stats-section {
  background: var(--gray-100);
  padding: 3rem 2rem;
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 2rem;
  max-width: 1200px;
  margin: 0 auto;
}

.stat-card {
  text-align: center;
  padding: 2rem;
  background: white;
  border-radius: var(--border-radius);
  box-shadow: var(--shadow-sm);
  transition: all var(--transition-base);
}

.stat-card:hover {
  transform: translateY(-4px);
  box-shadow: var(--shadow-md);
}

.stat-value {
  font-size: 2.5rem;
  font-weight: 800;
  background: var(--gradient-primary);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
  margin-bottom: 0.5rem;
}

.stat-label {
  color: var(--gray-600);
  font-size: 0.95rem;
  font-weight: 500;
}

/* 响应式设计 */
@media (max-width: 768px) {
  .hero-title {
    font-size: 2.5rem;
  }

  .hero-subtitle {
    font-size: 1rem;
  }

  .section-title {
    font-size: 2rem;
  }

  .features {
    grid-template-columns: 1fr;
  }

  .stats-section {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 480px) {
  .hero-title {
    font-size: 2rem;
  }

  .btn {
    padding: 0.75rem 1.5rem;
    font-size: 0.9rem;
  }

  .stats-section {
    grid-template-columns: 1fr;
  }

  .stat-value {
    font-size: 2rem;
  }
}
</style>
