/**
 * 工作流API服务
 * 负责与后端工作流API的通信
 */

import axios from 'axios'
import type { WorkflowDefinition } from '@/types/workflow'

// API基础URL
const API_BASE_URL = '/api/workflows'

// 创建axios实例
const apiClient = axios.create({
  baseURL: API_BASE_URL,
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json'
  }
})

// 请求拦截器：添加认证token
apiClient.interceptors.request.use(
  (config) => {
    // TODO: 从localStorage或状态管理中获取token
    const token = localStorage.getItem('authToken')
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
    return config
  },
  (error) => {
    return Promise.reject(error)
  }
)

// 响应拦截器：处理通用错误
apiClient.interceptors.response.use(
  (response) => {
    return response.data
  },
  (error) => {
    if (error.response) {
      // 服务器返回错误状态码
      const { status, data } = error.response
      switch (status) {
        case 401:
          // 未授权，跳转到登录页
          console.error('未授权访问')
          // TODO: 跳转到登录页
          break
        case 403:
          console.error('访问被拒绝')
          break
        case 404:
          console.error('资源不存在')
          break
        case 500:
          console.error('服务器内部错误')
          break
        default:
          console.error(`请求失败: ${status}`, data)
      }
    } else if (error.request) {
      // 网络错误
      console.error('网络错误，请检查网络连接')
    } else {
      // 其他错误
      console.error('请求配置错误', error.message)
    }
    return Promise.reject(error)
  }
)

// 工作流服务接口
export interface WorkflowService {
  // 获取工作流列表
  getWorkflows(): Promise<WorkflowDefinition[]>

  // 获取单个工作流
  getWorkflow(id: string): Promise<WorkflowDefinition>

  // 创建工作流
  createWorkflow(workflow: Omit<WorkflowDefinition, 'id'>): Promise<WorkflowDefinition>

  // 更新工作流
  updateWorkflow(id: string, workflow: Partial<WorkflowDefinition>): Promise<WorkflowDefinition>

  // 删除工作流
  deleteWorkflow(id: string): Promise<void>

  // 保存工作流（创建或更新）
  saveWorkflow(workflow: WorkflowDefinition): Promise<WorkflowDefinition>

  // 启动工作流实例
  startWorkflowInstance(workflowId: string, inputData?: any): Promise<any>

  // 获取工作流实例列表
  getWorkflowInstances(workflowId?: string): Promise<any[]>

  // 获取工作流实例详情
  getWorkflowInstance(instanceId: string): Promise<any>

  // 暂停工作流实例
  suspendWorkflowInstance(instanceId: string): Promise<void>

  // 恢复工作流实例
  resumeWorkflowInstance(instanceId: string): Promise<void>

  // 取消工作流实例
  cancelWorkflowInstance(instanceId: string): Promise<void>

  // 获取工作流执行历史
  getWorkflowHistory(instanceId: string): Promise<any[]>
}

// 工作流服务实现
class WorkflowApiService implements WorkflowService {
  async getWorkflows(): Promise<WorkflowDefinition[]> {
    return await apiClient.get('')
  }

  async getWorkflow(id: string): Promise<WorkflowDefinition> {
    return await apiClient.get(`/${id}`)
  }

  async createWorkflow(workflow: Omit<WorkflowDefinition, 'id'>): Promise<WorkflowDefinition> {
    return await apiClient.post('', workflow)
  }

  async updateWorkflow(id: string, workflow: Partial<WorkflowDefinition>): Promise<WorkflowDefinition> {
    return await apiClient.put(`/${id}`, workflow)
  }

  async deleteWorkflow(id: string): Promise<void> {
    await apiClient.delete(`/${id}`)
  }

  async saveWorkflow(workflow: WorkflowDefinition): Promise<WorkflowDefinition> {
    if (workflow.id) {
      return await this.updateWorkflow(workflow.id, workflow)
    } else {
      return await this.createWorkflow(workflow)
    }
  }

  async startWorkflowInstance(workflowId: string, inputData?: any): Promise<any> {
    return await apiClient.post(`/${workflowId}/instances`, {
      inputParameters: inputData
    })
  }

  async getWorkflowInstances(workflowId?: string): Promise<any[]> {
    const url = workflowId ? `/instances?workflowId=${workflowId}` : '/instances'
    return await apiClient.get(url)
  }

  async getWorkflowInstance(instanceId: string): Promise<any> {
    return await apiClient.get(`/instances/${instanceId}`)
  }

  async suspendWorkflowInstance(instanceId: string): Promise<void> {
    await apiClient.post(`/instances/${instanceId}/suspend`)
  }

  async resumeWorkflowInstance(instanceId: string): Promise<void> {
    await apiClient.post(`/instances/${instanceId}/resume`)
  }

  async cancelWorkflowInstance(instanceId: string): Promise<void> {
    await apiClient.post(`/instances/${instanceId}/cancel`)
  }

  async getWorkflowHistory(instanceId: string): Promise<any[]> {
    return await apiClient.get(`/instances/${instanceId}/history`)
  }
}

// 创建服务实例
export const workflowService = new WorkflowApiService()

// 导出服务
export default workflowService