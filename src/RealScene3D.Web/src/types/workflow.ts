/**
 * 工作流相关类型定义
 * 定义工作流设计器中使用的所有数据结构
 */

// 节点位置接口
export interface Position {
  x: number;
  y: number;
}

// 工作流节点接口
export interface WorkflowNode {
  id: string;
  type: string;
  label: string;
  position: Position;
  config?: Record<string, any>;
  inputs?: string[];
  outputs?: string[];
}

// 节点连接接口
export interface WorkflowConnection {
  id: string;
  source: string; // 源节点ID
  target: string; // 目标节点ID
  sourceOutput?: string;
  targetInput?: string;
}

// 工作流定义接口
export interface WorkflowDefinition {
  id?: string;
  name: string;
  description?: string;
  nodes: WorkflowNode[];
  connections: WorkflowConnection[];
  version?: string;
  createdAt?: string;
  updatedAt?: string;
}

// 节点类型配置
export interface NodeTypeConfig {
  type: string;
  label: string;
  category: string;
  icon?: string;
  color?: string;
  defaultConfig?: Record<string, any>;
  inputs?: string[];
  outputs?: string[];
  description?: string;
}

// 画布状态接口
export interface CanvasState {
  scale: number;
  offsetX: number;
  offsetY: number;
  selectedNodeId?: string;
  selectedConnectionId?: string;
}

// 拖拽状态接口
export interface DragState {
  isDragging: boolean;
  draggedNode?: WorkflowNode;
  dragOffset?: Position;
  dragStartPosition?: Position;
}

// 连接状态接口
export interface ConnectionState {
  isConnecting: boolean;
  connectionStart?: {
    nodeId: string;
    output?: string;
  };
  tempConnection?: {
    start: Position;
    end: Position;
  };
}