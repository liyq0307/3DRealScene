#ifndef OSGBREADER_H
#define OSGBREADER_H

#include <set>
#include <cmath>
#include <vector>
#include <string>
#include <cstring>
#include <algorithm>

#include <osg/Material>
#include <osg/PagedLOD>
#include <osgDB/ReadFile>
#include <osgDB/ConvertUTF>
#include <osgUtil/Optimizer>
#include <osgUtil/SmoothingVisitor>

using namespace std;

// 切片包围盒结构
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

// OSG树节点结构
struct OSGTree
{
	// 包围盒
	TileBox bbox;

	// 几何误差
	double geometricError;

	// 文件名
	std::string file_name;

	// 子节点
	std::vector<OSGTree> sub_nodes;

	// 节点类型：当PagedLOD添加子节点时，创建一个新的子节点
	// 0: 根节点, 1: PagedLOD节点（默认）, 2: 普通子节点
	int type;
};

// 图元状态结构
struct PrimitiveState
{
	// 顶点坐标访问器索引
	int vertexAccessor;

	// 法线方向访问器索引
	int normalAccessor;

	// 纹理坐标访问器索引
	int textcdAccessor;
};

// 网格信息结构
struct MeshInfo
{
	// 网格名称
	string name;

	// 最小坐标
	std::vector<double> min;

	// 最大坐标
	std::vector<double> max;
};

/*
* 信息访问器类，用于遍历OSG场景图并收集几何和纹理信息
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

// OsgbReader类定义
class OsgbReader
{
public:
	// 构造函数
	OsgbReader() = default;

	// 析构函数
	~OsgbReader() = default;

	// 转换为3D Tiles格式
	std::string Osgb23dTile(
		const std::string strInPath, const std::string& strOutPath, 
		double* pBox, int* pLen, double dCenterX, double dCenterY, int nMaxLevel,
		bool bEnableTextureCompress = false, bool bEnableMeshOpt = false, bool bEnableDraco = false);

	// 将单个OSGB文件转换为GLB缓冲区
	bool Osgb2GlbBuf(
		std::string strOsgbPath, std::string& strGlbBuff, int nNodeType,
		bool bEnableTextureCompress = false, bool bEnableMeshOpt = false, bool bEnableDraco = false);

	// 将OSGB转换为GLB格式
	bool Osgb2Glb(
		const std::string&  strInPath, const std::string& strOutPath, 
		bool bEnableTextureCompress = false, bool bEnableMeshOpt = false, bool bEnableDraco = false);

	// 批量处理整个倾斜摄影数据集
	// 处理data_dir/Data/目录下的所有Tile_*子目录
	// 返回tileset.json字符串和合并的包围盒
	std::string Osgb23dTileBatch(
		const std::string& pDataDir, const std::string& strOutputDir, double* pMergedBox, int* pJsonLen,
		double dCenterX, double dCenterY, int nMaxLevel,
		bool bEnableTextureCompress = false, bool bEnableMeshOpt = false, bool bEnableDraco = false);

private:
	// 将OSGB文件转换为GLB缓冲区（带网格信息）
	bool Osgb2GlbBuf(
		std::string path, std::string& glb_buff, MeshInfo& mesh_info, int node_type,
		bool enable_texture_compress = false, bool enable_meshopt = false, bool enable_draco = false, bool need_mesh_info = true);

	// 将OSGB文件转换为B3DM缓冲区
	bool Osgb2B3dmBuf(
		std::string path, std::string& b3dm_buf, TileBox& tile_box, int node_type,
		bool enable_texture_compress = false, bool enable_meshopt = false, bool enable_draco = false);

	// 处理切片任务
	void DoTileJob(OSGTree& tree, std::string out_path, int max_lvl, bool enable_texture_compress = false, bool enable_meshopt = false, bool enable_draco = false);

	// 编码切片JSON
	std::string EncodeTileJson(OSGTree& tree, double x, double y);

	// 获取OSGB文件的完整树结构
	OSGTree GetAllTree(std::string& file_name);
};

#endif // !OSGBREADER_H


