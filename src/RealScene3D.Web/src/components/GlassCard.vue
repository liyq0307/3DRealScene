<template>
  <div 
    :class="['glass-card', `glass-card-${variant}`, { 'glass-card-hover': hoverable }]"
    :style="cardStyle"
  >
    <slot />
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

interface Props {
  variant?: 'default' | 'primary' | 'secondary' | 'dark'
  hoverable?: boolean
  glow?: boolean
  padding?: 'sm' | 'md' | 'lg'
}

const props = withDefaults(defineProps<Props>(), {
  variant: 'default',
  hoverable: true,
  glow: false,
  padding: 'md'
})

const paddingMap = {
  sm: 'var(--spacing-md)',
  md: 'var(--spacing-lg)',
  lg: 'var(--spacing-xl)'
}

const cardStyle = computed(() => ({
  padding: paddingMap[props.padding],
  ...(props.glow && {
    boxShadow: 'var(--glow-cyan)'
  })
}))
</script>

<style scoped>
.glass-card {
  background: var(--glass-bg);
  backdrop-filter: blur(var(--glass-blur)) saturate(180%);
  -webkit-backdrop-filter: blur(var(--glass-blur)) saturate(180%);
  border: 1px solid var(--glass-border);
  border-radius: var(--radius-xl);
  box-shadow: var(--shadow-md);
  transition: all var(--transition-base);
  position: relative;
  overflow: hidden;
}

.glass-card::before {
  content: '';
  position: absolute;
  inset: 0;
  background: linear-gradient(
    135deg,
    rgba(255, 255, 255, 0.1) 0%,
    transparent 50%
  );
  pointer-events: none;
}

/* 变体样式 */
.glass-card-default {
  background: var(--glass-bg);
}

.glass-card-primary {
  background: rgba(59, 130, 246, 0.15);
  border-color: rgba(59, 130, 246, 0.3);
}

.glass-card-secondary {
  background: rgba(34, 211, 238, 0.15);
  border-color: rgba(34, 211, 238, 0.3);
}

.glass-card-dark {
  background: rgba(15, 23, 42, 0.8);
  border-color: rgba(148, 163, 184, 0.2);
}

/* 悬停效果 */
.glass-card-hover:hover {
  border-color: var(--neon-cyan);
  box-shadow: var(--glow-cyan), var(--shadow-lg);
  transform: translateY(-4px);
}

.glass-card-primary.glass-card-hover:hover {
  border-color: var(--neon-blue);
  box-shadow: var(--glow-blue), var(--shadow-lg);
}

.glass-card-secondary.glass-card-hover:hover {
  border-color: var(--neon-cyan);
  box-shadow: var(--glow-cyan), var(--shadow-lg);
}
</style>
