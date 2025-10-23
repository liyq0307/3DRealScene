<template>
  <div class="error-display" :class="[type]">
    <div class="error-icon">
      <span v-if="type === 'error'">❌</span>
      <span v-else-if="type === 'warning'">⚠️</span>
      <span v-else>ℹ️</span>
    </div>
    <div class="error-content">
      <h4 v-if="title" class="error-title">{{ title }}</h4>
      <p class="error-message">{{ message }}</p>
      <button v-if="retry" @click="handleRetry" class="retry-btn">
        重试
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
interface ErrorProps {
  type?: 'error' | 'warning' | 'info'
  title?: string
  message: string
  retry?: boolean
}

withDefaults(defineProps<ErrorProps>(), {
  type: 'error',
  title: '',
  retry: false
})

const emit = defineEmits<{
  retry: []
}>()

const handleRetry = () => {
  emit('retry')
}
</script>

<style scoped>
.error-display {
  display: flex;
  gap: 1rem;
  padding: 1.5rem;
  border-radius: 8px;
  background: white;
  border-left: 4px solid;
  margin: 1rem 0;
}

.error-display.error {
  border-color: #dc3545;
  background: #fff5f5;
}

.error-display.warning {
  border-color: #ffc107;
  background: #fffbf0;
}

.error-display.info {
  border-color: #007acc;
  background: #f0f8ff;
}

.error-icon {
  font-size: 1.5rem;
  flex-shrink: 0;
}

.error-content {
  flex: 1;
}

.error-title {
  margin: 0 0 0.5rem 0;
  font-size: 1rem;
  color: #333;
}

.error-message {
  margin: 0 0 1rem 0;
  color: #666;
  font-size: 0.9rem;
  line-height: 1.5;
}

.retry-btn {
  padding: 0.5rem 1rem;
  background: #007acc;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.85rem;
  transition: background 0.2s ease;
}

.retry-btn:hover {
  background: #005999;
}
</style>
