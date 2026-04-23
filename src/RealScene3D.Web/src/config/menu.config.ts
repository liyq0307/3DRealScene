/**
 * 菜单配置文件
 * 基于现有路由配置，定义菜单项和子菜单项
 */
import type { MenuItemConfig } from '@/types/menu.types'

/**
 * 菜单配置数据
 * 包含所有菜单项及其子菜单
 */
export const menuConfig: MenuItemConfig[] = [
  {
    id: 'home',
    label: '首页',
    icon: '🏠',
    path: '/',
    meta: {
      title: '返回首页'
    }
  },
  {
    id: 'scenes',
    label: '场景管理',
    icon: '🌍',
    children: [
      {
        id: 'scenes-list',
        label: '场景列表',
        icon: '📋',
        path: '/scenes',
        meta: {
          title: '查看场景列表'
        }
      },
      {
        id: 'scene-objects',
        label: '场景对象',
        icon: '🔧',
        path: '/scene-objects',
        meta: {
          title: '管理场景对象'
        }
      },
      {
        id: 'slicing',
        label: '切片管理',
        icon: '✂️',
        path: '/slicing',
        meta: {
          title: '管理切片'
        }
      }
    ],
    meta: {
      title: '场景管理'
    }
  },
  {
    id: 'metadata',
    label: '元数据管理',
    icon: '📊',
    children: [
      {
        id: 'video-metadata',
        label: '视频元数据',
        icon: '🎥',
        path: '/video-metadata',
        meta: {
          title: '管理视频元数据'
        }
      },
      {
        id: 'bim-model-metadata',
        label: 'BIM模型',
        icon: '🏗️',
        path: '/bim-model-metadata',
        meta: {
          title: '管理BIM模型元数据'
        }
      },
      {
        id: 'tilt-photography-metadata',
        label: '倾斜摄影',
        icon: '📸',
        path: '/tilt-photography-metadata',
        meta: {
          title: '管理倾斜摄影元数据'
        }
      }
    ],
    meta: {
      title: '元数据管理'
    }
  },
  {
    id: 'workflow',
    label: '工作流',
    icon: '⚡',
    children: [
      {
        id: 'workflow-designer',
        label: '工作流设计',
        icon: '🎨',
        path: '/workflow-designer',
        meta: {
          title: '设计工作流'
        }
      },
      {
        id: 'workflow-instances',
        label: '工作流实例',
        icon: '📈',
        path: '/workflow-instances',
        meta: {
          title: '查看工作流实例'
        }
      }
    ],
    meta: {
      title: '工作流管理'
    }
  },
  {
    id: 'monitoring',
    label: '系统监控',
    icon: '📊',
    path: '/monitoring',
    meta: {
      title: '系统监控'
    }
  }
]

/**
 * 根据路径查找菜单项
 * @param path 路由路径
 * @returns 菜单项配置或null
 */
export function findMenuByPath(path: string): MenuItemConfig | null {
  for (const item of menuConfig) {
    // 检查顶级菜单项
    if (item.path === path) {
      return item
    }
    // 检查子菜单项
    if (item.children) {
      for (const child of item.children) {
        if (child.path === path) {
          return child
        }
      }
    }
  }
  return null
}

/**
 * 根据ID查找菜单项
 * @param id 菜单项ID
 * @returns 菜单项配置或null
 */
export function findMenuById(id: string): MenuItemConfig | null {
  for (const item of menuConfig) {
    // 检查顶级菜单项
    if (item.id === id) {
      return item
    }
    // 检查子菜单项
    if (item.children) {
      for (const child of item.children) {
        if (child.id === id) {
          return child
        }
      }
    }
  }
  return null
}

/**
 * 获取父菜单项
 * @param childId 子菜单项ID
 * @returns 父菜单项配置或null
 */
export function getParentMenu(childId: string): MenuItemConfig | null {
  for (const item of menuConfig) {
    if (item.children) {
      for (const child of item.children) {
        if (child.id === childId) {
          return item
        }
      }
    }
  }
  return null
}
