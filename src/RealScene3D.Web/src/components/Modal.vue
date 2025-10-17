<template>
  <Teleport to="body">
    <Transition name="modal-fade">
      <div v-if="modelValue" class="modal-overlay" @click="handleOverlayClick">
        <Transition name="modal-scale">
          <div
            v-if="modelValue"
            class="modal-container"
            :class="[`modal-${size}`]"
            @click.stop
          >
            <!-- 模态框头部 -->
            <div class="modal-header">
              <h3 class="modal-title">
                <slot name="title">{{ title }}</slot>
              </h3>
              <button
                v-if="closable"
                class="modal-close"
                @click="handleClose"
                aria-label="关闭"
              >
                ✕
              </button>
            </div>

            <!-- 模态框内容 -->
            <div class="modal-body">
              <slot></slot>
            </div>

            <!-- 模态框底部 -->
            <div v-if="$slots.footer || showFooter" class="modal-footer">
              <slot name="footer">
                <button class="btn btn-secondary" @click="handleClose">
                  {{ cancelText }}
                </button>
                <button class="btn btn-primary" @click="handleConfirm">
                  {{ confirmText }}
                </button>
              </slot>
            </div>
          </div>
        </Transition>
      </div>
    </Transition>
  </Teleport>
</template>

<script setup lang="ts">
/**
 * 通用模态框组件
 *
 * 功能特性:
 * - 支持v-model双向绑定
 * - 多种尺寸选择 (sm, md, lg, xl)
 * - 可配置的关闭方式
 * - 精美的过渡动画
 * - 完全自定义的插槽系统
 *
 * @author liyq
 * @date 2025-10-15
 */

interface Props {
  modelValue: boolean
  title?: string
  size?: 'sm' | 'md' | 'lg' | 'xl'
  closable?: boolean
  closeOnOverlay?: boolean
  showFooter?: boolean
  confirmText?: string
  cancelText?: string
}

const props = withDefaults(defineProps<Props>(), {
  size: 'md',
  closable: true,
  closeOnOverlay: true,
  showFooter: true,
  confirmText: '确定',
  cancelText: '取消'
})

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  confirm: []
  close: []
}>()

const handleClose = () => {
  emit('update:modelValue', false)
  emit('close')
}

const handleConfirm = () => {
  emit('confirm')
}

const handleOverlayClick = () => {
  if (props.closeOnOverlay) {
    handleClose()
  }
}
</script>

<style scoped>
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(15, 23, 42, 0.6);
  backdrop-filter: blur(8px);
  -webkit-backdrop-filter: blur(8px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: var(--z-index-modal);
  padding: 1rem;
}

.modal-container {
  background: white;
  border-radius: var(--border-radius-lg);
  box-shadow: var(--shadow-2xl);
  border: 1px solid var(--border-color);
  max-height: 85vh;
  display: flex;
  flex-direction: column;
  position: relative;
}

.modal-sm {
  width: 400px;
  max-width: 90vw;
}

.modal-md {
  width: 600px;
  max-width: 90vw;
}

.modal-lg {
  width: 800px;
  max-width: 90vw;
}

.modal-xl {
  width: 1000px;
  max-width: 95vw;
}

.modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 1.5rem 2rem;
  border-bottom: 1px solid var(--border-color);
}

.modal-title {
  margin: 0;
  font-size: var(--font-size-xl);
  font-weight: 700;
  background: var(--gradient-primary-alt);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

.modal-close {
  width: 32px;
  height: 32px;
  border: none;
  background: var(--gray-100);
  color: var(--gray-600);
  border-radius: var(--border-radius);
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.25rem;
  transition: all var(--transition-base);
}

.modal-close:hover {
  background: var(--danger-light);
  color: var(--danger-color);
  transform: rotate(90deg);
}

.modal-body {
  flex: 1;
  padding: 2rem;
  overflow-y: auto;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
  padding: 1.5rem 2rem;
  border-top: 1px solid var(--border-color);
  background: var(--gray-50);
}

/* 通用按钮样式 */
.btn {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.6rem 1.4rem;
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius);
  background: white;
  cursor: pointer;
  transition: all var(--transition-base);
  font-size: var(--font-size-sm);
  font-weight: 600;
  position: relative;
  overflow: hidden;
}

.btn::before {
  content: '';
  position: absolute;
  inset: 0;
  opacity: 0;
  transition: opacity var(--transition-base);
  z-index: -1;
}

.btn:hover {
  transform: translateY(-2px);
  box-shadow: var(--shadow-md);
}

.btn-primary {
  background: var(--gradient-primary-alt);
  color: white;
  border: none;
  box-shadow: var(--shadow-colored);
}

.btn-primary::before {
  background: var(--gradient-info);
}

.btn-primary:hover {
  box-shadow: var(--shadow-xl);
}

.btn-primary:hover::before {
  opacity: 1;
}

.btn-secondary {
  background: var(--gray-100);
  color: var(--gray-700);
  border-color: var(--gray-300);
}

.btn-secondary:hover {
  background: var(--gray-200);
  border-color: var(--gray-400);
}

/* 过渡动画 */
.modal-fade-enter-active,
.modal-fade-leave-active {
  transition: opacity var(--transition-base);
}

.modal-fade-enter-from,
.modal-fade-leave-to {
  opacity: 0;
}

.modal-scale-enter-active {
  animation: modalScaleIn 0.3s cubic-bezier(0.68, -0.55, 0.265, 1.55);
}

.modal-scale-leave-active {
  animation: modalScaleOut 0.2s ease-in;
}

@keyframes modalScaleIn {
  from {
    opacity: 0;
    transform: scale(0.9) translateY(-20px);
  }
  to {
    opacity: 1;
    transform: scale(1) translateY(0);
  }
}

@keyframes modalScaleOut {
  from {
    opacity: 1;
    transform: scale(1);
  }
  to {
    opacity: 0;
    transform: scale(0.95);
  }
}

/* 响应式设计 */
@media (max-width: 768px) {
  .modal-container {
    max-height: 95vh;
  }

  .modal-header,
  .modal-body,
  .modal-footer {
    padding-left: 1.5rem;
    padding-right: 1.5rem;
  }
}
</style>
