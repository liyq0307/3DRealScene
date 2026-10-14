import JSZip from "jszip"
import { to3DCoords, getFileNameByPath, getMimeTypeByName, type ConvertedFile } from "./geo-export-common"

// 专为 SHP 或通用格式处理：保留所有属性，并确保坐标为 3D
function buildFull3DGeojson(source: any) {
  return {
    type: "FeatureCollection",
    features: source.features
      .filter((feature: any) => feature?.geometry?.coordinates && feature.geometry.coordinates.length > 0)
      .map((feature: any) => {
        const elevation = Number(feature?.properties?.elevation)
        const z = Number.isFinite(elevation) ? elevation : 0

        // 如果 geometry 是 2D 坐标，则转换为 3D，否则保持原样
        const coordinates = to3DCoords(feature.geometry.coordinates, z)

        return {
          ...feature,
          geometry: {
            ...feature.geometry,
            coordinates
          }
        }
      })
  }
}

// 导出 SHP 格式
export async function convertToSHP(Gdal: any, geojson: any): Promise<ConvertedFile[]> {
  try {
    const features = geojson.features || []
    // 按照几何类型分组，Shapefile 不支持在同一个图层中混合点、线、面
    const groups: Record<string, any[]> = {}

    features.forEach((feature: any) => {
      let type = feature?.geometry?.type
      if (!type) {
        return
      }

      // 简单处理 Multi 类型，映射到基础类型
      if (type.startsWith("Multi")) {
        type = type.replace("Multi", "")
      }

      if (!groups[type]) {
        groups[type] = []
      }
      groups[type].push(feature)
    })

    const allZipFiles: ConvertedFile[] = []
    const timestamp = new Date().getTime()
    //  逐个几何类型生成独立的 Shapefile，确保兼容性
    for (const [type, typeFeatures] of Object.entries(groups)) {
      const typeGeojson = buildFull3DGeojson({ type: "FeatureCollection", features: typeFeatures })
      const datasetName = `input_${type.toLowerCase()}_${timestamp}.json`
      const geojsonStr = JSON.stringify(typeGeojson)
      const inputFile = new File([geojsonStr], datasetName, { type: "application/geo+json" })

      const result = await Gdal.open([inputFile])
      if (!result || !result.datasets || result.datasets.length === 0) {
        continue
      }

      const inputDataset = result.datasets[0]
      const outputPrefix = `dgx_${type.toLowerCase()}_${timestamp}`

      try {
        // -nlt specify geometry type to ensure consistency
        // Point, LineString, Polygon
        let nltType = "FROM_SOURCE"
        if (type === "Point") {
          nltType = "POINTZ"
        } else if (type === "LineString") {
          nltType = "LINESTRINGZ"
        } else if (type === "Polygon") {
          nltType = "POLYGONZ"
        }

        await Gdal.ogr2ogr(
          inputDataset,
          ["-f", "ESRI Shapefile", "-nln", outputPrefix, "-overwrite", "-lco", "ENCODING=UTF-8", "-dim", "3", "-nlt", nltType],
          outputPrefix
        )

        const allFiles = await Gdal.getOutputFiles()
        const shpFiles = allFiles.map((f: any) => (typeof f === "string" ? f : f.path)).filter((f: string) => f && f.includes(`/${outputPrefix}.`))

        if (shpFiles.length > 0) {
          const zip = new JSZip()
          for (const filePath of shpFiles) {
            const bytes = await Gdal.getFileBytes(filePath)
            const name = getFileNameByPath(filePath)
            zip.file(name, bytes)
          }

          const zipBlob = await zip.generateAsync({ type: "blob" })
          const zipBytes = new Uint8Array(await zipBlob.arrayBuffer())
          const zipName = `${outputPrefix}.zip`

          allZipFiles.push({
            name: zipName,
            bytes: zipBytes,
            mimeType: "application/zip"
          })
        }
      } finally {
        await Gdal.close(inputDataset)
      }
    }

    if (allZipFiles.length === 0) {
      throw new Error("SHP conversion generated no files")
    }

    return allZipFiles
  } catch (err) {
    console.error("SHP conversion error:", err)
    throw err
  }
}
