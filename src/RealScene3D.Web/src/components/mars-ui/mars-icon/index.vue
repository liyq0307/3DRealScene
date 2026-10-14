<template>
  <span class="mars-icon" v-html="svgComponent"></span>
</template>
<script lang="ts">
import { computed, useAttrs, defineComponent } from "vue"
import * as svgModule from "@icon-park/svg"
import _ from "lodash"
export default defineComponent({
  name: "mars-icon",
  // inheritAttrs: false,
  props: {
    icon: {
      type: String
    },
    color: {
      type: String
    },
    width: {
      type: [String, Number],
      default: "14"
    }
  },
  setup(props) {
    const attrs = useAttrs()
    const iconName = computed(() => _.upperFirst(_.camelCase(props.icon)))

    const svgComponent = computed(() => {
      if (typeof svgModule[iconName.value] === "function") {
        return svgModule[iconName.value]({
          theme: "outline",
          fill: props.color,
          size: props.width,
          ...attrs
        })
      }
      return ""
    })

    return {
      attrs,
      svgComponent
    }
  }
})
</script>
<style lang="less" scoped>
.mars-icon {
  vertical-align: middle;
  line-height: 1;
}
</style>
