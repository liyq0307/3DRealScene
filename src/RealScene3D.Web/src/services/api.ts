/**
 * 前端API服务模块
 * 封装所有后端API调用的服务层，基于axios提供统一的HTTP客户端
 * 包含场景管理、用户认证、切片处理等完整API服务
 * 支持统一的错误处理、请求拦截和响应处理
 */

import axios from 'axios'
import authStore from '@/stores/auth'
import { useMessage } from '@/composables/useMessage'

/**
 * API基础URL配置
 * 从环境变量VITE_API_URL获取，支持开发和生产环境切换
 * 默认值：http://localhost:5000/api（开发环境）
 */
const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000/api'

/**
 * Axios实例配置
 * 统一的HTTP客户端，预配置基础URL和默认请求头
 * 可在此处添加请求/响应拦截器、认证token等全局配置
 */
const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json'
  }
})

// ==================== 请求拦截器 ====================

/**
 * 请求拦截器
 * 在每个请求发送前自动注入认证令牌
 */
api.interceptors.request.use(
  (config) => {
    // 获取token
    const token = authStore.token.value

    // 如果token存在，添加到请求头
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }

    return config
  },
  (error) => {
    return Promise.reject(error)
  }
)

/**
 * 响应拦截器
 * 统一处理错误响应和token过期
 */
api.interceptors.response.use(
  (response) => {
    return response
  },
  async (error) => {
    const { error: showError } = useMessage()

    // 处理不同的HTTP状态码
    if (error.response) {
      switch (error.response.status) {
        case 401:
          // 未授权 - token过期或无效
          showError('登录已过期，请重新登录')
          authStore.logout()
          // 重定向到登录页
          window.location.href = '/login'
          break
        case 403:
          // 禁止访问
          showError('您没有权限访问此资源')
          break
        case 404:
          // 资源未找到
          showError('请求的资源不存在')
          break
        case 500:
          // 服务器错误
          showError('服务器错误，请稍后重试')
          break
        default:
          showError(error.response.data?.message || '请求失败')
      }
    } else if (error.request) {
      // 请求已发送但没有收到响应
      showError('网络连接失败，请检查网络')
    } else {
      // 其他错误
      showError('请求失败：' + error.message)
    }

    return Promise.reject(error)
  }
)

// ==================== 场景管理服务 ====================

/**
 * 场景相关API服务
 * 提供3D场景的完整CRUD操作接口
 */
export const sceneService = {
  /**
   * 获取所有公开场景列表
   * @returns Promise<SceneDto[]> 场景数组
   */
  async getAllScenes() {
    const response = await api.get('/scenes')
    return response.data
  },

  /**
   * 根据ID获取单个场景详情
   * @param id 场景唯一标识符
   * @returns Promise<SceneDto> 场景详情对象
   */
  async getScene(id: string) {
    const response = await api.get(`/scenes/${id}`)
    return response.data
  },

  /**
   * 获取指定用户的所有场景列表
   * @param userId 用户ID
   * @returns Promise<SceneDto[]> 用户拥有的场景列表
   */
  async getUserScenes(userId: string) {
    const response = await api.get(`/scenes/user/${userId}`)
    return response.data
  },

  /**
   * 创建新场景
   * @param data 场景创建数据，包含名称、描述、地理边界等
   * @param ownerId 场景拥有者ID
   * @returns Promise<SceneDto> 创建成功的场景对象
   */
  async createScene(data: any, ownerId: string) {
    const response = await api.post('/scenes', data, {
      params: { ownerId }
    })
    return response.data
  },

  /**
   * 删除指定场景
   * @param id 场景ID
   * @param userId 用户ID（用于权限验证）
   */
  async deleteScene(id: string, userId: string) {
    await api.delete(`/scenes/${id}?userId=${userId}`)
  },

  /**
   * 更新场景信息
   * @param id 场景ID
   * @param data 更新数据
   * @param userId 用户ID（用于权限验证）
   * @returns Promise<SceneDto> 更新后的场景对象
   */
  async updateScene(id: string, data: any, userId: string) {
    const response = await api.put(`/scenes/${id}?userId=${userId}`, data)
    return response.data
  }
}

