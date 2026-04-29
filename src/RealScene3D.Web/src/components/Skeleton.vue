<template>
  <div :class="['skeleton', `skeleton-${variant}`]" :style="skeletonStyle">
    <slot />
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

interface Props {
  variant?: 'text' | 'circle' | 'rect' | 'card'
  width?: string
  height?: string
  rows?: number
  animated?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  variant: 'text',
  width: '100%',
  height: undefined,
  rows: 1,
  animated: true
})

const defaultHeights = {
  text: '1em',
  circle: '40px',
  rect: '100px',
  card: '200px'
}

const skeletonStyle = computed(() => {
  const style: Record<string, string> = {
    width: props.width
  }
  
  if (props.variant === 'circle') {
    style.height = props.height || props.width
  } else {
    style.height = props.height || defaultHeights[props.variant]
  }
  
  return style
})
</script>

<style scoped>
.skeleton {
  position: relative;
  overflow: hidden;
  background: linear-gradient(
    90deg,
    var(--gray-200) 25%,
    var(--gray-100) 50%,
    var(--gray-200) 75%
  );
  background-size: 200% 100%;
  animation: shimmer 1.5s linear infinite;
}

.skeleton-text {
  border-radius: var(--radius-sm);
  margin-bottom: 0.5em;
}

.skeleton-circle {
  border-radius: 50%;
}

.skeleton-rect {
  border-radius: var(--radius-md);
}

.skeleton-card {
  border-radius: var(--radius-lg);
  padding: var(--spacing-lg);
  display: flex;
  flex-direction: column;
  gap: var(--spacing-md);
}

@keyframes shimmer {
  0% {
    background-position: 200% 0;
  }
  100% {
    background-position: -200% 0;
  }
}

/* 深色主题 */
@media (prefers-color-scheme: dark) {
  .skeleton {
    background: linear-gradient(
      90deg,
      var(--gray-700) 25%,
      var(--gray-600) 50%,
      var(--gray-700) 75%
    );
    background-size: 200% 100%;
  }
}
</style>
