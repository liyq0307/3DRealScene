/**
 * Vue Router路由配置文件
 *
 * 功能说明：
 * - 配置应用的路由规则和导航结构
 * - 支持懒加载组件，提升首屏加载性能
 * - 使用HTML5历史模式，提供良好的URL体验
 * - 为后续功能扩展预留路由接口
 *
 * 技术栈：Vue 3 + Vue Router 4
 * 作者：liyq
 * 创建时间：2025-10-13
 */

import { createRouter, createWebHashHistory } from 'vue-router'
import authStore from '@/stores/auth'

// ==================== 路由规则配置 ====================

/**
 * 路由配置数组
 * 定义应用的所有页面路由，对应不同的视图组件
 */
const routes = [
  /**
   * 首页路由
   * 路径：/
   * 组件：Home.vue（主页组件）
   * 用途：应用入口和欢迎页面
   */
  {
    path: '/',
    name: 'Home',
    component: () => import('../views/Home.vue')
  },

  /**
   * 场景列表页面路由
   * 路径：/scenes
   * 组件：Scenes.vue（场景列表组件）
   * 用途：展示和管理所有3D场景
   */
  {
    path: '/scenes',
    name: 'Scenes',
    component: () => import('../views/Scenes.vue'),
    meta: { requiresAuth: true, title: '场景管理' }
  },

  /**
   * 场景预览页面路由
   * 路径：/scenes/:id/preview
   * 组件：ScenePreview.vue（场景预览组件）
   * 用途：全屏预览单个3D场景及其对象
   */
  {
    path: '/scenes/:id/preview',
    name: 'ScenePreview',
    component: () => import('../views/ScenePreview.vue'),
    meta: { requiresAuth: true, title: '场景预览' }
  },

  /**
   * 场景对象预览页面路由
   * 路径：/scenes/:sceneId/objects/:objectId/preview
   * 组件：SceneObjectPreview.vue（场景对象预览组件）
   * 用途：全屏预览单个场景对象的3D模型
   */
  {
    path: '/scenes/:sceneId/objects/:objectId/preview',
    name: 'SceneObjectPreview',
    component: () => import('../views/SceneObjectPreview.vue'),
    meta: { requiresAuth: true, title: '对象预览' }
  },

  /**
   * 工作流设计器路由
   * 路径：/workflow-designer
   * 组件：WorkflowDesigner.vue（工作流设计器组件）
   * 用途：可视化拖拽式工作流设计
   */
  {
    path: '/workflow-designer',
    name: 'WorkflowDesigner',
    component: () => import('../views/WorkflowDesigner.vue'),
    meta: { requiresAuth: true, title: '工作流设计器' }
  },

  /**
   * 系统监控路由
   * 路径：/monitoring
   * 组件：Monitoring.vue（系统监控组件）
   * 用途：系统监控、指标管理、告警管理、仪表板
   */
  {
    path: '/monitoring',
    name: 'Monitoring',
    component: () => import('../views/Monitoring.vue'),
    meta: { requiresAuth: true, title: '系统监控' }
  },

  /**
   * 3D模型切片管理路由
   * 路径：/slicing
   * 组件：Slicing.vue（切片管理组件）
   * 用途：3D模型切片任务管理、切片数据浏览、切片策略说明
   */
  {
    path: '/slicing',
    name: 'Slicing',
    component: () => import('../views/Slicing.vue'),
    meta: { requiresAuth: true, title: '3D模型切片' }
  },

  /**
   * 登录页面路由
   * 路径: /login
   * 组件: Login.vue (登录/注册组件)
   * 用途: 用户认证和注册
   */
  {
    path: '/login',
    name: 'Login',
    component: () => import('../views/Login.vue'),
    meta: { requiresAuth: false }
  },

  /**
   * 场景对象管理路由
   * 路径: /scene-objects
   * 组件: SceneObjects.vue (场景对象管理组件)
   * 用途: 管理3D场景中的对象、模型和元素
   */
  {
    path: '/scene-objects',
    name: 'SceneObjects',
    component: () => import('../views/SceneObjects.vue'),
    meta: { requiresAuth: true, title: '场景对象管理' }
  },

  /**
   * 工作流实例管理路由
   * 路径: /workflow-instances
   * 组件: WorkflowInstances.vue (工作流实例管理组件)
   * 用途: 查看和管理工作流执行实例
   */
  {
    path: '/workflow-instances',
    name: 'WorkflowInstances',
    component: () => import('../views/WorkflowInstances.vue'),
    meta: { requiresAuth: true, title: '工作流实例' }
  },

  /**
   * 用户个人中心路由
   * 路径: /profile
   * 组件: Profile.vue (用户个人中心组件)
   * 用途: 查看和编辑个人资料、修改密码、查看统计数据
   */
  {
    path: '/profile',
    name: 'Profile',
    component: () => import('../views/Profile.vue'),
    meta: { requiresAuth: true, title: '个人中心' }
  },

  /**
   * 视频元数据管理路由
   * 路径: /video-metadata
   * 组件: VideoMetadata.vue (视频元数据管理组件)
   * 用途: 管理系统中的视频资源元数据
   */
  {
    path: '/video-metadata',
    name: 'VideoMetadata',
    component: () => import('../views/VideoMetadata.vue'),
    meta: { requiresAuth: true, title: '视频元数据' }
  },

  /**
   * BIM模型元数据管理路由
   * 路径: /bim-model-metadata
   * 组件: BimModelMetadata.vue (BIM模型元数据管理组件)
   * 用途: 管理建筑信息模型(BIM)元数据
   */
  {
    path: '/bim-model-metadata',
    name: 'BimModelMetadata',
    component: () => import('../views/BimModelMetadata.vue'),
    meta: { requiresAuth: true, title: 'BIM模型元数据' }
  },

  /**
   * 倾斜摄影元数据管理路由
   * 路径: /tilt-photography-metadata
   * 组件: TiltPhotographyMetadata.vue (倾斜摄影元数据管理组件)
   * 用途: 管理倾斜摄影数据元数据
   */
  {
    path: '/tilt-photography-metadata',
    name: 'TiltPhotographyMetadata',
    component: () => import('../views/TiltPhotographyMetadata.vue'),
    meta: { requiresAuth: true, title: '倾斜摄影元数据' }
  }

  // ==================== 预留路由扩展空间 ====================
  // 未来版本中可以添加更多路由，例如：
  // {
  //   path: '/scenes/:id',
  //   name: 'SceneDetail',
  //   component: () => import('../views/SceneDetail.vue'),
  //   props: true
  // }
]

