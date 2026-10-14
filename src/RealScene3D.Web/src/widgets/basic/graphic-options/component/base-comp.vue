<template>
  <mars-input v-if="props.type === 'label'" readonly>{{ attrs.value || "" }}</mars-input>
  <component v-else :is="getComponent(props.type as keyof typeof components)" size="small" v-bind="attrs"> </component>
</template>
<script setup lang="ts">
import { useAttrs } from "vue"
const props = withDefaults(
  defineProps<{
    type: string
  }>(),
  {
    type: "label"
  }
)
const attrs = useAttrs()

const components = {
  number: "mars-input-number",
  input: "mars-input",
  radio: "mars-switch",
  slider: "mars-slider",
  color: "mars-color-picker",
  combobox: "mars-select",
  textarea: "mars-textarea"
}
function getComponent(type: keyof typeof components) {
  return components[type]
}
</script>
