<template>
  <svg 
    viewBox="0 0 24 24" 
    fill="none" 
    stroke="currentColor"
    :class="['icon-workflow', sizeClass]"
    :style="iconStyle"
    role="img"
    aria-label="工作流"
  >
    <circle cx="5" cy="6" r="3" stroke-width="2"/>
    <circle cx="19" cy="6" r="3" stroke-width="2"/>
    <circle cx="12" cy="18" r="3" stroke-width="2"/>
    <path d="M5 9v3a4 4 0 0 0 4 4h6a4 4 0 0 0 4-4V9" stroke-width="2"/>
    <line x1="8" y1="6" x2="16" y2="6" stroke-width="2"/>
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
.icon-workflow {
  display: inline-block;
  vertical-align: middle;
  transition: transform var(--transition-base);
}

.icon-workflow:hover {
  transform: rotate(5deg);
}

.icon-sm { width: 1rem; height: 1rem; }
.icon-md { width: 1.5rem; height: 1.5rem; }
.icon-lg { width: 2rem; height: 2rem; }
.icon-xl { width: 3rem; height: 3rem; }
</style>
