<template>
  <svg 
    viewBox="0 0 24 24" 
    fill="none" 
    stroke="currentColor"
    :class="['icon-layer', sizeClass]"
    :style="iconStyle"
    role="img"
    aria-label="图层"
  >
    <polygon points="12 2 2 7 12 12 22 7 12 2" stroke-width="2"/>
    <polyline points="2 17 12 22 22 17" stroke-width="2"/>
    <polyline points="2 12 12 17 22 12" stroke-width="2"/>
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
.icon-layer {
  display: inline-block;
  vertical-align: middle;
  transition: transform var(--transition-base);
}

.icon-layer:hover {
  transform: scale(1.05);
}

.icon-sm { width: 1rem; height: 1rem; }
.icon-md { width: 1.5rem; height: 1.5rem; }
.icon-lg { width: 2rem; height: 2rem; }
.icon-xl { width: 3rem; height: 3rem; }
</style>