// ==================== 场景对象管理服务 ====================

/**
 * 场景对象相关API服务
 * 提供场景中3D对象的完整管理功能
 */
export const sceneObjectService = {
  /**
   * 获取指定场景的所有对象列表
   * @param sceneId 场景ID
   * @returns Promise<SceneObjectDto[]> 场景对象数组
   */
  async getSceneObjects(sceneId: string) {
    const response = await api.get(`/sceneobjects/scene/${sceneId}`)
    return response.data
  },

  /**
   * 在场景中创建新的3D对象
   * @param data 对象创建数据，包含位置、旋转、缩放、模型路径等
   * @returns Promise<SceneObjectDto> 创建成功的对象信息
   */
  async createObject(data: any) {
    const response = await api.post('/sceneobjects', data)
    return response.data
  },

  /**
   * 删除指定的场景对象
   * @param id 对象ID
   */
  async deleteObject(id: string) {
    await api.delete(`/sceneobjects/${id}`)
  }
}

// ==================== 用户认证服务 ====================

/**
 * 用户认证相关API服务
 * 提供登录、注册、用户信息管理等认证功能
 */
export const userService = {
  /**
   * 用户登录
   * @param email 用户邮箱
   * @param password 用户密码
   * @returns Promise<LoginResponse> 登录响应，包含JWT令牌和用户信息
   */
  async login(email: string, password: string) {
    const response = await api.post('/users/login', { email, password })
    return response.data
  },

  /**
   * 用户注册
   * @param username 用户名
   * @param email 邮箱地址
   * @param password 密码
   * @returns Promise<LoginResponse> 注册成功的登录响应,包含JWT令牌和用户信息
   */
  async register(username: string, email: string, password: string) {
    const response = await api.post('/users/register', { username, email, password })
    return response.data
  }
}

/**
 * 工作流相关API服务
 * 提供工作流定义和实例管理的完整CRUD操作
 */
