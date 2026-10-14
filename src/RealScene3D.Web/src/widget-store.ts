/**
 * Widget store 配置 - Phase 1 基础框架阶段
 *
 * widgets 列表为空，由后续页面在挂载时通过 useWidget().activate() 动态扩展，
 * 或在 Phase 2/3 迁移 widgets/ 下功能时改为静态注册。
 */
import { defineAsyncComponent, markRaw } from 'vue'
import type { StoreOptions } from 'vuex'
import type { WidgetState } from '@mars/common/store/widget'

/**
 * 全局 widget 静态注册。每个 widget 一行,lazy import。
 *
 * 添加规则:
 *   {
 *     name: 'kebab-case-name',
 *     component: markRaw(defineAsyncComponent(() => import('@mars/widgets/<group>/<dir>/index.vue'))),
 *     autoDisable?: boolean,
 *     disableOther?: boolean
 *   }
 *
 * P2 开始填充 basic/*; P4 起填充 analysis/*。
 */
const widgetStore: StoreOptions<WidgetState> = {
  state: {
    widgets: [
      // basic widgets — P2 填充
      {
        name: 'manage-basemap',
        component: markRaw(defineAsyncComponent(() => import('@mars/widgets/basic/manage-basemap/index.vue')))
      },
      {
        name: 'location-point',
        component: markRaw(defineAsyncComponent(() => import('@mars/widgets/basic/location-point/index.vue')))
      },
      {
        name: 'toolbar',
        component: markRaw(defineAsyncComponent(() => import('@mars/widgets/basic/toolbar/index.vue')))
      },
      {
        name: 'graphic-options',
        autoDisable: true,
        component: markRaw(defineAsyncComponent(() => import('@mars/widgets/basic/graphic-options/index.vue')))
      },
      {
        name: 'query-poi',
        component: markRaw(defineAsyncComponent(() => import('@mars/widgets/basic/query-poi/index.vue')))
      },
      // analysis widgets — P4 填充
      {
        name: 'analysis',
        component: markRaw(defineAsyncComponent(() => import('@mars/widgets/analysis/index.vue')))
      },
      {
        name: 'analysis-toolbox',
        component: markRaw(defineAsyncComponent(() => import('@mars/widgets/analysis/toolbox/index.vue')))
      },
      {
        name: 'analysis-layer-tree',
        component: markRaw(defineAsyncComponent(() => import('@mars/widgets/analysis/layer-tree/index.vue')))
      },
      {
        name: 'analysis-history-list',
        component: markRaw(defineAsyncComponent(() => import('@mars/widgets/analysis/history-list/index.vue')))
      },
      {
        name: 'analysis-viewpoints',
        component: markRaw(defineAsyncComponent(() => import('@mars/widgets/analysis/viewpoints/index.vue')))
      },
      {
        name: 'analysis-model-tree',
        component: markRaw(defineAsyncComponent(() => import('@mars/widgets/analysis/model-tree/index.vue')))
      }
    ],
    openAtStart: [],
    defaultOption: {
      autoDisable: true,
      disableOther: false
    }
  }
}

export default widgetStore
