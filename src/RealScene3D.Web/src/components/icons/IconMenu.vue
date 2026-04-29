<template>
  <svg 
    viewBox="0 0 24 24" 
    fill="none" 
    stroke="currentColor"
    :class="['icon-menu', sizeClass]"
    :style="iconStyle"
    role="img"
    aria-label="菜单"
  >
    <line x1="3" y1="12" x2="21" y2="12" stroke-width="2"/>
    <line x1="3" y1="6" x2="21" y2="6" stroke-width="2"/>
    <line x1="3" y1="18" x2="21" y2="18" stroke-width="2"/>
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
.icon-menu {
  display: inline-block;
  vertical-align: middle;
  transition: transform var(--transition-base);
}

.icon-menu:hover {
  transform: scale(1.05);
}

.icon-sm { width: 1rem; height: 1rem; }
.icon-md { width: 1.5rem; height: 1.5rem; }
.icon-lg { width: 2rem; height: 2rem; }
.icon-xl { width: 3rem; height: 3rem; }
</style>
