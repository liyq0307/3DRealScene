<template>
  <!--
    场景卡片组件 - 现代化图像背景卡片设计
    采用背景图像叠加信息层的视觉布局
    支持查看、编辑、删除操作
  -->
  <div class="scene-card" :data-id="id" @click="handleCardClick">
    <!-- 背景图像区域 (可点击) -->
    <div class="scene-card-image">
      <img
        :src="imageLoaded ? imageUrl : ''"
        :data-src="imageUrl"
        :alt="name"
        loading="lazy"
        @error="handleImageError"
        @load="imageLoaded = true"
      />
      <!-- 图像加载前的占位 -->
      <div v-if="!imageLoaded" class="image-placeholder">
        <span class="placeholder-icon">🏙️</span>
      </div>
    </div>

    <!-- 信息栏 (半透明叠加) -->
    <div class="scene-card-info" @click.stop>
      <!-- 左侧：场景名称和时间 -->
      <div class="info-content">
        <h3 class="scene-name" :title="name">{{ name }}</h3>
        <span class="scene-time">{{ formatDateTime(createdAt) }}</span>
      </div>

      <!-- 右侧：三点菜单按钮 -->
      <button
        class="menu-trigger"
        @click="toggleMenu"
        @keydown.enter="toggleMenu"
        @keydown.space.prevent="toggleMenu"
        aria-label="操作菜单"
        :aria-expanded="menuVisible"
      >
        <span class="dots">⋯</span>
      </button>

      <!-- 三点菜单下拉 -->
      <SceneCardMenu
        v-if="menuVisible"
        @view="handleView"
        @edit="handleEdit"
        @delete="handleDelete"
        @close="closeMenu"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * 场景卡片组件 - Vue 3 组合式API实现
 *
 * 功能说明：
 * - 现代化图像背景卡片设计
 * - 背景图像展示（场景预览图或占位图）
 * - 底部叠加半透明信息栏
 * - 三点菜单操作（查看、编辑、删除）
 * - 悬停动画效果
 * - 响应式布局适配
 *
 * 技术栈：Vue 3 + TypeScript + CSS Scoped
 * 作者：liyq
 * 创建时间：2025-01-22
 */

import { ref, onMounted, onUnmounted } from 'vue'
import SceneCardMenu from './SceneCardMenu.vue'

// ==================== Props和Emits定义 ====================

/**
 * 组件Props接口定义
 */
interface Props {
  id: string              // 场景唯一标识
  name: string            // 场景名称
  description?: string    // 场景描述（可选）
  createdAt: string       // 创建时间 (ISO 8601)
  updatedAt: string       // 更新时间 (ISO 8601)
  previewImage?: string   // 预览图URL（可选）
}

const props = defineProps<Props>()

/**
 * 组件Emits接口定义
 */
const emit = defineEmits<{
  (e: 'view', id: string): void      // 查看场景
  (e: 'edit', id: string): void      // 编辑场景
  (e: 'delete', id: string): void    // 删除场景
}>()

// ==================== 响应式数据 ====================

/**
 * 菜单显示状态
 */
const menuVisible = ref(false)

/**
 * 图像加载状态
 */
const imageLoaded = ref(false)

/**
 * 图像URL（计算属性）
 */
const imageUrl = getPreviewImageUrl(props.previewImage)

// ==================== 工具函数 ====================

/**
 * 格式化日期时间
 * 格式：YYYY-MM-DD HH:mm:ss
 *
 * @param dateStr ISO 8601日期字符串
 * @returns 格式化后的日期时间字符串
 */
const formatDateTime = (dateStr: string): string => {
  if (!dateStr) return '-'

  const date = new Date(dateStr)

  // 使用toLocaleString格式化，精确到秒
  const formatted = date.toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hour12: false
  })

  // 将斜杠替换为横杠，保持YYYY-MM-DD格式
  return formatted.replace(/\//g, '-')
}

/**
 * 获取预览图URL
 * 如果有预览图则使用，否则使用默认占位图
 *
 * @param previewImage 预览图URL（可选）
 * @returns 实际使用的图像URL
 */
function getPreviewImageUrl(previewImage?: string): string {
  if (previewImage) {
    return previewImage
  }
  // 返回默认背景图（航拍城市河流图）
  return '/assets/images/scene-background.png'
}

/**
 * 处理图像加载错误
 * 加载失败时使用占位图
 *
 * @param event 错误事件
 */
const handleImageError = (event: Event): void => {
  const img = event.target as HTMLImageElement
  img.src = '/assets/images/scene-background.png'
  imageLoaded.value = true
}

// ==================== 交互处理 ====================

/**
 * 切换菜单显示状态
 */
const toggleMenu = (): void => {
  menuVisible.value = !menuVisible.value
}

