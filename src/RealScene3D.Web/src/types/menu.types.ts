/**
 * 菜单类型定义文件
 * 定义菜单相关的数据结构、组件接口和状态接口
 */

/**
 * 菜单元数据接口
 */
export interface MenuMeta {
  /** 是否需要认证 */
  requiresAuth?: boolean
  /** 菜单标题（用于tooltip） */
  title?: string
  /** 是否隐藏菜单项 */
  hidden?: boolean
}

/**
 * 菜单项配置接口
 */
export interface MenuItemConfig {
  /** 菜单项唯一标识 */
  id: string
  /** 菜单项显示文本 */
  label: string
  /** 菜单项图标（emoji或图标类名） */
  icon: string
  /** 路由路径 */
  path?: string
  /** 子菜单项 */
  children?: MenuItemConfig[]
  /** 元数据 */
  meta?: MenuMeta
}

/**
 * 菜单状态接口
 */
export interface MenuState {
  /** 菜单是否折叠 */
  collapsed: boolean
  /** 展开的子菜单ID列表 */
  expandedMenus: string[]
  /** 当前激活的菜单项ID */
  activeMenuId: string | null
}

/**
 * MenuItem组件Props接口
 */
export interface MenuItemProps {
  /** 菜单项ID */
  id: string
  /** 图标 */
  icon: string
  /** 显示文本 */
  label: string
  /** 路由路径 */
  path?: string
  /** 子菜单项 */
  children?: MenuItemConfig[]
  /** 菜单是否折叠 */
  collapsed: boolean
  /** 是否激活状态 */
  active: boolean
  /** 子菜单是否展开 */
  expanded: boolean
  /** 菜单层级（0为顶级） */
  level: number
}

/**
 * SubMenu组件Props接口
 */
export interface SubMenuProps {
  /** 子菜单项列表 */
  items: MenuItemConfig[]
  /** 菜单是否折叠 */
  collapsed: boolean
  /** 当前激活的菜单项ID */
  activeId: string | null
  /** 父菜单项ID */
  parentId: string
}

/**
 * MenuToggle组件Props接口
 */
export interface MenuToggleProps {
  /** 菜单是否折叠 */
  collapsed: boolean
}

/**
 * UserInfo组件Props接口
 */
export interface UserInfoProps {
  /** 菜单是否折叠 */
  collapsed: boolean
}

/**
 * SidebarMenu组件Props接口
 */
export interface SidebarMenuProps {
  /** 菜单是否折叠（v-model） */
  collapsed?: boolean
  /** 默认折叠状态 */
  defaultCollapsed?: boolean
}

/**
 * 响应式断点接口
 */
export interface ResponsiveBreakpoints {
  /** 是否为桌面端（≥1200px） */
  isDesktop: boolean
  /** 是否为平板端（768-1199px） */
  isTablet: boolean
  /** 是否为移动端（<768px） */
  isMobile: boolean
  /** 当前窗口宽度 */
  width: number
}