export const workflowService = {
  /**
   * 获取用户的工作流列表
   * @param userId 用户ID
   * @returns Promise<WorkflowDto[]> 工作流数组
   */
  async getUserWorkflows(userId: string) {
    const response = await api.get(`/workflows/user/${userId}`)
    const workflows = response.data

    // 解析每个工作流的 definition 字段
    return workflows.map((workflowDto: any) => {
      if (workflowDto.definition) {
        try {
          const definition = JSON.parse(workflowDto.definition)
          return {
            ...workflowDto,
            nodes: definition.nodes || [],
            connections: definition.connections || []
          }
        } catch (error) {
          console.error('Failed to parse workflow definition:', error)
          return workflowDto
        }
      }
      return workflowDto
    })
  },

  /**
   * 获取所有工作流列表
   * @returns Promise<WorkflowDto[]> 工作流数组
   */
  async getAllWorkflows() {
    const response = await api.get('/workflows')
    const workflows = response.data

    // 解析每个工作流的 definition 字段
    return workflows.map((workflowDto: any) => {
      if (workflowDto.definition) {
        try {
          const definition = JSON.parse(workflowDto.definition)
          return {
            ...workflowDto,
            nodes: definition.nodes || [],
            connections: definition.connections || []
          }
        } catch (error) {
          console.error('Failed to parse workflow definition:', error)
          return workflowDto
        }
      }
      return workflowDto
    })
  },

  /**
   * 根据ID获取单个工作流详情
   * @param id 工作流ID
   * @returns Promise<WorkflowDto> 工作流详情
   */
  async getWorkflow(id: string) {
    const response = await api.get(`/workflows/${id}`)
    const workflowDto = response.data

    // 解析后端返回的 definition 字段（JSON字符串）为前端使用的对象格式
    if (workflowDto.definition) {
      try {
        const definition = JSON.parse(workflowDto.definition)
        return {
          id: workflowDto.id,
          name: workflowDto.name,
          description: workflowDto.description,
          version: workflowDto.version,
          isEnabled: workflowDto.isEnabled,
          createdBy: workflowDto.createdBy,
          createdAt: workflowDto.createdAt,
          updatedAt: workflowDto.updatedAt,
          nodes: definition.nodes || [],
          connections: definition.connections || []
        }
      } catch (error) {
        console.error('Failed to parse workflow definition:', error)
        return workflowDto
      }
    }

    return workflowDto
  },

  /**
   * 创建新工作流
   * @param workflow 工作流数据，包含名称、描述、定义等
   * @param userId 用户ID
   * @returns Promise<WorkflowDto> 创建的工作流
   */
  async createWorkflow(workflow: any, userId: string) {
    // 将工作流对象转换为后端期望的格式
    const request = {
      name: workflow.name || '未命名工作流',
      description: workflow.description || '',
      definition: JSON.stringify({
        nodes: workflow.nodes || [],
        connections: workflow.connections || []
      }),
      version: workflow.version || '1.0.0'
    }

    const response = await api.post('/workflows', request, {
      params: { userId }
    })
    return response.data
  },

  /**
   * 更新工作流定义
   * @param id 工作流ID
   * @param workflow 更新的工作流数据
   * @param userId 用户ID
   * @returns Promise<WorkflowDto> 更新的工作流
   */
  async updateWorkflow(id: string, workflow: any, userId: string) {
    // 将工作流对象转换为后端期望的格式
    const request = {
      name: workflow.name || '未命名工作流',
      description: workflow.description || '',
      definition: JSON.stringify({
        nodes: workflow.nodes || [],
        connections: workflow.connections || []
      }),
      version: workflow.version || '1.0.0',
      isEnabled: workflow.isEnabled !== undefined ? workflow.isEnabled : true
    }

    const response = await api.put(`/workflows/${id}`, request, {
      params: { userId }
    })
    return response.data
  },

  /**
   * 删除工作流
   * @param id 工作流ID
   * @param userId 用户ID
   */
  async deleteWorkflow(id: string, userId: string) {
    await api.delete(`/workflows/${id}`, {
      params: { userId }
    })
  },

  /**
   * 保存工作流（创建或更新）
   * @param workflow 工作流数据
   * @param userId 用户ID
   * @returns Promise<WorkflowDto> 保存的工作流
   */
  async saveWorkflow(workflow: any, userId: string) {
    if (workflow.id) {
      return await this.updateWorkflow(workflow.id, workflow, userId)
    } else {
      return await this.createWorkflow(workflow, userId)
    }
  },

  /**
   * 启动工作流实例
   * @param workflowId 工作流ID
   * @param request 启动请求数据
   * @param userId 用户ID
   * @returns Promise<WorkflowInstanceDto> 创建的实例
   */
  async startWorkflowInstance(workflowId: string, request: any, userId: string) {
    const response = await api.post(`/workflows/${workflowId}/instances`, request, {
      params: { userId }
    })
    return response.data
  },

  /**
   * 获取工作流实例列表
   * @param workflowId 可选的工作流ID过滤
   * @param userId 可选的用户ID过滤
   * @param page 页码（从1开始）
   * @param pageSize 每页大小
   * @returns Promise<WorkflowInstanceDto[]> 实例数组
   */
  async getWorkflowInstances(workflowId?: string, userId?: string, page = 1, pageSize = 20) {
    const params: any = { page, pageSize }
    if (workflowId) params.workflowId = workflowId
    if (userId) params.userId = userId

    const response = await api.get('/workflows/instances', { params })
    return response.data
  },

  /**
   * 获取单个工作流实例详情
   * @param instanceId 实例ID
   * @returns Promise<WorkflowInstanceDto> 实例详情
   */
  async getWorkflowInstance(instanceId: string) {
    const response = await api.get(`/workflows/instances/${instanceId}`)
    return response.data
  },

  /**
   * 暂停工作流实例
   * @param instanceId 实例ID
   * @param userId 用户ID
   */
  async suspendWorkflowInstance(instanceId: string, userId: string) {
    await api.post(`/workflows/instances/${instanceId}/suspend`, null, {
      params: { userId }
    })
  },

  /**
   * 恢复工作流实例
   * @param instanceId 实例ID
   * @param userId 用户ID
   */
  async resumeWorkflowInstance(instanceId: string, userId: string) {
    await api.post(`/workflows/instances/${instanceId}/resume`, null, {
      params: { userId }
    })
  },

  /**
   * 取消工作流实例
   * @param instanceId 实例ID
   * @param userId 用户ID
   */
  async cancelWorkflowInstance(instanceId: string, userId: string) {
    await api.post(`/workflows/instances/${instanceId}/cancel`, null, {
      params: { userId }
    })
  },

  /**
   * 获取工作流执行历史
   * @param instanceId 实例ID
   * @returns Promise<WorkflowExecutionHistoryDto[]> 执行历史数组
   */
  async getWorkflowExecutionHistory(instanceId: string) {
    const response = await api.get(`/workflows/instances/${instanceId}/history`)
    return response.data
  }
}

