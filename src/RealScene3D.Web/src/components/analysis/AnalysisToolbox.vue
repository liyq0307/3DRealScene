<template>
  <div class="analysis-toolbox">
    <div class="toolbox-content">
      <div
        v-for="category in toolCategories"
        :key="category.category"
        class="module"
      >
        <div
          class="module-header"
          @click="toolbox.toggleCategory(category.category)"
        >
          <span class="module-title">{{ category.name }}</span>
          <span class="module-arrow" :class="{ expanded: toolbox.expandedCategories[category.category] }">▾</span>
        </div>

        <div class="module-body" v-show="toolbox.expandedCategories[category.category]">
          <div class="button-grid">
            <button
              v-for="tool in category.tools"
              :key="tool.key"
              @click="handleToolClick(category.category, tool.key)"
              :class="['tool-button', { active: toolbox.activeToolByCategory[category.category] === tool.key }]"
              :title="tool.description || tool.name"
            >
              <span class="tool-icon">{{ tool.icon }}</span>
              <span class="tool-name">{{ tool.name }}</span>
            </button>
          </div>

          <div
            v-if="toolbox.activeToolByCategory[category.category]"
            class="component-container"
          >
            <component
              :is="getComponent(toolbox.activeToolByCategory[category.category]!)"
              v-if="getComponent(toolbox.activeToolByCategory[category.category]!)"
              :viewer-instance="viewerInstance"
              :scene-objects="sceneObjects"
              @close="toolbox.setActiveTool(category.category, null)"
            />
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { defineAsyncComponent } from 'vue'
import { useToolboxStore } from '@/stores/toolbox'
import { TOOL_CATEGORIES } from '@/types/analysis'
import type { AnalysisToolType, AnalysisCategory } from '@/types/analysis'

defineProps<{
  viewerInstance?: any
  sceneObjects?: any[]
}>()

const toolbox = useToolboxStore()
const toolCategories = TOOL_CATEGORIES

const componentMap: Partial<Record<AnalysisToolType, any>> = {
  'coordinate': defineAsyncComponent(() => import('./CoordinateLocation.vue')),
  'flatten': defineAsyncComponent(() => import('./FlattenTerrain.vue')),
  'map-marking': defineAsyncComponent(() => import('./MapMarking.vue')),
  'viewpoint': defineAsyncComponent(() => import('./ViewpointManager.vue')),
  'distance': defineAsyncComponent(() => import('./DistanceMeasurement.vue')),
  'distance-surface': defineAsyncComponent(() => import('./DistanceSurfaceMeasurement.vue')),
  'height': defineAsyncComponent(() => import('./HeightMeasurement.vue')),
  'area': defineAsyncComponent(() => import('./AreaMeasurement.vue')),
  'area-surface': defineAsyncComponent(() => import('./AreaSurfaceMeasurement.vue')),
  'coordinate-measure': defineAsyncComponent(() => import('./CoordinateMeasure.vue')),
  'height-triangle': defineAsyncComponent(() => import('./TriangleMeasurement.vue')),
  'bearing': defineAsyncComponent(() => import('./BearingMeasurement.vue')),
  'visibility': defineAsyncComponent(() => import('./VisibilityAnalysis.vue')),
  'viewshed': defineAsyncComponent(() => import('./ViewshedAnalysis.vue')),
  'profile': defineAsyncComponent(() => import('./ProfileAnalysis.vue')),
  'skyline': defineAsyncComponent(() => import('./SkylineAnalysis.vue')),
  'business-format': defineAsyncComponent(() => import('./BusinessFormatAnalysis.vue')),
  'building-spacing': defineAsyncComponent(() => import('./BuildingSpacingAnalysis.vue')),
  'sun': defineAsyncComponent(() => import('./SunAnalysis.vue')),
  'flood': defineAsyncComponent(() => import('./VolumeCalculation.vue')),
  'plot-ratio': defineAsyncComponent(() => import('./PlotRatioAnalysis.vue')),
  'building-layout': defineAsyncComponent(() => import('./BuildingLayoutAnalysis.vue')),
  'site-selection': defineAsyncComponent(() => import('./SiteSelection.vue')),
  'tower-foundation': defineAsyncComponent(() => import('./TowerFoundationModeling.vue')),
  'pipeline': defineAsyncComponent(() => import('./PipelineAnalysis.vue')),
  'constraint': defineAsyncComponent(() => import('./ConstraintAnalysis.vue')),
  'volume': defineAsyncComponent(() => import('./VolumeCalculation.vue')),
  'contour': defineAsyncComponent(() => import('./ContourLineAnalysis.vue')),
  'layer-comparison': defineAsyncComponent(() => import('./LayerComparison.vue')),
  'performance': defineAsyncComponent(() => import('./PerformanceAnalysis.vue'))
}

