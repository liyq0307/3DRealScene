#ifndef OSGBREADER_H
#define OSGBREADER_H

#include <set>
#include <cmath>
#include <vector>
#include <string>
#include <algorithm>
#include <iostream>
#include <iomanip>

#include <osg/Material>
#include <osg/PagedLOD>
#include <osgDB/ReadFile>
#include <osgDB/ConvertUTF>
#include <osgUtil/Optimizer>
#include <osgUtil/SmoothingVisitor>

#ifndef STB_IMAGE_IMPLEMENTATION
#define STB_IMAGE_IMPLEMENTATION
#endif // !STB_IMAGE_IMPLEMENTATION
#ifndef STB_IMAGE_WRITE_IMPLEMENTATION
#define STB_IMAGE_WRITE_IMPLEMENTATION
#endif // !STB_IMAGE_WRITE_IMPLEMENTATION
#ifndef TINYGLTF_IMPLEMENTATION
#define TINYGLTF_IMPLEMENTATION
#endif // !TINYGLTF_IMPLEMENTATION
#include <tiny_gltf.h>
#include <json.hpp>

#include "Tileset.h"

using namespace std;

/**
 * @brief OSG树节点结构体
 */
struct OSGTree
{
	// 包围盒
	TileBox bbox;

	// 几何误差
	double geometricError = 0.0;

	// 文件名
	std::string file_name;

	// 子节点
	std::vector<OSGTree> sub_nodes;

	// 节点类型：当PagedLOD添加子节点时，创建一个新的子节点
	// 0: 根节点, 1: PagedLOD节点（默认）, 2: 普通子节点
	int type = 0;
};

/**
 * @brief 图元状态结构体，用于存储图元的顶点、法线和纹理坐标访问器索引
 */
struct PrimitiveState
{
	// 顶点坐标访问器索引
	int vertexAccessor;

	// 法线方向访问器索引
	int normalAccessor;

	// 纹理坐标访问器索引
	int textcdAccessor;
};

/**
 * @brief 网格信息结构体, 用于存储网格的元数据
 */
struct MeshInfo
{
	// 网格名称
	string name;

	// 最小坐标
	std::vector<double> min;

	// 最大坐标
	std::vector<double> max;
};

/**
 * @brief OSG构建状态结构体，用于在转换过程中跟踪缓冲区和模型信息
 */
struct OsgBuildState
{
	// 缓冲区指针
	tinygltf::Buffer* buffer;

	// 模型指针
	tinygltf::Model* model;

	// 当前缓冲区偏移量
	osg::Vec3f point_max;

	// 当前缓冲区偏移量
	osg::Vec3f point_min;

	// 当前缓冲区偏移量
	int draw_array_first;

	// 当前缓冲区顶点数量
	int draw_array_count;
};

/**
 * @brief 信息访问器类，用于遍历OSG场景图并收集几何和纹理信息
 */
class InfoVisitor : public osg::NodeVisitor
{
private:
	// 文件路径
	std::string path;

public:
	// 构造函数
	InfoVisitor(std::string _path, bool loadAllType = false)
		:osg::NodeVisitor(TRAVERSE_ALL_CHILDREN)
		, path(_path), is_loadAllType(loadAllType), is_pagedlod(loadAllType)
	{
	}

	// 析构函数
	~InfoVisitor()
	{
	}

	// 处理几何节点
	void apply(osg::Geometry& geometry);

	// 处理PagedLOD节点
	void apply(osg::PagedLOD& node);

public:
	// 存储PagedLOD几何体
	std::vector<osg::Geometry*> geometry_array;

	// 存储PagedLOD纹理
	std::set<osg::Texture*> texture_array;

	// 几何体与纹理映射关系
	std::map<osg::Geometry*, osg::Texture*> texture_map;

	// 子节点名称列表
	std::vector<std::string> sub_node_names;

	// 加载所有类型标志, true: 所有几何体都存储到geometry_array, false: 分类存储
	bool is_loadAllType;

	// 是否为PagedLOD节点
	bool is_pagedlod;

	// 存储其他几何体（非PagedLOD）
	std::vector<osg::Geometry*> other_geometry_array;

	// 存储其他纹理（非PagedLOD）
	std::set<osg::Texture*> other_texture_array;
};

/**
 * @brief OSGB转3D Tiles工具类
 */
class OSGB23dTiles
{
public:
	// 构造函数
	OSGB23dTiles() = default;

	// 析构函数
	~OSGB23dTiles() = default;

	/**
	 * @brief 将单个OSGB文件转换为3D Tiles
	 * @param strInPath 输入OSGB文件路径
	 * @param strOutPath 输出目录
	 * @param dCenterX 中心点经度
	 * @param dCenterY 中心点纬度
	 * @param nMaxLevel 最大切片层级
	 * @param bEnableTextureCompress 是否启用纹理压缩
	 * @param bEnableMeshOpt 是否启用网格优化
	 * @param bEnableDraco 是否启用Draco压缩
	 * @return 返回元组：(是否成功, tileset.json字符串, 包围盒[maxX, maxY, maxZ, minX, minY, minZ])
	 */
	std::tuple<bool, std::string, std::array<double, 6>> To3dTile(
		const std::string strInPath, const std::string& strOutPath,
		double dCenterX, double dCenterY, int nMaxLevel,
		bool bEnableTextureCompress = false, bool bEnableMeshOpt = false, bool bEnableDraco = false);

