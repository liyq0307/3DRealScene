// ============================================================================
// OsgbReader 实现
// 将 OSGB (OpenSceneGraph Binary) 文件转换为 GLB 和 3D Tiles 格式
// ============================================================================

#include "OsgbReader.h"
#include <Eigen/Eigen>

#ifdef _WIN32
#include <Windows.h>
#endif

#ifdef ENABLE_PROJ
#include <proj.h>  // PROJ coordinate transformation
#endif

// Basis Universal for KTX2 texture compression
#ifdef ENABLE_KTX2
#include <basisu/encoder/basisu_comp.h>
#include <basisu/transcoder/basisu_transcoder.h>
#endif

// Draco mesh compression
#include "MeshProcessor.h"
#include "Extern.h"
#include "GeoTransform.h"

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

#ifdef max
#undef max
#endif // max
#ifdef min
#undef min
#endif // max

// USE_OSGPLUGIN 在 Linux/macOS 上需要用于静态插件注册
// 在 Windows 上使用动态链接，插件在运行时加载
#if defined(__unix__) || defined(__APPLE__)
#include <osgDB/Registry>
USE_OSGPLUGIN(osg)
#endif

// ============================================================================
// Helper Functions
// ============================================================================

// 检查路径是否为目录
bool is_directory(const std::string& path)
{
#ifdef _WIN32
	DWORD attrs = GetFileAttributesA(path.c_str());
	return (attrs != INVALID_FILE_ATTRIBUTES) && (attrs & FILE_ATTRIBUTE_DIRECTORY);
#else
	struct stat st;
	return (stat(path.c_str(), &st) == 0) && S_ISDIR(st.st_mode);
#endif
}

// 在目录中查找根 OSGB 文件（不带 "_L" 级后缀的文件）
std::string find_root_osgb(const std::string& dir_path)
{
	// 标准化路径分隔符
	std::string normalized_path = dir_path;
	for (char& c : normalized_path)
	{
		if (c == '\\') c = '/';
	}
	// 移除尾部斜杠
	if (!normalized_path.empty() && normalized_path.back() == '/')
		normalized_path.pop_back();

	// 在目录中搜索根 OSGB 的辅助函数
	auto search_dir = [](const std::string& search_path) -> std::string
	{
#ifdef _WIN32
		WIN32_FIND_DATAA findData;
		std::string search_pattern = search_path + "/*";
		HANDLE hFind = FindFirstFileA(search_pattern.c_str(), &findData);

		if (hFind != INVALID_HANDLE_VALUE)
		{
			do
			{
				if (findData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)
				{
					std::string subdir = findData.cFileName;
					if (subdir != "." && subdir != "..")
					{
						// 在子目录中搜索根 OSGB
						std::string subdir_path = search_path + "/" + subdir;
						std::string osgb_pattern = subdir_path + "/*.osgb";
						WIN32_FIND_DATAA findOsgb;
						HANDLE hFindOsgb = FindFirstFileA(osgb_pattern.c_str(), &findOsgb);

						if (hFindOsgb != INVALID_HANDLE_VALUE)
						{
							do
							{
								std::string filename = findOsgb.cFileName;
								// 根 OSGB 文件不包含 "_L" 级别指示符
								if (filename.find("_L") == std::string::npos)
								{
									FindClose(hFindOsgb);
									FindClose(hFind);
									return subdir_path + "/" + filename;
								}
							} while (FindNextFileA(hFindOsgb, &findOsgb));
							FindClose(hFindOsgb);
						}
					}
				}
			} while (FindNextFileA(hFind, &findData));
			FindClose(hFind);
		}
#endif
		return "";
	};

	// 检查输入路径本身是否看起来像 Data 目录（包含带有 OSGB 文件的子目录）
	std::string result = search_dir(normalized_path);
	if (!result.empty())
		return result;

	// 尝试 Data 子目录
	std::string data_dir = normalized_path + "/Data";
	if (is_directory(data_dir))
	{
		result = search_dir(data_dir);
		if (!result.empty())
			return result;
	}

	return "";  // Not found
}

// 从路径获取父目录
std::string get_parent(std::string str)
{
	auto p0 = str.find_last_of("/\\");
	if (p0 != std::string::npos)
		return str.substr(0, p0);
	else
		return "";
}

std::string get_file_name(std::string path)
{
	auto p0 = path.find_last_of("/\\");
	if (p0 == std::string::npos)
		return path;
	return path.substr(p0 + 1);
}

// 字符串替换辅助函数
std::string replace(std::string str, std::string s0, std::string s1)
{
	auto p0 = str.find(s0);
	if (p0 == std::string::npos)
		return str;
	return str.replace(p0, s0.length(), s1);
}

// 转换为 OSG 字符串格式
std::string osg_string(const std::string& path)
{
#ifdef WIN32
	std::string root_path = osgDB::convertStringFromUTF8toCurrentCodePage(path);
#else
	std::string root_path = path;
#endif // WIN32
	return root_path;
}

// 转换为 UTF8 字符串格式
std::string utf8_string(const std::string& path)
{
#ifdef WIN32
	std::string utf8 = osgDB::convertStringFromCurrentCodePageToUTF8(path);
#else
	std::string utf8 = (path);
#endif // WIN32
	return utf8;
}

// 从文件名获取级别编号
int get_lvl_num(std::string file_name)
{
	std::string stem = get_file_name(file_name);
	auto p0 = stem.find("_L");
	auto p1 = stem.find("_", p0 + 2);
	if (p0 != std::string::npos && p1 != std::string::npos)
	{
		std::string substr = stem.substr(p0 + 2, p1 - p0 - 2);
		try
		{
			return std::stol(substr);
		}
		catch (...)
		{
			return -1;
		}
	}
	else if (p0 != std::string::npos)
	{
		int end = p0 + 2;
		while (true)
		{
			if (isdigit(stem[end]))
				end++;
			else
				break;
		}
		std::string substr = stem.substr(p0 + 2, end - p0 - 2);
		try
		{
			return std::stol(substr);
		}
		catch (...)
		{
			return -1;
		}
	}
	return -1;
}
// =================InLine Functions End==================

