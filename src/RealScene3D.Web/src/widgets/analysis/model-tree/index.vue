<template>
  <div class="model-tree">
    <div class="model-container">模型树</div>
  </div>
</template>

<script setup lang="ts">
import { onMounted } from "vue"

onMounted(() => {
  console.log("model-tree mounted")
})
</script>

<style lang="less" scoped>
.model-tree {
  width: 100%;
  height: 100%;
  padding: 8px;
}
.model-container {
  color: #fff;
}
</style>
