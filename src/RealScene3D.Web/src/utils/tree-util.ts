/**
 * 递归重组树节点字段名 / 树工具函数
 */

interface RawTreeNode {
  id: number
  name: string
  key?: string
  children?: RawTreeNode[]
  pid: number
  jsoncs?: string
  jsonjg?: string
  treeid?: string | number
}

interface TargetTreeNode {
  id: number | string
  title?: string
  key?: string
  children?: TargetTreeNode[]
  pid: number
  jsoncs?: string
  jsonjg?: string
}

interface TransformResult {
  tree: TargetTreeNode[]
}

export const transTree = (rawNodes: any): TransformResult => {
  const transformNode = (node: RawTreeNode): TargetTreeNode => {
    const transformedNode: TargetTreeNode = {
      id: node.treeid as any,
      title: node.name,
      key: node.treeid != null ? String(node.treeid) : undefined,
      ...(node as any)
    }

    if (node.children && node.children.length > 0) {
      transformedNode.children = node.children.map(child => transformNode(child))
    }

    return transformedNode
  }

  const transformedTree = rawNodes.map((node: RawTreeNode) => transformNode(node))
  return {
    tree: transformedTree
  }
}

export const collectAllNodeKeys = (nodes: RawTreeNode[]): string[] => {
  const keys: string[] = []
  const collectKey = (node: RawTreeNode) => {
    if (node.treeid != null) {
      keys.push(String(node.treeid))
    }
    if (node.children) {
      node.children.forEach(child => collectKey(child))
    }
  }
  nodes.forEach(node => collectKey(node))
  return keys
}

export function findNodeNameByPid(treeData: any[], pid: number | string): string {
  if (!treeData || pid === 0 || pid === "0") {
    return ""
  }
  for (const node of treeData) {
    if (node.id === pid) {
      return node.name
    }
    if (node.children && node.children.length > 0) {
      const name = findNodeNameByPid(node.children, pid)
      if (name) {
        return name
      }
    }
  }
  return ""
}

export function findNodeLevelByPid(treeData: any[], pid: number | string): number {
  if (!treeData || pid === 0 || pid === "0") {
    return 0
  }
  for (const node of treeData) {
    if (node.id === pid) {
      return node.idx || 1
    }
    if (node.children && node.children.length > 0) {
      const level = findNodeLevelByPid(node.children, pid)
      if (level !== 0) {
        return level
      }
    }
  }
  return 0
}

export function listToTree(list: any[], pid: number | string = 0): any[] {
  const tree: any[] = []
  list.forEach(item => {
    if (item.pid === pid) {
      const children = listToTree(list, item.id)
      if (children.length > 0) {
        item.children = children
      }
      tree.push(item)
    }
  })
  return tree
}