	/**
	 * @brief 将单个OSGB文件转换为GLB缓冲区
	 * @param strOsgbPath 输入OSGB文件路径
	 * @param strGlbBuff 输出GLB缓冲区字符串
	 * @param nNodeType 节点类型
	 * @param bEnableTextureCompress 是否启用纹理压缩
	 * @param bEnableMeshOpt 是否启用网格优化
	 * @param bEnableDraco 是否启用Draco压缩
	 * @return 返回转换是否成功
	 */
	bool ToGlbBuf(
		std::string strOsgbPath, std::string& strGlbBuff, int nNodeType,
		bool bEnableTextureCompress = false, bool bEnableMeshOpt = false, bool bEnableDraco = false);

	/**
	 * @brief 将单个OSGB文件转换为GLB文件
	 * @param strInPath 输入OSGB文件路径
	 * @param strOutPath 输出GLB文件路径
	 * @param bEnableTextureCompress 是否启用纹理压缩
	 * @param bEnableMeshOpt 是否启用网格优化
	 * @param bEnableDraco 是否启用Draco压缩
	 * @return 返回转换是否成功
	 */
	bool ToGlb(
		const std::string& strInPath, const std::string& strOutPath,
		bool bEnableTextureCompress = false, bool bEnableMeshOpt = false, bool bEnableDraco = false);

	/**
	 * @brief 将单个OSGB文件转换为GLB字节数组
	 * @param strOsgbPath 输入OSGB文件路径
	 * @param nNodeType 节点类型
	 * @param bEnableTextureCompress 是否启用纹理压缩
	 * @param bEnableMeshOpt 是否启用网格优化
	 * @param bEnableDraco 是否启用Draco压缩
	 * @return 返回GLB字节数组，失败返回空数组
	 */
	std::vector<uint8_t> ToGlbBufBytes(
		std::string strOsgbPath, int nNodeType,
		bool bEnableTextureCompress = false, bool bEnableMeshOpt = false, bool bEnableDraco = false);

	/**
	 * @brief 批量处理整个倾斜摄影数据集
	 * @param pDataDir 输入数据目录路径
	 * @param strOutputDir 输出3D Tiles目录路径
	 * @param dCenterX 切片中心X坐标
	 * @param dCenterY 切片中心Y坐标
	 * @param nMaxLevel 最大切片层级
	 * @param bEnableTextureCompress 是否启用纹理压缩
	 * @param bEnableMeshOpt 是否启用网格优化
	 * @param bEnableDraco 是否启用Draco压缩
	 * @return 返回成功或失败
	 */
	bool To3dTileBatch(
		const std::string& pDataDir, const std::string& strOutputDir,
		double dCenterX, double dCenterY, int nMaxLevel,
		bool bEnableTextureCompress = false, bool bEnableMeshOpt = false, bool bEnableDraco = false);

private:
	/**
	 * @brief 将OSGB文件转换为GLB缓冲区（带网格信息）
	 * @param path 输入OSGB文件路径
	 * @param glb_buff 输出GLB缓冲区字符串
	 * @param mesh_info 输出网格信息结构体
	 * @param node_type 节点类型
	 * @param enable_texture_compress 是否启用纹理压缩
	 * @param enable_meshopt 是否启用网格优化
	 * @param enable_draco 是否启用Draco压缩
	 * @return 返回转换是否成功
	 */
	bool ToGlbBuf(
		std::string path, std::string& glb_buff, MeshInfo& mesh_info, int node_type,
		bool enable_texture_compress = false, bool enable_meshopt = false, bool enable_draco = false, bool need_mesh_info = true);

	/**
	 * @brief 将OSGB文件转换为B3DM缓冲区
	 * @param path 输入OSGB文件路径
	 * @param b3dm_buf 输出B3DM缓冲区字符串
	 * @param tile_box 输出切片包围盒结构体
	 * @param node_type 节点类型
	 * @param enable_texture_compress 是否启用纹理压缩
	 * @param enable_meshopt 是否启用网格优化
	 * @param enable_draco 是否启用Draco压缩
	 * @return 返回转换是否成功
	 */
	bool ToB3dmBuf(
		std::string path, std::string& b3dm_buf, TileBox& tile_box, int node_type,
		bool enable_texture_compress = false, bool enable_meshopt = false, bool enable_draco = false);

	/**
	 * @brief 处理切片任务
	 * @param tree OSG树节点结构体
	 * @param out_path 输出目录路径
	 * @param max_lvl 最大切片层级
	 * @param enable_texture_compress 是否启用纹理压缩
	 * @param enable_meshopt 是否启用网格优化
	 * @param enable_draco 是否启用Draco压缩
	 * @return void
	 */
	void DoTileJob(OSGTree& tree, std::string out_path, int max_lvl, bool enable_texture_compress = false, bool enable_meshopt = false, bool enable_draco = false);

	/**
	 * @brief 编码切片JSON字符串
	 * @param tree OSG树节点结构体
	 * @param x 切片X坐标
	 * @param y 切片Y坐标
	 * @return 返回切片JSON字符串
	 */
	std::string EncodeTileJSON(OSGTree& tree, double x, double y);

	/**
	 * @brief 获取OSGB文件的完整树结构
	 * @param file_name 输入OSGB文件路径
	 * @return 返回OSG树节点结构体
	 */
	OSGTree GetAllTree(std::string& file_name);
};

#endif // !OSGBREADER_H


