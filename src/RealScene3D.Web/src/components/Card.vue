<template>
  <div
    class="card"
    :class="[`card-${variant}`, { 'card-hoverable': hoverable, 'card-bordered': bordered }]"
  >
    <!-- 卡片头部 -->
    <div v-if="$slots.header || title" class="card-header">
      <slot name="header">
        <h3 class="card-title">{{ title }}</h3>
      </slot>
    </div>

    <!-- 卡片主体内容 -->
    <div class="card-body">
      <slot></slot>
    </div>

    <!-- 卡片底部 -->
    <div v-if="$slots.footer" class="card-footer">
      <slot name="footer"></slot>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * 通用卡片组件
 *
 * 功能特性:
 * - 支持多种变体样式 (default, primary, success, warning, danger)
 * - 可选的悬停效果
 * - 可配置边框显示
 * - 灵活的插槽系统 (header, default, footer)
 *
 * @author liyq
 * @date 2025-10-15
 */

interface Props {
  title?: string
  variant?: 'default' | 'primary' | 'success' | 'warning' | 'danger' | 'info'
  hoverable?: boolean
  bordered?: boolean
}

withDefaults(defineProps<Props>(), {
  variant: 'default',
  hoverable: true,
  bordered: true
})
</script>

<style scoped>
.card {
  background: white;
  border-radius: var(--border-radius-lg);
  padding: 1.5rem;
  transition: all var(--transition-base);
  position: relative;
  overflow: hidden;
}

.card-bordered {
  border: 1px solid var(--border-color);
  box-shadow: var(--shadow-sm);
}

.card-hoverable {
  cursor: pointer;
}

.card-hoverable:hover {
  transform: translateY(-4px);
  box-shadow: var(--shadow-lg);
}

/* 卡片变体样式 */
.card-default {
  background: white;
}

.card-primary::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 4px;
  background: var(--gradient-primary-alt);
}

.card-success::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 4px;
  background: var(--gradient-success);
}

.card-warning::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 4px;
  background: var(--gradient-warning);
}

.card-danger::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 4px;
  background: var(--gradient-danger);
}

.card-info::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 4px;
  background: var(--gradient-info);
}

/* 卡片头部 */
.card-header {
  padding-bottom: 1rem;
  margin-bottom: 1rem;
  border-bottom: 1px solid var(--border-color);
}

.card-title {
  margin: 0;
  font-size: var(--font-size-lg);
  font-weight: 700;
  color: var(--gray-900);
}

/* 卡片主体 */
.card-body {
  color: var(--gray-700);
  font-size: var(--font-size-base);
  line-height: 1.6;
}

/* 卡片底部 */
.card-footer {
  padding-top: 1rem;
  margin-top: 1rem;
  border-top: 1px solid var(--border-color);
  display: flex;
  gap: 0.75rem;
  align-items: center;
}
</style>
