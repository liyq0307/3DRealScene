<template>
  <button
    :class="[
      'btn',
      `btn-${variant}`,
      `btn-${size}`,
      { 'btn-loading': loading, 'btn-disabled': disabled, 'btn-block': block }
    ]"
    :disabled="disabled || loading"
    @click="handleClick"
  >
    <span v-if="loading" class="btn-spinner"></span>
    <span v-if="icon && !loading" class="btn-icon">{{ icon }}</span>
    <span class="btn-text"><slot /></span>
  </button>
</template>

<script setup lang="ts">
interface ButtonProps {
  variant?: 'primary' | 'secondary' | 'success' | 'warning' | 'danger' | 'info' | 'ghost'
  size?: 'sm' | 'md' | 'lg'
  loading?: boolean
  disabled?: boolean
  block?: boolean
  icon?: string
}

const props = withDefaults(defineProps<ButtonProps>(), {
  variant: 'primary',
  size: 'md',
  loading: false,
  disabled: false,
  block: false,
  icon: ''
})

const emit = defineEmits<{
  click: [event: MouseEvent]
}>()

const handleClick = (event: MouseEvent) => {
  if (!props.loading && !props.disabled) {
    emit('click', event)
  }
}
</script>

<style scoped>
.btn {
  position: relative;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  font-weight: 600;
  border: none;
  border-radius: var(--border-radius);
  cursor: pointer;
  transition: all var(--transition-base);
  font-family: var(--font-family);
  white-space: nowrap;
  user-select: none;
  outline: none;
}

.btn:focus-visible {
  outline: 2px solid var(--primary-color);
  outline-offset: 2px;
}

/* 尺寸变体 */
.btn-sm {
  padding: 0.4rem 0.875rem;
  font-size: 0.85rem;
  gap: 0.35rem;
}

.btn-md {
  padding: 0.625rem 1.25rem;
  font-size: 0.95rem;
}

.btn-lg {
  padding: 0.875rem 1.75rem;
  font-size: 1.05rem;
  gap: 0.625rem;
}

/* 颜色变体 - Primary */
.btn-primary {
  background: var(--gradient-primary);
  color: white;
  box-shadow: var(--shadow-sm);
}

.btn-primary:hover:not(.btn-disabled):not(.btn-loading) {
  box-shadow: var(--shadow-md);
  transform: translateY(-1px);
}

.btn-primary:active:not(.btn-disabled):not(.btn-loading) {
  transform: translateY(0);
  box-shadow: var(--shadow-sm);
}

/* Secondary */
.btn-secondary {
  background: var(--gray-600);
  color: white;
  box-shadow: var(--shadow-sm);
}

.btn-secondary:hover:not(.btn-disabled):not(.btn-loading) {
  background: var(--gray-700);
  box-shadow: var(--shadow-md);
  transform: translateY(-1px);
}

/* Success */
.btn-success {
  background: var(--gradient-success);
  color: white;
  box-shadow: var(--shadow-sm);
}

.btn-success:hover:not(.btn-disabled):not(.btn-loading) {
  box-shadow: var(--shadow-md);
  transform: translateY(-1px);
}

/* Warning */
.btn-warning {
  background: var(--gradient-warning);
  color: var(--gray-900);
  box-shadow: var(--shadow-sm);
}

.btn-warning:hover:not(.btn-disabled):not(.btn-loading) {
  box-shadow: var(--shadow-md);
  transform: translateY(-1px);
}

/* Danger */
.btn-danger {
  background: var(--danger-color);
  color: white;
  box-shadow: var(--shadow-sm);
}

.btn-danger:hover:not(.btn-disabled):not(.btn-loading) {
  background: var(--danger-hover);
  box-shadow: var(--shadow-md);
  transform: translateY(-1px);
}

/* Info */
.btn-info {
  background: var(--gradient-info);
  color: white;
  box-shadow: var(--shadow-sm);
}

.btn-info:hover:not(.btn-disabled):not(.btn-loading) {
  box-shadow: var(--shadow-md);
  transform: translateY(-1px);
}

/* Ghost */
.btn-ghost {
  background: transparent;
  color: var(--primary-color);
  border: 1px solid var(--border-color);
}

.btn-ghost:hover:not(.btn-disabled):not(.btn-loading) {
  background: var(--primary-light);
  border-color: var(--primary-color);
}

/* 禁用状态 */
.btn-disabled {
  opacity: 0.6;
  cursor: not-allowed;
  transform: none !important;
}

/* 加载状态 */
.btn-loading {
  cursor: wait;
  position: relative;
}

.btn-loading .btn-text {
  opacity: 0.7;
}

/* 块级按钮 */
.btn-block {
  width: 100%;
  display: flex;
}

/* 图标 */
.btn-icon {
  font-size: 1.2em;
  display: flex;
  align-items: center;
}

/* 加载旋转器 */
.btn-spinner {
  width: 1em;
  height: 1em;
  border: 2px solid currentColor;
  border-right-color: transparent;
  border-radius: 50%;
  animation: spin 0.6s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}
</style>
