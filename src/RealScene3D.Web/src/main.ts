/**
 * Vue 3 应用程序入口文件
 *
 * 功能说明：
 * - 创建Vue应用实例
 * - 配置状态管理（Pinia）
 * - 配置路由系统（Vue Router）
 * - 挂载应用到DOM
 * - 导入全局样式
 *
 * 技术栈：Vue 3 + TypeScript + Pinia + Vue Router
 * 作者：liyq
 * 创建时间：2025-10-13
 */

import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import './style.css'

// ==================== 应用初始化 ====================

/**
 * 创建Vue应用实例
 * 以根组件App.vue为基础构建应用
 */
const app = createApp(App)

// ==================== 插件配置 ====================

/**
 * 配置Pinia状态管理
 * 提供全局状态管理和响应式数据存储
 */
app.use(createPinia())

/**
 * 配置Vue Router
 * 启用客户端路由，支持单页面应用（SPA）的页面导航
 */
app.use(router)

// ==================== 应用挂载 ====================

/**
 * 挂载应用到DOM
 * 将Vue应用实例挂载到index.html中的#app元素上
 * 应用启动的最后一步，之后用户界面开始渲染和交互
 */
app.mount('#app')
