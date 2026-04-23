/**
 * 响应式检测 Composable
 * 提供窗口宽度检测和断点判断
 */
import { ref, onMounted, onUnmounted, computed, type ComputedRef } from 'vue'
import type { ResponsiveBreakpoints } from '@/types/menu.types'

/**
 * 响应式断点定义
 */
const BREAKPOINTS = {
  mobile: 768,    // < 768px 为移动端
  tablet: 1200    // 768-1199px 为平板端，≥ 1200px 为桌面端
}

/**
 * 响应式检测
 */
export function useResponsive() {
  // 窗口宽度
  const width = ref(window.innerWidth)

  // 是否为桌面端（≥1200px）
  const isDesktop = computed(() => width.value >= BREAKPOINTS.tablet)

  // 是否为平板端（768-1199px）
  const isTablet = computed(() => 
    width.value >= BREAKPOINTS.mobile && width.value < BREAKPOINTS.tablet
  )

  // 是否为移动端（<768px）
  const isMobile = computed(() => width.value < BREAKPOINTS.mobile)

  /**
   * 更新窗口宽度
   */
  function updateWidth() {
    width.value = window.innerWidth
  }

  /**
   * 获取当前断点信息
   */
  const breakpoints: ComputedRef<ResponsiveBreakpoints> = computed(() => ({
    isDesktop: isDesktop.value,
    isTablet: isTablet.value,
    isMobile: isMobile.value,
    width: width.value
  }))

  /**
   * 根据断点获取菜单默认折叠状态
   * 桌面端默认展开，平板端默认折叠，移动端默认折叠
   */
  const defaultCollapsed = computed(() => !isDesktop.value)

  // 组件挂载时添加事件监听
  onMounted(() => {
    window.addEventListener('resize', updateWidth)
  })

  // 组件卸载时移除事件监听
  onUnmounted(() => {
    window.removeEventListener('resize', updateWidth)
  })

  return {
    // 状态
    width,
    isDesktop,
    isTablet,
    isMobile,
    breakpoints,
    defaultCollapsed,
    
    // 方法
    updateWidth
  }
}

/**
 * 全局响应式实例（单例模式）
 */
let responsiveInstance: ReturnType<typeof useResponsive> | null = null

/**
 * 获取全局响应式实例
 */
export function useResponsiveSingleton() {
  if (!responsiveInstance) {
    // 手动创建实例，不依赖组件生命周期
    const width = ref(window.innerWidth)
    
    const isDesktop = computed(() => width.value >= BREAKPOINTS.tablet)
    const isTablet = computed(() => 
      width.value >= BREAKPOINTS.mobile && width.value < BREAKPOINTS.tablet
    )
    const isMobile = computed(() => width.value < BREAKPOINTS.mobile)
    
    function updateWidth() {
      width.value = window.innerWidth
    }
    
    const breakpoints = computed(() => ({
      isDesktop: isDesktop.value,
      isTablet: isTablet.value,
      isMobile: isMobile.value,
      width: width.value
    }))
    
    const defaultCollapsed = computed(() => !isDesktop.value)
    
    // 添加全局事件监听
    window.addEventListener('resize', updateWidth)
    
    responsiveInstance = {
      width,
      isDesktop,
      isTablet,
      isMobile,
      breakpoints,
      defaultCollapsed,
      updateWidth
    }
  }
  return responsiveInstance
}
