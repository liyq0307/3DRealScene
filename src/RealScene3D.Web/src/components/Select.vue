<template>
  <div class="select-wrapper" :class="{ 'select-wrapper-error': error }">
    <label v-if="label" :for="selectId" class="select-label">
      {{ label }}
      <span v-if="required" class="required-mark">*</span>
    </label>
    <div class="select-container">
      <select
        :id="selectId"
        :value="modelValue"
        :disabled="disabled"
        class="select-field"
        @change="handleChange"
        @focus="handleFocus"
        @blur="handleBlur"
      >
        <option v-if="placeholder" value="" disabled>{{ placeholder }}</option>
        <option
          v-for="option in options"
          :key="option.value"
          :value="option.value"
        >
          {{ option.label }}
        </option>
      </select>
      <span class="select-arrow">▼</span>
    </div>
    <div v-if="error || hint" class="select-message">
      <span v-if="error" class="error-message">{{ error }}</span>
      <span v-else class="hint-message">{{ hint }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'

interface SelectOption {
  label: string
  value: string | number
}

interface SelectProps {
  modelValue: string | number
  options: SelectOption[]
  label?: string
  placeholder?: string
  disabled?: boolean
  required?: boolean
  error?: string
  hint?: string
}

const props = withDefaults(defineProps<SelectProps>(), {
  label: '',
  placeholder: '',
  disabled: false,
  required: false,
  error: '',
  hint: ''
})

const emit = defineEmits<{
  'update:modelValue': [value: string | number]
  focus: [event: FocusEvent]
  blur: [event: FocusEvent]
}>()

const isFocused = ref(false)
const selectId = computed(() => `select-${Math.random().toString(36).substring(7)}`)

const handleChange = (event: Event) => {
  const target = event.target as HTMLSelectElement
  emit('update:modelValue', target.value)
}

const handleFocus = (event: FocusEvent) => {
  isFocused.value = true
  emit('focus', event)
}

const handleBlur = (event: FocusEvent) => {
  isFocused.value = false
  emit('blur', event)
}
</script>

<style scoped>
.select-wrapper {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  width: 100%;
}

.select-label {
  font-size: 0.9rem;
  font-weight: 600;
  color: var(--gray-800);
  display: flex;
  align-items: center;
  gap: 0.25rem;
}

.required-mark {
  color: var(--danger-color);
}

.select-container {
  position: relative;
  display: flex;
  align-items: center;
}

.select-field {
  width: 100%;
  padding: 0.625rem 2.5rem 0.625rem 0.875rem;
  font-size: 0.95rem;
  font-family: var(--font-family);
  color: var(--gray-900);
  background: white;
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius);
  outline: none;
  cursor: pointer;
  transition: all var(--transition-base);
  appearance: none;
  -webkit-appearance: none;
  -moz-appearance: none;
}

.select-field:hover:not(:disabled):not(:focus) {
  border-color: var(--gray-400);
}

.select-field:focus {
  border-color: var(--primary-color);
  box-shadow: 0 0 0 3px var(--primary-light);
}

.select-field:disabled {
  background: var(--gray-100);
  color: var(--gray-600);
  cursor: not-allowed;
}

.select-arrow {
  position: absolute;
  right: 0.875rem;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--gray-600);
  font-size: 0.7rem;
  pointer-events: none;
  transition: transform var(--transition-base);
}

.select-field:focus + .select-arrow {
  transform: rotate(180deg);
}

.select-message {
  font-size: 0.85rem;
  min-height: 1.25rem;
}

.error-message {
  color: var(--danger-color);
  display: flex;
  align-items: center;
  gap: 0.25rem;
}

.hint-message {
  color: var(--gray-600);
}

.select-wrapper-error .select-field {
  border-color: var(--danger-color);
}

.select-wrapper-error .select-field:focus {
  box-shadow: 0 0 0 3px var(--danger-light);
}
</style>