// ==================== 监控系统服务 ====================

/**
 * 监控系统相关API服务
 * 提供系统监控、指标收集、告警管理、仪表板等功能
 */
export const monitoringService = {
  /**
   * 记录系统指标
   */
  async recordSystemMetric(data: any) {
    const response = await api.post('/monitoring/metrics/system', data)
    return response.data
  },

  /**
   * 记录业务指标
   */
  async recordBusinessMetric(data: any) {
    const response = await api.post('/monitoring/metrics/business', data)
    return response.data
  },

  /**
   * 获取系统指标历史数据
   */
  async getSystemMetrics(metricName: string, startTime: Date, endTime: Date, category = '') {
    const response = await api.get(`/monitoring/metrics/system/${metricName}`, {
      params: { startTime: startTime.toISOString(), endTime: endTime.toISOString(), category }
    })
    return response.data
  },

  /**
   * 获取业务指标历史数据
   */
  async getBusinessMetrics(metricName: string, startTime: Date, endTime: Date) {
    const response = await api.get(`/monitoring/metrics/business/${metricName}`, {
      params: { startTime: startTime.toISOString(), endTime: endTime.toISOString() }
    })
    return response.data
  },

  /**
   * 获取最新系统指标快照
   */
  async getLatestSystemMetrics(category = '') {
    const response = await api.get('/monitoring/metrics/system/snapshot', {
      params: { category }
    })
    return response.data
  },

  /**
   * 创建告警规则
   */
  async createAlertRule(data: any, userId: string) {
    const response = await api.post('/monitoring/alert-rules', data, {
      params: { userId }
    })
    return response.data
  },

  /**
   * 获取告警规则列表
   */
  async getAlertRules(userId?: string) {
    const response = await api.get('/monitoring/alert-rules', {
      params: userId ? { userId } : {}
    })
    return response.data
  },

  /**
   * 获取告警规则详情
   */
  async getAlertRule(id: string) {
    const response = await api.get(`/monitoring/alert-rules/${id}`)
    return response.data
  },

  /**
   * 更新告警规则
   */
  async updateAlertRule(id: string, data: any, userId: string) {
    const response = await api.put(`/monitoring/alert-rules/${id}`, data, {
      params: { userId }
    })
    return response.data
  },

  /**
   * 删除告警规则
   */
  async deleteAlertRule(id: string, userId: string) {
    await api.delete(`/monitoring/alert-rules/${id}`, {
      params: { userId }
    })
  },

  /**
   * 获取活跃告警事件
   */
  async getActiveAlerts() {
    const response = await api.get('/monitoring/alerts/active')
    return response.data
  },

  /**
   * 获取告警事件历史
   */
  async getAlertHistory(startTime: Date, endTime: Date, level?: number) {
    const params: any = {
      startTime: startTime.toISOString(),
      endTime: endTime.toISOString()
    }
    if (level !== undefined) {
      params.level = level
    }
    const response = await api.get('/monitoring/alerts/history', { params })
    return response.data
  },

  /**
   * 确认告警事件
   */
  async acknowledgeAlert(id: string, userId: string) {
    await api.post(`/monitoring/alerts/${id}/acknowledge`, null, {
      params: { userId }
    })
  },

  /**
   * 解决告警事件
   */
  async resolveAlert(id: string, userId: string) {
    await api.post(`/monitoring/alerts/${id}/resolve`, null, {
      params: { userId }
    })
  },

  /**
   * 创建监控仪表板
   */
  async createDashboard(data: any, userId: string) {
    const response = await api.post('/monitoring/dashboards', data, {
      params: { userId }
    })
    return response.data
  },

  /**
   * 获取监控仪表板列表
   */
  async getDashboards(userId?: string) {
    const response = await api.get('/monitoring/dashboards', {
      params: userId ? { userId } : {}
    })
    return response.data
  },

  /**
   * 获取仪表板详情
   */
  async getDashboard(id: string) {
    const response = await api.get(`/monitoring/dashboards/${id}`)
    return response.data
  },

  /**
   * 更新监控仪表板
   */
  async updateDashboard(id: string, data: any, userId: string) {
    const response = await api.put(`/monitoring/dashboards/${id}`, data, {
      params: { userId }
    })
    return response.data
  },

  /**
   * 删除监控仪表板
   */
  async deleteDashboard(id: string, userId: string) {
    await api.delete(`/monitoring/dashboards/${id}`, {
      params: { userId }
    })
  }
}

