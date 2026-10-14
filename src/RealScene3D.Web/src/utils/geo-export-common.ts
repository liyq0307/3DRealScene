// 将 GeoJSON 坐标转换为 3D 坐标，Z 值默认为 0 或从属性中提取
export function to3DCoords(coords: any, z: number): any {
  if (!Array.isArray(coords)) {
    return coords
  }
  if (coords.length === 0) {
    return coords
  }
  if (typeof coords[0] === "number") {
    const x = coords[0]
    const y = coords[1]
    return [x, y, z]
  }
  return coords.map((item) => to3DCoords(item, z))
}

// 根据路径获取文件名（兼容 Windows 和 Unix 路径分隔符）
export function getFileNameByPath(path: string) {
  const parts = path.split("/")
  return parts[parts.length - 1] || path
}

// 根据文件名判断 MIME 类型，主要针对 Shapefile 相关的扩展名
export function getMimeTypeByName(name: string) {
  if (name.endsWith(".dxf")) {
    return "application/dxf"
  }
  if (name.endsWith(".shp")) {
    return "application/octet-stream"
  }
  if (name.endsWith(".dbf")) {
    return "application/octet-stream"
  }
  if (name.endsWith(".shx")) {
    return "application/octet-stream"
  }
  if (name.endsWith(".prj")) {
    return "text/plain"
  }
  if (name.endsWith(".cpg")) {
    return "text/plain"
  }
  return "application/octet-stream"
}

export interface ConvertedFile {
  name: string
  bytes: Uint8Array
  mimeType: string
}
