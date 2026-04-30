<template>
  <div class="analysis-history">
    <div class="history-header">
      <h5>分析历史</h5>
      <button @click="clearAllHistory" class="btn-clear" v-if="results.length > 0">清空</button>
    </div>
    
    <div v-if="results.length === 0" class="empty-state">
      <span class="empty-icon">📊</span>
      <p>暂无分析记录</p>
    </div>
    
    <div v-else class="history-list">
      <div
        v-for="result in results"
        :key="result.id"
        class="history-item"
        :class="{ visible: result.visible }"
      >
        <div class="item-header">
          <span class="item-type">{{ getTypeLabel(result.type) }}</span>
          <span class="item-time">{{ formatTime(result.timestamp) }}</span>
        </div>
        
        <div class="item-name">{{ result.name }}</div>
        
        <div class="item-actions">
          <button @click="toggleVisibility(result.id)" class="btn-action" :title="result.visible ? '隐藏' : '显示'">
            {{ result.visible ? '👁️' : '👁️‍🗨️' }}
          </button>
          <button @click="viewDetail(result)" class="btn-action" title="查看详情">📋</button>
          <button @click="exportResult(result)" class="btn-action" title="导出">💾</button>
          <button @click="deleteResult(result.id)" class="btn-action delete" title="删除">🗑️</button>
        </div>
      </div>
    </div>
    
    <!-- 详情弹窗 -->
    <div v-if="selectedResult" class="detail-modal" @click.self="selectedResult = null">
      <div class="detail-content">
        <div class="detail-header">
          <h5>{{ selectedResult.name }}</h5>
          <button @click="selectedResult = null" class="btn-close">×</button>
        </div>
        <div class="detail-body">
          <pre>{{ JSON.stringify(selectedResult.data, null, 2) }}</pre>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useAnalysisStore } from '@/stores/analysis'

const analysisStore = useAnalysisStore()
const selectedResult = ref<any>(null)

const results = computed(() => analysisStore.results)

const typeLabels: Record<string, string> = {
  'performance': '性能分析',
  'visibility': '通视分析',
  'viewshed': '可视域分析',
  'profile': '剖面分析',
  'skyline': '天际线分析',
  'distance': '空间距离',
  'distance-surface': '贴地距离',
  'area': '水平面积',
  'area-surface': '贴地面积',
  'volume': '挖方填方',
  'height': '高度差',
  'height-triangle': '三角测量',
  'bearing': '方位角',
  'coordinate': '坐标定位',
  'coordinate-measure': '坐标测量',
  'plot-ratio': '容积率',
  'building-layout': '建筑布局',
  'building-spacing': '建筑间距',
  'layer-comparison': '卷帘对比',
  'sun': '日照分析',
  'slope': '坡度分析',
  'flood': '淹没分析',
  'contour': '等高线',
  'flatten': '压平',
  'map-marking': '图上标记',
  'viewpoint': '观测台',
  'site-selection': '在线选址',
  'tower-foundation': '塔基建模',
  'pipeline': '管线分析',
  'constraint': '限制分析',
  'business-format': '业态分析'
}

function getTypeLabel(type: string): string {
  return typeLabels[type] || type
}

function formatTime(timestamp: Date): string {
  const date = new Date(timestamp)
  return `${date.getHours().toString().padStart(2, '0')}:${date.getMinutes().toString().padStart(2, '0')}:${date.getSeconds().toString().padStart(2, '0')}`
}

function toggleVisibility(id: string) {
  analysisStore.toggleResultVisibility(id)
}

function viewDetail(result: any) {
  selectedResult.value = result
}

function exportResult(result: any) {
  const data = {
    id: result.id,
    type: result.type,
    name: result.name,
    timestamp: result.timestamp,
    data: result.data
  }
  
  const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `${result.type}_${result.id}.json`
  a.click()
  URL.revokeObjectURL(url)
}

function deleteResult(id: string) {
  analysisStore.removeResult(id)
}

