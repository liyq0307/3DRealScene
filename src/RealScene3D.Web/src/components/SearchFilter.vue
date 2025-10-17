<template>
  <div class="search-filter">
    <!-- 搜索框 -->
    <div class="search-box">
      <input
        v-model="localSearchText"
        type="text"
        class="search-input"
        :placeholder="placeholder"
        @keyup.enter="handleSearch"
      />
      <button class="search-btn" @click="handleSearch">
        <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
          <path
            d="M9 17A8 8 0 1 0 9 1a8 8 0 0 0 0 16zM18 18l-4.35-4.35"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            stroke-linejoin="round"
          />
        </svg>
        搜索
      </button>
    </div>

    <!-- 筛选器 -->
    <div v-if="filters && filters.length > 0" class="filters">
      <div
        v-for="filter in filters"
        :key="filter.key"
        class="filter-item"
      >
        <label class="filter-label">{{ filter.label }}:</label>
        <select
          v-model="localFilters[filter.key]"
          class="filter-select"
          @change="handleFilterChange"
        >
          <option value="">全部</option>
          <option
            v-for="option in filter.options"
            :key="option.value"
            :value="option.value"
          >
            {{ option.label }}
          </option>
        </select>
      </div>
    </div>

    <!-- 快速操作按钮 -->
    <div v-if="$slots.actions" class="quick-actions">
      <slot name="actions"></slot>
    </div>

    <!-- 清空筛选 -->
    <button
      v-if="hasActiveFilters"
      class="clear-btn"
      @click="clearFilters"
    >
      清空筛选
    </button>
  </div>
</template>

<script setup lang="ts">
/**
 * 搜索和筛选组件
 *
 * 功能特性:
 * - 支持文本搜索
 * - 支持多条件筛选
 * - 支持自定义快速操作
 * - 响应式设计
 *
 * @author liyq
 * @date 2025-10-15
 */

import { ref, computed, watch } from 'vue'

export interface FilterOption {
  label: string
  value: string | number
}

export interface Filter {
  key: string
  label: string
  options: FilterOption[]
}

interface Props {
  searchText?: string
  placeholder?: string
  filters?: Filter[]
  modelValue?: Record<string, any>
}

const props = withDefaults(defineProps<Props>(), {
  searchText: '',
  placeholder: '搜索...',
  filters: () => [],
  modelValue: () => ({})
})

const emit = defineEmits<{
  'update:searchText': [text: string]
  'update:modelValue': [filters: Record<string, any>]
  search: [text: string, filters: Record<string, any>]
  clear: []
}>()

const localSearchText = ref(props.searchText)
const localFilters = ref<Record<string, any>>({ ...props.modelValue })

// 检查是否有激活的筛选条件
const hasActiveFilters = computed(() => {
  return Object.values(localFilters.value).some(v => v !== '' && v !== null && v !== undefined) ||
         localSearchText.value.trim() !== ''
})

// 执行搜索
const handleSearch = () => {
  emit('update:searchText', localSearchText.value)
  emit('update:modelValue', { ...localFilters.value })
  emit('search', localSearchText.value, { ...localFilters.value })
}

// 筛选条件变化
const handleFilterChange = () => {
  emit('update:modelValue', { ...localFilters.value })
  emit('search', localSearchText.value, { ...localFilters.value })
}

// 清空所有筛选
const clearFilters = () => {
  localSearchText.value = ''
  localFilters.value = {}
  emit('update:searchText', '')
  emit('update:modelValue', {})
  emit('clear')
  emit('search', '', {})
}

// 监听外部变化
watch(() => props.searchText, (newVal) => {
  localSearchText.value = newVal
})

watch(() => props.modelValue, (newVal) => {
  localFilters.value = { ...newVal }
}, { deep: true })
</script>

<style scoped>
.search-filter {
  display: flex;
  flex-wrap: wrap;
  gap: 1rem;
  padding: 1.5rem;
  background: white;
  border-radius: var(--border-radius-lg);
  box-shadow: var(--shadow-sm);
  border: 1px solid var(--border-color);
  margin-bottom: 1.5rem;
  animation: fadeInDown 0.3s ease;
}

.search-box {
  display: flex;
  flex: 1;
  min-width: 300px;
  gap: 0.5rem;
}

.search-input {
  flex: 1;
  padding: 0.75rem 1rem;
  border: 1.5px solid var(--border-color);
  border-radius: var(--border-radius);
  font-size: var(--font-size-base);
  transition: all var(--transition-base);
  background: var(--gray-50);
}

.search-input:hover {
  border-color: var(--gray-400);
  background: white;
}

.search-input:focus {
  outline: none;
  border-color: var(--primary-color);
  background: white;
  box-shadow: 0 0 0 3px rgba(99, 102, 241, 0.1);
}

.search-btn {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem 1.5rem;
  background: var(--gradient-primary-alt);
  color: white;
  border: none;
  border-radius: var(--border-radius);
  font-size: var(--font-size-base);
  font-weight: 600;
  cursor: pointer;
  transition: all var(--transition-base);
  box-shadow: var(--shadow-colored);
  white-space: nowrap;
}

.search-btn:hover {
  box-shadow: var(--shadow-xl);
  transform: translateY(-2px);
}

.search-btn:active {
  transform: translateY(0);
}

.filters {
  display: flex;
  flex-wrap: wrap;
  gap: 1rem;
  align-items: center;
}

.filter-item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.filter-label {
  font-size: var(--font-size-sm);
  font-weight: 600;
  color: var(--gray-700);
  white-space: nowrap;
}

.filter-select {
  min-width: 120px;
  padding: 0.5rem 0.75rem;
  border: 1.5px solid var(--border-color);
  border-radius: var(--border-radius);
  background: var(--gray-50);
  font-size: var(--font-size-sm);
  cursor: pointer;
  transition: all var(--transition-base);
}

.filter-select:hover {
  border-color: var(--gray-400);
  background: white;
}

.filter-select:focus {
  outline: none;
  border-color: var(--primary-color);
  background: white;
  box-shadow: 0 0 0 3px rgba(99, 102, 241, 0.1);
}

.quick-actions {
  display: flex;
  gap: 0.5rem;
  margin-left: auto;
}

.clear-btn {
  padding: 0.75rem 1.25rem;
  background: var(--gray-100);
  color: var(--gray-700);
  border: 1px solid var(--gray-300);
  border-radius: var(--border-radius);
  font-size: var(--font-size-sm);
  font-weight: 600;
  cursor: pointer;
  transition: all var(--transition-base);
  white-space: nowrap;
}

.clear-btn:hover {
  background: var(--danger-light);
  color: var(--danger-color);
  border-color: var(--danger-color);
  transform: translateY(-2px);
  box-shadow: var(--shadow-md);
}

/* 响应式设计 */
@media (max-width: 768px) {
  .search-filter {
    flex-direction: column;
  }

  .search-box {
    min-width: 100%;
  }

  .filters {
    width: 100%;
    flex-direction: column;
    align-items: stretch;
  }

  .filter-item {
    flex-direction: column;
    align-items: stretch;
  }

  .filter-select {
    width: 100%;
  }

  .quick-actions {
    margin-left: 0;
    width: 100%;
    justify-content: stretch;
  }

  .clear-btn {
    width: 100%;
  }
}
</style>
