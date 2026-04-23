/**
 * 底图偏好设置管理Composable
 * 负责localStorage的读写和默认值处理
 */

const STORAGE_KEY = 'scene-preview-basemap-visible'

/**
 * 获取底图显示偏好
 * @returns {boolean} 底图是否显示，默认false
 */
export function getBasemapPreference(): boolean {
  try {
    const value = localStorage.getItem(STORAGE_KEY)
    return value === 'true'
  } catch (error) {
    console.warn('[useBasemapPreference] localStorage读取失败:', error)
    return false
  }
}

/**
 * 设置底图显示偏好
 * @param {boolean} visible - 底图是否显示
 */
export function setBasemapPreference(visible: boolean): void {
  try {
    localStorage.setItem(STORAGE_KEY, visible.toString())
  } catch (error) {
    console.warn('[useBasemapPreference] localStorage写入失败:', error)
  }
}