//=================== InfoVisitor ==================
void InfoVisitor::apply(osg::Geometry& geometry)
{
	if (geometry.getVertexArray() == nullptr ||
		geometry.getVertexArray()->getDataSize() == 0U ||
		geometry.getNumPrimitiveSets() == 0U)
	{
		return;
	}

	if (is_pagedlod)
	{
		geometry_array.push_back(&geometry);
	}
	else
	{
		other_geometry_array.push_back(&geometry);
	}

	if (GeoTransform::projTransform)
	{
		osg::Vec3Array* vertexArr = (osg::Vec3Array*)geometry.getVertexArray();
		void* transform = GeoTransform::projTransform;

		glm::dvec3 Min = glm::dvec3(DBL_MAX);
		glm::dvec3 Max = glm::dvec3(-DBL_MAX);
		for (int VertexIndex = 0; VertexIndex < vertexArr->size(); VertexIndex++)
		{
			osg::Vec3d Vertex = vertexArr->at(VertexIndex);
			glm::dvec3 vertex = glm::dvec3(Vertex.x(), Vertex.y(), Vertex.z());
			Min = glm::min(vertex, Min);
			Max = glm::max(vertex, Max);
		}

		/**
		 */
		auto Correction = [&](glm::dvec3 Point)
			{
				if (GeoTransform::IsENU)
				{
					glm::dvec3 absoluteENU = Point + glm::dvec3(GeoTransform::OriginX, GeoTransform::OriginY, GeoTransform::OriginZ);
					glm::dvec3 ecef = GeoTransform::CartographicToEcef(GeoTransform::GeoOriginLon, GeoTransform::GeoOriginLat, GeoTransform::GeoOriginHeight);

					const double pi = std::acos(-1.0);
					double lat = GeoTransform::GeoOriginLat * pi / 180.0;
					double lon = GeoTransform::GeoOriginLon * pi / 180.0;
					double sinLat = std::sin(lat), cosLat = std::cos(lat);
					double sinLon = std::sin(lon), cosLon = std::cos(lon);

					double ecef_x = -sinLon * absoluteENU.x - sinLat * cosLon * absoluteENU.y + cosLat * cosLon * absoluteENU.z;
					double ecef_y = cosLon * absoluteENU.x - sinLat * sinLon * absoluteENU.y + cosLat * sinLon * absoluteENU.z;
					double ecef_z = cosLat * absoluteENU.y + sinLat * absoluteENU.z;
					ecef = glm::dvec3(ecef.x + ecef_x, ecef.y + ecef_y, ecef.z + ecef_z);
					glm::dvec3 enu = GeoTransform::EcefToEnuMatrix * glm::dvec4(ecef, 1);

					return enu;
				}
				else
				{
					glm::dvec3 cartographic = Point + glm::dvec3(GeoTransform::OriginX, GeoTransform::OriginY, GeoTransform::OriginZ);

					PJ_COORD coord;
					coord.xyzt.x = cartographic.x;
					coord.xyzt.y = cartographic.y;
					coord.xyzt.z = cartographic.z;
					coord.xyzt.t = HUGE_VAL;

					PJ_COORD result = proj_trans((PJ*)transform, PJ_FWD, coord);
					if (result.xyzt.x != HUGE_VAL)
					{
						cartographic.x = result.xyzt.x;
						cartographic.y = result.xyzt.y;
						cartographic.z = result.xyzt.z;
					}

					glm::dvec3 ecef = GeoTransform::CartographicToEcef(cartographic.x, cartographic.y, cartographic.z);
					glm::dvec3 enu = GeoTransform::EcefToEnuMatrix * glm::dvec4(ecef, 1);

					return enu;
				}};

		vector<glm::dvec4> OriginalPoints(8);
		vector<glm::dvec4> CorrectedPoints(8);

		OriginalPoints[0] = glm::dvec4(Min.x, Min.y, Min.z, 1);
		OriginalPoints[1] = glm::dvec4(Max.x, Min.y, Min.z, 1);
		OriginalPoints[2] = glm::dvec4(Min.x, Max.y, Min.z, 1);
		OriginalPoints[3] = glm::dvec4(Min.x, Min.y, Max.z, 1);
		OriginalPoints[4] = glm::dvec4(Max.x, Max.y, Min.z, 1);
		OriginalPoints[5] = glm::dvec4(Min.x, Max.y, Max.z, 1);
		OriginalPoints[6] = glm::dvec4(Max.x, Min.y, Max.z, 1);
		OriginalPoints[7] = glm::dvec4(Max.x, Max.y, Max.z, 1);

		for (int i = 0; i < 8; i++)
		{
			CorrectedPoints[i] = glm::dvec4(Correction(OriginalPoints[i]), 1);
		}

		/**
		*/
		Eigen::MatrixXd A, B;
		A.resize(8, 4);
		B.resize(8, 4);
		for (int row = 0; row < 8; row++)
		{
			A.row(row) << OriginalPoints[row].x, OriginalPoints[row].y, OriginalPoints[row].z, 1;
		}

		for (int row = 0; row < 8; row++)
		{
			B.row(row) << CorrectedPoints[row].x, CorrectedPoints[row].y, CorrectedPoints[row].z, 1;
		}
		Eigen::BDCSVD<Eigen::MatrixXd> SVD(A, Eigen::ComputeThinU | Eigen::ComputeThinV);
		Eigen::MatrixXd X = SVD.solve(B);

		/*
		*/
		glm::dmat4 Transform = glm::dmat4(
			X(0, 0), X(0, 1), X(0, 2), X(0, 3),
			X(1, 0), X(1, 1), X(1, 2), X(1, 3),
			X(2, 0), X(2, 1), X(2, 2), X(2, 3),
			X(3, 0), X(3, 1), X(3, 2), X(3, 3));

		for (int VertexIndex = 0; VertexIndex < vertexArr->size(); VertexIndex++)
		{
			osg::Vec3d Vertex = vertexArr->at(VertexIndex);
			glm::dvec4 v = Transform * glm::dvec4(Vertex.x(), Vertex.y(), Vertex.z(), 1);
			Vertex = osg::Vec3d(v.x, v.y, v.z);
			vertexArr->at(VertexIndex) = Vertex;
		}
	}

	if (auto ss = geometry.getStateSet())
	{
		osg::Texture* tex = dynamic_cast<osg::Texture*>(ss->getTextureAttribute(0, osg::StateAttribute::TEXTURE));
		if (tex)
		{
			if (is_pagedlod)
			{
				texture_array.insert(tex);
			}
			else
			{
				other_texture_array.insert(tex);
			}

			texture_map[&geometry] = tex;
		}
	}
}

void InfoVisitor::apply(osg::PagedLOD& node)
{
	//std::string path = node.getDatabasePath();
	int n = node.getNumFileNames();
	for (size_t i = 1; i < n; i++)
	{
		std::string file_name = path + "/" + node.getFileName(i);
		sub_node_names.push_back(file_name);
	}

	if (!is_loadAllType)
	{
		is_pagedlod = true;
	}

	traverse(node);

	if (!is_loadAllType)
	{
		is_pagedlod = false;
	}
}

//=================== InfoVisitor End =================

//=================== 工具函数 ===================
template<class T>
void put_val(std::vector<unsigned char>& buf, T val)
{
	buf.insert(buf.end(), (unsigned char*)&val, (unsigned char*)&val + sizeof(T));
}

