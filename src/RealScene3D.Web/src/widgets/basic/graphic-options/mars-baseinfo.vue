<template>
  <div class="baseinfo-pannel">
    <a-row :gutter="[0, 10]">
      <template v-for="(item, i) in baseConfig" :key="i">
        <AttrComp
          :label="item.label"
          v-model:value="baseValue[item.name]"
          :type="item.type"
          size="small"
          :min="item.min || item.min === 0 ? item.min : -Infinity"
          :max="item.max || item.max === 0 ? item.max : Infinity"
          :step="item.step || 0.1"
          :options="item.data || []"
          @change="(target) => change(item, target?.target?.value ?? target)"
          :tofixed="item.toFixed"></AttrComp>
      </template>
    </a-row>
  </div>
</template>

<script lang="ts" setup>
import { ref, watch } from "vue"
import AttrComp from "./component/attr-comp.vue"
import baseConfig from "./config/base-info"

const props = defineProps<{
  data: any
}>()

const emit = defineEmits(["baseChange"])

const baseValue = ref({})

watch(
  () => props.data,
  () => {
    if (props.data) {
      baseValue.value = { ...props.data }

      baseConfig.forEach((item: any) => {
        baseValue.value[item.name] = baseValue.value[item.name] ?? getViewDefval(item, baseValue.value) // 数据中没有的地方使用默认值
      })
    }
  },
  {
    immediate: true
  }
)

const change = (item, value) => {
  emit("baseChange", { [item.name]: value })
}

function getViewDefval(config, options) {
  if (typeof config.defval === "function") {
    return config.defval(options, baseValue.value, props.data)
  } else {
    return config.defval
  }
}
</script>

<style lang="less" scoped>
.baseinfo-pannel {
  padding: 15px 15px 2px 0px;
}
</style>
