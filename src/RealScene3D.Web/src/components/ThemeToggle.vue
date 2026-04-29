<template>
  <button
    class="theme-toggle"
    :class="`theme-toggle-${theme}`"
    @click="toggleTheme"
    :aria-label="`切换主题 (当前: ${themeLabel})`"
    :title="themeLabel"
  >
    <Transition name="theme-icon" mode="out-in">
      <svg v-if="theme === 'light'" key="light" viewBox="0 0 24 24" fill="none" stroke="currentColor" class="theme-icon">
        <circle cx="12" cy="12" r="5" stroke-width="2"/>
        <line x1="12" y1="1" x2="12" y2="3" stroke-width="2"/>
        <line x1="12" y1="21" x2="12" y2="23" stroke-width="2"/>
        <line x1="4.22" y1="4.22" x2="5.64" y2="5.64" stroke-width="2"/>
        <line x1="18.36" y1="18.36" x2="19.78" y2="19.78" stroke-width="2"/>
        <line x1="1" y1="12" x2="3" y2="12" stroke-width="2"/>
        <line x1="21" y1="12" x2="23" y2="12" stroke-width="2"/>
        <line x1="4.22" y1="19.78" x2="5.64" y2="18.36" stroke-width="2"/>
        <line x1="18.36" y1="5.64" x2="19.78" y2="4.22" stroke-width="2"/>
      </svg>
      <svg v-else-if="theme === 'dark'" key="dark" viewBox="0 0 24 24" fill="none" stroke="currentColor" class="theme-icon">
        <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" stroke-width="2"/>
      </svg>
      <svg v-else key="cyberpunk" viewBox="0 0 24 24" fill="none" stroke="currentColor" class="theme-icon">
        <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2" stroke-width="2"/>
      </svg>
    </Transition>
  </button>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useTheme } from '@/composables/useTheme'

const { theme, toggleTheme } = useTheme()

const themeLabel = computed(() => {
  const labels = {
    light: '亮色主题',
    dark: '暗色主题',
    cyberpunk: '赛博朋克主题'
  }
  return labels[theme.value]
})
</script>

<style scoped>
.theme-toggle {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  border: none;
  border-radius: var(--radius-md);
  background: transparent;
  cursor: pointer;
  transition: all var(--transition-base);
  position: relative;
  overflow: hidden;
}

.theme-toggle:hover {
  background: var(--glass-bg-light);
  transform: rotate(15deg);
}

.theme-toggle:active {
  transform: rotate(15deg) scale(0.95);
}

.theme-icon {
  width: 20px;
  height: 20px;
  color: var(--gray-600);
  transition: color var(--transition-base);
}

.theme-toggle-light .theme-icon {
  color: var(--warning-color);
}

.theme-toggle-dark .theme-icon {
  color: var(--gray-400);
}

.theme-toggle-cyberpunk .theme-icon {
  color: var(--neon-cyan);
  filter: drop-shadow(0 0 6px var(--neon-cyan));
}

/* 过渡动画 */
.theme-icon-enter-active,
.theme-icon-leave-active {
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

.theme-icon-enter-from {
  opacity: 0;
  transform: rotate(-90deg) scale(0.5);
}

.theme-icon-leave-to {
  opacity: 0;
  transform: rotate(90deg) scale(0.5);
}
</style>