template<class T>
void put_val(std::string& buf, T val)
{
	buf.append((unsigned char*)&val, (unsigned char*)&val + sizeof(T));
}

void write_buf(void* context, void* data, int len)
{
	std::vector<char>* buf = (std::vector<char>*)context;
	buf->insert(buf->end(), (char*)data, (char*)data + len);
}

template<class T>
void alignment_buffer(std::vector<T>& buf)
{
	while (buf.size() % 4 != 0)
	{
		buf.push_back(0x00);
	}
}

void expand_bbox3d(osg::Vec3f& point_max, osg::Vec3f& point_min, osg::Vec3f point)
{
	point_max.x() = std::max(point.x(), point_max.x());
	point_min.x() = std::min(point.x(), point_min.x());
	point_max.y() = std::max(point.y(), point_max.y());
	point_min.y() = std::min(point.y(), point_min.y());
	point_max.z() = std::max(point.z(), point_max.z());
	point_min.z() = std::min(point.z(), point_min.z());
}

void expand_bbox2d(osg::Vec2f& point_max, osg::Vec2f& point_min, osg::Vec2f point)
{
	point_max.x() = std::max(point.x(), point_max.x());
	point_min.x() = std::min(point.x(), point_min.x());
	point_max.y() = std::max(point.y(), point_max.y());
	point_min.y() = std::min(point.y(), point_min.y());
}

std::vector<double> convert_bbox(TileBox tile)
{
	double center_mx = (tile.max[0] + tile.min[0]) / 2;
	double center_my = (tile.max[1] + tile.min[1]) / 2;
	double center_mz = (tile.max[2] + tile.min[2]) / 2;
	double x_meter = (tile.max[0] - tile.min[0]) * 1;
	double y_meter = (tile.max[1] - tile.min[1]) * 1;
	double z_meter = (tile.max[2] - tile.min[2]) * 1;
	if (x_meter < 0.01) { x_meter = 0.01; }
	if (y_meter < 0.01) { y_meter = 0.01; }
	if (z_meter < 0.01) { z_meter = 0.01; }
	std::vector<double> v =
	{
		center_mx,center_my,center_mz,
		x_meter / 2, 0, 0,
		0, y_meter / 2, 0,
		0, 0, z_meter / 2
	};
	return v;
}

void expend_box(TileBox& box, TileBox& box_new)
{
	if (box_new.max.empty() || box_new.min.empty())
	{
		return;
	}
	if (box.max.empty())
	{
		box.max = box_new.max;
	}
	if (box.min.empty())
	{
		box.min = box_new.min;
	}
	for (int i = 0; i < 3; i++)
	{
		if (box.min[i] > box_new.min[i])
			box.min[i] = box_new.min[i];
		if (box.max[i] < box_new.max[i])
			box.max[i] = box_new.max[i];
	}
}

TileBox extend_tile_box(OSGTree& tree)
{
	TileBox box = tree.bbox;
	for (auto& i : tree.sub_nodes)
	{
		TileBox sub_tile = extend_tile_box(i);
		expend_box(box, sub_tile);
	}
	tree.bbox = box;
	return box;
}

std::string get_boundingBox(TileBox bbox)
{
	std::string box_str = "\"boundingVolume\":{";
	box_str += "\"box\":[";
	std::vector<double> v_box = convert_bbox(bbox);
	for (auto v : v_box)
	{
		box_str += std::to_string(v);
		box_str += ",";
	}
	box_str.pop_back();
	box_str += "]}";
	return box_str;
}

std::string get_boundingRegion(TileBox bbox, double x, double y)
{
	std::string box_str = "\"boundingVolume\":{";
	box_str += "\"region\":[";
	std::vector<double> v_box(6);
	v_box[0] = meter_to_longti(bbox.min[0], y) + x;
	v_box[1] = meter_to_lati(bbox.min[1]) + y;
	v_box[2] = meter_to_longti(bbox.max[0], y) + x;
	v_box[3] = meter_to_lati(bbox.max[1]) + y;
	v_box[4] = bbox.min[2];
	v_box[5] = bbox.max[2];

	for (auto v : v_box) {
		box_str += std::to_string(v);
		box_str += ",";
	}
	box_str.pop_back();
	box_str += "]}";
	return box_str;
}

void calc_geometric_error(OSGTree& tree)
{
	const double EPS = 1e-12;
	// 深度优先
	for (auto& i : tree.sub_nodes)
	{
		calc_geometric_error(i);
	}
	if (tree.sub_nodes.empty())
	{
		tree.geometricError = 0.0;
	}
	else
	{
		bool has = false;
		OSGTree leaf;
		for (auto& i : tree.sub_nodes)
		{
			if (abs(i.geometricError) > EPS)
			{
				has = true;
				leaf = i;
			}
		}

		auto get_geometric_error = [] (TileBox & bbox)
		{
			if (bbox.max.empty() || bbox.min.empty())
			{
				LOG_E("bbox 为空！");

				return 0.0;
			}

			double max_err = std::max((bbox.max[0] - bbox.min[0]), (bbox.max[1] - bbox.min[1]));
			max_err = std::max(max_err, (bbox.max[2] - bbox.min[2]));

			return max_err / 20.0;
		};
		
		if (has == false)
		{
			tree.geometricError = get_geometric_error(tree.bbox);
		}
		else
		{
			tree.geometricError = leaf.geometricError * 2.0;
		}
	}
}

//===============工具结束==================

//===============OsgbReader==================
std::string OsgbReader::Osgb23dTile(
	const std::string strInPath, const std::string& strOutPath, 
	double* pBox, int* pLen, double dCenterX, double dCenterY, int nMaxLevel,
	bool bEnableTextureCompress, bool bEnableMeshOpt, bool bEnableDraco)
{
	std::string path = osg_string(strInPath);

	// Auto-detect directory and find root OSGB file
	if (is_directory(path))
	{
		printf("[INFO] Input is directory, searching for root OSGB file...\n");
		std::string root_osgb = find_root_osgb(path);
		if (root_osgb.empty())
		{
			LOG_E("No root OSGB file found in directory [%s]!", strInPath.c_str());
			return "";
		}
		printf("[INFO] Found root OSGB: %s\n", root_osgb.c_str());
		path = root_osgb;
	}

	OSGTree root = GetAllTree(path);
	if (root.file_name.empty())
	{
		LOG_E("打开文件 [%s] 失败！", strInPath.c_str());
		return "";
	}

	DoTileJob(root, strOutPath, nMaxLevel, 
		bEnableTextureCompress, bEnableMeshOpt, bEnableDraco);

	extend_tile_box(root);

	if (root.bbox.max.empty() || root.bbox.min.empty())
	{
		LOG_E("[%s] bbox 为空！", strInPath.c_str());
		return "";
	}

	calc_geometric_error(root);

	root.geometricError = 1000.0;
	std::string strJson = EncodeTileJson(root, dCenterX, dCenterY);
	root.bbox.extend(0.2);
	memcpy(pBox, root.bbox.max.data(), 3 * sizeof(double));
	memcpy(pBox + 3, root.bbox.min.data(), 3 * sizeof(double));
	*pLen = strJson.length();

	return strJson;
}

