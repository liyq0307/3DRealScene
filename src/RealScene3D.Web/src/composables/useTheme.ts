import { ref, watch, readonly } from 'vue'

type Theme = 'light' | 'dark' | 'cyberpunk'

const THEME_KEY = 'realscene3d-theme'

const currentTheme = ref<Theme>(getInitialTheme())

function getInitialTheme(): Theme {
  if (typeof window === 'undefined') return 'cyberpunk'
  
  const saved = localStorage.getItem(THEME_KEY) as Theme
  if (saved && ['light', 'dark', 'cyberpunk'].includes(saved)) {
    return saved
  }
  
  if (window.matchMedia('(prefers-color-scheme: dark)').matches) {
    return 'cyberpunk'
  }
  
  return 'light'
}

function applyTheme(theme: Theme) {
  if (typeof document === 'undefined') return
  
  const root = document.documentElement
  root.setAttribute('data-theme', theme)
  
  if (theme === 'cyberpunk' || theme === 'dark') {
    root.classList.add('dark')
    root.style.colorScheme = 'dark'
  } else {
    root.classList.remove('dark')
    root.style.colorScheme = 'light'
  }
}

export function useTheme() {
  const setTheme = (theme: Theme) => {
    currentTheme.value = theme
    localStorage.setItem(THEME_KEY, theme)
    applyTheme(theme)
  }
  
  const toggleTheme = () => {
    const themes: Theme[] = ['light', 'dark', 'cyberpunk']
    const currentIndex = themes.indexOf(currentTheme.value)
    const nextIndex = (currentIndex + 1) % themes.length
    setTheme(themes[nextIndex])
  }
  
  const isDark = () => {
    return currentTheme.value === 'dark' || currentTheme.value === 'cyberpunk'
  }
  
  watch(currentTheme, applyTheme, { immediate: true })
  
  if (typeof window !== 'undefined') {
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
      if (!localStorage.getItem(THEME_KEY)) {
        setTheme(e.matches ? 'cyberpunk' : 'light')
      }
    })
  }
  
  return {
    theme: readonly(currentTheme),
    setTheme,
    toggleTheme,
    isDark
  }
}
