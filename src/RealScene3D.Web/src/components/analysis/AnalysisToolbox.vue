<template>
  <div class="analysis-toolbox">
    <div class="toolbox-header">
      <h3>可视化分析</h3>
      <button @click="$emit('close')" class="btn-close" title="关闭">×</button>
    </div>

    <div class="toolbox-tabs">
      <button
        @click="activeTab = 'tools'"
        :class="['tab-btn', { active: activeTab === 'tools' }]"
      >
        工具
      </button>
      <button
        @click="activeTab = 'history'"
        :class="['tab-btn', { active: activeTab === 'history' }]"
      >
        历史
      </button>
    </div>

    <div class="toolbox-content">
      <div v-show="activeTab === 'tools'">
        <div class="tool-categories">
          <div
            v-for="category in toolCategories"
            :key="category.name"
            class="category"
          >
            <div class="category-title">{{ category.name }}</div>
            <div class="tool-grid">
              <button
                v-for="tool in category.tools"
                :key="tool.key"
                @click="selectTool(tool.key)"
                :class="['tool-button', { active: currentTool === tool.key }]"
                :title="tool.description || tool.name"
              >
                <span class="tool-icon">{{ tool.icon }}</span>
                <span class="tool-name">{{ tool.name }}</span>
              </button>
            </div>
          </div>
        </div>

        <div v-if="currentTool" class="tool-panel">
          <component
            :is="currentToolComponent"
            v-if="currentToolComponent"
            :viewer-instance="viewerInstance"
            :scene-objects="sceneObjects"
            @close="currentTool = null"
          />
        </div>
      </div>

      <div v-show="activeTab === 'history'">
        <AnalysisHistory />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, defineAsyncComponent } from 'vue'
import AnalysisHistory from './AnalysisHistory.vue'
import { TOOL_CATEGORIES, TOOL_COMPONENT_MAP } from '@/types/analysis'
import type { AnalysisToolType } from '@/types/analysis'

defineProps<{
  viewerInstance?: any
  sceneObjects?: any[]
}>()

defineEmits<{
  close: []
}>()

const activeTab = ref<'tools' | 'history'>('tools')
const currentTool = ref<AnalysisToolType | null>(null)

const toolCategories = TOOL_CATEGORIES

const componentMap: Partial<Record<AnalysisToolType, any>> = {
  // 基础工具
  'coordinate': defineAsyncComponent(() => import('./CoordinateLocation.vue')),
  'flatten': defineAsyncComponent(() => import('./FlattenTerrain.vue')),
  'map-marking': defineAsyncComponent(() => import('./MapMarking.vue')),
  'viewpoint': defineAsyncComponent(() => import('./ViewpointManager.vue')),
  // 测量工具
  'distance': defineAsyncComponent(() => import('./DistanceMeasurement.vue')),
  'distance-surface': defineAsyncComponent(() => import('./DistanceSurfaceMeasurement.vue')),
  'height': defineAsyncComponent(() => import('./HeightMeasurement.vue')),
  'area': defineAsyncComponent(() => import('./AreaMeasurement.vue')),
  'area-surface': defineAsyncComponent(() => import('./AreaSurfaceMeasurement.vue')),
  'coordinate-measure': defineAsyncComponent(() => import('./CoordinateMeasure.vue')),
  'height-triangle': defineAsyncComponent(() => import('./TriangleMeasurement.vue')),
  'bearing': defineAsyncComponent(() => import('./BearingMeasurement.vue')),
  // 空间分析
  'visibility': defineAsyncComponent(() => import('./VisibilityAnalysis.vue')),
  'viewshed': defineAsyncComponent(() => import('./ViewshedAnalysis.vue')),
  'profile': defineAsyncComponent(() => import('./ProfileAnalysis.vue')),
  'skyline': defineAsyncComponent(() => import('./SkylineAnalysis.vue')),
  'business-format': defineAsyncComponent(() => import('./BusinessFormatAnalysis.vue')),
  'building-spacing': defineAsyncComponent(() => import('./BuildingSpacingAnalysis.vue')),
  // 环境分析
  'sun': defineAsyncComponent(() => import('./SunAnalysis.vue')),
  'flood': defineAsyncComponent(() => import('./VolumeCalculation.vue')),
  // 规划分析
  'plot-ratio': defineAsyncComponent(() => import('./PlotRatioAnalysis.vue')),
  'building-layout': defineAsyncComponent(() => import('./BuildingLayoutAnalysis.vue')),
  'site-selection': defineAsyncComponent(() => import('./SiteSelection.vue')),
  'tower-foundation': defineAsyncComponent(() => import('./TowerFoundationModeling.vue')),
  'pipeline': defineAsyncComponent(() => import('./PipelineAnalysis.vue')),
  'constraint': defineAsyncComponent(() => import('./ConstraintAnalysis.vue')),
  // 工程分析
  'volume': defineAsyncComponent(() => import('./VolumeCalculation.vue')),
  'contour': defineAsyncComponent(() => import('./ContourLineAnalysis.vue')),
  // 对比工具
  'layer-comparison': defineAsyncComponent(() => import('./LayerComparison.vue')),
  // 性能监控
  'performance': defineAsyncComponent(() => import('./PerformanceAnalysis.vue'))
}