bool OsgbReader::Osgb2GlbBuf(std::string path, std::string& glb_buff, int node_type, bool enable_texture_compress, bool enable_meshopt, bool enable_draco)
{
	return Osgb2GlbBuf(path, glb_buff, MeshInfo(), node_type, enable_texture_compress, enable_meshopt, enable_draco, false);
}

bool OsgbReader::Osgb2Glb(const std::string& strInPath, const std::string& strOutPath, bool bEnableTextureCompress/* = false*/, bool bEnableMeshOpt/* = false*/, bool bEnableDraco/* = false*/)
{
	MeshInfo minfo;
	std::string glb_buf = "";
	std::string path = osg_string(strInPath);

	// 自动检测目录并查找根 OSGB 文件
	if (is_directory(path))
	{
		printf("[INFO] 输入是目录，正在搜索根 OSGB 文件...\n");
		std::string root_osgb = find_root_osgb(path);
		if (root_osgb.empty())
		{
			LOG_E("在目录 [%s] 中未找到根 OSGB 文件！", strInPath.c_str());
			return false;
		}
		printf("[INFO] 找到根 OSGB：%s\n", root_osgb.c_str());
		path = root_osgb;
	}

	bool ret = Osgb2GlbBuf(path, glb_buf, minfo, -1, 
		bEnableTextureCompress, bEnableMeshOpt, bEnableDraco);
	if (!ret)
	{
		LOG_E("转换为 glb 失败");

		return false;
	}

	ret = write_file(strOutPath.c_str(), glb_buf.data(), (unsigned long)glb_buf.size());
	if (!ret)
	{
		LOG_E("写入 glb 文件失败");

		return false;
	}

	return true;
}

struct OsgBuildState
{
	tinygltf::Buffer* buffer;

	tinygltf::Model* model;

	osg::Vec3f point_max;

	osg::Vec3f point_min;

	int draw_array_first;

	int draw_array_count;
};

template<class T>
void WriteOsgIndecis(T* drawElements, OsgBuildState* osgState, int componentType)
{
	unsigned max_index = 0;
	unsigned min_index = 1 << 30;
	unsigned buffer_start = osgState->buffer->data.size();

	unsigned IndNum = drawElements->getNumIndices();
	for (unsigned m = 0; m < IndNum; m++)
	{
		auto idx = drawElements->at(m);
		put_val(osgState->buffer->data, idx);
		if (idx > max_index) max_index = idx;
		if (idx < min_index) min_index = idx;
	}
	alignment_buffer(osgState->buffer->data);

	tinygltf::Accessor acc;
	acc.bufferView = osgState->model->bufferViews.size();
	acc.type = TINYGLTF_TYPE_SCALAR;
	acc.componentType = componentType;
	acc.count = IndNum;
	acc.maxValues = { (double)max_index };
	acc.minValues = { (double)min_index };
	osgState->model->accessors.push_back(acc);

	tinygltf::BufferView bfv;
	bfv.buffer = 0;
	bfv.target = TINYGLTF_TARGET_ELEMENT_ARRAY_BUFFER;
	bfv.byteOffset = buffer_start;
	bfv.byteLength = osgState->buffer->data.size() - buffer_start;
	osgState->model->bufferViews.push_back(bfv);
}

void WriteVec3Array(osg::Vec3Array* v3f, OsgBuildState* osgState, osg::Vec3f& point_max, osg::Vec3f& point_min)
{
	int vec_start = 0;
	int vec_end = v3f->size();
	if (osgState->draw_array_first >= 0)
	{
		vec_start = osgState->draw_array_first;
		vec_end = osgState->draw_array_count + vec_start;
	}
	unsigned buffer_start = osgState->buffer->data.size();
	for (int vidx = vec_start; vidx < vec_end; vidx++)
	{
		osg::Vec3f point = v3f->at(vidx);
		put_val(osgState->buffer->data, point.x());
		put_val(osgState->buffer->data, point.y());
		put_val(osgState->buffer->data, point.z());
		expand_bbox3d(point_max, point_min, point);
	}
	alignment_buffer(osgState->buffer->data);

	tinygltf::Accessor acc;
	acc.bufferView = osgState->model->bufferViews.size();
	acc.count = vec_end - vec_start;
	acc.componentType = TINYGLTF_COMPONENT_TYPE_FLOAT;
	acc.type = TINYGLTF_TYPE_VEC3;
	acc.maxValues = { point_max.x(), point_max.y(), point_max.z() };
	acc.minValues = { point_min.x(), point_min.y(), point_min.z() };
	osgState->model->accessors.push_back(acc);

	tinygltf::BufferView bfv;
	bfv.buffer = 0;
	bfv.target = TINYGLTF_TARGET_ARRAY_BUFFER;
	bfv.byteOffset = buffer_start;
	bfv.byteLength = osgState->buffer->data.size() - buffer_start;
	osgState->model->bufferViews.push_back(bfv);
}

void WriteVec2Array(osg::Vec2Array* v2f, OsgBuildState* osgState)
{
	int vec_start = 0;
	int vec_end = v2f->size();
	if (osgState->draw_array_first >= 0)
	{
		vec_start = osgState->draw_array_first;
		vec_end = osgState->draw_array_count + vec_start;
	}
	osg::Vec2f point_max(-1e38, -1e38);
	osg::Vec2f point_min(1e38, 1e38);
	unsigned buffer_start = osgState->buffer->data.size();
	for (int vidx = vec_start; vidx < vec_end; vidx++)
	{
		osg::Vec2f point = v2f->at(vidx);
		put_val(osgState->buffer->data, point.x());
		put_val(osgState->buffer->data, point.y());
		expand_bbox2d(point_max, point_min, point);
	}
	alignment_buffer(osgState->buffer->data);

	tinygltf::Accessor acc;
	acc.bufferView = osgState->model->bufferViews.size();
	acc.count = vec_end - vec_start;
	acc.componentType = TINYGLTF_COMPONENT_TYPE_FLOAT;
	acc.type = TINYGLTF_TYPE_VEC2;
	acc.maxValues = { point_max.x(), point_max.y() };
	acc.minValues = { point_min.x(), point_min.y() };
	osgState->model->accessors.push_back(acc);

	tinygltf::BufferView bfv;
	bfv.buffer = 0;
	bfv.target = TINYGLTF_TARGET_ARRAY_BUFFER;
	bfv.byteOffset = buffer_start;
	bfv.byteLength = osgState->buffer->data.size() - buffer_start;
	osgState->model->bufferViews.push_back(bfv);
}