// ==================== 切片管理服务 ====================

/**
 * 3D模型切片管理API服务
 * 提供切片任务管理、切片数据获取、优化算法等功能
 */
export const slicingService = {
  /**
   * 创建切片任务
   */
  async createSlicingTask(data: any, userId: string) {
    const response = await api.post('/slicing/tasks', data, {
      params: { userId }
    })
    return response.data
  },

  /**
   * 获取切片任务详情
   */
  async getSlicingTask(id: string) {
    const response = await api.get(`/slicing/tasks/${id}`)
    return response.data
  },

  /**
   * 获取用户的切片任务列表
   */
  async getUserSlicingTasks(userId: string, page = 1, pageSize = 20) {
    const response = await api.get(`/slicing/tasks/user/${userId}`, {
      params: { page, pageSize }
    })
    return response.data
  },

  /**
   * 获取切片任务实时进度信息
   */
  async getSlicingProgress(id: string) {
    const response = await api.get(`/slicing/tasks/${id}/progress`)
    return response.data
  },

  /**
   * 取消切片任务
   */
  async cancelSlicingTask(id: string, userId: string) {
    await api.post(`/slicing/tasks/${id}/cancel`, null, {
      params: { userId }
    })
  },

  /**
   * 删除切片任务
   */
  async deleteSlicingTask(id: string, userId: string) {
    await api.delete(`/slicing/tasks/${id}`, {
      params: { userId }
    })
  },

  /**
   * 获取特定切片数据
   */
  async getSlice(taskId: string, level: number, x: number, y: number, z: number) {
    const response = await api.get(`/slicing/tasks/${taskId}/slices/${level}/${x}/${y}/${z}`)
    return response.data
  },

  /**
   * 获取指定层级的所有切片元数据
   */
  async getSliceMetadata(taskId: string, level: number) {
    const response = await api.get(`/slicing/tasks/${taskId}/slices/${level}/metadata`)
    return response.data
  },

  /**
   * 下载切片文件
   */
  async downloadSlice(taskId: string, level: number, x: number, y: number, z: number) {
    const response = await api.get(
      `/slicing/tasks/${taskId}/slices/${level}/${x}/${y}/${z}/download`,
      { responseType: 'blob' }
    )

    // 创建下载链接
    const url = window.URL.createObjectURL(new Blob([response.data]))
    const link = document.createElement('a')
    link.href = url
    link.setAttribute('download', `slice_${level}_${x}_${y}_${z}.b3dm`)
    document.body.appendChild(link)
    link.click()
    link.remove()
    window.URL.revokeObjectURL(url)
  },

  /**
   * 批量获取切片
   */
  async getSlicesBatch(taskId: string, level: number, coordinates: Array<[number, number, number]>) {
    const response = await api.post(`/slicing/tasks/${taskId}/slices/${level}/batch`, coordinates)
    return response.data
  },

  /**
   * 执行视锥剔除
   * @param taskId 切片任务ID
   * @param viewport 视口信息，包含相机位置、方向和视锥体参数
   * @param level 可选的LOD层级
   * @returns Promise<any[]> 可见的切片元数据数组
   */
  async performFrustumCulling(taskId: string, viewport: any, level?: number) {
    const requestBody = {
      cameraPosition: {
        x: viewport.camera?.position?.x || 0,
        y: viewport.camera?.position?.y || 0,
        z: viewport.camera?.position?.z || 0
      },
      cameraDirection: {
        x: viewport.camera?.direction?.x || 0,
        y: viewport.camera?.direction?.y || 0,
        z: viewport.camera?.direction?.z || 1
      },
      fieldOfView: viewport.frustum?.fov || Math.PI / 3,
      nearPlane: viewport.frustum?.near || 1.0,
      farPlane: viewport.frustum?.far || 10000.0
    }

    const params = level !== undefined ? { level } : {}
    const response = await api.post(`/slicing/tasks/${taskId}/frustum-culling`, requestBody, {
      params
    })
    return response.data
  },

  /**
   * 预测加载切片
   * @param taskId 切片任务ID
   * @param viewport 当前视口信息，包含相机位置、方向等
   * @param movementVector 移动向量，表示相机移动方向和速度
   * @param predictionTime 预测时间（秒），默认2.0秒
   * @param level 可选的LOD层级
   * @returns Promise<any[]> 需要预加载的切片元数据数组
   */
  async predictLoading(
    taskId: string,
    viewport: any,
    movementVector: any,
    predictionTime = 2.0,
    level?: number
  ) {
    const requestBody = {
      currentViewport: {
        cameraPosition: {
          x: viewport.camera?.position?.x || 0,
          y: viewport.camera?.position?.y || 0,
          z: viewport.camera?.position?.z || 0
        },
        cameraDirection: {
          x: viewport.camera?.direction?.x || 0,
          y: viewport.camera?.direction?.y || 0,
          z: viewport.camera?.direction?.z || 1
        },
        fieldOfView: viewport.frustum?.fov || Math.PI / 3,
        nearPlane: viewport.frustum?.near || 1.0,
        farPlane: viewport.frustum?.far || 10000.0
      },
      movementVector: {
        x: movementVector.x || 0,
        y: movementVector.y || 0,
        z: movementVector.z || 0
      }
    }

    const params: any = { predictionTime }
    if (level !== undefined) {
      params.level = level
    }

    const response = await api.post(`/slicing/tasks/${taskId}/predict-loading`, requestBody, {
      params
    })
    return response.data
  },

  /**
   * 获取切片策略信息
   */
  async getSlicingStrategies() {
    const response = await api.get('/slicing/strategies')
    return response.data
  },

  /**
   * 获取增量更新索引
   */
  async getIncrementalUpdateIndex(taskId: string) {
    const response = await api.get(`/slicing/tasks/${taskId}/incremental-index`)
    return response.data
  }
}

/**
 * 默认导出axios实例
 * 可用于自定义API调用或扩展其他服务
 */
export default api
