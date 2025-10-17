<template>
  <teleport to="body">
    <div class="message-container">
      <transition-group name="message">
        <div
          v-for="message in messages"
          :key="message.id"
          :class="['message', `message-${message.type}`]"
          @click="removeMessage(message.id)"
        >
          <span class="message-icon">{{ getIcon(message.type) }}</span>
          <span class="message-content">{{ message.content }}</span>
          <button class="message-close" @click.stop="removeMessage(message.id)">
            ✕
          </button>
        </div>
      </transition-group>
    </div>
  </teleport>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import messageService, { type MessageType } from '@/composables/useMessage'

const messages = computed(() => messageService.messages.value)

const removeMessage = (id: string) => {
  messageService.removeMessage(id)
}

const getIcon = (type: MessageType): string => {
  const iconMap: Record<MessageType, string> = {
    success: '✓',
    error: '✗',
    warning: '⚠',
    info: 'ℹ'
  }
  return iconMap[type]
}
</script>

<style scoped>
.message-container {
  position: fixed;
  top: 20px;
  right: 20px;
  z-index: 9999;
  display: flex;
  flex-direction: column;
  gap: 10px;
  pointer-events: none;
}

.message {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 300px;
  max-width: 500px;
  padding: 12px 16px;
  background: white;
  border-radius: 8px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  pointer-events: all;
  cursor: pointer;
  transition: all 0.3s ease;
}

.message:hover {
  transform: translateX(-5px);
  box-shadow: 0 6px 16px rgba(0, 0, 0, 0.2);
}

.message-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  font-size: 14px;
  font-weight: bold;
  flex-shrink: 0;
}

.message-content {
  flex: 1;
  font-size: 14px;
  line-height: 1.5;
  color: #333;
}

.message-close {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 20px;
  height: 20px;
  border: none;
  background: none;
  color: #999;
  cursor: pointer;
  font-size: 16px;
  line-height: 1;
  padding: 0;
  transition: color 0.2s ease;
}

.message-close:hover {
  color: #333;
}

/* 消息类型样式 */
.message-success {
  border-left: 4px solid #4caf50;
}

.message-success .message-icon {
  background: #e8f5e9;
  color: #4caf50;
}

.message-error {
  border-left: 4px solid #f44336;
}

.message-error .message-icon {
  background: #ffebee;
  color: #f44336;
}

.message-warning {
  border-left: 4px solid #ff9800;
}

.message-warning .message-icon {
  background: #fff3e0;
  color: #ff9800;
}

.message-info {
  border-left: 4px solid #2196f3;
}

.message-info .message-icon {
  background: #e3f2fd;
  color: #2196f3;
}

/* 动画效果 */
.message-enter-active,
.message-leave-active {
  transition: all 0.3s ease;
}

.message-enter-from {
  opacity: 0;
  transform: translateX(100%);
}

.message-leave-to {
  opacity: 0;
  transform: translateX(100%);
}

.message-move {
  transition: transform 0.3s ease;
}
</style>
