/**
 * 通用预览组合式函数
 *
 * 功能说明:
 * - 提供场景和场景对象的统一预览逻辑
 * - 支持全屏切换、模型格式转换等通用功能
 * - 遵循DRY原则,避免代码重复
 *
 * 技术栈: Vue 3 + TypeScript
 * 作者: liyq
 * 创建时间: 2025-12-08
 */

import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useMessage } from '@/composables/useMessage'
import { sceneService, sceneObjectService } from '@/services/api'

/**
 * 预览类型枚举
 */
export type PreviewType = 'scene' | 'object'

/**
 * 预览选项接口
 */
export interface PreviewOptions {
  /**
   * 预览类型
   */
  type: PreviewType

  /**
   * 资源ID (场景ID或对象ID)
   */
  id: string

  /**
   * 返回路由名称
   */
  backRouteName: string
}

/**
 * 通用预览组合式函数
 *
 * @param options 预览选项
 * @returns 预览相关的状态和方法
 */
export function usePreview(options: PreviewOptions) {
  const router = useRouter()
  const { success: showSuccess, error: showError } = useMessage()

  // ==================== 响应式状态 ====================

  const loading = ref(true)
  const error = ref<string | null>(null)
  const data = ref<any>(null)
  const sceneObjects = ref<any[]>([])
  const showFormatNotice = ref(true)
  const isFullscreen = ref(false)

  // ==================== 计算属性 ====================

  /**
   * 标题文本
   */
  const title = computed(() => {
    if (!data.value) return '加载中...'
    return data.value.name || '未命名'
  })

  /**
   * 描述文本
   */
  const description = computed(() => {
    return data.value?.description || ''
  })

  /**
   * 检查是否有不支持的模型格式
   */
  const hasUnsupportedModels = computed(() => {
    if (!sceneObjects.value || sceneObjects.value.length === 0) return false

    const nativelySupportedFormats = ['gltf', 'glb', 'json']
    const convertibleFormats = ['obj', 'fbx', 'dae', 'stl', '3ds', 'blend', 'ply', 'las', 'laz', 'e57', 'osgb']

    return sceneObjects.value.some(obj => {
      if (!obj.displayPath) return false

      const fileExt = obj.displayPath.split('?')[0].split('.').pop()?.toLowerCase()

      // 如果是可转换格式,并且没有完成的切片任务,则认为是不支持的
      if (convertibleFormats.includes(fileExt || '')) {
        return !obj.slicingTaskId || obj.slicingTaskStatus !== 'Completed'
      }

      return false
    })
  })

  // ==================== 业务逻辑方法 ====================

  /**
   * 加载预览数据
   */
  const loadPreviewData = async () => {
    try {
      console.log(`[usePreview] 加载${options.type}预览数据, ID:`, options.id)

      if (options.type === 'scene') {
        // 加载场景数据
        data.value = await sceneService.getScene(options.id)

        if (data.value.sceneObjects && data.value.sceneObjects.length > 0) {
          sceneObjects.value = data.value.sceneObjects
        }

        console.log('[usePreview] 场景数据加载成功, 对象数量:', sceneObjects.value.length)
      } else if (options.type === 'object') {
        // 加载场景对象数据
        // 注意: sceneObjectService 没有单独获取对象的API
        // 我们需要先通过场景ID获取对象列表,然后找到目标对象
        // 这里假设对象ID包含场景ID信息,或者我们通过另一种方式获取

        // 临时方案: 如果API支持,直接获取对象详情
        // 否则需要从路由参数中传递场景ID
        const response = await fetch(`/api/sceneobjects/${options.id}`)
        data.value = await response.json()

        // 单个对象预览时,sceneObjects只包含当前对象
        sceneObjects.value = [data.value]

        console.log('[usePreview] 场景对象数据加载成功')
      }

      loading.value = false
    } catch (err) {
      console.error('[usePreview] 加载预览数据失败:', err)
      error.value = err instanceof Error ? err.message : '加载失败'
      loading.value = false
      showError(`加载${options.type === 'scene' ? '场景' : '对象'}失败`)
    }
  }

  /**
   * 返回上一页
   */
  const goBack = () => {
    router.push({ name: options.backRouteName })
  }

  /**
   * 切换全屏模式
   */
  const toggleFullscreen = () => {
    if (!document.fullscreenElement) {
      document.documentElement.requestFullscreen()
      isFullscreen.value = true
    } else {
      if (document.exitFullscreen) {
        document.exitFullscreen()
        isFullscreen.value = false
      }
    }
  }

  /**
   * 转换模型为3D Tiles格式
   */
  const convertModelsToTiles = async () => {
    if (!sceneObjects.value || sceneObjects.value.length === 0) {
      showError('没有可转换的模型')
      return
    }

    const convertibleFormats = ['obj', 'fbx', 'dae', 'stl', '3ds', 'blend', 'ply', 'las', 'laz', 'e57', 'osgb']

    // 找出需要转换的模型
    const modelsToConvert = sceneObjects.value.filter(obj => {
      if (!obj.displayPath) return false
      const fileExt = obj.displayPath.split('?')[0].split('.').pop()?.toLowerCase()
      return convertibleFormats.includes(fileExt || '') &&
             (!obj.slicingTaskId || obj.slicingTaskStatus !== 'Completed')
    })

    if (modelsToConvert.length === 0) {
      showError('没有需要转换的模型')
      return
    }

    if (confirm(`确定要为 ${modelsToConvert.length} 个模型创建切片任务吗?`)) {
      showSuccess(`准备为 ${modelsToConvert.length} 个模型创建切片任务(功能待实现)`)
      // TODO: 调用切片服务API创建切片任务
    }
  }

  /**
   * Cesium就绪回调
   */
  const onCesiumReady = (viewer: any) => {
    console.log('[usePreview] Cesium地球初始化成功', viewer)
    showSuccess('Cesium 3D地球加载成功')
  }

  /**
   * Cesium错误回调
   */
  const onCesiumError = (err: Error) => {
    console.error('[usePreview] Cesium初始化失败:', err)
    showError('Cesium地球加载失败: ' + err.message)
  }

  /**
   * 监听全屏状态变化
   */
  const handleFullscreenChange = () => {
    isFullscreen.value = !!document.fullscreenElement
  }

  /**
   * 初始化预览
   */
  const initialize = () => {
    loadPreviewData()
    document.addEventListener('fullscreenchange', handleFullscreenChange)
  }

  /**
   * 清理资源
   */
  const cleanup = () => {
    document.removeEventListener('fullscreenchange', handleFullscreenChange)
  }

  // ==================== 返回API ====================

  return {
    // 状态
    loading,
    error,
    data,
    sceneObjects,
    showFormatNotice,
    isFullscreen,

    // 计算属性
    title,
    description,
    hasUnsupportedModels,

    // 方法
    loadPreviewData,
    goBack,
    toggleFullscreen,
    convertModelsToTiles,
    onCesiumReady,
    onCesiumError,
    handleFullscreenChange,
    initialize,
    cleanup
  }
}
