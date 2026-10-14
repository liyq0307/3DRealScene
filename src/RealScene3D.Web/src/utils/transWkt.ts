// 矩形转wkt

export function rectTwoPointsToWktSafe(positions: number[][]) {
  const leftTopX = positions[0][0]
  const leftTopY = positions[0][1]
  const rightBottomX = positions[1][0]
  const rightBottomY = positions[1][1]
  const minX = Math.min(leftTopX, rightBottomX)
  const maxX = Math.max(leftTopX, rightBottomX)
  const maxY = Math.max(leftTopY, rightBottomY)
  const minY = Math.min(leftTopY, rightBottomY)

  const points = [
    [minX, maxY], [maxX, maxY], [maxX, minY], [minX, minY], [minX, maxY]
  ]

  const pointStr = points.map(pt => `${pt[0]} ${pt[1]}`).join(", ")
  return `POLYGON ((${pointStr}))`
}

/**
 * 从圆心+米级半径生成圆形 WKT（适配地理坐标系）
 */
export function circleToWktByMeter(cx: number, cy: number, radius: number, sides: number = 36) {
  const meterPerDegreeLat = 111319
  const meterPerDegreeLng = 111319 * Math.cos(cy * Math.PI / 180)

  const points: string[][] = []
  for (let i = 0; i <= sides; i++) {
    const angle = (i / sides) * 2 * Math.PI
    const lngOffset = (radius * Math.cos(angle)) / meterPerDegreeLng
    const latOffset = (radius * Math.sin(angle)) / meterPerDegreeLat
    const x = cx + lngOffset
    const y = cy + latOffset
    points.push([x.toFixed(6), y.toFixed(6)])
  }

  const pointStr = points.map(pt => `${pt[0]} ${pt[1]}`).join(", ")
  return `POLYGON ((${pointStr}))`
}
