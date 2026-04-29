<template>
  <div class="empty-state">
    <!-- 图标/插画 -->
    <div class="empty-state-icon">
      <slot name="icon">
        <component :is="iconComponent" v-if="iconComponent" size="xl" :glow="true" />
        <div v-else class="empty-state-default-icon">∅</div>
      </slot>
    </div>
    
    <!-- 标题 -->
    <h3 class="empty-state-title">{{ title }}</h3>
    
    <!-- 描述 -->
    <p v-if="description" class="empty-state-description">{{ description }}</p>
    
    <!-- 操作按钮 -->
    <div v-if="$slots.action" class="empty-state-action">
      <slot name="action" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { IconCube, IconLayer, IconData } from './icons'

interface Props {
  title: string
  description?: string
  icon?: 'cube' | 'layer' | 'data' | 'custom'
}

const props = defineProps<Props>()

const iconMap = {
  cube: IconCube,
  layer: IconLayer,
  data: IconData,
  custom: null
}

const iconComponent = computed(() => iconMap[props.icon || 'cube'])
</script>

<style scoped>
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: var(--spacing-3xl) var(--spacing-xl);
  text-align: center;
  min-height: 300px;
}

/* 图标区域 */
.empty-state-icon {
  margin-bottom: var(--spacing-lg);
  color: var(--neon-cyan);
  animation: float 3s ease-in-out infinite;
}

.empty-state-default-icon {
  font-size: 4rem;
  color: var(--gray-400);
  font-weight: 300;
}

@keyframes float {
  0%, 100% { transform: translateY(0); }
  50% { transform: translateY(-10px); }
}

/* 标题 */
.empty-state-title {
  font-size: 1.5rem;
  font-weight: 700;
  color: var(--gray-700);
  margin: 0 0 var(--spacing-sm);
}

/* 描述 */
.empty-state-description {
  font-size: 1rem;
  color: var(--gray-500);
  max-width: 400px;
  margin: 0 0 var(--spacing-lg);
  line-height: 1.6;
}

/* 操作区域 */
.empty-state-action {
  margin-top: var(--spacing-sm);
}

/* 深色主题 */
@media (prefers-color-scheme: dark) {
  .empty-state-title {
    color: var(--gray-300);
  }
  
  .empty-state-description {
    color: var(--gray-400);
  }
}

/* 霓虹主题 */
[data-theme="cyberpunk"] .empty-state-title {
  color: var(--neon-cyan);
  text-shadow: 0 0 10px rgba(34, 211, 238, 0.3);
}
</style>
