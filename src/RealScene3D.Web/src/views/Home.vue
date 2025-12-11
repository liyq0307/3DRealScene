<template>
  <div class="home">
    <!-- 3D旋转地球背景 -->
    <div class="globe-container">
      <div class="globe">
        <!-- 经线 -->
        <div class="meridian" v-for="i in 16" :key="'meridian-' + i" :style="getMeridianStyle(i)"></div>
        <!-- 纬线 -->
        <div class="parallel" v-for="i in 10" :key="'parallel-' + i" :style="getParallelStyle(i)"></div>

        <!-- 地球表面光晕 -->
        <div class="globe-surface"></div>

        <!-- 大陆轮廓点 -->
        <div class="continents">
          <div class="continent-dot" v-for="i in 80" :key="'dot-' + i" :style="getContinentDotStyle(i)"></div>
        </div>

        <!-- 地球核心光晕 -->
        <div class="globe-core"></div>

        <!-- 轨道环 -->
        <div class="orbit-ring"></div>
      </div>
    </div>

    <!-- 3D城市建筑剪影 -->
    <div class="city-silhouette">
      <!-- 后排建筑 -->
      <div class="building-layer back-layer">
        <div class="building" v-for="i in 15" :key="'back-' + i" :style="getBackBuildingStyle(i)"></div>
      </div>
      <!-- 中排建筑 -->
      <div class="building-layer mid-layer">
        <div class="building" v-for="i in 20" :key="'mid-' + i" :style="getMidBuildingStyle(i)"></div>
      </div>
      <!-- 前排建筑 -->
      <div class="building-layer front-layer">
        <div class="building" v-for="i in 25" :key="'front-' + i" :style="getFrontBuildingStyle(i)"></div>
      </div>
    </div>

    <!-- 3D网格背景 -->
    <div class="grid-background"></div>

    <!-- 动态粒子效果 -->
    <div class="particles">
      <div v-for="i in 40" :key="i" class="particle" :style="getParticleStyle(i)"></div>
    </div>

    <!-- 扫描线效果 -->
    <div class="scan-line"></div>

    <!-- 主内容区 -->
    <div class="hero-container">
      <!-- 标题区域 -->
      <div class="hero-content">
        <div class="glitch-container">
          <h1 class="hero-title">实景三维</h1>
        </div>
        <div class="subtitle-container">
          <p class="hero-subtitle">Real Scene 3D Platform</p>
          <div class="subtitle-line"></div>
        </div>
        <p class="hero-description">
          <span class="typed-text">构建数字孪生世界</span>
          <br />
          <span class="typed-text delay-1">精准复现真实场景</span>
          <br />
          <span class="typed-text delay-2">驱动智慧城市未来</span>
        </p>

        <!-- 操作按钮 -->
        <div class="hero-actions">
          <router-link to="/scenes" class="btn btn-primary">
            <span class="btn-icon">▶</span>
            <span class="btn-text">进入平台</span>
            <span class="btn-glow"></span>
          </router-link>
          <router-link to="/workflow-designer" class="btn btn-secondary">
            <span class="btn-icon">✦</span>
            <span class="btn-text">工作流设计</span>
          </router-link>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * 实景三维平台首页
 *
 * 设计理念：
 * - 蓝色科技风主题
 * - 3D旋转地球和城市建筑剪影背景
 * - 参考专业GIS平台设计风格
 * - 动态粒子和网格效果
 * - 沉浸式视觉体验
 *
 * 技术栈：Vue 3 + TypeScript + CSS3 动画
 * 作者：liyq
 * 创建时间：2025-12-11
 */

// 生成随机粒子样式
const getParticleStyle = (index: number) => {
  const size = Math.random() * 2 + 1
  const duration = Math.random() * 25 + 15
  const delay = Math.random() * 5
  const x = Math.random() * 100
  const y = Math.random() * 100

  return {
    width: `${size}px`,
    height: `${size}px`,
    left: `${x}%`,
    top: `${y}%`,
    animationDuration: `${duration}s`,
    animationDelay: `${delay}s`
  }
}

// 生成经线样式
const getMeridianStyle = (index: number) => {
  const rotation = (index - 1) * 11.25 // 每11.25度一条经线
  return {
    transform: `rotateY(${rotation}deg)`
  }
}

