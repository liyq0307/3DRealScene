<template>
  <span
    class="badge"
    :class="[
      `badge-${variant}`,
      `badge-${size}`,
      { 'badge-dot': dot, 'badge-outlined': outlined }
    ]"
  >
    <span v-if="dot" class="badge-dot-indicator"></span>
    <slot>{{ label }}</slot>
  </span>
</template>

<script setup lang="ts">
/**
 * 徽章/标签组件
 *
 * 功能特性:
 * - 多种颜色变体 (primary, success, warning, danger, info, gray)
 * - 三种尺寸 (sm, md, lg)
 * - 支持描边样式
 * - 支持圆点指示器
 *
 * @author liyq
 * @date 2025-10-15
 */

interface Props {
  label?: string
  variant?: 'primary' | 'success' | 'warning' | 'danger' | 'info' | 'gray'
  size?: 'sm' | 'md' | 'lg'
  dot?: boolean
  outlined?: boolean
}

withDefaults(defineProps<Props>(), {
  variant: 'primary',
  size: 'md',
  dot: false,
  outlined: false
})
</script>

<style scoped>
.badge {
  display: inline-flex;
  align-items: center;
  gap: 0.375rem;
  font-weight: 600;
  line-height: 1;
  text-align: center;
  white-space: nowrap;
  vertical-align: baseline;
  border-radius: var(--border-radius-full);
  transition: all var(--transition-base);
}

/* 尺寸 */
.badge-sm {
  padding: 0.25rem 0.625rem;
  font-size: var(--font-size-xs);
}

.badge-md {
  padding: 0.375rem 0.875rem;
  font-size: var(--font-size-sm);
}

.badge-lg {
  padding: 0.5rem 1rem;
  font-size: var(--font-size-base);
}

/* 颜色变体 - 填充样式 */
.badge-primary {
  background: var(--gradient-primary-alt);
  color: white;
  box-shadow: 0 2px 8px rgba(99, 102, 241, 0.25);
}

.badge-success {
  background: var(--gradient-success);
  color: white;
  box-shadow: 0 2px 8px rgba(16, 185, 129, 0.25);
}

.badge-warning {
  background: var(--gradient-warning);
  color: white;
  box-shadow: 0 2px 8px rgba(245, 158, 11, 0.25);
}

.badge-danger {
  background: var(--gradient-danger);
  color: white;
  box-shadow: 0 2px 8px rgba(239, 68, 68, 0.25);
}

.badge-info {
  background: var(--gradient-info);
  color: white;
  box-shadow: 0 2px 8px rgba(6, 182, 212, 0.25);
}

.badge-gray {
  background: var(--gray-200);
  color: var(--gray-700);
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
}

/* 描边样式 */
.badge-outlined {
  background: transparent !important;
  box-shadow: none !important;
  border: 1.5px solid currentColor;
}

.badge-primary.badge-outlined {
  color: var(--primary-color);
  background: var(--primary-lighter) !important;
}

.badge-success.badge-outlined {
  color: var(--success-color);
  background: var(--success-light) !important;
}

.badge-warning.badge-outlined {
  color: var(--warning-color);
  background: var(--warning-light) !important;
}

.badge-danger.badge-outlined {
  color: var(--danger-color);
  background: var(--danger-light) !important;
}

.badge-info.badge-outlined {
  color: var(--info-color);
  background: var(--info-light) !important;
}

.badge-gray.badge-outlined {
  color: var(--gray-600);
  background: var(--gray-100) !important;
  border-color: var(--gray-400);
}

/* 圆点指示器 */
.badge-dot {
  padding-left: 0.5rem;
}

.badge-dot-indicator {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: currentColor;
  animation: pulse 2s cubic-bezier(0.4, 0, 0.6, 1) infinite;
}

@keyframes pulse {
  0%, 100% {
    opacity: 1;
  }
  50% {
    opacity: 0.5;
  }
}

/* 交互效果 */
.badge:hover {
  transform: translateY(-1px);
  box-shadow: var(--shadow-md);
}
</style>