function getComponent(toolType: AnalysisToolType) {
  return componentMap[toolType] || null
}

function handleToolClick(category: AnalysisCategory, toolKey: AnalysisToolType) {
  toolbox.setActiveTool(category, toolKey)
}
</script>

<style scoped>
.analysis-toolbox {
  width: 260px;
  background: #0a0a14;
  display: flex;
  flex-direction: column;
  height: 100%;
  border-left: 1px solid rgba(255, 255, 255, 0.04);
}

.toolbox-content {
  flex: 1;
  overflow-y: auto;
  padding: 0;
}

.toolbox-content::-webkit-scrollbar {
  width: 3px;
}

.toolbox-content::-webkit-scrollbar-track {
  background: transparent;
}

.toolbox-content::-webkit-scrollbar-thumb {
  background: rgba(255, 255, 255, 0.1);
  border-radius: 2px;
}

.module {
  border-bottom: 1px solid rgba(255, 255, 255, 0.03);
}

.module-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 7px 12px;
  background: rgba(18, 18, 43, 0.6);
  cursor: pointer;
  user-select: none;
  transition: background 0.2s ease;
}

.module-header:hover {
  background: rgba(18, 18, 43, 0.9);
}

.module-title {
  font-size: 12px;
  font-weight: 500;
  color: rgba(255, 255, 255, 0.7);
  letter-spacing: 0.5px;
}

.module-arrow {
  font-size: 10px;
  color: rgba(255, 255, 255, 0.3);
  transition: transform 0.25s ease;
  display: inline-block;
}

.module-arrow.expanded {
  transform: rotate(180deg);
}

.module-body {
  padding: 10px 8px;
}

.button-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 8px;
}

.tool-button {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  padding: 0;
  background: #1a1a30;
  border: 1px solid rgba(255, 255, 255, 0.06);
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
  min-height: 56px;
  position: relative;
  overflow: hidden;
}

.tool-button::before {
  content: '';
  position: absolute;
  inset: 0;
  border-radius: 6px;
  opacity: 0;
  transition: opacity 0.2s ease;
  background: radial-gradient(circle at center, rgba(99, 102, 241, 0.35) 0%, transparent 70%);
}

.tool-button:hover {
  border-color: rgba(99, 102, 241, 0.2);
}

.tool-button:hover::before {
  opacity: 0.5;
}

.tool-button.active {
  background: rgba(99, 102, 241, 0.2);
  border-color: rgba(99, 102, 241, 0.5);
  box-shadow: 0 0 10px rgba(99, 102, 241, 0.3), inset 0 0 12px rgba(99, 102, 241, 0.15);
}

.tool-button.active::before {
  opacity: 1;
}

.tool-icon {
  font-size: 1.2rem;
  line-height: 1;
  margin-top: 8px;
  position: relative;
  z-index: 1;
}

.tool-name {
  font-size: 10px;
  color: rgba(255, 255, 255, 0.75);
  text-align: center;
  line-height: 1.2;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 100%;
  padding: 0 2px 6px;
  position: relative;
  z-index: 1;
}

.tool-button.active .tool-name {
  color: #fff;
}

.component-container {
  margin-top: 8px;
  padding: 8px;
  background: rgba(10, 10, 20, 0.8);
  border-radius: 6px;
  border: 1px solid rgba(99, 102, 241, 0.12);
}
</style>