void WriteElementArrayPrimitive(osg::Geometry* g, osg::PrimitiveSet* ps, OsgBuildState* osgState, PrimitiveState* pmtState)
{
	tinygltf::Primitive primits;
	primits.indices = osgState->model->accessors.size();
	osgState->draw_array_first = -1;
	osg::PrimitiveSet::Type t = ps->getType();
	switch (t)
	{
	case(osg::PrimitiveSet::DrawElementsUBytePrimitiveType):
	{
		const osg::DrawElementsUByte* drawElements = static_cast<const osg::DrawElementsUByte*>(ps);
		WriteOsgIndecis(drawElements, osgState, TINYGLTF_COMPONENT_TYPE_UNSIGNED_BYTE);
		break;
	}
	case(osg::PrimitiveSet::DrawElementsUShortPrimitiveType):
	{
		const osg::DrawElementsUShort* drawElements = static_cast<const osg::DrawElementsUShort*>(ps);
		WriteOsgIndecis(drawElements, osgState, TINYGLTF_COMPONENT_TYPE_UNSIGNED_SHORT);
		break;
	}
	case(osg::PrimitiveSet::DrawElementsUIntPrimitiveType):
	{
		const osg::DrawElementsUInt* drawElements = static_cast<const osg::DrawElementsUInt*>(ps);
		WriteOsgIndecis(drawElements, osgState, TINYGLTF_COMPONENT_TYPE_UNSIGNED_INT);
		break;
	}
	case osg::PrimitiveSet::DrawArraysPrimitiveType:
	{
		primits.indices = -1;
		osg::DrawArrays* da = dynamic_cast<osg::DrawArrays*>(ps);
		osgState->draw_array_first = da->getFirst();
		osgState->draw_array_count = da->getCount();
		break;
	}
	default:
	{
		LOG_E("不支持的 osg::PrimitiveSet::Type [%d]", t);
		exit(1);
		break;
	}
	}
	if (pmtState->vertexAccessor > -1 && osgState->draw_array_first == -1)
	{
		primits.attributes["POSITION"] = pmtState->vertexAccessor;
	}
	else
	{
		osg::Vec3f point_max(-1e38, -1e38, -1e38);
		osg::Vec3f point_min(1e38, 1e38, 1e38);
		osg::Vec3Array* vertexArr = (osg::Vec3Array*)g->getVertexArray();
		primits.attributes["POSITION"] = osgState->model->accessors.size();
		if (pmtState->vertexAccessor == -1 && osgState->draw_array_first == -1)
		{
			pmtState->vertexAccessor = osgState->model->accessors.size();
		}
		WriteVec3Array(vertexArr, osgState, point_max, point_min);
		if (point_min.x() <= point_max.x() && point_min.y() <= point_max.y() && point_min.z() <= point_max.z())
		{
			expand_bbox3d(osgState->point_max, osgState->point_min, point_max);
			expand_bbox3d(osgState->point_max, osgState->point_min, point_min);
		}
	}
	osg::Vec3Array* normalArr = (osg::Vec3Array*)g->getNormalArray();
	if (normalArr)
	{
		if (pmtState->normalAccessor > -1 && osgState->draw_array_first == -1)
		{
			primits.attributes["NORMAL"] = pmtState->normalAccessor;
		}
		else
		{
			osg::Vec3f point_max(-1e38, -1e38, -1e38);
			osg::Vec3f point_min(1e38, 1e38, 1e38);
			primits.attributes["NORMAL"] = osgState->model->accessors.size();
			if (pmtState->normalAccessor == -1 && osgState->draw_array_first == -1)
			{
				pmtState->normalAccessor = osgState->model->accessors.size();
			}

			WriteVec3Array(normalArr, osgState, point_max, point_min);
		}
	}

	osg::Vec2Array* texArr = (osg::Vec2Array*)g->getTexCoordArray(0);
	if (texArr)
	{
		if (pmtState->textcdAccessor > -1 && osgState->draw_array_first == -1)
		{
			primits.attributes["TEXCOORD_0"] = pmtState->textcdAccessor;
		}
		else
		{
			primits.attributes["TEXCOORD_0"] = osgState->model->accessors.size();
			if (pmtState->textcdAccessor == -1 && osgState->draw_array_first == -1)
			{
				pmtState->textcdAccessor = osgState->model->accessors.size();
			}

			WriteVec2Array(texArr, osgState);
		}
	}

	primits.material = -1;

	switch (ps->getMode())
	{
	case GL_TRIANGLES:
		primits.mode = TINYGLTF_MODE_TRIANGLES;
		break;
	case GL_TRIANGLE_STRIP:
		primits.mode = TINYGLTF_MODE_TRIANGLE_STRIP;
		break;
	case GL_TRIANGLE_FAN:
		primits.mode = TINYGLTF_MODE_TRIANGLE_FAN;
		break;
	default:
		LOG_E("不支持的原始模式：%d", (int)ps->getMode());
		exit(1);
		break;
	}

	osgState->model->meshes.back().primitives.push_back(primits);
}

void WriteOsgGeometry(osg::Geometry* g, OsgBuildState* osgState, bool enable_simplify, bool enable_draco)
{
	if (enable_simplify)
	{
		SimplificationParams simplication_params;
		simplication_params.enable_simplification = true;
		MeshProcessor::SimplifyMeshGeometry(g, simplication_params);
	}
	if (enable_draco)
	{
		std::vector<unsigned char> compressed_data;
		size_t compressed_size = 0;
		DracoCompressionParams draco_params;
		draco_params.enable_compression = true;

		MeshProcessor::CompressMeshGeometry(g, draco_params, compressed_data, compressed_size);
	}

	osg::PrimitiveSet::Type t = g->getPrimitiveSet(0)->getType();
	PrimitiveState pmtState = { -1, -1, -1 };
	for (unsigned int k = 0; k < g->getNumPrimitiveSets(); k++)
	{
		osg::PrimitiveSet* ps = g->getPrimitiveSet(k);
		if (t != ps->getType())
		{
			LOG_E("osgb 中 PrimitiveSets 类型不相同");
			exit(1);
		}

		WriteElementArrayPrimitive(g, ps, osgState, &pmtState);
	}
}

tinygltf::Material make_color_material_osgb(double r, double g, double b)
{
	tinygltf::Material material;
	material.name = "default";
	material.pbrMetallicRoughness.baseColorFactor = { r, g, b, 1.0 };
	material.pbrMetallicRoughness.metallicFactor = 0.0;
	material.pbrMetallicRoughness.roughnessFactor = 1.0;
	return material;
}

