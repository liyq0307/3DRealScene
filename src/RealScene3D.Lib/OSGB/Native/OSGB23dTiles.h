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
 * @brief B3DM转换结果结构体，用于ToB3DM方法返回值（SWIG友好）
 */
struct B3DMResult
{
	// 转换是否成功
	bool success = false;

	// tileset.json内容字符串
	std::string tilesetJson = "";

	// 包围盒：[maxX, maxY, maxZ, minX, minY, minZ]
	std::array<double, 6> boundingBox = {};
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
 * @brief Draco压缩状态结构体，用于存储Draco压缩相关信息
 */
struct DracoState
{
	// 是否已压缩
	bool compressed = false;

	// 缓冲区视图索引
	int bufferView = -1;

	// 顶点坐标访问器索引
	int posId = -1;

	// 法线方向访问器索引
	int normId = -1;

	// 纹理坐标访问器索引
	int texId = -1;

	// 批次ID访问器索引
	int batchId = -1;
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

	// 存储PagedLOD材质映射关系
	std::set<osg::Material*> material_set;

	// 几何体和材质映射关系
	std::map<osg::Geometry*, osg::Material*> material_map;

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
	 * @brief 将单个OSGB文件转换为B3DM
	 * @param strInPath 输入OSGB文件路径
	 * @param strOutPath 输出目录
	 * @param dCenterX 中心点经度
	 * @param dCenterY 中心点纬度
	 * @param nMaxLevel 最大切片层级
	 * @param bEnableTextureCompress 是否启用纹理压缩
	 * @param bEnableMeshOpt 是否启用网格优化
	 * @param bEnableDraco 是否启用Draco压缩
	 * @return B3DMResult结构体，包含成功标志、tileset.json字符串和包围盒
	 */
	B3DMResult ToB3DM(
		const std::string strInPath,
		const std::string& strOutPath,
		double dCenterX,
		double dCenterY,
		int nMaxLevel,
		bool bEnableTextureCompress = false,
		bool bEnableMeshOpt = false,
		bool bEnableDraco = false);

	/**
	 * @brief 将单个OSGB文件转换为GLB文件
	 * @param strInPath 输入OSGB文件路径
	 * @param strOutPath 输出GLB文件路径
	 * @param bBinary 是否输出二进制文件
	 * @param bEnableTextureCompress 是否启用纹理压缩
	 * @param bEnableMeshOpt 是否启用网格优化
	 * @param bEnableDraco 是否启用Draco压缩
	 * @return 返回转换是否成功
	 */
	bool ToGLB(
		const std::string& strInPath, 
		const std::string& strOutPath,
		bool bBinary = true,
		bool bEnableTextureCompress = false, 
		bool bEnableMeshOpt = false,
		bool bEnableDraco = false);

	/**
	 * @brief 将单个OSGB文件转换为GLB字节数组
	 * @param strOsgbPath 输入OSGB文件路径
	 * @param nNodeType 节点类型
	 * @param bBinary 是否输出二进制文件
	 * @param bEnableTextureCompress 是否启用纹理压缩
	 * @param bEnableMeshOpt 是否启用网格优化
	 * @param bEnableDraco 是否启用Draco压缩
	 * @return 返回GLB字节数组，失败返回空数组
	 */
	std::vector<uint8_t> ToGLBBuf(
		std::string strOsgbPath, 
		int nNodeType,
		bool bBinary = true,
		bool bEnableTextureCompress = false, 
		bool bEnableMeshOpt = false, 
		bool bEnableDraco = false);

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
	bool ToB3DMBatch(
		const std::string& pDataDir, 
		const std::string& strOutputDir,
		double dCenterX, 
		double dCenterY, 
		int nMaxLevel,
		bool bEnableTextureCompress = false, 
		bool bEnableMeshOpt = false, 
		bool bEnableDraco = false);

private:
	/**
	 * @brief 写入OSG索引数据到GLTF构建状态
	 * @tparam T 索引数据类型
	 * @param drawElements 输入OSG绘制元素指针
	 * @param osgState OSG构建状态指针
	 * @param componentType 索引组件类型
	 * @return void
	 */
	template<class T>
	void WriteOsgIndecis(T* drawElements, OsgBuildState* osgState, int componentType);

