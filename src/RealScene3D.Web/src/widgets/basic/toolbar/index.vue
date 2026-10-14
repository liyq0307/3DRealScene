<template>
  <mars-dialog :draggable="false" customClass="base-pannel" right="10" top="10">
    <template v-for="(item, i) in data" :key="i">
      <div
        v-if="item.widget && !item.children"
        :class="['toolbar-item', activeTools.includes(item.widget) ? 'active' : '']"
        @mouseenter="enterWidget(item.widget ?? i)"
        @mouseleave="leaveWidget(item.widget ?? i)"
        @click="showWidget(item.widget)"
      >
        <mars-icon v-if="item.icon" :icon="item.icon" width="18"></mars-icon>
        <span class="title">{{ item.name }}</span>
      </div>

      <mars-dropdown-menu v-if="item.children && !item.widget" trigger="hover" placement="bottomRight">
        <div
          :class="['toolbar-item', isIncludesMenu(item.children, i) ? 'active' : '']"
          @mouseenter="enterWidget(item.widget ?? i)"
          @mouseleave="leaveWidget(item.widget ?? i)"
        >
          <mars-icon v-if="item.icon" :icon="item.icon" width="18"></mars-icon>
          <span class="title">{{ item.name }}</span>
        </div>
        <template #overlay>
          <a-menu @click="(d) => clickMenu(d, i)" @mouseenter="enterWidget(item.widget ?? i)" @mouseleave="leaveWidget(item.widget ?? i)">
            <a-menu-item v-for="child in item.children" :key="child.widget" :title="child.name">
              <mars-icon :icon="child.icon" width="18"></mars-icon>
              <span>{{ child.name }}</span>
            </a-menu-item>
          </a-menu>
        </template>
      </mars-dropdown-menu>
    </template>
  </mars-dialog>
</template>

<script setup lang="ts">
/**
 * 导航菜单按钮 (右上角)
 */
import { ref } from "vue"
import { useWidget } from "@mars/common/store/widget"

const { currentWidget, activate, isActivate } = useWidget()

currentWidget.onUpdate((widget: string | any) => {
  setTimeout(() => {
    if (widget && typeof widget === "string") {
      leaveWidget(widget)
    } else if (widget.enter) {
      enterWidget(widget.name)
    }
  }, 5)
})

const data: any = [
  { name: "底图", icon: "international", widget: "manage-basemap" },
  {
    name: "工具",
    icon: "tool",
    children: [
      { name: "坐标定位", icon: "local", widget: "location-point" }
    ]
  }
]

const activeTools = ref<any[]>([])

const isIncludesMenu = (menu: any, index: any) => {
  if (menu.filter((child: any) => activeTools.value.includes(child.widget) || activeTools.value.includes(index))?.length) {
    return true
  }
  return false
}

const enterWidget = (widget: string | number) => {
  activeTools.value = activeTools.value.filter((item) => item !== widget)
  activeTools.value.push(widget)
}

const leaveWidget = (widget: string | number) => {
  activeTools.value.forEach((tool, index) => {
    if (typeof tool === "string") {
      if (isActivate(tool)) {
        return
      } else if (tool !== widget) {
        activeTools.value.splice(index, 1)
      }
    }
    if (tool === widget) {
      activeTools.value.splice(index, 1)
    }
  })
}

const showWidget = (widget: string) => {
  activate(widget)
}

const clickMenu = ({ key }: any, index: any) => {
  leaveWidget(index)
  enterWidget(key)
  activate(key)
}
</script>

<style lang="less">
.base-pannel {
  height: 38px;
  background: var(--mars-poi-border) !important;

  .mars-dialog__content {
    padding: 0 !important;
    background-image: none !important;
    overflow: hidden !important;
    border: 1px solid;
    border: none;
    background-color: var(--mars-bg-base);
    height: 40px;
  }

  .toolbar-item {
    width: 85px;
    text-align: center;
    display: inline-flex;
    justify-content: center;
    align-items: center;
    height: 100%;
    color: var(--mars-text-color);
    font-family: D-DIN;
    font-size: 14px;
    cursor: pointer;
    position: relative;

    .title { padding-left: 10px; padding-bottom: 3px; }
    &:first-child { border-top-left-radius: 4px; border-bottom-left-radius: 4px; }
    &:last-child { border-top-right-radius: 4px; border-bottom-right-radius: 4px; }
  }

  .ant-dropdown-open { .active(); }

  .active {
    background-color: var(--mars-hover-color);
    color: #ffffff;
  }
}
</style>