const currentToolComponent = computed(() => {
  if (!currentTool.value) return null
  return componentMap[currentTool.value]
})

function selectTool(key: AnalysisToolType) {
  currentTool.value = currentTool.value === key ? null : key
}
</script>

<style scoped>
.analysis-toolbox {
  position: fixed;
  right: 10px;
  top: 60px;
  width: 320px;
  background: rgba(15, 15, 20, 0.95);
  backdrop-filter: blur(10px);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.4);
  z-index: 1000;
  display: flex;
  flex-direction: column;
  max-height: calc(100vh - 80px);
}

.toolbox-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.6rem 0.8rem;
  background: rgba(99, 102, 241, 0.15);
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px 8px 0 0;
}

.toolbox-header h3 {
  margin: 0;
  font-size: 0.9rem;
  font-weight: 600;
  color: #a5b4fc;
}

.btn-close {
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(255, 255, 255, 0.1);
  border: none;
  border-radius: 4px;
  color: white;
  font-size: 1.2rem;
  cursor: pointer;
  transition: all 0.2s ease;
}

.btn-close:hover {
  background: rgba(239, 68, 68, 0.3);
}

.toolbox-tabs {
  display: flex;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.tab-btn {
  flex: 1;
  padding: 0.5rem;
  background: transparent;
  border: none;
  color: rgba(255, 255, 255, 0.6);
  font-size: 0.8rem;
  cursor: pointer;
  transition: all 0.2s ease;
}

.tab-btn:hover {
  color: rgba(255, 255, 255, 0.9);
}

.tab-btn.active {
  color: #a5b4fc;
  background: rgba(99, 102, 241, 0.1);
  border-bottom: 2px solid #a5b4fc;
}

.toolbox-content {
  flex: 1;
  overflow-y: auto;
  padding: 0.6rem;
}

.tool-categories {
  margin-bottom: 0.6rem;
}

.category {
  margin-bottom: 0.8rem;
}

.category-title {
  font-size: 0.7rem;
  color: rgba(255, 255, 255, 0.5);
  margin-bottom: 0.4rem;
  padding-left: 0.2rem;
}

.tool-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 0.4rem;
}

.tool-button {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.3rem;
  padding: 0.5rem;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.tool-button:hover {
  background: rgba(99, 102, 241, 0.15);
  border-color: rgba(99, 102, 241, 0.3);
  transform: translateY(-2px);
}

.tool-button.active {
  background: rgba(99, 102, 241, 0.25);
  border-color: rgba(99, 102, 241, 0.5);
}

.tool-icon {
  font-size: 1.3rem;
}

.tool-name {
  font-size: 0.7rem;
  color: rgba(255, 255, 255, 0.9);
  text-align: center;
}

.tool-panel {
  border-top: 1px solid rgba(255, 255, 255, 0.1);
  padding-top: 0.6rem;
}
</style>