function clearAllHistory() {
  analysisStore.clearAll()
}
</script>

<style scoped>
.analysis-history {
  width: 260px;
  background: #0a0a14;
  display: flex;
  flex-direction: column;
  height: 100%;
  border-left: 1px solid rgba(255, 255, 255, 0.04);
}

.history-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 12px;
  background: rgba(18, 18, 43, 0.6);
  border-bottom: 1px solid rgba(255, 255, 255, 0.04);
  flex-shrink: 0;
}

.history-header h5 {
  margin: 0;
  font-size: 12px;
  font-weight: 500;
  color: rgba(255, 255, 255, 0.7);
  letter-spacing: 0.5px;
}

.btn-clear {
  padding: 2px 8px;
  background: rgba(239, 68, 68, 0.15);
  border: 1px solid rgba(239, 68, 68, 0.2);
  border-radius: 3px;
  color: #ef4444;
  font-size: 10px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.btn-clear:hover {
  background: rgba(239, 68, 68, 0.3);
}

.empty-state {
  text-align: center;
  padding: 2rem 1rem;
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
}

.empty-icon {
  font-size: 2rem;
  opacity: 0.3;
  display: block;
  margin-bottom: 0.5rem;
}

.empty-state p {
  margin: 0;
  font-size: 11px;
  color: rgba(255, 255, 255, 0.35);
}

.history-list {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
}

.history-list::-webkit-scrollbar {
  width: 3px;
}

.history-list::-webkit-scrollbar-thumb {
  background: rgba(255, 255, 255, 0.1);
  border-radius: 2px;
}

.history-item {
  background: #1a1a30;
  border: 1px solid rgba(255, 255, 255, 0.05);
  border-radius: 4px;
  padding: 8px;
  margin-bottom: 6px;
  transition: all 0.2s ease;
}

.history-item.visible {
  background: rgba(99, 102, 241, 0.08);
  border-color: rgba(99, 102, 241, 0.15);
}

.item-header {
  display: flex;
  justify-content: space-between;
  margin-bottom: 4px;
}

.item-type {
  font-size: 10px;
  color: rgba(99, 102, 241, 0.8);
  font-weight: 500;
}

.item-time {
  font-size: 10px;
  color: rgba(255, 255, 255, 0.3);
}

.item-name {
  font-size: 12px;
  color: rgba(255, 255, 255, 0.8);
  margin-bottom: 6px;
}

.item-actions {
  display: flex;
  gap: 4px;
}

.btn-action {
  padding: 2px 6px;
  background: rgba(255, 255, 255, 0.06);
  border: none;
  border-radius: 3px;
  font-size: 11px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.btn-action:hover {
  background: rgba(255, 255, 255, 0.12);
}

.btn-action.delete:hover {
  background: rgba(239, 68, 68, 0.25);
}

.detail-modal {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 2000;
}

.detail-content {
  background: #0a0a14;
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 8px;
  width: 90%;
  max-width: 500px;
  max-height: 80vh;
  overflow: hidden;
}

.detail-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 12px;
  background: rgba(18, 18, 43, 0.8);
  border-bottom: 1px solid rgba(255, 255, 255, 0.06);
}

.detail-header h5 {
  margin: 0;
  font-size: 12px;
  color: rgba(255, 255, 255, 0.85);
}

.btn-close {
  width: 22px;
  height: 22px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(255, 255, 255, 0.08);
  border: none;
  border-radius: 50%;
  color: rgba(255, 255, 255, 0.7);
  font-size: 1rem;
  cursor: pointer;
  transition: all 0.2s ease;
}

.btn-close:hover {
  background: rgba(255, 255, 255, 0.15);
  color: #fff;
}

.detail-body {
  padding: 10px;
  overflow-y: auto;
  max-height: calc(80vh - 45px);
}

.detail-body pre {
  margin: 0;
  font-size: 10px;
  color: rgba(255, 255, 255, 0.75);
  white-space: pre-wrap;
  word-break: break-all;
}
</style>