// ==================== 路由器实例创建 ====================

/**
 * 创建并配置Vue Router实例
 *
 * 配置说明：
 * - 使用Hash模式确保兼容性和稳定性
 * - 配置滚动行为，提供更好的用户体验
 * - 添加全局错误处理
 */
const router = createRouter({
  history: createWebHashHistory(),
  routes,
  // 滚动行为配置
  scrollBehavior(to, _from, savedPosition) {
    // 如果有保存的滚动位置，返回到该位置
    if (savedPosition) {
      return savedPosition
    }
    // 如果路由有hash锚点，滚动到锚点位置
    if (to.hash) {
      return {
        el: to.hash,
        behavior: 'smooth'
      }
    }
    // 默认滚动到顶部
    return { top: 0, behavior: 'smooth' }
  }
})

// ==================== 路由器导出 ====================

// ==================== 路由守卫 ====================

/**
 * 全局前置守卫
 * 在每次路由跳转前执行，用于权限验证和路由拦截
 *
 * 功能：
 * - 检查路由是否需要认证
 * - 未登录用户重定向到登录页
 * - 已登录用户访问登录页时重定向到首页
 */
router.beforeEach(async (to, _from, next) => {
  try {
    // 获取认证状态
    const isAuthenticated = authStore.isAuthenticated.value
    // 改为默认不需要认证，只有明确设置 requiresAuth: true 的路由才需要认证
    const requiresAuth = to.meta.requiresAuth === true

    // 如果访问登录页且已登录，重定向到首页
    if (to.name === 'Login' && isAuthenticated) {
      // 使用replace模式重定向，避免添加到历史记录
      next({ name: 'Home', replace: true })
      return
    }

    // 如果需要认证但未登录，重定向到登录页
    if (requiresAuth && !isAuthenticated) {
      next({ name: 'Login', query: { redirect: to.fullPath } })
      return
    }

    // 继续导航
    next()
  } catch (error) {
    console.error('路由守卫错误:', error)
    // 发生错误时，重定向到首页
    next({ name: 'Home' })
  }
})

/**
 * 全局后置钩子
 * 在每次路由跳转后执行，用于页面标题更新等操作
 */
router.afterEach((to) => {
  // 更新页面标题
  const baseTitle = '3D Real Scene Viewer'
  document.title = to.meta.title ? `${to.meta.title} - ${baseTitle}` : baseTitle
})

/**
 * 导出路由器实例
 * 在main.ts中被应用实例使用，提供全局路由功能
 */
export default router
