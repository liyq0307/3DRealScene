<template>
  <div ref="containerRef" class="virtual-scroll" @scroll="handleScroll">
    <!-- 占位符,用于维持滚动条高度 -->
    <div :style="{ height: `${totalHeight}px`, position: 'relative' }">
      <!-- 可见项容器 -->
      <div :style="{ transform: `translateY(${offsetY}px)` }">
        <div
          v-for="item in visibleItems"
          :key="getItemKey(item)"
          :style="{ height: `${itemHeight}px` }"
          class="virtual-item"
        >
          <slot :item="item" :index="item.index"></slot>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts" generic="T">
/**
 * 虚拟滚动组件
 *
 * 功能特性:
 * - 只渲染可见区域的元素,优化长列表性能
 * - 支持动态数据更新
 * - 平滑滚动体验
 * - 缓冲区机制避免白屏
 *
 * @author liyq
 * @date 2025-10-15
 */

import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'

interface Props {
  items: T[]
  itemHeight: number
  bufferSize?: number
  keyField?: string
}

const props = withDefaults(defineProps<Props>(), {
  bufferSize: 5,
  keyField: 'id'
})

const containerRef = ref<HTMLElement | null>(null)
const scrollTop = ref(0)
const containerHeight = ref(0)

// 计算总高度
const totalHeight = computed(() => props.items.length * props.itemHeight)

// 计算可见区域的起始和结束索引
const visibleRange = computed(() => {
  const start = Math.floor(scrollTop.value / props.itemHeight)
  const end = Math.ceil((scrollTop.value + containerHeight.value) / props.itemHeight)

  // 添加缓冲区
  return {
    start: Math.max(0, start - props.bufferSize),
    end: Math.min(props.items.length, end + props.bufferSize)
  }
})

// 计算偏移量
const offsetY = computed(() => visibleRange.value.start * props.itemHeight)

// 计算可见项
const visibleItems = computed(() => {
  const { start, end } = visibleRange.value
  return props.items
    .slice(start, end)
    .map((item, index) => ({
      ...item,
      index: start + index
    }))
})

// 获取项的唯一键
const getItemKey = (item: any) => {
  return item[props.keyField] || item.index
}

// 处理滚动
const handleScroll = (event: Event) => {
  const target = event.target as HTMLElement
  scrollTop.value = target.scrollTop
}

// 更新容器高度
const updateContainerHeight = () => {
  if (containerRef.value) {
    containerHeight.value = containerRef.value.clientHeight
  }
}

// 滚动到指定项
const scrollToIndex = (index: number, behavior: ScrollBehavior = 'smooth') => {
  if (containerRef.value) {
    containerRef.value.scrollTo({
      top: index * props.itemHeight,
      behavior
    })
  }
}

// 暴露方法给父组件
defineExpose({
  scrollToIndex,
  scrollToTop: () => scrollToIndex(0),
  scrollToBottom: () => scrollToIndex(props.items.length - 1)
})

onMounted(() => {
  updateContainerHeight()
  window.addEventListener('resize', updateContainerHeight)
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', updateContainerHeight)
})

watch(() => props.items.length, () => {
  // 数据变化时重新计算
  updateContainerHeight()
})
</script>

<style scoped>
.virtual-scroll {
  width: 100%;
  height: 100%;
  overflow-y: auto;
  overflow-x: hidden;
  position: relative;
}

.virtual-item {
  width: 100%;
  box-sizing: border-box;
}

/* 滚动条样式 */
.virtual-scroll::-webkit-scrollbar {
  width: 8px;
}

.virtual-scroll::-webkit-scrollbar-track {
  background: var(--gray-100);
  border-radius: 4px;
}

.virtual-scroll::-webkit-scrollbar-thumb {
  background: var(--gray-400);
  border-radius: 4px;
  transition: background var(--transition-base);
}

.virtual-scroll::-webkit-scrollbar-thumb:hover {
  background: var(--gray-500);
}
</style>
