<template>
  <div class="history-list">
    <div class="history-container">历史记录</div>
  </div>
</template>

<script setup lang="ts">
import { onMounted } from "vue"

onMounted(() => {
  console.log("history-list mounted")
})
</script>

<style lang="less" scoped>
.history-list {
  width: 100%;
  height: 100%;
  padding: 8px;
}
.history-container {
  color: #fff;
}
</style>
