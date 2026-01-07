#ifndef TILESET_H
#define TILESET_H

#include <string>
#include <vector>

/**
 * @brief 包围盒结构体
 */
struct Box
{
	double dMatrix[12];
};

/**
 * @brief 范围结构体
 */
struct Region
{
	double dMinX;
	double dMinY;
	double dMaxX;
	double dMaxY;
	double dMinHeight;
	double dMaxHeight;
};

/**
 * @brief 切片包围盒结构体, 包含最大和最小坐标及扩展方法
 */
struct TileBox
{
	// 最大坐标
	std::vector<double> max;

	// 最小坐标
	std::vector<double> min;

	// 扩展包围盒，按比例放大
	void extend(double ratio)
	{
		ratio /= 2;
		double x = max[0] - min[0];
		double y = max[1] - min[1];
		double z = max[2] - min[2];

		max[0] += x * ratio;
		max[1] += y * ratio;
		max[2] += z * ratio;

		min[0] -= x * ratio;
		min[1] -= y * ratio;
		min[2] -= z * ratio;
	}
};

/**
 * @brief 边界体积类型枚举
 */
enum class BoundingVolumeType
{
	// 3D Tiles box格式：12个值 [centerX, centerY, centerZ, halfX, 0, 0, 0, halfY, 0, 0, 0, halfZ]
	Box, 

	// 3D Tiles region格式：6个值 [west, south, east, north, minHeight, maxHeight]    
	Region   
};

/**
 * @brief 统一的边界体积表示
 *
 * 用于表示3D Tiles中的boundingVolume，支持box和region两种格式。
 *
 * @example
 * // 从Box创建
 * Box box;
 * BoundingVolume bv = BoundingVolume::FromBox(box);
 * std::string json = bv.ToJson();  // 生成 "boundingVolume":{"box":[...]}
 */
struct BoundingVolume
{
	BoundingVolumeType type;      // 边界体积类型
	std::vector<double> data;     // 边界体积数据：box=12个值，region=6个值

	/**
	 * @brief 从Box结构创建BoundingVolume
	 * @param box Box结构引用
	 * @return BoundingVolume实例
	 */
	static BoundingVolume FromBox(const Box& box);

	/**
	 * @brief 从Region结构创建BoundingVolume
	 * @param region Region结构引用
	 * @return BoundingVolume实例
	 */
	static BoundingVolume FromRegion(const Region& region);

	/**
	 * @brief 生成JSON字符串表示
	 * @return 返回boundingVolume的JSON片段，如 "boundingVolume":{"box":[...]}
	 */
	std::string ToJson() const;
};

/**
 * @brief 从TileBox创建BoundingVolume的辅助函数
 *
 * 将TileBox转换为3D Tiles标准的box格式边界体积。
 *
 * @param tileBox TileBox结构引用
 * @return BoundingVolume实例
 */
BoundingVolume BoundingVolumeFromTileBox(const TileBox& tileBox);

/**
 * @brief 3D Tiles tileset节点结构
 *
 * 统一表示tileset.json中的节点。
 * 支持递归的树形结构。
 *
 * @example
 * // 创建简单的tileset节点
 * TilesetNode node;
 * node.geometricError = 100.0;
 * node.boundingVolume = BoundingVolume::FromBox(box);
 * node.contentUri = "tile.b3dm";
 * std::string json = node.ToJson(true);  // 生成完整的tileset.json
 */
struct TilesetNode
{
	double geometricError = 0.0;                           // 几何误差
	BoundingVolume boundingVolume;                         // 边界体积
	std::string contentUri;                                // 内容URI（空字符串表示无content）
	std::vector<double> transform;                         // 变换矩阵（16个值，列主序），空表示无transform
	std::vector<TilesetNode> children;                     // 子节点

	/**
	 * @brief 生成节点的JSON字符串
	 * @param bIncludeAsset 是否包含asset和版本信息（仅用于根节点）
	 * @return 返回完整的tileset JSON字符串
	 *
	 * @details
	 * 当 bIncludeAsset=true 时，生成完整的tileset结构：
	 * {
	 *   "asset": {"version": "1.0", "gltfUpAxis": "Z"},
	 *   "geometricError": ...,
	 *   "root": { ... }
	 * }
	 *
	 * 当 bIncludeAsset=false 时，仅生成节点本身的JSON。
	 */
	std::string ToJson(bool bIncludeAsset = false) const;
};

#endif // TILESET_H
