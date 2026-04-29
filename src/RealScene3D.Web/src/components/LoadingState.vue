<template>
  <div :class="['loading-state', `loading-state-${size}`]">
    <div :class="['loading-spinner', `spinner-${variant}`]">
      <div v-for="i in 4" :key="i" class="spinner-dot"></div>
    </div>
    <p v-if="text" class="loading-text">{{ text }}</p>
  </div>
</template>

<script setup lang="ts">
interface Props {
  variant?: 'default' | 'neon' | 'pulse'
  size?: 'sm' | 'md' | 'lg'
  text?: string
}

withDefaults(defineProps<Props>(), {
  variant: 'neon',
  size: 'md',
  text: '加载中...'
})
</script>

<style scoped>
.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--spacing-md);
}

/* 尺寸变体 */
.loading-state-sm .loading-spinner {
  width: 24px;
  height: 24px;
}

.loading-state-md .loading-spinner {
  width: 40px;
  height: 40px;
}

.loading-state-lg .loading-spinner {
  width: 56px;
  height: 56px;
}

/* 旋转器 */
.loading-spinner {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
}

.spinner-dot {
  position: absolute;
  width: 25%;
  height: 25%;
  border-radius: 50%;
  background: var(--neon-cyan);
  animation: spin-dot 1.2s cubic-bezier(0.5, 0, 0.5, 1) infinite;
}

.spinner-dot:nth-child(1) {
  animation-delay: -0.45s;
  top: 0;
  left: 50%;
  transform: translateX(-50%);
}

.spinner-dot:nth-child(2) {
  animation-delay: -0.3s;
  top: 50%;
  right: 0;
  transform: translateY(-50%);
}

.spinner-dot:nth-child(3) {
  animation-delay: -0.15s;
  bottom: 0;
  left: 50%;
  transform: translateX(-50%);
}

.spinner-dot:nth-child(4) {
  animation-delay: 0s;
  top: 50%;
  left: 0;
  transform: translateY(-50%);
}

/* Neon 变体 */
.spinner-neon .spinner-dot {
  background: var(--neon-cyan);
  box-shadow: 0 0 10px var(--neon-cyan);
}

/* Pulse 变体 */
.spinner-pulse .spinner-dot {
  animation: pulse-dot 1.2s ease-in-out infinite;
}

.spinner-pulse .spinner-dot:nth-child(1) { animation-delay: 0s; }
.spinner-pulse .spinner-dot:nth-child(2) { animation-delay: 0.15s; }
.spinner-pulse .spinner-dot:nth-child(3) { animation-delay: 0.3s; }
.spinner-pulse .spinner-dot:nth-child(4) { animation-delay: 0.45s; }

@keyframes spin-dot {
  0%, 100% {
    transform: scale(0.8);
    opacity: 0.5;
  }
  50% {
    transform: scale(1.2);
    opacity: 1;
  }
}

@keyframes pulse-dot {
  0%, 100% {
    transform: scale(0.8);
    opacity: 0.5;
  }
  50% {
    transform: scale(1.2);
    opacity: 1;
  }
}

/* 加载文字 */
.loading-text {
  font-size: 0.875rem;
  color: var(--gray-600);
  margin: 0;
  animation: pulse-opacity 1.5s ease-in-out infinite;
}

@keyframes pulse-opacity {
  0%, 100% { opacity: 0.6; }
  50% { opacity: 1; }
}

/* 深色主题 */
@media (prefers-color-scheme: dark) {
  .loading-text {
    color: var(--gray-400);
  }
}
</style>
