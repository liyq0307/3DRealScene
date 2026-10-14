<template>
  <div class="viewpoints-list">
    <div class="viewpoints-container">多视角</div>
  </div>
</template>

<script setup lang="ts">
import { onMounted } from "vue"

onMounted(() => {
  console.log("viewpoints mounted")
})
</script>

<style lang="less" scoped>
.viewpoints-list {
  width: 100%;
  height: 100%;
  padding: 8px;
}
.viewpoints-container {
  color: #fff;
}
</style>
