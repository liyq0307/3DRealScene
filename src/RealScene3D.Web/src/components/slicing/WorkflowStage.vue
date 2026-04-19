<template>
  <div 
    :class="['workflow-stage', `stage-${position}`]"
    role="region"
    :aria-label="`${title}配置区域`"
  >
    <div class="stage-header">
      <span v-if="icon" class="stage-icon">{{ icon }}</span>
      <h3 class="stage-title">{{ title }}</h3>
    </div>
    <div class="stage-content">
      <slot></slot>
    </div>
  </div>
</template>

<script setup lang="ts">
interface Props {
  title: string
  icon?: string
  position: 'left' | 'center' | 'right'
}

withDefaults(defineProps<Props>(), {
  icon: ''
})
</script>

<style scoped>
.workflow-stage {
  flex: 1;
  background: white;
  border: 1px solid #e1e5e9;
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
  padding: 1rem;
  display: flex;
  flex-direction: column;
  min-width: 280px;
  transition: transform 0.2s ease, box-shadow 0.2s ease;
}

.workflow-stage:hover {
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
}

.stage-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 1rem;
  padding-bottom: 0.75rem;
  border-bottom: 2px solid #007acc;
}

.stage-icon {
  font-size: 1.25rem;
}

.stage-title {
  margin: 0;
  font-size: 1rem;
  font-weight: 600;
  color: #007acc;
  flex: 1;
}

.stage-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

/* 响应式布局 */
@media (max-width: 1200px) {
  .workflow-stage {
    min-width: 240px;
  }
}
</style>
