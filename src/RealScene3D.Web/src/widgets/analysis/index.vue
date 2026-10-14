<template>
  <div class="analysis-widget">
    <!-- Right panel: tools / history / viewpoints -->
    <div class="fx-container" :class="{ 'is-collapsed': isCollapsed }">
      <div class="tabs-wrapper">
        <a-tabs v-model:activeKey="activeKey" tab-position="right" @tab-click="tabClick">
          <a-tab-pane :key="index" v-for="(item, index) in toolsList">
            <template #tab>
              <div class="tab-item">
                <mars-icon :icon="item.icon" :width="16" />
                <span class="tab-text">{{ item.name }}</span>
              </div>
            </template>
          </a-tab-pane>
        </a-tabs>
        <div v-if="activeKey === 0">
          <!-- Tools -->
          <ToolsBox />
        </div>
        <div v-else-if="activeKey === 1">
          <!-- History -->
          <HistoryList />
        </div>
        <div v-else-if="activeKey === 2">
          <!-- Viewpoints -->
          <ViewpointsList />
        </div>
      </div>
      <div
        class="collapse-btn"
        :style="{ left: activeKey === -1 ? '-31px' : '-301px' }"
        @click="toggleCollapse"
      >
        <mars-icon :icon="isCollapsed ? 'arrow-left' : 'arrow-right'" :width="20" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onUnmounted, ref } from "vue"
import useLifecycle from "@mars/common/uses/use-lifecycle"
import * as mapWork from "./map"
import ToolsBox from "@mars/widgets/analysis/toolbox/index.vue"
import HistoryList from "@mars/widgets/analysis/history-list/index.vue"
import ViewpointsList from "@mars/widgets/analysis/viewpoints/index.vue"
import { useAnalysisStore } from "@mars/stores/analysis"

// Enable map lifecycle
useLifecycle(mapWork)

const store = useAnalysisStore()
const activeKey = ref(-1)
const isCollapsed = ref(false)
const toolsList = ref([
  { name: "工具", icon: "tool" },
  { name: "列表", icon: "list" },
  { name: "多视角", icon: "land-surveying" }
])

const toggleCollapse = () => {
  if (isCollapsed.value === false) {
    activeKey.value = -1
    if (mapWork.map) {
      mapWork.map.control.toolbar.container.style.right = "-4px"
      mapWork.map.control.compass.container.style.right = "5px"
    }
  } else {
    if (mapWork.map) {
      mapWork.map.control.toolbar.container.style.right = "32px"
      mapWork.map.control.compass.container.style.right = "38px"
    }
  }
  isCollapsed.value = !isCollapsed.value
}

const tabClick = (tab: any) => {
  if (mapWork.map) {
    mapWork.map.control.toolbar.container.style.right = "304px"
    mapWork.map.control.compass.container.style.right = "310px"
  }
}

onUnmounted(() => {
  console.log("analysis/index.vue unmounted")
  if (store.clearAllData) {
    store.clearAllData()
  }
})
</script>

<style lang="less" scoped>
.analysis-widget {
  position: absolute;
  inset: 0;
  pointer-events: none;
  z-index: 90;
}

.fx-container {
  position: absolute;
  right: 0px;
  top: 0;
  bottom: 28px;
  height: auto;
  display: flex;
  flex-direction: column;
  transition: transform 0.3s ease;
  pointer-events: auto;
  z-index: 100;

  &.is-collapsed {
    transform: translateX(100%);
  }

  .tabs-wrapper {
    flex: 1;
    background-color: #0f172a;
    border: 1px solid #334155;
    overflow: hidden;

    :deep(.ant-tabs) {
      height: 100%;
      color: #f1f5f9;

      .ant-tabs-nav {
        margin: 0;
        &::before {
          display: none;
        }
      }
      .ant-tabs-content-holder {
        display: none;
      }
      .ant-tabs-tab {
        padding: 12px 0;
        margin: 0;
        justify-content: center;
        background: transparent;
        border: none;

        &-active {
          background-color: #6366f1;
          transition: background-color 0.3s;
        }
      }

      .tab-item {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 4px;
        width: 34px;

        .tab-text {
          writing-mode: vertical-rl;
          letter-spacing: 2px;
          color: #e2e8f0;
        }
      }

      .ant-tabs-tab-active .tab-text {
        color: #ffffff;
      }
    }
  }

  .collapse-btn {
    position: absolute;
    bottom: 0px;
    width: 32px;
    height: 32px;
    background-color: #1e293b;
    border: 1px solid #334155;
    border-radius: 4px 0 0 4px;
    display: flex;
    align-items: center;
    justify-content: center;
    cursor: pointer;
    color: #e2e8f0;
    transition: left 0.3s ease;

    &:hover {
      color: #f1f5f9;
      background-color: #334155;
    }
  }
}
</style>
