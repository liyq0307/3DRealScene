<template>
  <svg 
    viewBox="0 0 24 24" 
    fill="currentColor"
    :class="['icon-play', sizeClass]"
    :style="iconStyle"
    role="img"
    aria-label="播放"
  >
    <polygon points="5 3 19 12 5 21 5 3"/>
  </svg>
</template>

<script setup lang="ts">
import { computed } from 'vue'

interface Props {
  size?: 'sm' | 'md' | 'lg' | 'xl'
  color?: string
  glow?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  size: 'md',
  color: 'currentColor',
  glow: false
})

const sizeClass = computed(() => `icon-${props.size}`)

const iconStyle = computed(() => ({
  color: props.color,
  ...(props.glow && {
    filter: 'drop-shadow(0 0 8px rgba(34, 211, 238, 0.5))'
  })
}))
</script>

<style scoped>
.icon-play {
  display: inline-block;
  vertical-align: middle;
  transition: transform var(--transition-base);
}

.icon-play:hover {
  transform: translateX(2px);
}

.icon-sm { width: 1rem; height: 1rem; }
.icon-md { width: 1.5rem; height: 1.5rem; }
.icon-lg { width: 2rem; height: 2rem; }
.icon-xl { width: 3rem; height: 3rem; }
</style>
