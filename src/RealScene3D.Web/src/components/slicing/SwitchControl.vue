<template>
  <div class="switch-control">
    <label class="switch-label" v-if="label">
      {{ label }}
    </label>
    <button
      type="button"
      role="switch"
      :aria-checked="modelValue"
      :aria-label="label"
      :class="['switch-button', { active: modelValue }]"
      @click="toggle"
      @keydown.enter.prevent="toggle"
      @keydown.space.prevent="toggle"
    >
      <span class="switch-slider"></span>
    </button>
  </div>
</template>

<script setup lang="ts">
interface Props {
  modelValue: boolean
  label?: string
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
}

const props = withDefaults(defineProps<Props>(), {
  label: ''
})

const emit = defineEmits<Emits>()

const toggle = () => {
  emit('update:modelValue', !props.modelValue)
}
</script>

<style scoped>
.switch-control {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}

.switch-label {
  font-size: 0.9rem;
  color: #333;
  font-weight: 500;
  user-select: none;
  cursor: pointer;
  flex: 1;
}

.switch-button {
  position: relative;
  width: 48px;
  height: 24px;
  border: none;
  border-radius: 12px;
  background: #ccc;
  cursor: pointer;
  transition: background-color 0.2s ease;
  padding: 0;
  outline: none;
  flex-shrink: 0;
}

.switch-button:focus {
  box-shadow: 0 0 0 3px rgba(0, 122, 204, 0.3);
}

.switch-button.active {
  background: #007acc;
}

.switch-slider {
  position: absolute;
  top: 2px;
  left: 2px;
  width: 20px;
  height: 20px;
  border-radius: 50%;
  background: white;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
  transition: transform 0.2s ease;
  pointer-events: none;
}

.switch-button.active .switch-slider {
  transform: translateX(24px);
}

/* 悬停效果 */
.switch-button:hover {
  background: #bbb;
}

.switch-button.active:hover {
  background: #005999;
}
</style>