bool OsgbReader::Osgb2GlbBuf(
	std::string path, std::string& glb_buff, MeshInfo& mesh_info, int node_type, 
	bool enable_texture_compress, bool enable_meshopt, bool enable_draco, bool need_mesh_info/* = true*/)
{
	vector<string> fileNames = { path };
	std::string parent_path = get_parent(path);

	osg::ref_ptr<osg::Node> root = osgDB::readNodeFiles(fileNames);
	if (!root.valid())
	{
		return false;
	}
	InfoVisitor infoVisitor(parent_path, node_type == -1);
	root->accept(infoVisitor);
	if (node_type == 2 || infoVisitor.geometry_array.empty())
	{
		infoVisitor.geometry_array = infoVisitor.other_geometry_array;
		infoVisitor.texture_array = infoVisitor.other_texture_array;
	}
	if (infoVisitor.geometry_array.empty())
		return false;

	osgUtil::SmoothingVisitor sv;
	root->accept(sv);

	tinygltf::TinyGLTF gltf;
	tinygltf::Model model;
	tinygltf::Buffer buffer;

	osg::Vec3f point_max, point_min;
	OsgBuildState osgState =
	{
		&buffer, &model, osg::Vec3f(-1e38,-1e38,-1e38), osg::Vec3f(1e38,1e38,1e38), -1, -1
	};
	model.meshes.resize(1);
	int primitive_idx = 0;
	for (auto g : infoVisitor.geometry_array)
	{
		if (!g->getVertexArray() || g->getVertexArray()->getDataSize() == 0)
			continue;

		WriteOsgGeometry(g, &osgState, enable_meshopt, enable_draco);
		if (infoVisitor.texture_array.size())
		{
			for (unsigned int k = 0; k < g->getNumPrimitiveSets(); k++)
			{
				auto tex = infoVisitor.texture_map[g];
				if (tex)
				{
					for (auto texture : infoVisitor.texture_array)
					{
						model.meshes[0].primitives[primitive_idx].material++;
						if (tex == texture)
							break;
					}
				}
				primitive_idx++;
			}
		}
	}
	if (model.meshes[0].primitives.empty())
		return false;

	if (need_mesh_info)
	{
		mesh_info.min =
		{
			osgState.point_min.x(),
			osgState.point_min.y(),
			osgState.point_min.z()
		};

		mesh_info.max =
		{
			osgState.point_max.x(),
			osgState.point_max.y(),
			osgState.point_max.z()
		};
	}

	{
		for (auto tex : infoVisitor.texture_array)
		{
			unsigned buffer_start = buffer.data.size();

			std::vector<unsigned char> image_data;
			std::string mime_type;
			if (MeshProcessor::ProcessTexture(tex, image_data, mime_type, enable_texture_compress))
			{
				buffer.data.insert(buffer.data.end(), image_data.begin(), image_data.end());

				tinygltf::Image image;
				image.mimeType = mime_type;
				image.bufferView = model.bufferViews.size();
				model.images.push_back(image);

				tinygltf::BufferView bfv;
				bfv.buffer = 0;
				bfv.byteOffset = buffer_start;
				alignment_buffer(buffer.data);
				bfv.byteLength = buffer.data.size() - buffer_start;
				model.bufferViews.push_back(bfv);
			}
		}
	}
	{
		tinygltf::Node node;
		node.mesh = 0;
		model.nodes.push_back(node);
	}
	{
		tinygltf::Scene sence;
		sence.nodes.push_back(0);
		model.scenes = { sence };
		model.defaultScene = 0;
	}
	{
		tinygltf::Sampler sample;
		sample.magFilter = TINYGLTF_TEXTURE_FILTER_LINEAR;
		sample.minFilter = TINYGLTF_TEXTURE_FILTER_NEAREST_MIPMAP_LINEAR;
		sample.wrapS = TINYGLTF_TEXTURE_WRAP_REPEAT;
		sample.wrapT = TINYGLTF_TEXTURE_WRAP_REPEAT;
		model.samplers = { sample };
	}
	if (enable_texture_compress)
	{
		model.extensionsRequired = { "KHR_materials_unlit", "KHR_texture_basisu" };
		model.extensionsUsed = { "KHR_materials_unlit", "KHR_texture_basisu" };
	}
	else
	{
		model.extensionsRequired = { "KHR_materials_unlit" };
		model.extensionsUsed = { "KHR_materials_unlit" };
	}

	if (enable_draco)
	{
		model.extensionsRequired.push_back("KHR_draco_mesh_compression");
		model.extensionsUsed.push_back("KHR_draco_mesh_compression");
	}

	for (int i = 0; i < infoVisitor.texture_array.size(); i++)
	{
		tinygltf::Material mat = make_color_material_osgb(1.0, 1.0, 1.0);
		mat.pbrMetallicRoughness.baseColorTexture.index = i;
		model.materials.push_back(mat);
	}

	// finish buffer
	model.buffers.push_back(std::move(buffer));
	// texture
	{
		int texture_index = 0;
		for (auto tex : infoVisitor.texture_array)
		{
			tinygltf::Texture texture;
			texture.sampler = 0;

			if (enable_texture_compress)
			{
				tinygltf::Value::Object basisu_ext;
				basisu_ext["source"] = tinygltf::Value(texture_index);
				texture.extensions["KHR_texture_basisu"] = tinygltf::Value(basisu_ext);
			}
			else
			{
				texture.source = texture_index;
			}

			texture_index++;
			model.textures.push_back(texture);
		}
	}
	model.asset.version = "2.0";
	model.asset.generator = "fanvanzh";

	std::ostringstream ss;
	bool res = gltf.WriteGltfSceneToStream(&model, ss, false, true);
	if (res)
	{
		glb_buff = ss.str();
	}

	return true;
}

