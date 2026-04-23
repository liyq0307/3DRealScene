/**
 * 菜单状态管理 Composable
 * 管理菜单的折叠状态、展开的子菜单、激活的菜单项
 */
import { ref, watch, type Ref } from 'vue'
import type { MenuState } from '@/types/menu.types'

const STORAGE_KEY = 'menu-state'

/**
 * 从localStorage加载状态
 */
function loadState(): MenuState {
  try {
    const stored = localStorage.getItem(STORAGE_KEY)
    if (stored) {
      return JSON.parse(stored)
    }
  } catch (error) {
    console.error('Failed to load menu state:', error)
  }
  // 默认状态
  return {
    collapsed: false,
    expandedMenus: [],
    activeMenuId: null
  }
}

/**
 * 保存状态到localStorage
 */
function saveState(state: MenuState): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(state))
  } catch (error) {
    console.error('Failed to save menu state:', error)
  }
}

/**
 * 菜单状态管理
 */
export function useMenuState() {
  // 从localStorage加载初始状态
  const initialState = loadState()
  
  // 菜单折叠状态
  const collapsed = ref(initialState.collapsed)
  
  // 展开的子菜单ID列表
  const expandedMenus = ref<string[]>(initialState.expandedMenus)
  
  // 当前激活的菜单项ID
  const activeMenuId = ref<string | null>(initialState.activeMenuId)

  // 监听状态变化，自动保存
  watch(
    [collapsed, expandedMenus, activeMenuId],
    () => {
      saveState({
        collapsed: collapsed.value,
        expandedMenus: expandedMenus.value,
        activeMenuId: activeMenuId.value
      })
    },
    { deep: true }
  )

  /**
   * 切换菜单折叠状态
   */
  function toggleCollapse() {
    collapsed.value = !collapsed.value
  }

  /**
   * 设置菜单折叠状态
   */
  function setCollapsed(value: boolean) {
    collapsed.value = value
  }

  /**
   * 切换子菜单展开状态
   */
  function toggleExpand(menuId: string) {
    const index = expandedMenus.value.indexOf(menuId)
    if (index > -1) {
      // 已展开，则收起
      expandedMenus.value.splice(index, 1)
    } else {
      // 未展开，则展开
      expandedMenus.value.push(menuId)
    }
  }

  /**
   * 展开子菜单
   */
  function expandMenu(menuId: string) {
    if (!expandedMenus.value.includes(menuId)) {
      expandedMenus.value.push(menuId)
    }
  }

  /**
   * 收起子菜单
   */
  function collapseMenu(menuId: string) {
    const index = expandedMenus.value.indexOf(menuId)
    if (index > -1) {
      expandedMenus.value.splice(index, 1)
    }
  }

  /**
   * 收起所有子菜单
   */
  function collapseAll() {
    expandedMenus.value = []
  }

  /**
   * 设置当前激活的菜单项
   */
  function setActiveMenu(menuId: string | null) {
    activeMenuId.value = menuId
  }

  /**
   * 检查子菜单是否展开
   */
  function isExpanded(menuId: string): boolean {
    return expandedMenus.value.includes(menuId)
  }

  /**
   * 检查菜单项是否激活
   */
  function isActive(menuId: string): boolean {
    return activeMenuId.value === menuId
  }

  return {
    // 状态
    collapsed,
    expandedMenus,
    activeMenuId,
    
    // 方法
    toggleCollapse,
    setCollapsed,
    toggleExpand,
    expandMenu,
    collapseMenu,
    collapseAll,
    setActiveMenu,
    isExpanded,
    isActive
  }
}

/**
 * 全局菜单状态实例（单例模式）
 */
let menuStateInstance: ReturnType<typeof useMenuState> | null = null

/**
 * 获取全局菜单状态实例
 */
export function useMenuStateSingleton() {
  if (!menuStateInstance) {
    menuStateInstance = useMenuState()
  }
  return menuStateInstance
}
