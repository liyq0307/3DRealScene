import { defineStore } from 'pinia'
import { ref } from 'vue'

/**
 * 菜单/图层树选中节点 store
 *
 * P4 阶段从 cityplan/src/stores/menu.ts 对齐迁移 (cityplan 自身体量小,
 * 此处提供等效字段)。本项目 admin 壳菜单不依赖此 store,仅供分析 widgets 使用。
 */
export const useMenuStore = defineStore('menu', () => {
  // 当前勾选的 key 列表
  const activeCheckedKeys = ref<any[]>([])
  // 当前勾选的节点对象列表
  const activeCheckedNodes = ref<any[]>([])

  function setCheckedKeys(keys: any[]) {
    activeCheckedKeys.value = Array.isArray(keys) ? keys : []
  }

  function setActiveCheckedNodes(nodes: any[]) {
    activeCheckedNodes.value = Array.isArray(nodes) ? nodes : []
  }

  return {
    activeCheckedKeys,
    activeCheckedNodes,
    setCheckedKeys,
    setActiveCheckedNodes
  }
})
