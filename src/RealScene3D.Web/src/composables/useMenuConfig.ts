/**
 * 菜单配置管理 Composable
 * 提供菜单配置数据和查找方法
 */
import { ref } from 'vue'
import { menuConfig, findMenuByPath, findMenuById, getParentMenu } from '@/config/menu.config'
import type { MenuItemConfig } from '@/types/menu.types'

/**
 * 菜单配置管理
 */
export function useMenuConfig() {
  // 将菜单配置转为响应式ref
  const menuItems = ref<MenuItemConfig[]>(menuConfig)

  /**
   * 获取所有菜单项
   */
  function getMenuItems(): MenuItemConfig[] {
    return menuConfig
  }

  /**
   * 根据路径查找菜单项
   */
  function getMenuByPath(path: string): MenuItemConfig | null {
    return findMenuByPath(path)
  }

  /**
   * 根据ID查找菜单项
   */
  function getMenuById(id: string): MenuItemConfig | null {
    return findMenuById(id)
  }

  /**
   * 获取父菜单项
   */
  function getParent(childId: string): MenuItemConfig | null {
    return getParentMenu(childId)
  }

  /**
   * 检查菜单项是否有子菜单
   */
  function hasChildren(item: MenuItemConfig): boolean {
    return Boolean(item.children && item.children.length > 0)
  }

  /**
   * 检查路径是否匹配菜单项或其子菜单
   */
  function isPathActive(item: MenuItemConfig, currentPath: string): boolean {
    // 检查顶级菜单项
    if (item.path === currentPath) {
      return true
    }
    // 检查子菜单项
    if (item.children) {
      return item.children.some(child => child.path === currentPath)
    }
    return false
  }

  /**
   * 获取菜单项的激活路径列表（用于展开父菜单）
   */
  function getActiveMenuPath(currentPath: string): string[] {
    const activePath: string[] = []
    const menuItem = findMenuByPath(currentPath)
    
    if (menuItem) {
      // 如果是子菜单项，添加父菜单ID
      const parent = getParentMenu(menuItem.id)
      if (parent) {
        activePath.push(parent.id)
      }
      // 添加当前菜单项ID
      activePath.push(menuItem.id)
    }
    
    return activePath
  }

  return {
    // 数据
    menuItems,
    
    // 方法
    getMenuItems,
    getMenuByPath,
    getMenuById,
    getParent,
    hasChildren,
    isPathActive,
    getActiveMenuPath
  }
}