bool OsgbReader::Osgb2B3dmBuf(
	std::string path, std::string& b3dm_buf, TileBox& tile_box, int node_type, 
	bool enable_texture_compress, bool enable_meshopt, bool enable_draco)
{
	using nlohmann::json;

	std::string glb_buf;
	MeshInfo minfo;
	bool ret = Osgb2GlbBuf(path, glb_buf, minfo, node_type, enable_texture_compress, enable_meshopt, enable_draco);
	if (!ret)
	{
		return false;
	}

	tile_box.max = minfo.max;
	tile_box.min = minfo.min;

	int mesh_count = 1;
	std::string feature_json_string;
	feature_json_string += "{\"BATCH_LENGTH\":";
	feature_json_string += std::to_string(mesh_count);
	feature_json_string += "}";
	while ((feature_json_string.size() + 28) % 8 != 0)
	{
		feature_json_string.push_back(' ');
	}

	json batch_json;
	std::vector<int> ids;
	for (int i = 0; i < mesh_count; ++i)
	{
		ids.push_back(i);
	}

	std::vector<std::string> names;
	for (int i = 0; i < mesh_count; ++i)
	{
		std::string mesh_name = "mesh_";
		mesh_name += std::to_string(i);
		names.push_back(mesh_name);
	}

	batch_json["batchId"] = ids;
	batch_json["name"] = names;
	std::string batch_json_string = batch_json.dump();
	while (batch_json_string.size() % 8 != 0)
	{
		batch_json_string.push_back(' ');
	}

	int feature_json_len = feature_json_string.size();
	int feature_bin_len = 0;
	int batch_json_len = batch_json_string.size();
	int batch_bin_len = 0;
	int total_len = 28 /*header size*/ + feature_json_len + batch_json_len + glb_buf.size();

	b3dm_buf += "b3dm";
	int version = 1;
	put_val(b3dm_buf, version);
	put_val(b3dm_buf, total_len);
	put_val(b3dm_buf, feature_json_len);
	put_val(b3dm_buf, feature_bin_len);
	put_val(b3dm_buf, batch_json_len);
	put_val(b3dm_buf, batch_bin_len);
	b3dm_buf.append(feature_json_string.begin(), feature_json_string.end());
	b3dm_buf.append(batch_json_string.begin(), batch_json_string.end());
	b3dm_buf.append(glb_buf);

	return true;
}

void OsgbReader::DoTileJob(OSGTree& tree, std::string out_path, int max_lvl, bool enable_texture_compress, bool enable_meshopt, bool enable_draco)
{
	if (tree.file_name.empty())
	{
		return;
	}

	int lvl = get_lvl_num(tree.file_name);
	if (lvl > max_lvl) 
	{
		return;
	}

	if (tree.type > 0)
	{
		std::string b3dm_buf;
		Osgb2B3dmBuf(tree.file_name, b3dm_buf, tree.bbox, tree.type, enable_texture_compress, enable_meshopt, enable_draco);
		std::string out_file = out_path;
		out_file += "/";
		out_file += replace(get_file_name(tree.file_name), ".osgb", tree.type != 2 ? ".b3dm" : "o.b3dm");
		if (!b3dm_buf.empty())
		{
			write_file(out_file.c_str(), b3dm_buf.data(), b3dm_buf.size());
		}
	}

	for (auto& i : tree.sub_nodes)
	{
		DoTileJob(i, out_path, max_lvl, enable_texture_compress, enable_meshopt, enable_draco);
	}
}

std::string OsgbReader::EncodeTileJson(OSGTree& tree, double x, double y)
{
	if (tree.bbox.max.empty() || tree.bbox.min.empty())
	{
		return "";
	}

	std::string file_name = get_file_name(tree.file_name);
	std::string parent_str = get_parent(tree.file_name);
	std::string file_path = get_file_name(parent_str);

	char buf[512];
	sprintf(buf, "{ \"geometricError\":%.2f,", tree.geometricError);
	std::string tile = buf;
	TileBox cBox = tree.bbox;

	std::string content_box = get_boundingBox(cBox);
	TileBox bbox = tree.bbox;

	std::string tile_box = get_boundingBox(bbox);

	tile += tile_box;
	if (tree.type > 0)
	{
		tile += ", \"content\":{ \"uri\":";
		std::string uri_path = "./";
		uri_path += file_name;
		std::string uri = replace(uri_path, ".osgb", tree.type != 2 ? ".b3dm" : "o.b3dm");
		tile += "\"";
		tile += uri;
		tile += "\",";
		tile += content_box;
		tile += "}";
	}

	tile += ",\"children\":[";
	for (auto& i : tree.sub_nodes)
	{
		std::string node_json = EncodeTileJson(i, x, y);
		if (!node_json.empty()) {
			tile += node_json;
			tile += ",";
		}
	}
	if (tile.back() == ',')
	{
		tile.pop_back();
	}

	tile += "]}";

	return tile;
}

OSGTree OsgbReader::GetAllTree(std::string& file_name)
{
	OSGTree root_tile;
	vector<string> fileNames = { file_name };

	InfoVisitor infoVisitor(get_parent(file_name));
	{
		osg::ref_ptr<osg::Node> root = osgDB::readNodeFiles(fileNames);
		if (!root)
		{
			std::string name = utf8_string(file_name.c_str());
			LOG_E("read node files [%s] fail!", name.c_str());
			return root_tile;
		}
		root_tile.file_name = file_name;
		root_tile.type = 1;
		root->accept(infoVisitor);
	}

	for (auto& i : infoVisitor.sub_node_names)
	{
		OSGTree tree = GetAllTree(i);
		if (!tree.file_name.empty())
		{
			if (tree.type == 0)
			{
				for (auto& node : tree.sub_nodes)
					root_tile.sub_nodes.push_back(node);
			}
			else
			{
				root_tile.sub_nodes.push_back(tree);
			}
		}
	}

	if (!infoVisitor.other_geometry_array.empty() && !infoVisitor.geometry_array.empty())
	{
		OSGTree new_root_tile;
		new_root_tile.type = 0;
		new_root_tile.file_name = file_name;
		OSGTree tile;
		tile.type = 2;
		tile.file_name = file_name;
		new_root_tile.sub_nodes.push_back(root_tile);
		new_root_tile.sub_nodes.push_back(tile);
		root_tile = new_root_tile;
	}

	return root_tile;
}

//===============OsgbReader 批量处理==================

