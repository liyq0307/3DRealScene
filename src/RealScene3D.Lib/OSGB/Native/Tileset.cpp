#include "Tileset.h"
#include "OSGBTools.h"

// ============================================================================
// 内部辅助函数（仅在本文件内使用）
// ============================================================================

namespace 
{

	/**
	 * @brief 将TileBox转换为3D Tiles box格式数组
	 *
	 * 将TileBox的min/max坐标转换为3D Tiles标准的box格式：
	 * [centerX, centerY, centerZ, halfX, 0, 0, 0, halfY, 0, 0, 0, halfZ]
	 *
	 * @param tile TileBox结构
	 * @return 包含12个double值的vector
	 */
	std::vector<double> convert_bbox(const TileBox& tile)
	{
		double center_mx = (tile.max[0] + tile.min[0]) / 2;
		double center_my = (tile.max[1] + tile.min[1]) / 2;
		double center_mz = (tile.max[2] + tile.min[2]) / 2;
		double x_meter = (tile.max[0] - tile.min[0]) * 1;
		double y_meter = (tile.max[1] - tile.min[1]) * 1;
		double z_meter = (tile.max[2] - tile.min[2]) * 1;

		// 确保最小尺寸
		if (x_meter < 0.01) { x_meter = 0.01; }
		if (y_meter < 0.01) { y_meter = 0.01; }
		if (z_meter < 0.01) { z_meter = 0.01; }

		std::vector<double> v =
		{
			center_mx, center_my, center_mz,
			x_meter / 2, 0, 0,
			0, y_meter / 2, 0,
			0, 0, z_meter / 2
		};

		return v;
	}

} // anonymous namespace

// ============================================================================
// BoundingVolume 实现
// ============================================================================

BoundingVolume BoundingVolume::FromBox(const Box& box)
{
	BoundingVolume bv;
	bv.type = BoundingVolumeType::Box;
	bv.data.assign(box.dMatrix, box.dMatrix + 12);
	return bv;
}

BoundingVolume BoundingVolume::FromRegion(const Region& region)
{
	BoundingVolume bv;
	bv.type = BoundingVolumeType::Region;
	bv.data = {
		region.dMinX,
		region.dMinY,
		region.dMaxX,
		region.dMaxY,
		region.dMinHeight,
		region.dMaxHeight
	};
	return bv;
}

std::string BoundingVolume::ToJson() const
{
	std::string json = "\"boundingVolume\":{";

	if (type == BoundingVolumeType::Box)
	{
		json += "\"box\":[";
	}
	else
	{
		json += "\"region\":[";
	}

	// 添加数据数组
	for (size_t i = 0; i < data.size(); ++i)
	{
		json += std::to_string(data[i]);
		if (i < data.size() - 1)
		{
			json += ",";
		}
	}

	json += "]}";
	return json;
}

// ============================================================================
// TileBox 转换函数
// ============================================================================

BoundingVolume BoundingVolumeFromTileBox(const TileBox& tileBox)
{
	BoundingVolume bv;
	bv.type = BoundingVolumeType::Box;
	bv.data = convert_bbox(tileBox);
	return bv;
}

// ============================================================================
// TilesetNode 实现
// ============================================================================

std::string TilesetNode::ToJson(bool bIncludeAsset) const
{
	std::string json;

	// 根节点：包含asset信息
	if (bIncludeAsset)
	{
		json += "{\"asset\":{\"version\":\"1.0\",\"gltfUpAxis\":\"Z\"},";
		json += "\"geometricError\":" + std::to_string(geometricError) + ",";
		json += "\"root\":";
	}

	// 节点主体
	json += "{";
	json += "\"geometricError\":" + std::to_string(geometricError) + ",";

	// 可选：变换矩阵
	if (!transform.empty() && transform.size() == 16)
	{
		json += "\"transform\":[";
		for (size_t i = 0; i < 16; ++i)
		{
			json += std::to_string(transform[i]);
			if (i < 15)
			{
				json += ",";
			}
		}
		json += "],";
	}

	// 边界体积
	json += boundingVolume.ToJson();

	// 可选：内容URI
	if (!contentUri.empty())
	{
		json += ",\"content\":{\"uri\":\"" + contentUri + "\"}";
	}

	// 可选：子节点
	if (!children.empty())
	{
		json += ",\"refine\":\"REPLACE\"";  // 3D Tiles默认的细化策略
		json += ",\"children\":[";
		for (size_t i = 0; i < children.size(); ++i)
		{
			json += children[i].ToJson(false);  // 子节点不包含asset
			if (i < children.size() - 1)
			{
				json += ",";
			}
		}
		json += "]";
	}

	json += "}";

	// 闭合根节点
	if (bIncludeAsset)
	{
		json += "}";
	}

	return json;
}