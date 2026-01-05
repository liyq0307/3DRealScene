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

// ��Ƭ��Χ�нṹ
struct TileBox
{
	// �������
	std::vector<double> max;

	// ��С���� 
	std::vector<double> min;

	// ��չ��Χ�У���������
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

// OSG���ڵ�ṹ
struct OSGTree
{
	// ��Χ��
	TileBox bbox;

	// �������
	double geometricError;

	// �ļ���
	std::string file_name;

	// �ӽڵ�
	std::vector<OSGTree> sub_nodes;

	// ���ڵ����PagedLOD�������ڵ�ʱ������һ���µ���ڵ�
	// 0: ��, 1: PagedLOD�ڵ㣨Ĭ�ϣ�, 2: �����ڵ�;
	int type;
};

// ͼԪ״̬�ṹ
struct PrimitiveState
{
	// �������������
	int vertexAccessor;

	// ���߷���������
	int normalAccessor;

	// �����������������
	int textcdAccessor;
};

// ������Ϣ�ṹ
struct MeshInfo
{
	// ��������
	string name;

	// ��С����
	std::vector<double> min;

	// �������
	std::vector<double> max;
};

/*
* ��Ϣ������������OSG����ͼ���ռ��������������Ϣ
*/
class InfoVisitor : public osg::NodeVisitor
{
private:
	// �ļ�·��
	std::string path;

public:
	// ���캯��
	InfoVisitor(std::string _path, bool loadAllType = false)
		:osg::NodeVisitor(TRAVERSE_ALL_CHILDREN)
		, path(_path), is_loadAllType(loadAllType), is_pagedlod(loadAllType)
	{
	}

	// ��������
	~InfoVisitor()
	{
	}

	// ����������ڵ�
	void apply(osg::Geometry& geometry);

	// ����PagedLOD�ڵ�
	void apply(osg::PagedLOD& node);

public:
	// �洢PagedLOD������
	std::vector<osg::Geometry*> geometry_array;

	// �洢PagedLOD����
	std::set<osg::Texture*> texture_array;

	// ������������ӳ��
	std::map<osg::Geometry*, osg::Texture*> texture_map;

	// �ӽڵ�����
	std::vector<std::string> sub_node_names;

	// �������ͱ�־, true: �����м�����洢��geometry_array, false: �����ʹ洢
	bool is_loadAllType;

	// �Ƿ�ΪPagedLOD�ڵ�
	bool is_pagedlod;

	// �洢����������
	std::vector<osg::Geometry*> other_geometry_array;

	// �洢��������
	std::set<osg::Texture*> other_texture_array;
};

// OsgbReader�ඨ��
class OsgbReader
{
public:
	// ���캯��
	OsgbReader() = default;

	// ��������
	~OsgbReader() = default;

	// Convert 3D Tiles (batch process directory)
	void* Osgb23dTile(
		const char* in_path, const char* out_path, double* box, int* len, double x, double y, int max_lvl,
		bool enable_texture_compress = false, bool enable_meshopt = false, bool enable_draco = false);

	// Convert single OSGB file to GLB buffer
	bool Osgb2GlbBuf(
		std::string path, std::string& glb_buff, int node_type,
		bool enable_texture_compress = false, bool enable_meshopt = false, bool enable_draco = false);

	// Convert OSGB to GLB format
	bool Osgb2Glb(
		const char* in, const char* out, bool enable_texture_compress = false, bool enable_meshopt = false, bool enable_draco = false);

	// Batch process entire oblique photography dataset (similar to Rust osgb.rs)
	// Processes all Tile_* subdirectories in data_dir/Data/ folder
	// Returns tileset.json string and merged bounding box
	void* Osgb23dTileBatch(
		const char* data_dir, const char* output_dir, double* merged_box, int* json_len,
		double center_x, double center_y, int max_lvl,
		bool enable_texture_compress = false, bool enable_meshopt = false, bool enable_draco = false);

private:
	// ��OSGB�ļ�ת��ΪGLB������
	bool Osgb2GlbBuf(
		std::string path, std::string& glb_buff, MeshInfo& mesh_info, int node_type,
		bool enable_texture_compress = false, bool enable_meshopt = false, bool enable_draco = false, bool need_mesh_info = true);

	// ��OSGB�ļ�ת��ΪB3DM������
	bool Osgb2B3dmBuf(
		std::string path, std::string& b3dm_buf, TileBox& tile_box, int node_type, 
		bool enable_texture_compress = false, bool enable_meshopt = false, bool enable_draco = false);

	// ������Ƭ��ҵ
	void DoTileJob(OSGTree& tree, std::string out_path, int max_lvl, bool enable_texture_compress = false, bool enable_meshopt = false, bool enable_draco = false);

	// ������ƬJSON
	std::string EncodeTileJson(OSGTree& tree, double x, double y);

	// ��ȡOSGB�������������ṹ
	OSGTree GetAllTree(std::string& file_name);
};

#endif // !OSGBREADER_H


