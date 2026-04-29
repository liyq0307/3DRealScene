<template>
  <svg 
    viewBox="0 0 24 24" 
    fill="none" 
    stroke="currentColor"
    :class="['icon-earth', sizeClass]"
    :style="iconStyle"
    role="img"
    aria-label="地球"
  >
    <circle cx="12" cy="12" r="10" stroke-width="2"/>
    <path d="M2 12h20" stroke-width="2"/>
    <path 
      d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z" 
      stroke-width="2"
    />
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
.icon-earth {
  display: inline-block;
  vertical-align: middle;
  transition: transform var(--transition-base), filter var(--transition-base);
}

.icon-earth:hover {
  transform: rotate(15deg);
}

.icon-sm { width: 1rem; height: 1rem; }
.icon-md { width: 1.5rem; height: 1.5rem; }
.icon-lg { width: 2rem; height: 2rem; }
.icon-xl { width: 3rem; height: 3rem; }
</style>
