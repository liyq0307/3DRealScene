<template>
  <div v-if="visible" class="viewshed-property-editor">
    <div class="editor-header">
      <h5>可视域属性编辑</h5>
      <button @click="$emit('close')" class="btn-close">✕</button>
    </div>

    <div class="editor-body">
      <div class="prop-item">
        <label>水平张角</label>
        <div class="slider-group">
          <input type="range" v-model.number="localProps.horizontalAngle" min="1" max="60" step="0.1" @input="onPropChange('horizontalAngle')" />
          <span class="slider-val">{{ localProps.horizontalAngle.toFixed(1) }}°</span>
        </div>
      </div>

      <div class="prop-item">
        <label>垂直张角</label>
        <div class="slider-group">
          <input type="range" v-model.number="localProps.verticalAngle" min="10" max="30" step="0.1" @input="onPropChange('verticalAngle')" />
          <span class="slider-val">{{ localProps.verticalAngle.toFixed(1) }}°</span>
        </div>
      </div>

      <div class="prop-item">
        <label>投射距离</label>
        <div class="slider-group">
          <input type="range" v-model.number="localProps.distance" min="1" max="5000" step="1" @input="onPropChange('distance')" />
          <span class="slider-val">{{ localProps.distance }}m</span>
        </div>
      </div>

      <div class="prop-item">
        <label>四周方向</label>
        <div class="slider-group">
          <input type="range" v-model.number="localProps.heading" min="0" max="360" step="0.1" @input="onPropChange('heading')" />
          <span class="slider-val">{{ localProps.heading.toFixed(1) }}°</span>
        </div>
      </div>

      <div class="prop-item">
        <label>俯仰角度</label>
        <div class="slider-group">
          <input type="range" v-model.number="localProps.pitch" min="-180" max="180" step="0.1" @input="onPropChange('pitch')" />
          <span class="slider-val">{{ localProps.pitch.toFixed(1) }}°</span>
        </div>
      </div>

      <div class="prop-item">
        <label>视椎框线</label>
        <label class="checkbox-label">
          <input type="checkbox" v-model="localProps.showFrustum" @change="onPropChange('showFrustum')" />
          <span>是否显示</span>
        </label>
      </div>

      <div class="prop-item">
        <label>透明度</label>
        <div class="slider-group">
          <input type="range" v-model.number="localProps.opacity" min="0" max="1" step="0.1" @input="onPropChange('opacity')" />
          <span class="slider-val">{{ localProps.opacity.toFixed(1) }}</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { reactive, watch } from 'vue'
import type { ViewshedPropertyData } from '@/types/analysis'

const props = defineProps<{
  visible: boolean
  viewshedProps: ViewshedPropertyData
}>()

const emit = defineEmits<{
  close: []
  update: [prop: string, value: any]
}>()

const localProps = reactive<ViewshedPropertyData>({ ...props.viewshedProps })

// 同步外部属性变化
watch(() => props.viewshedProps, (newVal) => {
  Object.assign(localProps, newVal)
}, { deep: true })

function onPropChange(prop: string) {
  emit('update', prop, (localProps as any)[prop])
}
</script>

<style scoped>
.viewshed-property-editor {
  background: rgba(10, 10, 20, 0.95);
  border: 1px solid rgba(99, 102, 241, 0.3);
  border-radius: 8px;
  padding: 0.6rem;
  margin-top: 0.6rem;
}

.editor-header {
  display: flex; justify-content: space-between; align-items: center;
  margin-bottom: 0.6rem;
}
.editor-header h5 { margin: 0; font-size: 0.8rem; color: rgba(255, 255, 255, 0.9); }
.btn-close {
  width: 20px; height: 20px; display: flex; align-items: center; justify-content: center;
  border: none; border-radius: 50%; background: rgba(255, 255, 255, 0.08);
  color: rgba(255, 255, 255, 0.6); cursor: pointer; font-size: 0.8rem;
}
.btn-close:hover { background: rgba(255, 255, 255, 0.15); color: white; }

.editor-body { display: flex; flex-direction: column; gap: 0.4rem; }
.prop-item { display: flex; align-items: center; justify-content: space-between; }
.prop-item label { font-size: 0.72rem; color: rgba(255, 255, 255, 0.7); white-space: nowrap; }

.slider-group { display: flex; align-items: center; gap: 0.4rem; flex: 1; margin-left: 0.5rem; }
.slider-group input[type="range"] {
  flex: 1; height: 3px; -webkit-appearance: none; appearance: none;
  background: rgba(255, 255, 255, 0.15); border-radius: 2px; outline: none;
}
.slider-group input[type="range"]::-webkit-slider-thumb {
  -webkit-appearance: none; appearance: none;
  width: 10px; height: 10px; border-radius: 50%;
  background: #a5b4fc; cursor: pointer;
}
.slider-val { font-size: 0.65rem; color: #a5b4fc; min-width: 40px; text-align: right; }

.checkbox-label {
  display: flex; align-items: center; gap: 0.3rem; cursor: pointer;
}
.checkbox-label input[type="checkbox"] {
  width: 14px; height: 14px; accent-color: #6366f1;
}
.checkbox-label span { font-size: 0.72rem; color: rgba(255, 255, 255, 0.7); }
</style>
