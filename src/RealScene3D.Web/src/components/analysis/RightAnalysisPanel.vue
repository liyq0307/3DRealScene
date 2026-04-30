<template>
  <div class="fx-container" :class="{ 'is-collapsed': toolbox.isCollapsed }">
    <div class="tabs-wrapper">
      <div class="tab-bar">
        <div
          v-for="(tab, index) in tabs"
          :key="tab.key"
          class="tab-item"
          :class="{ active: toolbox.activeTab === tab.key }"
          @click="handleTabClick(tab.key)"
        >
          <svg class="tab-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" v-html="tab.svgPath" />
          <span class="tab-text">{{ tab.label }}</span>
        </div>
      </div>
      <div v-if="toolbox.activeTab === 'tools'" class="tab-content">
        <AnalysisToolbox
          :viewer-instance="viewerInstance"
          :scene-objects="sceneObjects"
        />
      </div>
      <div v-else-if="toolbox.activeTab === 'history'" class="tab-content">
        <AnalysisHistory />
      </div>
    </div>
    <div
      class="collapse-btn"
      @click="toggleCollapse"
    >
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <polyline :points="toolbox.isCollapsed ? '9 18 15 12 9 6' : '15 18 9 12 15 6'" />
      </svg>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useToolboxStore } from '@/stores/toolbox'
import AnalysisToolbox from './AnalysisToolbox.vue'
import AnalysisHistory from './AnalysisHistory.vue'

defineProps<{
  viewerInstance?: any
  sceneObjects?: any[]
}>()

const toolbox = useToolboxStore()

const tabs = [
  {
    key: 'tools' as const,
    label: '工具',
    svgPath: '<path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z"/>'
  },
  {
    key: 'history' as const,
    label: '列表',
    svgPath: '<line x1="8" y1="6" x2="21" y2="6"/><line x1="8" y1="12" x2="21" y2="12"/><line x1="8" y1="18" x2="21" y2="18"/><line x1="3" y1="6" x2="3.01" y2="6"/><line x1="3" y1="12" x2="3.01" y2="12"/><line x1="3" y1="18" x2="3.01" y2="18"/>'
  }
]

function handleTabClick(key: 'tools' | 'history') {
  if (toolbox.isCollapsed) {
    toolbox.isCollapsed = false
  }
  toolbox.setActiveTab(key)
}

function toggleCollapse() {
  if (!toolbox.isCollapsed) {
    toolbox.activeTab = null
    toolbox.toggleCollapse()
  } else {
    toolbox.isCollapsed = false
    if (!toolbox.activeTab) {
      toolbox.activeTab = 'tools'
    }
  }
}
</script>

<style scoped>
.fx-container {
  position: absolute;
  right: 0;
  top: 0;
  bottom: 0;
  display: flex;
  flex-direction: column;
  transition: transform 0.3s ease;
  z-index: 100;
}

.fx-container.is-collapsed {
  transform: translateX(calc(100% - 34px));
}

.tabs-wrapper {
  flex: 1;
  display: flex;
  background: #12122b;
  border-left: 1px solid rgba(255, 255, 255, 0.08);
  overflow: hidden;
}

.tab-bar {
  width: 34px;
  min-width: 34px;
  display: flex;
  flex-direction: column;
  background: #12122b;
}

.tab-item {
  padding: 12px 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  cursor: pointer;
  transition: background-color 0.2s ease;
}

.tab-item:hover {
  background: rgba(99, 102, 241, 0.1);
}

.tab-item.active {
  background: rgba(99, 102, 241, 0.3);
}

.tab-icon {
  width: 16px;
  height: 16px;
  color: rgba(255, 255, 255, 0.6);
}

.tab-item.active .tab-icon {
  color: #fff;
}

.tab-text {
  writing-mode: vertical-rl;
  letter-spacing: 2px;
  font-size: 11px;
  color: rgba(255, 255, 255, 0.6);
}

.tab-item.active .tab-text {
  color: #fff;
}

.tab-content {
  flex: 1;
  min-width: 0;
  overflow: hidden;
}

.collapse-btn {
  position: absolute;
  bottom: 0;
  right: 34px;
  width: 32px;
  height: 32px;
  background: #12122b;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 4px 0 0 4px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  z-index: 10;
}

.collapse-btn svg {
  width: 16px;
  height: 16px;
  color: rgba(255, 255, 255, 0.6);
}

.collapse-btn:hover svg {
  color: #6366f1;
}
</style>
