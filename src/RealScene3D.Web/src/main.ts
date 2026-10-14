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

import 'mars3d-cesium/Build/Cesium/Widgets/widgets.css'
import 'mars3d/mars3d.css'
import 'mars3d'
import '@mars/components/mars-work/expand/index'

import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import MarsUI from '@mars/components/mars-ui'
import { injectState, key as widgetKey } from '@mars/common/store/widget'
import widgetStore from './widget-store'
import './style.css'

// ==================== 应用初始化 ====================

const app = createApp(App)

// ==================== 插件配置 ====================

// Mars3D 公共 UI 组件库（注册全局 mars-* 组件 + 按需 ant-design-vue）
app.use(MarsUI)

// Pinia 状态管理（业务领域 store）
app.use(createPinia())

// Vue Router
app.use(router)

// Vuex widget store（Mars3D widget 激活/关闭机制专用）
app.use(injectState(widgetStore), widgetKey)

// ==================== 应用挂载 ====================

app.mount('#app')
