<template>
  <div class="slicing-create-page">
    <!-- 页面头部 -->
    <header class="page-header">
      <div class="header-left">
        <button @click="handleBack" class="btn-back" type="button">
          <span class="back-icon">←</span>
          <span>返回</span>
        </button>
        <h1>创建切片任务</h1>
      </div>
    </header>

    <!-- 页面内容 -->
    <div class="page-content">
      <div class="form-container">
        <!-- 基本信息区域 -->
        <section class="form-section">
          <h2 class="section-title">基本信息</h2>
          <div class="form-row">
            <div class="form-group form-group-name">
              <label>任务名称 <span class="required">*</span></label>
              <input
                v-model="currentFormData.name"
                type="text"
                class="form-input"
                placeholder="请输入任务名称"
              />
            </div>
            <div class="form-group form-group-type">
              <label>数据类型</label>
              <div class="type-selector">
                <button
                  :class="['type-option', { active: dataType === DataType.General }]"
                  @click="selectDataType(DataType.General)"
                  type="button"
                >
                  <span class="type-icon">📦</span>
                  <span class="type-text">通用3D模型</span>
                </button>
                <button
                  :class="['type-option', { active: dataType === DataType.Oblique }]"
                  @click="selectDataType(DataType.Oblique)"
                  type="button"
                >
                  <span class="type-icon">🗺️</span>
                  <span class="type-text">倾斜摄影</span>
                </button>
              </div>
            </div>
          </div>
          <div class="form-group">
            <label>任务描述</label>
            <textarea
              v-model="currentFormData.description"
              class="form-textarea"
              placeholder="请输入任务描述（可选）"
            ></textarea>
          </div>
        </section>

        <!-- 工作流配置区域 -->
        <section class="form-section workflow-section">
          <h2 class="section-title">
            <span class="title-icon">⚙️</span>
            工作流配置
          </h2>
          <!-- 界面切换 -->
          <transition name="fade" mode="out-in">
            <!-- 倾斜摄影界面 -->
            <ObliqueWorkflowLayout
              v-if="dataType === DataType.Oblique"
              :key="'oblique'"
              :formData="obliqueFormData"
              @update:formData="updateObliqueFormData"
              @publish="handlePublish"
            />

            <!-- 通用模型界面 -->
            <GeneralWorkflowLayout
              v-else
              :key="'general'"
              :formData="generalFormData"
              @update:formData="updateGeneralFormData"
            />
          </transition>
        </section>

        <!-- 操作按钮 -->
        <div class="form-actions">
          <button type="button" class="btn btn-secondary" @click="handleBack">
            取消
          </button>
          <button
            type="button"
            class="btn btn-primary"
            :disabled="isSubmitting"
            @click="handleSubmit"
          >
            {{ isSubmitting ? '创建中...' : '创建任务' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import ObliqueWorkflowLayout from '@/components/slicing/ObliqueWorkflowLayout.vue'
import GeneralWorkflowLayout from '@/components/slicing/GeneralWorkflowLayout.vue'
import { 
  DataType, 
  type ObliqueSliceFormData, 
  type GeneralSliceFormData 
} from '@/types/obliqueSlice'
import { 
  getDefaultObliqueFormData, 
  getDefaultGeneralFormData,
  validateObliqueForm,
  mapObliqueFormDataToRequest
} from '@/composables/useObliqueSlice'
import { slicingService, sceneObjectService } from '@/services/api'
import { useAuthStore } from '@/stores/auth'
import fileHandleStore from '@/services/fileHandleStore'

const router = useRouter()
const route = useRoute()
const authStore = useAuthStore()
const userId = authStore.currentUser.value?.id || '00000000-0000-0000-0000-000000000001'

// 场景对象相关状态
const sceneObjectId = ref<string>('')
const sceneObject = ref<any>(null)
const isLoadingSceneObject = ref(false)

// 状态管理
const dataType = ref<DataType>(DataType.General)
const obliqueFormData = ref<ObliqueSliceFormData>(getDefaultObliqueFormData())
const generalFormData = ref<GeneralSliceFormData>(getDefaultGeneralFormData())
const isSubmitting = ref(false)

// 当前表单数据
const currentFormData = computed(() => {
  if (dataType.value === DataType.Oblique) {
    return obliqueFormData.value
  }
  return generalFormData.value
})

/**
 * 扫描目录并定位主模型文件
 * 针对本地文件句柄执行真实扫描，针对服务器路径执行启发式识别
 */
const resolveMainModelPath = async (path: string): Promise<string> => {
  if (!path || !path.trim()) return ''
  
  // 1. 处理本地文件句柄 (local-file-handle://uuid)
  if (path.startsWith('local-file-handle://')) {
    try {
      const uuid = path.replace('local-file-handle://', '')
      const handle = await fileHandleStore.getHandle<any>(uuid)
      
      if (handle && handle.kind === 'directory') {
        console.log('[SlicingCreate] 正在扫描本地目录句柄:', handle.name)
        
        // 检查权限
        let permission = await handle.queryPermission({ mode: 'read' })
        if (permission !== 'granted') {
          permission = await handle.requestPermission({ mode: 'read' })
        }
        
        if (permission !== 'granted') return path

        const modelFiles: { name: string; size: number }[] = []
        
        // 遍历目录
        for await (const entry of (handle as any).values()) {
          if (entry.kind === 'file') {
            const lowerName = entry.name.toLowerCase()
            if (lowerName.endsWith('.obj') || lowerName.endsWith('.fbx') || 
                lowerName.endsWith('.gltf') || lowerName.endsWith('.glb') ||
                lowerName.endsWith('.ifc') || lowerName.endsWith('.dae')) {
              const file = await entry.getFile()
              modelFiles.push({ name: entry.name, size: file.size })
            }
          }
        }

        if (modelFiles.length > 0) {
          // 查找优先级：同名 OBJ > odm_textured_model_geo.obj > 任意 OBJ > 最大文件
          const folderName = handle.name.toLowerCase()
          let target = modelFiles.find(f => f.name.toLowerCase() === `${folderName}.obj`)
          if (!target) target = modelFiles.find(f => f.name.toLowerCase() === 'odm_textured_model_geo.obj')
          if (!target) target = modelFiles.find(f => f.name.toLowerCase() === 'odm_textured_model.obj')
          if (!target) target = modelFiles.find(f => f.name.toLowerCase().endsWith('.obj'))
          if (!target) target = modelFiles.sort((a, b) => b.size - a.size)[0]

          if (target) {
            console.log(`[SlicingCreate] 已通过扫描自动定位主模型: ${target.name}`)
            return `${path}/${target.name}`
          }
        }
      }
    } catch (err) {
      console.warn('[SlicingCreate] 扫描本地目录失败:', err)
    }
    return path
  }

  // 2. 处理物理路径 (启发式识别)
  // 标准化路径：将所有正斜杠转换为反斜杠（针对 Windows 路径）
  let normalizedPath = path.replace(/\//g, '\\')
  const parts = normalizedPath.split('\\')
  const lastPart = parts.pop() || ''
  
  if (lastPart && !lastPart.includes('.')) {
    console.log('[SlicingCreate] 检测到物理文件夹路径，执行启发式识别')
    const lowerPart = lastPart.toLowerCase()
    
    // 基础路径（去除末尾斜杠）
    const basePath = normalizedPath.replace(/[\\\/]?$/, '')
    
    // 如果文件夹名包含特定关键字，尝试追加标准入口文件名
    if (lowerPart.includes('obj')) {
      // 检查是否可能是 ODM 文件夹
      if (lowerPart.includes('odm')) {
        return `${basePath}\\odm_textured_model_geo.obj`
      }
      // 默认尝试 同名.obj
      return `${basePath}\\${lastPart}.obj`
    } else if (lowerPart.includes('fbx')) {
      return `${basePath}\\${lastPart}.fbx`
    } else if (lowerPart.includes('gltf') || lowerPart.includes('glb')) {
      return `${basePath}\\${lastPart}.gltf`
    }
  }

  return path
}

// 加载场景对象数据（如果通过 query 参数传递了 sceneObjectId）
onMounted(async () => {
  const querySceneObjectId = route.query.sceneObjectId as string
  if (querySceneObjectId) {
    sceneObjectId.value = querySceneObjectId
    isLoadingSceneObject.value = true
    
    try {
      const response = await sceneObjectService.getObject(querySceneObjectId)
      sceneObject.value = response
      
      // 预填充表单数据
      if (sceneObject.value) {
        generalFormData.value.name = `切片任务 - ${sceneObject.value.name}`
        const modelPath = sceneObject.value.modelPath || ''
        
        // ✅ 异步解析主模型路径
        generalFormData.value.modelPath = await resolveMainModelPath(modelPath)
      }
    } catch (error) {
      console.error('加载场景对象失败:', error)
      alert('加载场景对象信息失败，请手动填写模型路径')
    } finally {
      isLoadingSceneObject.value = false
    }
  }
})

// 返回列表页面
const handleBack = () => {
  router.push({ name: 'Slicing' })
}

// 选择数据类型
const selectDataType = (type: DataType) => {
  // 保存通用字段
  const preservedName = currentFormData.value.name
  const preservedDescription = currentFormData.value.description
  
  // 切换数据类型
  dataType.value = type
  
  // 根据新类型初始化表单数据
  if (type === DataType.Oblique) {
    obliqueFormData.value = getDefaultObliqueFormData()
  } else {
    generalFormData.value = getDefaultGeneralFormData()
  }
  
  // 恢复通用字段
  currentFormData.value.name = preservedName
  currentFormData.value.description = preservedDescription
}

// 更新倾斜摄影表单数据
const updateObliqueFormData = (data: ObliqueSliceFormData) => {
  obliqueFormData.value = { ...data }
}

// 更新通用模型表单数据
const updateGeneralFormData = (data: GeneralSliceFormData) => {
  generalFormData.value = { ...data }
}

// 处理提交
const handleSubmit = async () => {
  if (isSubmitting.value) return
  
  // 验证任务名称
  if (!currentFormData.value.name?.trim()) {
    alert('请输入任务名称')
    return
  }
  
  try {
    isSubmitting.value = true
    let requestData: any

    if (dataType.value === DataType.Oblique) {
      // 倾斜摄影验证
      if (!obliqueFormData.value.dataPath?.trim()) {
        alert('请选择数据路径')
        return
      }
      
      const errors = validateObliqueForm(obliqueFormData.value)
      if (errors.size > 0) {
        const errorMessages = Array.from(errors.values()).join('\n')
        alert(errorMessages)
        return
      }
      
      requestData = mapObliqueFormDataToRequest(obliqueFormData.value, userId)
    } else {
      // 通用模型验证
      if (!generalFormData.value.modelPath?.trim()) {
        alert('请输入模型路径')
        return
      }
      
      // ✅ 修复：如果路径指向文件夹（无扩展名），尝试自动补全主模型路径
      let finalModelPath = generalFormData.value.modelPath.trim()
      const lastPart = finalModelPath.split(/[\\\/]/).pop() || ''
      
      // 简单判断是否有扩展名
      if (lastPart && !lastPart.includes('.')) {
        console.log('[SlicingCreate] 检测到文件夹路径，尝试自动补全主模型文件')
        const lowerPart = lastPart.toLowerCase()
        
        // 根据文件夹名称猜测入口文件
        if (lowerPart.includes('obj')) {
          finalModelPath = finalModelPath.replace(/[\\\/]?$/, '') + `/${lastPart}.obj`
        } else if (lowerPart.includes('fbx')) {
          finalModelPath = finalModelPath.replace(/[\\\/]?$/, '') + `/${lastPart}.fbx`
        } else if (lowerPart.includes('gltf') || lowerPart.includes('glb')) {
          finalModelPath = finalModelPath.replace(/[\\\/]?$/, '') + `/${lastPart}.gltf`
        } else if (lowerPart.includes('ifc')) {
          finalModelPath = finalModelPath.replace(/[\\\/]?$/, '') + `/${lastPart}.ifc`
        } else if (lowerPart.includes('dae')) {
          finalModelPath = finalModelPath.replace(/[\\\/]?$/, '') + `/${lastPart}.dae`
        }
        
        console.log('[SlicingCreate] 路径补全结果:', finalModelPath)
      }
      
      requestData = {
        name: generalFormData.value.name,
        sourceModelPath: finalModelPath,
        modelType: 'General3DModel',
        outputPath: generalFormData.value.outputPath || '',
        sceneObjectId: sceneObjectId.value || undefined, // 关联场景对象ID
        slicingConfig: {
          outputFormat: generalFormData.value.outputFormat,
          coordinateSystem: 'EPSG:4326',
          customSettings: '{}',
          divisions: generalFormData.value.divisions,
          lodLevels: generalFormData.value.lodLevels,
          enableMeshDecimation: generalFormData.value.enableMeshDecimation,
          generateTileset: true,
          compressOutput: generalFormData.value.enableCompression,
          enableIncrementalUpdates: false,
          textureStrategy: generalFormData.value.textureStrategy
        }
      }
    }

    console.log('发送的请求数据:', JSON.stringify(requestData, null, 2))
    await slicingService.createSlicingTask(requestData, userId)
    
    // 提交成功，跳转到列表页面
    alert('切片任务创建成功！')
    router.push({ name: 'Slicing' })
  } catch (error: any) {
    console.error('创建切片任务失败:', error)
    console.error('错误详情:', error.response?.data)
    console.error('错误状态:', error.response?.status)
    const errorMessage = error.response?.data?.message || error.message || '创建任务失败'
    alert(`创建任务失败: ${errorMessage}`)
  } finally {
    isSubmitting.value = false
  }
}

// 处理发布
const handlePublish = () => {
  // TODO: 实现发布服务逻辑
  alert('发布服务功能开发中...')
}
</script>

<style scoped>
.slicing-create-page {
  min-height: 100vh;
  background: #f5f7fa;
  display: flex;
  flex-direction: column;
}

/* 页面头部 */
.page-header {
  background: white;
  border-bottom: 1px solid #e1e5e9;
  padding: 1rem 2rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.05);
}

.header-left {
  display: flex;
  align-items: center;
  gap: 1.5rem;
}

.btn-back {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 1rem;
  border: 1px solid #e1e5e9;
  border-radius: 6px;
  background: white;
  cursor: pointer;
  font-size: 0.9rem;
  color: #666;
  transition: all 0.2s ease;
}

.btn-back:hover {
  border-color: #1890ff;
  color: #1890ff;
  background: #f0f7ff;
}

.back-icon {
  font-size: 1.1rem;
}

.page-header h1 {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 600;
  color: #333;
}

/* 页面内容 */
.page-content {
  flex: 1;
  padding: 1.5rem;
  max-width: 1400px;
  margin: 0 auto;
  width: 100%;
}

.form-container {
  background: white;
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
  overflow: hidden;
}

/* 表单区域 */
.form-section {
  padding: 1.5rem;
  border-bottom: 1px solid #f0f0f0;
}

.form-section:last-of-type {
  border-bottom: none;
}

.section-title {
  margin: 0 0 1.25rem 0;
  font-size: 1rem;
  font-weight: 600;
  color: #333;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.title-icon {
  font-size: 1.1rem;
}

/* 表单行 */
.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.5rem;
  margin-bottom: 1rem;
}

/* 表单组 */
.form-group {
  margin-bottom: 1rem;
}

.form-group:last-child {
  margin-bottom: 0;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 500;
  font-size: 0.9rem;
  color: #333;
}

.required {
  color: #ff4d4f;
}

.form-input,
.form-textarea {
  width: 100%;
  padding: 0.5rem 0.75rem;
  border: 1px solid #d9d9d9;
  border-radius: 6px;
  font-size: 0.9rem;
  transition: all 0.2s ease;
}

.form-input:focus,
.form-textarea:focus {
  outline: none;
  border-color: #1890ff;
  box-shadow: 0 0 0 2px rgba(24, 144, 255, 0.1);
}

.form-textarea {
  min-height: 80px;
  max-height: 120px;
  resize: vertical;
  line-height: 1.5;
}

/* 数据类型选择器 */
.type-selector {
  display: flex;
  gap: 0.75rem;
}

.type-option {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  padding: 0.6rem 1rem;
  border: 1px solid #d9d9d9;
  border-radius: 6px;
  background: white;
  cursor: pointer;
  font-size: 0.9rem;
  color: #666;
  transition: all 0.2s ease;
}

.type-option:hover {
  border-color: #1890ff;
  color: #1890ff;
}

.type-option.active {
  border-color: #1890ff;
  background: #e6f7ff;
  color: #1890ff;
}

.type-icon {
  font-size: 1.1rem;
}

.type-text {
  font-weight: 500;
}

/* 工作流区域 */
.workflow-section {
  background: #fafafa;
}

/* 操作按钮 */
.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
  padding: 1rem 1.5rem;
  background: #fafafa;
  border-top: 1px solid #f0f0f0;
}

.btn {
  padding: 0.5rem 1.5rem;
  border-radius: 6px;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 500;
  transition: all 0.2s ease;
}

.btn-primary {
  background: #1890ff;
  color: white;
  border: none;
}

.btn-primary:hover:not(:disabled) {
  background: #40a9ff;
}

.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-secondary {
  background: white;
  color: #666;
  border: 1px solid #d9d9d9;
}

.btn-secondary:hover {
  border-color: #1890ff;
  color: #1890ff;
}

/* 过渡动画 */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

/* 响应式设计 */
@media (max-width: 768px) {
  .page-header {
    padding: 1rem;
  }

  .page-content {
    padding: 1rem;
  }

  .form-section {
    padding: 1rem;
  }

  .form-row {
    grid-template-columns: 1fr;
    gap: 1rem;
  }

  .type-selector {
    flex-direction: column;
  }
}
</style>
