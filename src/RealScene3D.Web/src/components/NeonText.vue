<template>
  <component 
    :is="tag"
    :class="['neon-text', `neon-text-${color}`, { 'neon-text-pulse': pulse }]"
    :style="textStyle"
  >
    <slot />
  </component>
</template>

<script setup lang="ts">
import { computed } from 'vue'

interface Props {
  tag?: 'h1' | 'h2' | 'h3' | 'h4' | 'h5' | 'h6' | 'p' | 'span'
  color?: 'cyan' | 'blue' | 'green' | 'purple' | 'pink'
  size?: 'sm' | 'md' | 'lg' | 'xl'
  pulse?: boolean
  glow?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  tag: 'h1',
  color: 'cyan',
  size: 'lg',
  pulse: false,
  glow: true
})

const sizeMap = {
  sm: 'clamp(1rem, 3vw, 1.5rem)',
  md: 'clamp(1.5rem, 5vw, 3rem)',
  lg: 'clamp(2rem, 6vw, 5rem)',
  xl: 'clamp(2.5rem, 8vw, 7rem)'
}

const colorMap = {
  cyan: 'var(--neon-cyan)',
  blue: 'var(--neon-blue)',
  green: 'var(--neon-green)',
  purple: 'var(--neon-purple)',
  pink: 'var(--neon-pink)'
}

const textStyle = computed(() => ({
  fontSize: sizeMap[props.size],
  ...(props.glow && {
    textShadow: `
      0 0 10px ${colorMap[props.color]},
      0 0 20px ${colorMap[props.color]},
      0 0 40px ${colorMap[props.color]}
    `
  })
}))
</script>

<style scoped>
.neon-text {
  font-weight: 900;
  letter-spacing: 0.05em;
  transition: all var(--transition-base);
}

/* 颜色变体 */
.neon-text-cyan {
  color: var(--neon-cyan);
}

.neon-text-blue {
  color: var(--neon-blue);
}

.neon-text-green {
  color: var(--neon-green);
}

.neon-text-purple {
  color: var(--neon-purple);
}

.neon-text-pink {
  color: var(--neon-pink);
}

/* 脉冲动画 */
.neon-text-pulse {
  animation: neon-pulse 2s ease-in-out infinite;
}

@keyframes neon-pulse {
  0%, 100% {
    opacity: 1;
    filter: brightness(1);
  }
  50% {
    opacity: 0.9;
    filter: brightness(1.2);
  }
}

/* 悬停效果 */
.neon-text:hover {
  filter: brightness(1.3);
}
</style>
