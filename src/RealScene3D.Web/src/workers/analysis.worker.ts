/**
 * 分析计算 Worker
 * 处理复杂计算任务，避免阻塞主线程
 */

// 监听主线程消息
self.onmessage = function (e: MessageEvent) {
  const { id, type, data } = e.data

  try {
    let result: any

    switch (type) {
      case 'profile-calculate':
        result = calculateProfile(data)
        break
      case 'volume-calculate':
        result = calculateVolume(data)
        break
      case 'contour-calculate':
        result = calculateContour(data)
        break
      case 'slope-calculate':
        result = calculateSlope(data)
        break
      case 'building-spacing':
        result = calculateBuildingSpacing(data)
        break
      case 'business-format':
        result = calculateBusinessFormat(data)
        break
      default:
        throw new Error(`未知的计算类型: ${type}`)
    }

    self.postMessage({ id, result })
  } catch (err) {
    self.postMessage({ id, error: (err as Error).message })
  }
}

/** 剖面数据计算 */
function calculateProfile(data: {
  positions: Array<{ lng: number; lat: number; alt: number }>
  sampleCount: number
}) {
  const { positions, sampleCount } = data
  if (!positions || positions.length < 2) {
    return { profileData: [], totalLength: 0, maxElevation: 0, minElevation: 0 }
  }

  const profileData: Array<{ distance: number; elevation: number }> = []
  let totalDistance = 0
  let maxElevation = -Infinity
  let minElevation = Infinity

  for (let i = 0; i < positions.length; i++) {
    const elevation = positions[i].alt || 0
    if (elevation > maxElevation) maxElevation = elevation
    if (elevation < minElevation) minElevation = elevation

    if (i > 0) {
      const dx = positions[i].lng - positions[i - 1].lng
      const dy = positions[i].lat - positions[i - 1].lat
      const dz = positions[i].alt - positions[i - 1].alt
      totalDistance += Math.sqrt(dx * dx + dy * dy + dz * dz) * 111319.9 // 粗略转换
    }

    profileData.push({ distance: totalDistance, elevation })
  }

  return { profileData, totalLength: totalDistance, maxElevation, minElevation }
}

/** 体积计算 */
function calculateVolume(data: {
  positions: Array<{ lng: number; lat: number; alt: number }>
  baseHeight: number
  method: string
}) {
  const { positions, baseHeight, method } = data
  if (!positions || positions.length < 3) {
    return { totalVolume: 0, fillVolume: 0, cutVolume: 0, area: 0 }
  }

  // 计算多边形面积（Shoelace公式）
  let area = 0
  const n = positions.length
  for (let i = 0; i < n; i++) {
    const j = (i + 1) % n
    area += positions[i].lng * positions[j].lat
    area -= positions[j].lng * positions[i].lat
  }
  area = Math.abs(area) / 2 * 111319.9 * 111319.9 // 粗略转换

  // 计算平均高度
  let sumHeight = 0
  for (const p of positions) {
    sumHeight += p.alt || 0
  }
  const avgHeight = sumHeight / n

  const heightDiff = avgHeight - baseHeight
  const totalVolume = Math.abs(area * heightDiff)
  const fillVolume = heightDiff > 0 ? area * heightDiff : 0
  const cutVolume = heightDiff < 0 ? area * Math.abs(heightDiff) : 0

  return { totalVolume, fillVolume, cutVolume, area, avgHeight, baseHeight, calcMethod: method }
}

/** 等高线计算 */
function calculateContour(data: {
  positions: Array<{ x: number; y: number; z: number }>
  spacing: number
  gridSize: number
}) {
  const { positions, spacing, gridSize } = data
  // 简化的等高线生成算法
  const contourLines: Array<{ elevation: number; points: Array<{ x: number; y: number }> }> = []

  if (positions.length === 0) return { contourLines }

  let minZ = Infinity, maxZ = -Infinity
  for (const p of positions) {
    if (p.z < minZ) minZ = p.z
    if (p.z > maxZ) maxZ = p.z
  }

  for (let z = Math.ceil(minZ / spacing) * spacing; z <= maxZ; z += spacing) {
    contourLines.push({
      elevation: z,
      points: [] // 实际实现需要Marching Squares算法
    })
  }

  return { contourLines, minElevation: minZ, maxElevation: maxZ }
}

/** 坡度计算 */
function calculateSlope(data: {
  positions: Array<{ lng: number; lat: number; alt: number }>
  sampleInterval: number
}) {
  const { positions } = data
  if (positions.length < 2) return { minSlope: 0, maxSlope: 0, avgSlope: 0 }

  const slopes: number[] = []
  for (let i = 1; i < positions.length; i++) {
    const dx = (positions[i].lng - positions[i - 1].lng) * 111319.9
    const dy = (positions[i].lat - positions[i - 1].lat) * 111319.9
    const dz = positions[i].alt - positions[i - 1].alt
    const horizontalDist = Math.sqrt(dx * dx + dy * dy)
    if (horizontalDist > 0) {
      slopes.push(Math.atan(Math.abs(dz) / horizontalDist) * (180 / Math.PI))
    }
  }

  if (slopes.length === 0) return { minSlope: 0, maxSlope: 0, avgSlope: 0 }

  const minSlope = Math.min(...slopes)
  const maxSlope = Math.max(...slopes)
  const avgSlope = slopes.reduce((a, b) => a + b, 0) / slopes.length

  return { minSlope, maxSlope, avgSlope, slopes }
}

/** 建筑间距计算 */
function calculateBuildingSpacing(data: {
  buildings: Array<{ id: string; position: { lng: number; lat: number }; minDistance: number }>
}) {
  const { buildings } = data
  const pairs: Array<{
    buildingA: string; buildingB: string
    distance: number; minRequiredDistance: number; compliant: boolean
  }> = []

  for (let i = 0; i < buildings.length; i++) {
    for (let j = i + 1; j < buildings.length; j++) {
      const a = buildings[i]
      const b = buildings[j]
      const dx = (a.position.lng - b.position.lng) * 111319.9
      const dy = (a.position.lat - b.position.lat) * 111319.9
      const distance = Math.sqrt(dx * dx + dy * dy)
      const minRequired = Math.max(a.minDistance || 0, b.minDistance || 0)

      pairs.push({
        buildingA: a.id,
        buildingB: b.id,
        distance,
        minRequiredDistance: minRequired,
        compliant: distance >= minRequired
      })
    }
  }

  const violations = pairs.filter(p => !p.compliant).length
  return {
    pairs,
    totalViolations: violations,
    totalPairs: pairs.length,
    complianceRate: pairs.length > 0 ? (pairs.length - violations) / pairs.length : 1
  }
}

/** 业态分析计算 */
function calculateBusinessFormat(data: {
  items: Array<{ category: string; area: number }>
}) {
  const { items } = data
  const categoryMap = new Map<string, { count: number; area: number }>()

  for (const item of items) {
    const existing = categoryMap.get(item.category)
    if (existing) {
      existing.count++
      existing.area += item.area
    } else {
      categoryMap.set(item.category, { count: 1, area: item.area })
    }
  }

  const totalArea = items.reduce((sum, item) => sum + item.area, 0)
  const statistics = Array.from(categoryMap.entries()).map(([category, data]) => ({
    category,
    count: data.count,
    area: data.area,
    percentage: totalArea > 0 ? data.area / totalArea : 0
  }))

  return { statistics, totalArea, totalCount: items.length }
}
