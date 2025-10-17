<template>
  <div class="pagination">
    <button
      class="pagination-btn"
      :disabled="currentPage === 1"
      @click="changePage(1)"
    >
      首页
    </button>
    <button
      class="pagination-btn"
      :disabled="currentPage === 1"
      @click="changePage(currentPage - 1)"
    >
      上一页
    </button>

    <div class="pagination-pages">
      <button
        v-for="page in visiblePages"
        :key="page"
        class="pagination-page"
        :class="{ active: page === currentPage }"
        @click="changePage(page)"
      >
        {{ page }}
      </button>
    </div>

    <button
      class="pagination-btn"
      :disabled="currentPage === totalPages"
      @click="changePage(currentPage + 1)"
    >
      下一页
    </button>
    <button
      class="pagination-btn"
      :disabled="currentPage === totalPages"
      @click="changePage(totalPages)"
    >
      末页
    </button>

    <div class="pagination-info">
      <span>共 {{ total }} 条</span>
      <select v-model.number="localPageSize" class="page-size-select">
        <option :value="10">10条/页</option>
        <option :value="20">20条/页</option>
        <option :value="50">50条/页</option>
        <option :value="100">100条/页</option>
      </select>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * 分页组件
 *
 * 功能特性:
 * - 支持首页、末页、上一页、下一页导航
 * - 智能显示页码(最多显示7个页码)
 * - 可配置每页数量
 * - 响应式设计
 *
 * @author liyq
 * @date 2025-10-15
 */

import { computed, watch, ref } from 'vue'

interface Props {
  currentPage: number
  pageSize: number
  total: number
}

const props = withDefaults(defineProps<Props>(), {
  currentPage: 1,
  pageSize: 20,
  total: 0
})

const emit = defineEmits<{
  'update:currentPage': [page: number]
  'update:pageSize': [size: number]
  'change': [page: number, pageSize: number]
}>()

const localPageSize = ref(props.pageSize)

// 计算总页数
const totalPages = computed(() => Math.ceil(props.total / props.pageSize))

// 计算可见的页码
const visiblePages = computed(() => {
  const pages: number[] = []
  const maxVisible = 7
  const current = props.currentPage
  const total = totalPages.value

  if (total <= maxVisible) {
    // 总页数少于最大显示数,显示所有页码
    for (let i = 1; i <= total; i++) {
      pages.push(i)
    }
  } else {
    // 智能显示页码
    if (current <= 4) {
      // 当前页在前面,显示前5页和最后一页
      for (let i = 1; i <= 5; i++) pages.push(i)
      pages.push(total)
    } else if (current >= total - 3) {
      // 当前页在后面,显示第一页和最后5页
      pages.push(1)
      for (let i = total - 4; i <= total; i++) pages.push(i)
    } else {
      // 当前页在中间,显示首页、当前页前后各2页、尾页
      pages.push(1)
      for (let i = current - 2; i <= current + 2; i++) {
        pages.push(i)
      }
      pages.push(total)
    }
  }

  return pages
})

// 切换页码
const changePage = (page: number) => {
  if (page < 1 || page > totalPages.value || page === props.currentPage) {
    return
  }
  emit('update:currentPage', page)
  emit('change', page, props.pageSize)
}

// 监听每页数量变化
watch(localPageSize, (newSize) => {
  emit('update:pageSize', newSize)
  emit('change', 1, newSize) // 改变每页数量时重置到第一页
})
</script>

<style scoped>
.pagination {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 1rem;
  background: white;
  border-radius: var(--border-radius-lg);
  box-shadow: var(--shadow-sm);
  flex-wrap: wrap;
}

.pagination-btn,
.pagination-page {
  min-width: 2.5rem;
  height: 2.5rem;
  padding: 0 0.75rem;
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius);
  background: white;
  color: var(--gray-700);
  font-size: var(--font-size-sm);
  font-weight: 600;
  cursor: pointer;
  transition: all var(--transition-base);
}

.pagination-btn:hover:not(:disabled),
.pagination-page:hover:not(.active) {
  background: var(--primary-light);
  color: var(--primary-color);
  border-color: var(--primary-color);
  transform: translateY(-2px);
}

.pagination-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  background: var(--gray-100);
}

.pagination-pages {
  display: flex;
  gap: 0.25rem;
}

.pagination-page.active {
  background: var(--gradient-primary-alt);
  color: white;
  border-color: transparent;
  box-shadow: var(--shadow-colored);
}

.pagination-info {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-left: auto;
  font-size: var(--font-size-sm);
  color: var(--gray-600);
}

.page-size-select {
  padding: 0.5rem;
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius);
  background: white;
  font-size: var(--font-size-sm);
  cursor: pointer;
  transition: all var(--transition-base);
}

.page-size-select:hover {
  border-color: var(--primary-color);
}

.page-size-select:focus {
  outline: none;
  border-color: var(--primary-color);
  box-shadow: 0 0 0 3px rgba(99, 102, 241, 0.1);
}

/* 响应式设计 */
@media (max-width: 768px) {
  .pagination {
    justify-content: center;
  }

  .pagination-info {
    margin-left: 0;
    width: 100%;
    justify-content: center;
  }
}
</style>
