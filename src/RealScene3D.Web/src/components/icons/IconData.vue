<template>
  <svg 
    viewBox="0 0 24 24" 
    fill="none" 
    stroke="currentColor"
    :class="['icon-data', sizeClass]"
    :style="iconStyle"
    role="img"
    aria-label="数据"
  >
    <ellipse cx="12" cy="5" rx="9" ry="3" stroke-width="2"/>
    <path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3" stroke-width="2"/>
    <path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5" stroke-width="2"/>
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
.icon-data {
  display: inline-block;
  vertical-align: middle;
  transition: transform var(--transition-base);
}

.icon-data:hover {
  transform: translateY(-2px);
}

.icon-sm { width: 1rem; height: 1rem; }
.icon-md { width: 1.5rem; height: 1.5rem; }
.icon-lg { width: 2rem; height: 2rem; }
.icon-xl { width: 3rem; height: 3rem; }
</style>
