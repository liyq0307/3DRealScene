<template>
  <div :class="['skeleton-card']">
    <!-- 头部骨架 -->
    <div v-if="header" class="skeleton-card-header">
      <Skeleton v-if="avatar" variant="circle" width="40px" />
      <div class="skeleton-card-header-content">
        <Skeleton variant="text" width="60%" />
        <Skeleton variant="text" width="40%" height="0.8em" />
      </div>
    </div>
    
    <!-- 内容骨架 -->
    <div class="skeleton-card-content">
      <Skeleton 
        v-for="i in rows" 
        :key="i" 
        variant="text" 
        :width="i === rows ? '70%' : '100%'" 
      />
    </div>
    
    <!-- 底部骨架 -->
    <div v-if="footer" class="skeleton-card-footer">
      <Skeleton variant="rect" width="80px" height="32px" />
      <Skeleton variant="rect" width="80px" height="32px" />
    </div>
  </div>
</template>

<script setup lang="ts">
import Skeleton from './Skeleton.vue'

interface Props {
  header?: boolean
  avatar?: boolean
  rows?: number
  footer?: boolean
}

withDefaults(defineProps<Props>(), {
  header: true,
  avatar: true,
  rows: 3,
  footer: true
})
</script>

<style scoped>
.skeleton-card {
  background: var(--glass-bg);
  backdrop-filter: blur(var(--glass-blur));
  border: 1px solid var(--glass-border);
  border-radius: var(--radius-xl);
  padding: var(--spacing-lg);
  box-shadow: var(--shadow-md);
}

.skeleton-card-header {
  display: flex;
  align-items: center;
  gap: var(--spacing-md);
  margin-bottom: var(--spacing-md);
}

.skeleton-card-header-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.skeleton-card-content {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-bottom: var(--spacing-md);
}

.skeleton-card-footer {
  display: flex;
  gap: var(--spacing-sm);
  justify-content: flex-end;
}
</style>
