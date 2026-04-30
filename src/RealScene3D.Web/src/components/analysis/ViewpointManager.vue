<template>
  <div class="viewpoint-manager">
    <div class="panel-header">
      <h4>观测台</h4>
    </div>

    <div class="instructions">
      <p>保存当前地图视角，快速切换到已保存视角</p>
    </div>

    <div class="actions">
      <button class="btn-primary" @click="handleSave" :disabled="!isReady">
        保存当前视角
      </button>
    </div>

    <div class="viewpoint-list">
      <div v-for="vp in viewpoints" :key="vp.id" class="viewpoint-item">
        <div class="vp-info">
          <span class="vp-name">{{ vp.name }}</span>
          <span class="vp-detail">
            {{ vp.cameraView?.lng?.toFixed(2) }}, {{ vp.cameraView?.lat?.toFixed(2) }}
          </span>
        </div>
        <div class="vp-actions">
          <button class="btn-icon" @click="handleFlyTo(vp)" title="飞行到此">✈</button>
          <button class="btn-icon" @click="handleRename(vp)" title="重命名">✏</button>
          <button class="btn-icon btn-del" @click="handleDelete(vp.id)" title="删除">✕</button>
        </div>
      </div>
      <div v-if="viewpoints.length === 0" class="empty-tip">暂无观测台，请保存当前视角</div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useAnalysisTool } from '@/composables/useAnalysisTool'
import { useAnalysisStore } from '@/stores/analysis'
import type { CameraView } from '@/types/analysis'

const props = defineProps<{ viewerInstance?: any }>()
const { getCameraView, flyToView, isReady, init } = useAnalysisTool(ref(props.viewerInstance))
const store = useAnalysisStore()

const viewpoints = computed(() => store.viewpoints)

onMounted(() => { if (!isReady.value) init() })

function handleSave() {
  const view = getCameraView()
  if (view) {
    const name = `观测台 ${viewpoints.value.length + 1}`
    store.addViewpoint(name, view)
    store.addResult({
      type: 'viewpoint',
      name,
      data: { cameraView: view },
      visible: true
    })
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
  if (confirm('确定删除此观测台？')) {
    store.removeViewpoint(id)
  }
}
</script>

<style scoped>
.viewpoint-manager { padding: 0.6rem; }
.panel-header { margin-bottom: 0.6rem; }
.panel-header h4 { margin: 0; font-size: 0.85rem; color: rgba(255, 255, 255, 0.9); }

.instructions {
  background: rgba(99, 102, 241, 0.1); border: 1px solid rgba(99, 102, 241, 0.2);
  border-radius: 6px; padding: 0.5rem; margin-bottom: 0.6rem;
}
.instructions p { margin: 0; font-size: 0.7rem; color: rgba(255, 255, 255, 0.8); }

.actions { margin-bottom: 0.6rem; }
.btn-primary {
  width: 100%; padding: 0.4rem; background: rgba(99, 102, 241, 0.2);
  color: #a5b4fc; border: 1px solid rgba(99, 102, 241, 0.4);
  border-radius: 4px; font-size: 0.75rem; cursor: pointer; transition: all 0.2s ease;
}
.btn-primary:hover:not(:disabled) { background: rgba(99, 102, 241, 0.3); }
.btn-primary:disabled { opacity: 0.4; cursor: not-allowed; }

.viewpoint-list { max-height: 280px; overflow-y: auto; }
.viewpoint-item {
  display: flex; justify-content: space-between; align-items: center;
  padding: 0.4rem 0.5rem; background: rgba(255, 255, 255, 0.05);
  border-radius: 6px; margin-bottom: 4px; transition: background 0.2s;
}
.viewpoint-item:hover { background: rgba(255, 255, 255, 0.08); }
.vp-info { display: flex; flex-direction: column; gap: 2px; }
.vp-name { font-size: 0.75rem; color: rgba(255, 255, 255, 0.9); }
.vp-detail { font-size: 0.65rem; color: rgba(255, 255, 255, 0.4); }
.vp-actions { display: flex; gap: 4px; }
.btn-icon {
  width: 24px; height: 24px; display: flex; align-items: center; justify-content: center;
  background: rgba(255, 255, 255, 0.08); border: 1px solid rgba(255, 255, 255, 0.12);
  border-radius: 4px; cursor: pointer; font-size: 0.7rem; color: rgba(255, 255, 255, 0.7);
}
.btn-icon:hover { background: rgba(255, 255, 255, 0.15); }
.btn-del { color: rgba(239, 68, 68, 0.8); }
.btn-del:hover { background: rgba(239, 68, 68, 0.15); }

.empty-tip { font-size: 0.72rem; color: rgba(255, 255, 255, 0.3); text-align: center; padding: 1.5rem 0; }
</style>