// 生成纬线样式
const getParallelStyle = (index: number) => {
  const angle = (index - 5) * 15 // -60deg 到 +60deg
  const scale = Math.cos((angle * Math.PI) / 180)
  return {
    transform: `rotateX(${angle}deg) scale(${scale})`,
    opacity: Math.abs(scale) * 0.5
  }
}

// 生成大陆轮廓点样式
const getContinentDotStyle = (index: number) => {
  // 模拟随机分布在地球表面的点
  const theta = Math.random() * Math.PI * 2
  const phi = Math.acos(2 * Math.random() - 1)

  const x = 50 + 45 * Math.sin(phi) * Math.cos(theta)
  const y = 50 + 45 * Math.sin(phi) * Math.sin(theta)
  const z = 45 * Math.cos(phi)

  const size = Math.random() * 3 + 1
  const delay = Math.random() * 3

  return {
    left: `${x}%`,
    top: `${y}%`,
    width: `${size}px`,
    height: `${size}px`,
    transform: `translateZ(${z}px)`,
    animationDelay: `${delay}s`
  }
}

// 生成后排建筑样式
const getBackBuildingStyle = (index: number) => {
  const height = Math.random() * 80 + 40
  const width = Math.random() * 40 + 30
  const x = (index - 1) * 6.5

  return {
    width: `${width}px`,
    height: `${height}px`,
    left: `${x}%`,
    animationDelay: `${Math.random() * 2}s`
  }
}

// 生成中排建筑样式
const getMidBuildingStyle = (index: number) => {
  const height = Math.random() * 120 + 60
  const width = Math.random() * 45 + 35
  const x = (index - 1) * 5

  return {
    width: `${width}px`,
    height: `${height}px`,
    left: `${x}%`,
    animationDelay: `${Math.random() * 2}s`
  }
}

// 生成前排建筑样式
const getFrontBuildingStyle = (index: number) => {
  const height = Math.random() * 180 + 80
  const width = Math.random() * 50 + 40
  const x = (index - 1) * 4

  return {
    width: `${width}px`,
    height: `${height}px`,
    left: `${x}%`,
    animationDelay: `${Math.random() * 2}s`
  }
}
</script>

