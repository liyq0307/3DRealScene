<template>
  <img
    ref="imgRef"
    :data-src="src"
    :alt="alt"
    :class="['lazy-image', { 'lazy-image-loaded': isLoaded }]"
    :style="imageStyle"
    loading="lazy"
    @load="handleLoad"
  />
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'

interface Props {
  src: string
  alt: string
  width?: string | number
  height?: string | number
  objectFit?: 'cover' | 'contain' | 'fill' | 'none'
  placeholder?: string
}

const props = withDefaults(defineProps<Props>(), {
  objectFit: 'cover',
  placeholder: 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 300"%3E%3Crect fill="%23F3F4F6" width="400" height="300"/%3E%3C/svg%3E'
})

const imgRef = ref<HTMLImageElement>()
const isLoaded = ref(false)

const imageStyle = computed(() => ({
  width: typeof props.width === 'number' ? `${props.width}px` : props.width,
  height: typeof props.height === 'number' ? `${props.height}px` : props.height,
  objectFit: props.objectFit
}))

const handleLoad = () => {
  isLoaded.value = true
}

onMounted(() => {
  if (!imgRef.value) return
  
  if ('IntersectionObserver' in window) {
    const observer = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          const img = entry.target as HTMLImageElement
          const dataSrc = img.dataset.src
          if (dataSrc) {
            img.src = dataSrc
          }
          observer.unobserve(img)
        }
      })
    }, {
      rootMargin: '50px'
    })
    
    observer.observe(imgRef.value)
  } else {
    if (imgRef.value.dataset.src) {
      imgRef.value.src = imgRef.value.dataset.src
    }
  }
})
</script>

<style scoped>
.lazy-image {
  opacity: 0;
  transition: opacity 0.3s ease-in-out;
  background: var(--gray-100);
}

.lazy-image-loaded {
  opacity: 1;
}

/* 深色主题 */
@media (prefers-color-scheme: dark) {
  .lazy-image {
    background: var(--gray-800);
  }
}
</style>