std::string OsgbReader::Osgb23dTileBatch(
	const std::string& pDataDir, const std::string& strOutputDir, 
	double* pMergedBox, int* pJsonLen, double dCenterX, double dCenterY, int nMaxLevel, 
	bool bEnableTextureCompress, bool bEnableMeshOpt, bool bEnableDraco)
{
	// 1. 构建 Data 目录路径
	std::string data_path = osg_string(pDataDir);
	// 标准化路径分隔符
	for (char& c : data_path)
	{
		if (c == '\\') c = '/';
	}
	// 移除尾部斜杠
	if (!data_path.empty() && data_path.back() == '/')
		data_path.pop_back();

	// 检查路径是否以 "/Data" 结尾
	std::string check_data_dir = data_path;
	size_t data_pos = data_path.rfind("/Data");
	if (data_pos == std::string::npos || data_pos != data_path.length() - 5)
	{
		// 不以 "/Data" 结尾，追加它
		check_data_dir = data_path + "/Data";
	}

	printf("[INFO] 在以下位置搜索瓦片：%s\n", check_data_dir.c_str());

	if (!is_directory(check_data_dir))
	{
		LOG_E("Data 目录不存在：%s", check_data_dir.c_str());
		return NULL;
	}

	// 2. 创建输出目录
	std::string out_path = strOutputDir;
#ifdef _WIN32
	CreateDirectoryA(out_path.c_str(), NULL);
	std::string out_data_path = out_path + "/Data";
	CreateDirectoryA(out_data_path.c_str(), NULL);
#else
	mkdir(out_path.c_str(), 0755);
	std::string out_data_path = out_path + "/Data";
	mkdir(out_data_path.c_str(), 0755);
#endif

	// 3. 遍历 Data 目录，收集所有 Tile_* 目录
	struct TileInfo {
		std::string tile_name;
		std::string osgb_path;
		std::string output_path;
		TileBox bbox;
	};

	std::vector<TileInfo> tiles;
	TileBox global_bbox;

#ifdef _WIN32
	WIN32_FIND_DATAA findData;
	std::string search_pattern = check_data_dir + "/*";
	HANDLE hFind = FindFirstFileA(search_pattern.c_str(), &findData);

	if (hFind == INVALID_HANDLE_VALUE)
	{
		LOG_E("列出目录失败：%s", check_data_dir.c_str());
		return NULL;
	}

	do
	{
		if (findData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)
		{
			std::string tile_name = findData.cFileName;
			if (tile_name == "." || tile_name == "..")
				continue;

			// Check if directory starts with "Tile_"
			if (tile_name.find("Tile_") != 0)
				continue;

			// Find root OSGB file in this directory (without _L suffix)
			std::string tile_dir = check_data_dir + "/" + tile_name;
			std::string osgb_file = tile_dir + "/" + tile_name + ".osgb";

			// Check if file exists
			DWORD attrs = GetFileAttributesA(osgb_file.c_str());
			if (attrs != INVALID_FILE_ATTRIBUTES && !(attrs & FILE_ATTRIBUTE_DIRECTORY))
			{
				// File exists, add to processing list
				TileInfo info;
				info.tile_name = tile_name;
				info.osgb_path = osgb_file;
				info.output_path = out_data_path + "/" + tile_name;
				CreateDirectoryA(info.output_path.c_str(), NULL);
				tiles.push_back(info);
			}
		}
	} while (FindNextFileA(hFind, &findData));
	FindClose(hFind);
#endif

	if (tiles.empty())
	{
		LOG_E("未在 %s 中找到 Tile_* 目录", check_data_dir.c_str());
		return NULL;
	}

	printf("[INFO] Found %zu tile directories to process\n", tiles.size());

	// 4. Process each tile
	std::vector<std::string> tile_jsons;

	for (size_t i = 0; i < tiles.size(); i++)
	{
		TileInfo& tile = tiles[i];
		printf("[INFO] Processing tile %zu/%zu: %s\n", i + 1, tiles.size(), tile.tile_name.c_str());

		double bbox_data[6] = {0};
		int bbox_len = 0;

		std::string strTileJson = Osgb23dTile(
			tile.osgb_path,
			tile.output_path,
			bbox_data,
			&bbox_len,
			dCenterX,
			dCenterY,
			nMaxLevel,
			bEnableTextureCompress,
			bEnableMeshOpt,
			bEnableDraco
		);

		if (!strTileJson.empty())
		{
			tile_jsons.push_back(strTileJson);

			// 更新边界框
			tile.bbox.max = {bbox_data[0], bbox_data[1], bbox_data[2]};
			tile.bbox.min = {bbox_data[3], bbox_data[4], bbox_data[5]};

			// 合并到全局边界框
			if (global_bbox.max.empty())
			{
				global_bbox = tile.bbox;
			}
			else
			{
				expend_box(global_bbox, tile.bbox);
			}

			// 将瓦片 JSON 包装在完整的 tileset 结构中（遵循 Rust 实现）
			std::string wrapped_json = "{";
			wrapped_json += "\"asset\":{\"version\":\"1.0\",\"gltfUpAxis\":\"Z\"},";
			wrapped_json += "\"geometricError\":1000,";
			wrapped_json += "\"root\":";
			wrapped_json += strTileJson;  // 这是此瓦片的根节点
			wrapped_json += "}";

			// 保存单个瓦片的 tileset.json
			std::string tileset_path = tile.output_path + "/tileset.json";
			write_file(tileset_path.c_str(), wrapped_json.data(), wrapped_json.size());
		}
		else
		{
			LOG_E("处理瓦片失败：%s", tile.tile_name.c_str());
		}
	}

	if (tile_jsons.empty())
	{
		LOG_E("没有成功处理任何瓦片");
		return NULL;
	}

	// 5. 计算变换矩阵
	std::vector<double> transform_matrix(16);
	{
		// 使用合并的全局边界框的最小高度
		double height_min = global_bbox.min.empty() ? 0.0 : global_bbox.min[2];
		transform_c(dCenterX, dCenterY, height_min, transform_matrix.data());
	}

	// 6. 生成根 tileset.json（遵循 Rust 实现结构）
	std::string root_json = "{";

	// 添加资产信息
	root_json += "\"asset\":{\"version\":\"1.0\",\"gltfUpAxis\":\"Z\"},";

	// Add root geometric error
	root_json += "\"geometricError\":2000,";

	// 添加根对象
	root_json += "\"root\":{";

	// 添加变换矩阵（16 个元素，按列主序）
	root_json += "\"transform\":[";
	for (int i = 0; i < 16; i++)
	{
		char buf[64];
		sprintf(buf, "%.10f", transform_matrix[i]);
		root_json += buf;
		if (i < 15)
			root_json += ",";
	}
	root_json += "],";

	// 添加根边界框
	root_json += get_boundingBox(global_bbox);
	root_json += ",";

	// 添加根几何误差（冗余但匹配 Rust 结构）
	root_json += "\"geometricError\":2000,";

	// 添加子项（所有瓦片）
	root_json += "\"children\":[";

	for (size_t i = 0; i < tiles.size(); i++)
	{
		const TileInfo& tile = tiles[i];

		root_json += "{";
		root_json += get_boundingBox(tile.bbox);
		root_json += ",\"geometricError\":1000,";
		root_json += "\"content\":{\"uri\":\"./Data/" + tile.tile_name + "/tileset.json\"}";
		root_json += "}";

		if (i < tiles.size() - 1)
			root_json += ",";
	}

	root_json += "]";  // Close children array
	root_json += "}";  // Close root object
	root_json += "}";  // Close root tileset

	// 7. 保存根 tileset.json
	std::string root_tileset_path = strOutputDir + "/tileset.json";
	write_file(root_tileset_path.c_str(), root_json.data(), root_json.size());

	printf("[INFO] 批量处理完成！生成了包含 %zu 个瓦片的根 tileset.json\n", tiles.size());

	// 7. 返回结果
	if (nullptr !=  pMergedBox)
	{
		memcpy(pMergedBox, global_bbox.max.data(), 3 * sizeof(double));
		memcpy(pMergedBox + 3, global_bbox.min.data(), 3 * sizeof(double));
	}

	*pJsonLen = root_json.length();

	return root_json;
}

//===============OsgbReader 结束==================