<style scoped>
/* ==================== 基础布局 ==================== */
.home {
  position: relative;
  width: 100%;
  min-height: 100vh;
  background: linear-gradient(180deg, #001447 0%, #002b7a 50%, #001447 100%);
  overflow: hidden;
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  perspective: 1500px;
}

/* ==================== 3D旋转地球 ==================== */
.globe-container {
  position: absolute;
  top: 50%;
  right: 8%;
  transform: translateY(-50%);
  width: 550px;
  height: 550px;
  perspective: 1500px;
  z-index: 5;
}

.globe {
  position: relative;
  width: 100%;
  height: 100%;
  transform-style: preserve-3d;
  animation: rotateGlobe 80s linear infinite;
}

@keyframes rotateGlobe {
  0% { transform: rotateY(0deg) rotateX(-15deg); }
  100% { transform: rotateY(360deg) rotateX(-15deg); }
}

/* 地球表面 */
.globe-surface {
  position: absolute;
  top: 50%;
  left: 50%;
  width: 450px;
  height: 450px;
  margin: -225px 0 0 -225px;
  background: radial-gradient(circle at 30% 30%,
    rgba(41, 128, 185, 0.15) 0%,
    rgba(30, 90, 150, 0.1) 50%,
    transparent 100%);
  border-radius: 50%;
  box-shadow:
    inset 0 0 50px rgba(41, 128, 185, 0.3),
    0 0 50px rgba(41, 128, 185, 0.2);
}

/* 经线 */
.meridian {
  position: absolute;
  top: 50%;
  left: 50%;
  width: 450px;
  height: 450px;
  margin: -225px 0 0 -225px;
  border: 1.5px solid rgba(52, 152, 219, 0.4);
  border-radius: 50%;
  transform-style: preserve-3d;
}

/* 纬线 */
.parallel {
  position: absolute;
  top: 50%;
  left: 50%;
  width: 450px;
  height: 450px;
  margin: -225px 0 0 -225px;
  border: 1.5px solid rgba(52, 152, 219, 0.35);
  border-radius: 50%;
  transform-style: preserve-3d;
}

/* 大陆轮廓点 */
.continents {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  transform-style: preserve-3d;
}

.continent-dot {
  position: absolute;
  background: radial-gradient(circle, rgba(52, 152, 219, 0.9), rgba(41, 128, 185, 0.4));
  border-radius: 50%;
  box-shadow: 0 0 8px rgba(52, 152, 219, 0.8);
  animation: dotPulse 3s ease-in-out infinite;
}

@keyframes dotPulse {
  0%, 100% { opacity: 0.6; transform: scale(1); }
  50% { opacity: 1; transform: scale(1.3); }
}

/* 地球核心光晕 */
.globe-core {
  position: absolute;
  top: 50%;
  left: 50%;
  width: 120px;
  height: 120px;
  margin: -60px 0 0 -60px;
  background: radial-gradient(circle,
    rgba(52, 152, 219, 0.6) 0%,
    rgba(41, 128, 185, 0.3) 40%,
    transparent 70%);
  border-radius: 50%;
  box-shadow:
    0 0 80px rgba(52, 152, 219, 0.6),
    0 0 120px rgba(41, 128, 185, 0.4),
    inset 0 0 40px rgba(52, 152, 219, 0.3);
  animation: coreGlow 4s ease-in-out infinite;
}

@keyframes coreGlow {
  0%, 100% {
    transform: scale(1);
    opacity: 0.7;
    box-shadow:
      0 0 80px rgba(52, 152, 219, 0.6),
      0 0 120px rgba(41, 128, 185, 0.4);
  }
  50% {
    transform: scale(1.15);
    opacity: 1;
    box-shadow:
      0 0 100px rgba(52, 152, 219, 0.8),
      0 0 150px rgba(41, 128, 185, 0.6);
  }
}

/* 轨道环 */
.orbit-ring {
  position: absolute;
  top: 50%;
  left: 50%;
  width: 520px;
  height: 520px;
  margin: -260px 0 0 -260px;
  border: 2px solid rgba(52, 152, 219, 0.2);
  border-radius: 50%;
  animation: orbitRotate 15s linear infinite;
}

.orbit-ring::before {
  content: '';
  position: absolute;
  top: -6px;
  left: 50%;
  width: 12px;
  height: 12px;
  margin-left: -6px;
  background: radial-gradient(circle, rgba(52, 152, 219, 1), rgba(52, 152, 219, 0.3));
  border-radius: 50%;
  box-shadow: 0 0 15px rgba(52, 152, 219, 0.8);
}

@keyframes orbitRotate {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

/* ==================== 3D城市建筑剪影 ==================== */
.city-silhouette {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  height: 280px;
  z-index: 2;
  overflow: hidden;
}

.building-layer {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  height: 100%;
}

.building {
  position: absolute;
  bottom: 0;
  transition: opacity 0.3s ease;
}

/* 后排建筑层 */
.back-layer .building {
  background: linear-gradient(180deg,
    rgba(20, 60, 120, 0.4) 0%,
    rgba(15, 45, 90, 0.5) 100%);
  opacity: 0.6;
  animation: buildingGlow 4s ease-in-out infinite;
}

/* 中排建筑层 */
.mid-layer .building {
  background: linear-gradient(180deg,
    rgba(25, 75, 140, 0.6) 0%,
    rgba(20, 60, 110, 0.7) 100%);
  opacity: 0.75;
  animation: buildingGlow 4s ease-in-out infinite;
}

/* 前排建筑层 */
.front-layer .building {
  background: linear-gradient(180deg,
    rgba(30, 90, 160, 0.8) 0%,
    rgba(25, 75, 130, 0.9) 100%);
  box-shadow:
    0 -2px 20px rgba(52, 152, 219, 0.3),
    inset 0 -3px 10px rgba(52, 152, 219, 0.2);
  animation: buildingGlow 4s ease-in-out infinite;
}

/* 建筑顶部灯光 */
.front-layer .building::before,
.mid-layer .building::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 4px;
  background: linear-gradient(90deg,
    transparent 0%,
    rgba(52, 152, 219, 0.8) 50%,
    transparent 100%);
  box-shadow: 0 0 10px rgba(52, 152, 219, 0.8);
}

/* 建筑窗户 */
.front-layer .building::after,
.mid-layer .building::after {
  content: '';
  position: absolute;
  top: 15%;
  left: 15%;
  right: 15%;
  bottom: 20%;
  background:
    repeating-linear-gradient(
      0deg,
      transparent,
      transparent 12px,
      rgba(52, 152, 219, 0.15) 12px,
      rgba(52, 152, 219, 0.15) 14px
    ),
    repeating-linear-gradient(
      90deg,
      transparent,
      transparent 12px,
      rgba(52, 152, 219, 0.15) 12px,
      rgba(52, 152, 219, 0.15) 14px
    );
}

@keyframes buildingGlow {
  0%, 100% { opacity: 0.7; }
  50% { opacity: 1; }
}

/* ==================== 3D网格背景 ==================== */
.grid-background {
  position: absolute;
  inset: 0;
  background-image:
    linear-gradient(rgba(52, 152, 219, 0.08) 1px, transparent 1px),
    linear-gradient(90deg, rgba(52, 152, 219, 0.08) 1px, transparent 1px);
  background-size: 60px 60px;
  animation: gridMove 25s linear infinite;
  opacity: 0.4;
  z-index: 0;
}

@keyframes gridMove {
  0% { transform: perspective(600px) rotateX(65deg) translateY(0); }
  100% { transform: perspective(600px) rotateX(65deg) translateY(60px); }
}

/* ==================== 粒子效果 ==================== */
.particles {
  position: absolute;
  inset: 0;
  pointer-events: none;
  z-index: 1;
}

.particle {
  position: absolute;
  background: radial-gradient(circle, rgba(52, 152, 219, 0.9), transparent);
  border-radius: 50%;
  animation: float linear infinite;
}

@keyframes float {
  0% {
    transform: translateY(0) translateX(0);
    opacity: 0;
  }
  10% {
    opacity: 0.8;
  }
  90% {
    opacity: 0.8;
  }
  100% {
    transform: translateY(-100vh) translateX(calc((var(--random-x, 0) - 0.5) * 100px));
    opacity: 0;
  }
}

/* ==================== 扫描线效果 ==================== */
.scan-line {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 3px;
  background: linear-gradient(90deg,
    transparent,
    rgba(52, 152, 219, 0.9) 50%,
    transparent
  );
  animation: scan 5s linear infinite;
  box-shadow: 0 0 25px rgba(52, 152, 219, 0.8);
  z-index: 3;
}

@keyframes scan {
  0% { transform: translateY(0); opacity: 0; }
  50% { opacity: 1; }
  100% { transform: translateY(100vh); opacity: 0; }
}

/* ==================== 主内容区 ==================== */
.hero-container {
  position: relative;
  z-index: 10;
  width: 100%;
  max-width: 1400px;
  padding: 2rem;
}

.hero-content {
  text-align: center;
  color: white;
  animation: fadeInUp 1.2s ease-out;
}

@keyframes fadeInUp {
  from {
    opacity: 0;
    transform: translateY(50px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

/* ==================== 标题 ==================== */
.glitch-container {
  margin-bottom: 1.5rem;
}

.hero-title {
  font-size: clamp(3.5rem, 12vw, 8rem);
  font-weight: 900;
  margin: 0;
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
  position: relative;
}

@keyframes titleShine {
  0% { background-position: 0% center; }
  100% { background-position: 200% center; }
}

/* ==================== 副标题 ==================== */
.subtitle-container {
  margin-bottom: 2.5rem;
  display: inline-block;
}

.hero-subtitle {
  font-size: clamp(1.1rem, 3.5vw, 1.75rem);
  font-weight: 300;
  letter-spacing: 0.4em;
  margin: 0 0 0.75rem 0;
  color: rgba(116, 192, 244, 0.9);
  text-transform: uppercase;
}

.subtitle-line {
  height: 3px;
  background: linear-gradient(90deg,
    transparent,
    rgba(52, 152, 219, 0.9) 50%,
    transparent
  );
  box-shadow: 0 0 10px rgba(52, 152, 219, 0.6);
  animation: expandLine 2s ease-out;
}

@keyframes expandLine {
  from { transform: scaleX(0); }
  to { transform: scaleX(1); }
}

/* ==================== 描述文字 ==================== */
.hero-description {
  font-size: clamp(1.1rem, 2.5vw, 1.4rem);
  line-height: 2.2;
  color: rgba(200, 230, 255, 0.95);
  margin: 0 auto 4rem;
  max-width: 900px;
  font-weight: 300;
  text-shadow: 0 2px 10px rgba(0, 0, 0, 0.3);
}

.typed-text {
  display: inline-block;
  opacity: 0;
  animation: typeIn 1s ease-out forwards;
}

.delay-1 { animation-delay: 0.4s; }
.delay-2 { animation-delay: 0.8s; }

@keyframes typeIn {
  from {
    opacity: 0;
    transform: translateY(15px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

/* ==================== 按钮样式 ==================== */
.hero-actions {
  display: flex;
  justify-content: center;
  gap: 2rem;
  margin: 4rem auto 0;
  flex-wrap: wrap;
  animation: fadeIn 1.2s ease-out 1s backwards;
}

@keyframes fadeIn {
  from { opacity: 0; }
  to { opacity: 1; }
}

.btn {
  position: relative;
  display: inline-flex;
  align-items: center;
  gap: 0.75rem;
  padding: 1.4rem 3.5rem;
  font-size: 1.15rem;
  font-weight: 600;
  text-decoration: none;
  border-radius: 50px;
  transition: all 0.4s ease;
  overflow: hidden;
  text-transform: uppercase;
  letter-spacing: 0.15em;
}

.btn-icon {
  font-size: 1.4rem;
  transition: transform 0.4s ease;
}

.btn:hover .btn-icon {
  transform: translateX(8px);
}

.btn-primary {
  background: linear-gradient(135deg, #3498db, #2980b9);
  color: white;
  border: 2px solid rgba(52, 152, 219, 0.5);
  box-shadow:
    0 12px 35px rgba(52, 152, 219, 0.5),
    inset 0 2px 0 rgba(255, 255, 255, 0.3);
}

.btn-glow {
  position: absolute;
  inset: 0;
  background: linear-gradient(135deg, transparent, rgba(255, 255, 255, 0.4), transparent);
  transform: translateX(-100%);
  transition: transform 0.7s ease;
}

.btn-primary:hover .btn-glow {
  transform: translateX(100%);
}

.btn-primary:hover {
  transform: translateY(-4px);
  box-shadow:
    0 18px 45px rgba(52, 152, 219, 0.7),
    inset 0 2px 0 rgba(255, 255, 255, 0.3);
  border-color: rgba(52, 152, 219, 0.8);
}

.btn-secondary {
  background: transparent;
  color: rgba(116, 192, 244, 1);
  border: 2px solid rgba(52, 152, 219, 0.7);
  backdrop-filter: blur(10px);
  box-shadow: 0 8px 25px rgba(52, 152, 219, 0.3);
}

.btn-secondary:hover {
  background: rgba(52, 152, 219, 0.15);
  border-color: rgba(52, 152, 219, 1);
  transform: translateY(-4px);
  box-shadow: 0 12px 35px rgba(52, 152, 219, 0.5);
  color: white;
}

/* ==================== 响应式设计 ==================== */
@media (max-width: 1200px) {
  .globe-container {
    width: 450px;
    height: 450px;
    right: 5%;
  }

  .globe-surface,
  .meridian,
  .parallel {
    width: 370px;
    height: 370px;
    margin: -185px 0 0 -185px;
  }

  .orbit-ring {
    width: 430px;
    height: 430px;
    margin: -215px 0 0 -215px;
  }
}

@media (max-width: 768px) {
  .hero-container {
    padding: 1.5rem;
  }

  .globe-container {
    width: 320px;
    height: 320px;
    top: 15%;
    right: -30px;
    opacity: 0.3;
  }

  .globe-surface,
  .meridian,
  .parallel {
    width: 260px;
    height: 260px;
    margin: -130px 0 0 -130px;
  }

  .city-silhouette {
    height: 200px;
  }

  .hero-actions {
    flex-direction: column;
    gap: 1.25rem;
  }

  .btn {
    width: 100%;
    max-width: 320px;
    justify-content: center;
    padding: 1.2rem 2.5rem;
  }
}

@media (max-width: 480px) {
  .hero-description {
    font-size: 1rem;
    line-height: 2;
  }

  .grid-background {
    background-size: 40px 40px;
  }

  .globe-container {
    display: none;
  }

  .city-silhouette {
    height: 150px;
  }
}

/* ==================== 性能优化 ==================== */
@media (prefers-reduced-motion: reduce) {
  *,
  *::before,
  *::after {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
  }
}
</style>
