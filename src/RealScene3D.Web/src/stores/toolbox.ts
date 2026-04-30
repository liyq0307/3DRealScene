import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { AnalysisToolType, AnalysisCategory } from '@/types/analysis'

export const useToolboxStore = defineStore('toolbox', () => {
  const isVisible = ref(true)
  const isCollapsed = ref(true)
  const activeTab = ref<'tools' | 'history' | null>(null)
  const activeToolByCategory = ref<Record<AnalysisCategory, AnalysisToolType | null>>({
    basic: null,
    measurement: null,
    spatial: null,
    environment: null,
    planning: null,
    engineering: null,
    comparison: null,
    monitoring: null
  })
  const expandedCategories = ref<Record<AnalysisCategory, boolean>>({
    basic: true,
    measurement: true,
    spatial: true,
    environment: true,
    planning: true,
    engineering: true,
    comparison: true,
    monitoring: true
  })

  function setToolboxVisible(visible: boolean) {
    isVisible.value = visible
  }

  function toggleToolbox() {
    isVisible.value = !isVisible.value
  }

  function toggleCollapse() {
    isCollapsed.value = !isCollapsed.value
  }

  function setActiveTab(tab: 'tools' | 'history' | null) {
    activeTab.value = tab
  }

  function setActiveTool(category: AnalysisCategory, toolType: AnalysisToolType | null) {
    if (activeToolByCategory.value[category] === toolType) {
      activeToolByCategory.value[category] = null
    } else {
      Object.keys(activeToolByCategory.value).forEach((key) => {
        activeToolByCategory.value[key as AnalysisCategory] = null
      })
      activeToolByCategory.value[category] = toolType
    }
  }

  function toggleCategory(category: AnalysisCategory) {
    expandedCategories.value[category] = !expandedCategories.value[category]
  }

  function clearActiveTool() {
    Object.keys(activeToolByCategory.value).forEach((key) => {
      activeToolByCategory.value[key as AnalysisCategory] = null
    })
  }

  function getActiveTool(): AnalysisToolType | null {
    for (const category of Object.keys(activeToolByCategory.value)) {
      const tool = activeToolByCategory.value[category as AnalysisCategory]
      if (tool) return tool
    }
    return null
  }

  return {
    isVisible,
    isCollapsed,
    activeTab,
    activeToolByCategory,
    expandedCategories,
    setToolboxVisible,
    toggleToolbox,
    toggleCollapse,
    setActiveTab,
    setActiveTool,
    toggleCategory,
    clearActiveTool,
    getActiveTool
  }
})