/**
 * 关闭菜单
 */
const closeMenu = (): void => {
  menuVisible.value = false
}

/**
 * 处理卡片点击（背景图像区域）
 * 触发查看场景事件
 */
const handleCardClick = (): void => {
  // 只有当菜单未显示时才触发查看
  if (!menuVisible.value) {
    emit('view', props.id)
  }
}

/**
 * 处理查看场景
 */
const handleView = (): void => {
  emit('view', props.id)
  closeMenu()
}

/**
 * 处理编辑场景
 */
const handleEdit = (): void => {
  emit('edit', props.id)
  closeMenu()
}

/**
 * 处理删除场景
 */
const handleDelete = (): void => {
  emit('delete', props.id)
  closeMenu()
}

/**
 * 处理外部点击（关闭菜单）
 */
const handleOutsideClick = (event: MouseEvent): void => {
  // 如果菜单未显示，不需要处理
  if (!menuVisible.value) return
  
  const target = event.target as HTMLElement
  const cardElement = document.querySelector(`.scene-card[data-id="${props.id}"]`)

  // 如果点击的不是卡片内部，则关闭菜单
  if (cardElement && !cardElement.contains(target)) {
    closeMenu()
  }
}

// ==================== 生命周期钩子 ====================

/**
 * 组件挂载：添加外部点击监听器
 */
onMounted(() => {
  document.addEventListener('click', handleOutsideClick)
})

/**
 * 组件卸载：移除外部点击监听器
 */
onUnmounted(() => {
  document.removeEventListener('click', handleOutsideClick)
})
</script>

<style scoped>
/**
 * 场景卡片样式定义
 * 现代化图像背景卡片设计
 */

/* 卡片主容器 */
.scene-card {
  position: relative;
  width: 100%;
  height: 180px;
  border-radius: 8px;
  overflow: hidden;
  background: #f5f5f5;
  cursor: pointer;
  transition: transform 0.25s cubic-bezier(0.4, 0, 0.2, 1),
              box-shadow 0.25s cubic-bezier(0.4, 0, 0.2, 1);
  will-change: transform, box-shadow;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

/* 卡片悬停效果 */
.scene-card:hover {
  transform: translateY(-6px);
  box-shadow: 0 12px 24px rgba(0, 0, 0, 0.2);
}

/* 背景图像区域 */
.scene-card-image {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 20%; /* 信息栏占20% */
  overflow: hidden;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.scene-card-image img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  object-position: center;
  transition: opacity 0.3s ease;
}

/* 图像占位符 */
.image-placeholder {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.placeholder-icon {
  font-size: 3rem;
  opacity: 0.5;
}

/* 信息栏 (半透明叠加) */
.scene-card-info {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  height: 20%;
  min-height: 48px;
  background: rgba(255, 255, 255, 0.92);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 0.875rem;
  backdrop-filter: blur(8px);
  -webkit-backdrop-filter: blur(8px);
  border-top: 1px solid rgba(0, 0, 0, 0.05);
}

/* 信息内容 (左侧) */
.info-content {
  flex: 1;
  min-width: 0; /* 允许文本截断 */
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

/* 场景名称 */
.scene-name {
  margin: 0;
  font-size: 0.95rem;
  font-weight: 600;
  color: #333333;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  line-height: 1.2;
}

/* 时间戳 */
.scene-time {
  font-size: 0.75rem;
  color: #666666;
  white-space: nowrap;
}

/* 三点菜单按钮 */
.menu-trigger {
  flex-shrink: 0;
  width: 32px;
  height: 32px;
  border: none;
  background: transparent;
  color: #666666;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: all 0.2s ease;
  margin-left: 0.5rem;
}

.menu-trigger:hover {
  background: rgba(0, 0, 0, 0.05);
  color: #333333;
}

.menu-trigger:focus {
  outline: 2px solid rgba(0, 122, 204, 0.5);
  outline-offset: 2px;
}

.dots {
  font-size: 1.25rem;
  line-height: 1;
  letter-spacing: 0.05em;
}

/* ==================== 响应式布局 ==================== */

/* 大屏 (≥1200px) */
@media (min-width: 1200px) {
  .scene-card {
    height: 200px;
  }

  .scene-name {
    font-size: 0.95rem;
  }

  .scene-time {
    font-size: 0.75rem;
  }
}

/* 中屏 (768px - 1199px) */
@media (min-width: 768px) and (max-width: 1199px) {
  .scene-card {
    height: 180px;
  }
}

/* 小屏 (<768px) */
@media (max-width: 767px) {
  .scene-card {
    height: 160px;
  }

  .scene-name {
    font-size: 0.875rem;
  }

  .scene-time {
    font-size: 0.7rem;
  }

  .scene-card-info {
    padding: 0 0.75rem;
  }
}
</style>
