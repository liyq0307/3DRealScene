<template>
  <span
    class="badge"
    :class="[
      `badge-${variant}`,
      `badge-${size}`,
      {
        'badge-dot': dot,
        'badge-outlined': outlined,
        'badge-neon': neon
      }
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
  neon?: boolean
}

withDefaults(defineProps<Props>(), {
  variant: 'primary',
  size: 'md',
  dot: false,
  outlined: false,
  neon: false
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

/* 霓虹效果 */
.badge-neon {
  position: relative;
  border: 1px solid rgba(255, 255, 255, 0.1);
}

.badge-neon.badge-primary {
  background: linear-gradient(135deg, rgba(99, 102, 241, 0.15), rgba(139, 92, 246, 0.15));
  color: var(--neon-primary);
  border-color: rgba(99, 102, 241, 0.4);
  box-shadow:
    0 0 20px rgba(99, 102, 241, 0.4),
    inset 0 0 15px rgba(99, 102, 241, 0.1);
  text-shadow: 0 0 10px var(--neon-primary);
}

.badge-neon.badge-success {
  background: linear-gradient(135deg, rgba(16, 185, 129, 0.15), rgba(20, 184, 166, 0.15));
  color: var(--neon-green);
  border-color: rgba(16, 185, 129, 0.4);
  box-shadow:
    0 0 20px rgba(16, 185, 129, 0.4),
    inset 0 0 15px rgba(16, 185, 129, 0.1);
  text-shadow: 0 0 10px var(--neon-green);
}

.badge-neon.badge-warning {
  background: linear-gradient(135deg, rgba(245, 158, 11, 0.15), rgba(251, 191, 36, 0.15));
  color: var(--neon-orange);
  border-color: rgba(245, 158, 11, 0.4);
  box-shadow:
    0 0 20px rgba(245, 158, 11, 0.4),
    inset 0 0 15px rgba(245, 158, 11, 0.1);
  text-shadow: 0 0 10px var(--neon-orange);
}

.badge-neon.badge-danger {
  background: linear-gradient(135deg, rgba(239, 68, 68, 0.15), rgba(248, 113, 113, 0.15));
  color: var(--neon-red);
  border-color: rgba(239, 68, 68, 0.4);
  box-shadow:
    0 0 20px rgba(239, 68, 68, 0.4),
    inset 0 0 15px rgba(239, 68, 68, 0.1);
  text-shadow: 0 0 10px var(--neon-red);
}

.badge-neon.badge-info {
  background: linear-gradient(135deg, rgba(6, 182, 212, 0.15), rgba(34, 211, 238, 0.15));
  color: var(--neon-blue);
  border-color: rgba(6, 182, 212, 0.4);
  box-shadow:
    0 0 20px rgba(6, 182, 212, 0.4),
    inset 0 0 15px rgba(6, 182, 212, 0.1);
  text-shadow: 0 0 10px var(--neon-blue);
}

.badge-neon.badge-gray {
  background: linear-gradient(135deg, rgba(148, 163, 184, 0.15), rgba(203, 213, 225, 0.15));
  color: var(--gray-300);
  border-color: rgba(148, 163, 184, 0.3);
  box-shadow:
    0 0 15px rgba(148, 163, 184, 0.2),
    inset 0 0 10px rgba(148, 163, 184, 0.05);
}

.badge-neon:hover {
  transform: translateY(-2px);
  filter: brightness(1.2);
}

.badge-neon.badge-primary:hover {
  box-shadow:
    0 0 30px rgba(99, 102, 241, 0.6),
    0 0 50px rgba(99, 102, 241, 0.3),
    inset 0 0 20px rgba(99, 102, 241, 0.15);
}

.badge-neon.badge-success:hover {
  box-shadow:
    0 0 30px rgba(16, 185, 129, 0.6),
    0 0 50px rgba(16, 185, 129, 0.3),
    inset 0 0 20px rgba(16, 185, 129, 0.15);
}

.badge-neon.badge-warning:hover {
  box-shadow:
    0 0 30px rgba(245, 158, 11, 0.6),
    0 0 50px rgba(245, 158, 11, 0.3),
    inset 0 0 20px rgba(245, 158, 11, 0.15);
}

.badge-neon.badge-danger:hover {
  box-shadow:
    0 0 30px rgba(239, 68, 68, 0.6),
    0 0 50px rgba(239, 68, 68, 0.3),
    inset 0 0 20px rgba(239, 68, 68, 0.15);
}

.badge-neon.badge-info:hover {
  box-shadow:
    0 0 30px rgba(6, 182, 212, 0.6),
    0 0 50px rgba(6, 182, 212, 0.3),
    inset 0 0 20px rgba(6, 182, 212, 0.15);
}
</style>
