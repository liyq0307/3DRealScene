<template>
  <div class="input-wrapper" :class="{ 'input-wrapper-error': error }">
    <label v-if="label" :for="inputId" class="input-label">
      {{ label }}
      <span v-if="required" class="required-mark">*</span>
    </label>
    <div class="input-container">
      <span v-if="prefixIcon" class="input-prefix-icon">{{ prefixIcon }}</span>
      <input
        :id="inputId"
        :type="type"
        :value="modelValue"
        :placeholder="placeholder"
        :disabled="disabled"
        :readonly="readonly"
        :class="[
          'input-field',
          { 'input-with-prefix': prefixIcon, 'input-with-suffix': suffixIcon || clearable }
        ]"
        @input="handleInput"
        @focus="handleFocus"
        @blur="handleBlur"
      />
      <span v-if="clearable && modelValue && !disabled && !readonly" class="input-clear" @click="handleClear">
        ✕
      </span>
      <span v-else-if="suffixIcon" class="input-suffix-icon">{{ suffixIcon }}</span>
    </div>
    <div v-if="error || hint" class="input-message">
      <span v-if="error" class="error-message">{{ error }}</span>
      <span v-else class="hint-message">{{ hint }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'

interface InputProps {
  modelValue: string | number
  type?: 'text' | 'password' | 'email' | 'number' | 'tel' | 'url'
  label?: string
  placeholder?: string
  disabled?: boolean
  readonly?: boolean
  required?: boolean
  error?: string
  hint?: string
  prefixIcon?: string
  suffixIcon?: string
  clearable?: boolean
}

const props = withDefaults(defineProps<InputProps>(), {
  type: 'text',
  label: '',
  placeholder: '',
  disabled: false,
  readonly: false,
  required: false,
  error: '',
  hint: '',
  prefixIcon: '',
  suffixIcon: '',
  clearable: false
})

const emit = defineEmits<{
  'update:modelValue': [value: string | number]
  focus: [event: FocusEvent]
  blur: [event: FocusEvent]
}>()

const isFocused = ref(false)
const inputId = computed(() => `input-${Math.random().toString(36).substring(7)}`)

const handleInput = (event: Event) => {
  const target = event.target as HTMLInputElement
  emit('update:modelValue', props.type === 'number' ? Number(target.value) : target.value)
}

const handleFocus = (event: FocusEvent) => {
  isFocused.value = true
  emit('focus', event)
}

const handleBlur = (event: FocusEvent) => {
  isFocused.value = false
  emit('blur', event)
}

const handleClear = () => {
  emit('update:modelValue', '')
}
</script>

<style scoped>
.input-wrapper {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  width: 100%;
}

.input-label {
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

.input-container {
  position: relative;
  display: flex;
  align-items: center;
}

.input-field {
  width: 100%;
  padding: 0.625rem 0.875rem;
  font-size: 0.95rem;
  font-family: var(--font-family);
  color: var(--gray-900);
  background: white;
  border: 1px solid var(--border-color);
  border-radius: var(--border-radius);
  outline: none;
  transition: all var(--transition-base);
}

.input-field::placeholder {
  color: var(--gray-500);
}

.input-field:hover:not(:disabled):not(:focus) {
  border-color: var(--gray-400);
}

.input-field:focus {
  border-color: var(--primary-color);
  box-shadow: 0 0 0 3px var(--primary-light);
}

.input-field:disabled {
  background: var(--gray-100);
  color: var(--gray-600);
  cursor: not-allowed;
}

.input-field:read-only {
  background: var(--gray-50);
}

.input-with-prefix {
  padding-left: 2.5rem;
}

.input-with-suffix {
  padding-right: 2.5rem;
}

.input-prefix-icon,
.input-suffix-icon {
  position: absolute;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--gray-600);
  font-size: 1.1rem;
  pointer-events: none;
}

.input-prefix-icon {
  left: 0.875rem;
}

.input-suffix-icon {
  right: 0.875rem;
}

.input-clear {
  position: absolute;
  right: 0.875rem;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 1.25rem;
  height: 1.25rem;
  color: var(--gray-600);
  cursor: pointer;
  border-radius: 50%;
  transition: all var(--transition-fast);
  font-size: 0.9rem;
}

.input-clear:hover {
  background: var(--gray-200);
  color: var(--gray-800);
}

.input-message {
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

.input-wrapper-error .input-field {
  border-color: var(--danger-color);
}

.input-wrapper-error .input-field:focus {
  box-shadow: 0 0 0 3px var(--danger-light);
}

/* 数字输入框移除默认箭头 */
.input-field[type='number']::-webkit-inner-spin-button,
.input-field[type='number']::-webkit-outer-spin-button {
  -webkit-appearance: none;
  margin: 0;
}

.input-field[type='number'] {
  -moz-appearance: textfield;
}
</style>
