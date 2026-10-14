<template>
  <component :is="widget.component" v-bind="getWidgetAttr(widget)" />
</template>
<script lang="ts">
import { useAttrs, defineComponent, provide } from "vue"
import type { Widget } from "@mars/common/store/widget"

export default defineComponent({
  name: "mars-widget",
  props: {
    widget: {
      type: Object as () => Widget,
      required: true
    },
    visible: Boolean
  },
  emits: ["update:visible"],
  setup(props) {
    const attrs = useAttrs()

    provide("getCurrentWidget", () => {
      return props.widget.name
    })

    const getWidgetAttr = (widget: Widget) => {
      let attr: Record<string, any> = {}
      if (widget.meta && widget.meta.props) {
        attr = {
          ...attr,
          ...widget.meta.props
        }
      }
      if (widget.data && widget.data.props) {
        attr = {
          ...attr,
          ...widget.data.props
        }
      }
      // visible 是当前组件自己的属性,不需要转发给子组件
      return attr
    }

    return {
      attrs,
      getWidgetAttr
    }
  }
})
</script>