	/**
	 * @brief 写入OSG三维向量数组到GLTF构建状态
	 * @param v3f 输入OSG三维向量数组指针
	 * @param osgState OSG构建状态指针
	 * @param point_max 输出最大坐标
	 * @param point_min 输出最小坐标
	 * @return void
	 */
	void WriteVec3Array(osg::Vec3Array* v3f, OsgBuildState* osgState, osg::Vec3f& point_max, osg::Vec3f& point_min);

	/**
	 * @brief 写入OSG二维向量数组到GLTF构建状态
	 * @param v2f 输入OSG二维向量数组指针
	 * @param osgState OSG构建状态指针
	 * @return void
	 */
	void WriteVec2Array(osg::Vec2Array* v2f, OsgBuildState* osgState);

	/**
	 * @brief 写入索引向量到GLTF构建状态
	 * @param indices 输入索引向量
	 * @param osgState OSG构建状态指针
	 * @param dracoState Draco压缩状态指针
	 * @return 返回索引访问器索引
	 */
	int WriteIndexVector(const std::vector<uint32_t>& indices, OsgBuildState* osgState, DracoState* dracoState);

	/**
	 * @brief 写入OSG图元数据到GLTF构建状态
	 * @param pGeometry 输入OSG几何体指针
	 * @param ps 输入OSG图元集指针
	 * @param osgState OSG构建状态指针
	 * @param pmtState 图元状态指针
	 * @param dracoState Draco压缩状态指针
	 * @return void
	 */
	void WriteElementArrayPrimitive(osg::Geometry* pGeometry, osg::PrimitiveSet* ps, OsgBuildState* osgState, PrimitiveState* pmtState, DracoState* dracoState);

	/**
	 * @brief 写入OSG几何体数据到GLTF构建状态
	 * @param pGeometry 输入OSG几何体指针
	 * @param osgState OSG构建状态指针
	 * @param bEnableSimplification 是否启用网格简化
	 * @param bEnableDraco 是否启用Draco压缩
	 * @return void
	 */	
	void WriteOsgGeometry(osg::Geometry* pGeometry, OsgBuildState* osgState, bool bEnableSimplification, bool bEnableDraco);

	/**
	 * @brief 将OSGB文件转换为GLB缓冲区（带网格信息）
	 * @param path 输入OSGB文件路径
	 * @param glb_buff 输出GLB缓冲区字符串
	 * @param mesh_info 输出网格信息结构体
	 * @param node_type 节点类型
	 * @param bBinary 是否输出二进制文件
	 * @param enable_texture_compress 是否启用纹理压缩
	 * @param enable_meshopt 是否启用网格优化
	 * @param enable_draco 是否启用Draco压缩
	 * @return 返回转换是否成功
	 */
	bool ToGLBBuf(
		std::string path, 
		std::string& glb_buff, 
		MeshInfo& mesh_info, 
		int node_type,
		bool bBinary = true,
		bool enable_texture_compress = false, 
		bool enable_meshopt = false, 
		bool enable_draco = false, 
		bool need_mesh_info = true);

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
	bool ToB3DMBuf(
		std::string path, 
		std::string& b3dm_buf, 
		TileBox& tile_box, 
		int node_type,
		bool enable_texture_compress = false, 
		bool enable_meshopt = false, 
		bool enable_draco = false);

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
	void DoTileJob(
		OSGTree& tree, 
		std::string out_path, 
		int max_lvl, 
		bool enable_texture_compress = false, 
		bool enable_meshopt = false, 
		bool enable_draco = false);

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


