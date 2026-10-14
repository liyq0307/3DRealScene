<template>
  <a-col :span="8" class="attr-label">{{ $props.label }}:</a-col>
  <a-col :span="16" class="attr-value">
    <slot v-if="slots.right" name="right"></slot>
    <template v-else>
      <BaseComp :type="getBindValue.type" v-bind="getBindValue"></BaseComp>
    </template>
  </a-col>
</template>

<script lang="ts">
import { useAttrs, useSlots, computed, unref } from "vue"
import BaseComp from "./base-comp.vue"

export default {
  inheritAttrs: false, // 禁用属性继承
  props: ["label"], // 声明 props
  components: { BaseComp },
  setup: () => {
    const slots = useSlots()
    const attrs = useAttrs()
    const getBindValue = computed(() => { return { ...unref(attrs), type: attrs.type as string } })

    return { getBindValue, slots }
  }
}
</script>

<style lang="less" scoped>
.attr-label {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  text-align: right;
  padding-right: 8px;
  white-space: nowrap;
  word-break: keep-all;
}

.attr-value {
  display: flex;
  align-items: center;
}
</style>
