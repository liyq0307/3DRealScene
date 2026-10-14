import { to3DCoords, type ConvertedFile } from "./geo-export-common"

// 专为 DXF 输出处理：需要 3D 坐标且 properties 通常为空以避免图层字段警告
function buildDxfGeojson(source: any) {
  return {
    type: "FeatureCollection",
    features: source.features
      .filter((feature: any) => feature?.geometry?.coordinates && feature.geometry.coordinates.length > 0)
      .map((feature: any) => {
        const elevation = Number(feature?.properties?.elevation)
        const z = Number.isFinite(elevation) ? elevation : 0
        return {
          type: "Feature",
          geometry: {
            type: feature.geometry.type,
            coordinates: to3DCoords(feature.geometry.coordinates, z)
          },
          properties: {}
        }
      })
  }
}

// 导出 DXF 格式
export async function convertToDXF(Gdal: any, geojson: any): Promise<ConvertedFile[]> {
  const datasetName = "input_dxf.json"
  const inputGeojson = buildDxfGeojson(geojson)
  const geojsonStr = JSON.stringify(inputGeojson)

  const inputFile = new File([geojsonStr], datasetName, { type: "application/geo+json" })
  const result = await Gdal.open([inputFile])

  try {
    const timestamp = new Date().getTime()
    const outputName = `dgx_${timestamp}.dxf`
    const output = await Gdal.ogr2ogr(result.datasets[0], ["-f", "DXF"], outputName)
    const bytes = await Gdal.getFileBytes(output)
    return [{ name: outputName, bytes, mimeType: "application/dxf" }]
  } finally {
    if (result.datasets[0]) {
      await Gdal.close(result.datasets[0])
    }
  }
}
