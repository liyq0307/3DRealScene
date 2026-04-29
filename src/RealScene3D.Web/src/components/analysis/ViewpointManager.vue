<template>
  <div class="viewpoint-manager">
    <div class="section-title">观测台</div>
    <div class="btn-group">
      <button class="btn-primary" @click="handleSave">保存当前视角</button>
    </div>
    <div class="viewpoint-list">
      <div v-for="vp in viewpoints" :key="vp.id" class="viewpoint-item">
        <span class="vp-name">{{ vp.name }}</span>
        <div class="vp-actions">
          <button class="btn-small" @click="handleFlyTo(vp)" title="飞行到此">✈</button>
          <button class="btn-small" @click="handleRename(vp)" title="重命名">✏</button>
          <button class="btn-small btn-del" @click="handleDelete(vp.id)" title="删除">✕</button>
        </div>
      </div>
      <div v-if="viewpoints.length === 0" class="empty-tip">暂无观测台，请保存当前视角</div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import { useAnalysisStore } from '@/stores/analysis'
import type { CameraView } from '@/types/analysis'

const props = defineProps<{ viewerInstance?: any }>()
const { getCameraView, flyToView, isReady, init } = useAnalysisTool(ref(props.viewerInstance))
const store = useAnalysisStore()

const viewpoints = computed(() => store.viewpoints)
import { computed } from 'vue'

onMounted(() => { if (!isReady.value) init() })

function handleSave() {
  const view = getCameraView()
  if (view) {
    const name = `观测台 ${viewpoints.value.length + 1}`
    store.addViewpoint(name, view)
  }
}

function handleFlyTo(vp: { cameraView: CameraView }) {
  flyToView(vp.cameraView)
}

function handleRename(vp: { id: string; name: string }) {
  const newName = prompt('请输入新名称', vp.name)
  if (newName && newName.trim()) {
    store.renameViewpoint(vp.id, newName.trim())
  }
}

function handleDelete(id: string) {
  store.removeViewpoint(id)
}
</script>

<style scoped>
.viewpoint-manager { padding: 12px; }
.section-title { font-size: 14px; font-weight: bold; margin-bottom: 12px; color: #e0e0e0; }
.btn-group { margin-bottom: 12px; }
.btn-primary { width: 100%; padding: 6px; background: #1890ff; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 12px; }
.viewpoint-list { max-height: 300px; overflow-y: auto; }
.viewpoint-item { display: flex; justify-content: space-between; align-items: center; padding: 6px 8px; background: #2a2a2a; border-radius: 4px; margin-bottom: 4px; }
.vp-name { font-size: 12px; color: #e0e0e0; }
.vp-actions { display: flex; gap: 4px; }
.btn-small { padding: 2px 6px; background: #333; color: #aaa; border: 1px solid #555; border-radius: 3px; cursor: pointer; font-size: 11px; }
.btn-del { color: #ff4d4f; }
.empty-tip { font-size: 12px; color: #666; text-align: center; padding: 20px 0; }
</style>
