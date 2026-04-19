<template>
  <div class="workflow-arrow" aria-hidden="true">
    <svg 
      width="40" 
      height="40" 
      viewBox="0 0 40 40" 
      fill="none" 
      xmlns="http://www.w3.org/2000/svg"
    >
      <path 
        d="M8 20H32M32 20L24 12M32 20L24 28" 
        stroke="#4FC3F7" 
        stroke-width="3" 
        stroke-linecap="round" 
        stroke-linejoin="round"
      />
    </svg>
  </div>
</template>

<script setup lang="ts">
// 无需props，纯展示组件
</script>

<style scoped>
.workflow-arrow {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  flex-shrink: 0;
  animation: pulse 2s ease-in-out infinite;
}

@keyframes pulse {
  0%, 100% {
    opacity: 1;
  }
  50% {
    opacity: 0.6;
  }
}

/* 响应式布局 */
@media (max-width: 1200px) {
  .workflow-arrow {
    width: 30px;
  }
}
</style>
