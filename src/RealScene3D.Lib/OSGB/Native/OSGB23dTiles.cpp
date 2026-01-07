// ============================================================================
// OSGB23dTiles 实现
// 将 OSGB (OpenSceneGraph Binary) 文件转换为 GLB 和 3D Tiles 格式
// ============================================================================

#include <Eigen/Eigen>

#ifdef ENABLE_PROJ
#include <proj.h>  // PROJ coordinate transformation
#endif

// Basis Universal for KTX2 texture compression
#ifdef ENABLE_KTX2
#include <basisu/encoder/basisu_comp.h>
#include <basisu/transcoder/basisu_transcoder.h>
#endif

#include "OSGB23dTiles.h"
#include "OSGBTools.h"
#include "MeshProcessor.h"
#include "GeoTransform.h"

// USE_OSGPLUGIN 在 Linux/macOS 上需要用于静态插件注册
// 在 Windows 上使用动态链接，插件在运行时加载
#if defined(__unix__) || defined(__APPLE__)
#include <osgDB/Registry>
USE_OSGPLUGIN(osg)
#endif

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
		geometry_array.emplace_back(&geometry);
	}
	else
	{
		other_geometry_array.emplace_back(&geometry);
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
	std::string path = node.getDatabasePath();
	int n = node.getNumFileNames();
	for (size_t i = 1; i < n; i++)
	{
		std::string file_name = path + "/" + node.getFileName(i);
		sub_node_names.emplace_back(file_name);
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
		buf.emplace_back(0x00);
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
		{
			box.min[i] = box_new.min[i];
		}

		if (box.max[i] < box_new.max[i])
		{
			box.max[i] = box_new.max[i];
		}
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

		auto get_geometric_error = [](TileBox& bbox)
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

//===============OSGB23dTiles==================
std::tuple<bool, std::string, std::array<double, 6>> OSGB23dTiles::To3dTile(
	const std::string strInPath, const std::string& strOutPath,
	double dCenterX, double dCenterY, int nMaxLevel,
	bool bEnableTextureCompress, bool bEnableMeshOpt, bool bEnableDraco)
{
	std::string path = OSGBTools::OSGString(strInPath);

	// 自动检测目录并查找根 OSGB 文件
	if (OSGBTools::IsDirectory(path))
	{
		std::cout << "[INFO] Input is directory, searching for root OSGB file..." << std::endl;
		std::string root_osgb = OSGBTools::FindRootOSGB(path);
		if (root_osgb.empty())
		{
			LOG_E("No root OSGB file found in directory [%s]!", strInPath.c_str());
			return std::make_tuple(false, "", std::array<double, 6>{});
		}
		std::cout << "[INFO] Found root OSGB: " << root_osgb << std::endl;
		path = root_osgb;
	}

	OSGTree root = GetAllTree(path);
	if (root.file_name.empty())
	{
		LOG_E("打开文件 [%s] 失败！", strInPath.c_str());
		return std::make_tuple(false, "", std::array<double, 6>{});
	}

	DoTileJob(root, strOutPath, nMaxLevel,
		bEnableTextureCompress, bEnableMeshOpt, bEnableDraco);

	extend_tile_box(root);

	if (root.bbox.max.empty() || root.bbox.min.empty())
	{
		LOG_E("[%s] bbox 为空！", strInPath.c_str());
		return std::make_tuple(false, "", std::array<double, 6>{});
	}

	calc_geometric_error(root);

	root.geometricError = 1000.0;
	std::string strJson = EncodeTileJSON(root, dCenterX, dCenterY);
	root.bbox.extend(0.2);

	// 构建包围盒数组
	std::array<double, 6> box;
	std::copy(root.bbox.max.begin(), root.bbox.max.end(), box.begin());
	std::copy(root.bbox.min.begin(), root.bbox.min.end(), box.begin() + 3);

	return std::make_tuple(true, strJson, box);
}

bool OSGB23dTiles::ToGlbBuf(std::string path, std::string& glb_buff, int node_type, bool enable_texture_compress, bool enable_meshopt, bool enable_draco)
{
	return ToGlbBuf(path, glb_buff, MeshInfo(), node_type, enable_texture_compress, enable_meshopt, enable_draco, false);
}

bool OSGB23dTiles::ToGlb(const std::string& strInPath, const std::string& strOutPath, bool bEnableTextureCompress/* = false*/, bool bEnableMeshOpt/* = false*/, bool bEnableDraco/* = false*/)
{
	MeshInfo minfo;
	std::string glb_buf = "";
	std::string path = OSGBTools::OSGString(strInPath);

	// 自动检测目录并查找根 OSGB 文件
	if (OSGBTools::IsDirectory(path))
	{
		std::cout << "[INFO] 输入是目录，正在搜索根 OSGB 文件..." << std::endl;
		std::string root_osgb = OSGBTools::FindRootOSGB(path);
		if (root_osgb.empty())
		{
			LOG_E("在目录 [%s] 中未找到根 OSGB 文件！", strInPath.c_str());
			return false;
		}
		std::cout << "[INFO] 找到根 OSGB：" << root_osgb << std::endl;
		path = root_osgb;
	}

	bool ret = ToGlbBuf(path, glb_buf, minfo, -1, bEnableTextureCompress, bEnableMeshOpt, bEnableDraco);
	if (!ret)
	{
		LOG_E("转换为 glb 失败");

		return false;
	}

	ret = OSGBTools::WriteFile(strOutPath.c_str(), glb_buf.data(), (unsigned long)glb_buf.size());
	if (!ret)
	{
		LOG_E("写入 glb 文件失败");

		return false;
	}

	return true;
}

std::vector<uint8_t> OSGB23dTiles::ToGlbBufBytes(
	std::string strOsgbPath, int nNodeType,
	bool bEnableTextureCompress, bool bEnableMeshOpt, bool bEnableDraco)
{
	std::string glb_buff;
	MeshInfo mesh_info;

	bool ret = ToGlbBuf(strOsgbPath, glb_buff, mesh_info, nNodeType, bEnableTextureCompress, bEnableMeshOpt, bEnableDraco, false);

	if (!ret)
	{
		return std::vector<uint8_t>();
	}

	// 将 string 转换为 vector<uint8_t>
	std::vector<uint8_t> result(glb_buff.begin(), glb_buff.end());
	return result;
}

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
	osgState->model->accessors.emplace_back(acc);

	tinygltf::BufferView bfv;
	bfv.buffer = 0;
	bfv.target = TINYGLTF_TARGET_ELEMENT_ARRAY_BUFFER;
	bfv.byteOffset = buffer_start;
	bfv.byteLength = osgState->buffer->data.size() - buffer_start;
	osgState->model->bufferViews.emplace_back(bfv);
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
	osgState->model->accessors.emplace_back(acc);

	tinygltf::BufferView bfv;
	bfv.buffer = 0;
	bfv.target = TINYGLTF_TARGET_ARRAY_BUFFER;
	bfv.byteOffset = buffer_start;
	bfv.byteLength = osgState->buffer->data.size() - buffer_start;
	osgState->model->bufferViews.emplace_back(bfv);
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
	osgState->model->accessors.emplace_back(acc);

	tinygltf::BufferView bfv;
	bfv.buffer = 0;
	bfv.target = TINYGLTF_TARGET_ARRAY_BUFFER;
	bfv.byteOffset = buffer_start;
	bfv.byteLength = osgState->buffer->data.size() - buffer_start;
	osgState->model->bufferViews.emplace_back(bfv);
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

	osgState->model->meshes.back().primitives.emplace_back(primits);
}

void WriteOsgGeometry(osg::Geometry* g, OsgBuildState* osgState, bool enable_simplify, bool enable_draco)
{
	if (enable_simplify)
	{
		SimplificationParams simplication_params;
		simplication_params.bEnableSimplification = true;
		MeshProcessor::SimplifyMeshGeometry(g, simplication_params);
	}
	if (enable_draco)
	{
		std::vector<unsigned char> compressed_data;
		size_t compressed_size = 0;
		DracoCompressionParams draco_params;
		draco_params.bEnableCompression = true;

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

	// PBR 材质配置
	material.pbrMetallicRoughness.baseColorFactor = { r, g, b, 1.0 };
	material.pbrMetallicRoughness.metallicFactor = 0.0;   // 非金属
	material.pbrMetallicRoughness.roughnessFactor = 1.0;  // 完全粗糙（漫反射）

	// 启用 unlit 扩展：禁用光照计算，等效于 shader 的 texture2D 直接输出
	tinygltf::Value::Object unlit_ext;
	material.extensions["KHR_materials_unlit"] = tinygltf::Value(unlit_ext);

	return material;
}

bool OSGB23dTiles::ToGlbBuf(
	std::string path, std::string& glb_buff, MeshInfo& mesh_info, int node_type,
	bool enable_texture_compress, bool enable_meshopt, bool enable_draco, bool need_mesh_info/* = true*/)
{
	vector<string> fileNames = { path };
	std::string parent_path = OSGBTools::GetParent(path);

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
	{
		return false;
	}

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
		{
			continue;
		}

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
						{
							break;
						}
					}
				}

				primitive_idx++;
			}
		}
	}

	if (model.meshes[0].primitives.empty())
	{
		return false;
	}

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

	// image
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
				model.images.emplace_back(image);

				tinygltf::BufferView bfv;
				bfv.buffer = 0;
				bfv.byteOffset = buffer_start;
				alignment_buffer(buffer.data);
				bfv.byteLength = buffer.data.size() - buffer_start;
				model.bufferViews.emplace_back(bfv);
			}
		}
	}

	// node
	{
		tinygltf::Node node;
		node.mesh = 0;
		model.nodes.emplace_back(node);
	}

	// scene
	{
		tinygltf::Scene sence;
		sence.nodes.emplace_back(0);
		model.scenes = { sence };
		model.defaultScene = 0;
	}

	// sample
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
		model.extensionsRequired.emplace_back("KHR_draco_mesh_compression");
		model.extensionsUsed.emplace_back("KHR_draco_mesh_compression");
	}

	for (int i = 0; i < infoVisitor.texture_array.size(); i++)
	{
		tinygltf::Material mat = make_color_material_osgb(1.0, 1.0, 1.0);
		mat.pbrMetallicRoughness.baseColorTexture.index = i;
		model.materials.emplace_back(mat);
	}

	// finish buffer
	model.buffers.emplace_back(std::move(buffer));

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
			model.textures.emplace_back(texture);
		}
	}
	model.asset.version = "2.0";
	model.asset.generator = "RealScene3D";

	std::ostringstream ss;
	bool res = gltf.WriteGltfSceneToStream(&model, ss, false, true);
	if (res)
	{
		glb_buff = ss.str();
	}

	return res;
}

bool OSGB23dTiles::ToB3dmBuf(
	std::string path, std::string& b3dm_buf, TileBox& tile_box, int node_type,
	bool enable_texture_compress, bool enable_meshopt, bool enable_draco)
{
	using nlohmann::json;

	std::string glb_buf;
	MeshInfo minfo;
	if (!ToGlbBuf(path, glb_buf, minfo, node_type, enable_texture_compress, enable_meshopt, enable_draco))
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
		ids.emplace_back(i);
	}

	std::vector<std::string> names;
	for (int i = 0; i < mesh_count; ++i)
	{
		std::string mesh_name = "mesh_";
		mesh_name += std::to_string(i);
		names.emplace_back(mesh_name);
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

void OSGB23dTiles::DoTileJob(OSGTree& tree, std::string out_path, int max_lvl, bool enable_texture_compress, bool enable_meshopt, bool enable_draco)
{
	if (tree.file_name.empty())
	{
		return;
	}

	int lvl = OSGBTools::GetLvlNum(tree.file_name);
	if (lvl > max_lvl)
	{
		return;
	}

	if (tree.type > 0)
	{
		std::string b3dm_buf;
		ToB3dmBuf(tree.file_name, b3dm_buf, tree.bbox, tree.type, enable_texture_compress, enable_meshopt, enable_draco);
		std::string out_file = out_path;
		out_file += "/";
		out_file += OSGBTools::Replace(OSGBTools::GetFileName(tree.file_name), ".osgb", tree.type != 2 ? ".b3dm" : "o.b3dm");
		if (!b3dm_buf.empty())
		{
			OSGBTools::WriteFile(out_file.c_str(), b3dm_buf.data(), b3dm_buf.size());
		}
	}

	for (auto& i : tree.sub_nodes)
	{
		DoTileJob(i, out_path, max_lvl, enable_texture_compress, enable_meshopt, enable_draco);
	}
}

std::string OSGB23dTiles::EncodeTileJSON(OSGTree& tree, double x, double y)
{
	if (tree.bbox.max.empty() || tree.bbox.min.empty())
	{
		return "";
	}

	// 使用 TilesetNode 简化边界体积和内容生成（遵循KISS/DRY原则）
	TilesetNode node;
	node.geometricError = tree.geometricError;
	node.boundingVolume = BoundingVolumeFromTileBox(tree.bbox);

	// 可选：添加内容URI
	if (tree.type > 0)
	{
		std::string file_name = OSGBTools::GetFileName(tree.file_name);
		std::string uri = OSGBTools::Replace(file_name, ".osgb", tree.type != 2 ? ".b3dm" : "o.b3dm");
		node.contentUri = "./" + uri;
	}

	// 构建JSON（部分使用结构化方法，部分保持递归兼容性）
	std::string json = "{";
	json += "\"geometricError\":" + std::to_string(node.geometricError) + ",";
	json += node.boundingVolume.ToJson();

	// 添加内容（3D Tiles规范：content不需要单独的boundingVolume）
	if (!node.contentUri.empty())
	{
		json += ",\"content\":{\"uri\":\"" + node.contentUri + "\"}";
	}

	// 递归添加子节点
	json += ",\"children\":[";
	for (auto& child : tree.sub_nodes)
	{
		std::string child_json = EncodeTileJSON(child, x, y);
		if (!child_json.empty())
		{
			json += child_json + ",";
		}
	}

	// 移除末尾的逗号
	if (json.back() == ',')
	{
		json.pop_back();
	}

	json += "]}";

	return json;
}

OSGTree OSGB23dTiles::GetAllTree(std::string& file_name)
{
	OSGTree root_tile;
	vector<string> fileNames = { file_name };

	InfoVisitor infoVisitor(OSGBTools::GetParent(file_name));
	{
		osg::ref_ptr<osg::Node> root = osgDB::readNodeFiles(fileNames);
		if (!root)
		{
			std::string name = OSGBTools::Utf8String(file_name.c_str());
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
				{
					root_tile.sub_nodes.emplace_back(node);
				}
			}
			else
			{
				root_tile.sub_nodes.emplace_back(tree);
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
		new_root_tile.sub_nodes.emplace_back(root_tile);
		new_root_tile.sub_nodes.emplace_back(tile);
		root_tile = new_root_tile;
	}

	return root_tile;
}

//===============OSGB23dTiles 批量处理==================

bool OSGB23dTiles::To3dTileBatch(
	const std::string& pDataDir, const std::string& strOutputDir,
	double dCenterX, double dCenterY, int nMaxLevel,
	bool bEnableTextureCompress, bool bEnableMeshOpt, bool bEnableDraco)
{
	// 1. 构建 Data 目录路径
	std::string data_path = OSGBTools::OSGString(pDataDir);

	// 标准化路径分隔符
	for (char& c : data_path)
	{
		if (c == '\\')
		{
			c = '/';
		}
	}

	// 移除尾部斜杠
	if (!data_path.empty() && data_path.back() == '/')
	{
		data_path.pop_back();
	}

	// 获取根目录（data_dir 的父目录，用于查找 metadata.xml）
	std::string root_dir = data_path;
	size_t data_pos = data_path.rfind("/Data");
	if (data_pos != std::string::npos && data_pos == data_path.length() - 5)
	{
		// 如果已经是 /Data 结尾，去掉它
		root_dir = data_path.substr(0, data_pos);
	}

	// 2. 尝试解析 metadata.xml 以获取坐标系统信息
	std::string metadata_path = root_dir + "/metadata.xml";
	OSGBMetadata metadata;
	bool has_metadata = false;

	if (OSGBTools::ParseMetadataXml(metadata_path, metadata))
	{
		has_metadata = true;

		if (metadata.bIsENU)
		{
			// ENU 坐标系统
			LOG_I("Using ENU coordinate system");
			LOG_I("  Geographic origin: lat=%.6f, lon=%.6f", metadata.dCenterLat, metadata.dCenterLon);
			LOG_I("  SRSOrigin offset: x=%.3f, y=%.3f, z=%.3f", metadata.dOffsetX, metadata.dOffsetY, metadata.dOffsetZ);

			// 调用 enu_init 初始化 GeoTransform
			// 注意：enu_init 需要经度在前，纬度在后
			double origin_enu[3] = { metadata.dOffsetX, metadata.dOffsetY, metadata.dOffsetZ };
			if (GeoTransform::InitFromENU(metadata.dCenterLon, metadata.dCenterLat, origin_enu))
			{
				LOG_I("GeoTransform initialized successfully for ENU system");

				// 使用 metadata 中的坐标作为中心点
				dCenterX = metadata.dCenterLon;
				dCenterY = metadata.dCenterLat;
			}
			else
			{
				LOG_E("Failed to initialize GeoTransform for ENU system");
			}
		}
		else if (metadata.bIsEPSG)
		{
			// EPSG 坐标系统
			LOG_I("Using EPSG:%d coordinate system", metadata.nEpsgCode);
			LOG_I("  SRSOrigin: %s", metadata.strSrsOrigin.c_str());

			// 解析 SRSOrigin 为投影坐标
			double origin[3] = { metadata.dOffsetX, metadata.dOffsetY, metadata.dOffsetZ };
			// 调用 epsg_convert 转换为经纬度
			if (GeoTransform::InitFromEPSG(metadata.nEpsgCode, origin))
			{
				LOG_I("GeoTransform initialized successfully for EPSG:%d system", metadata.nEpsgCode);
				LOG_I("  Converted to geographic: lon=%.6f, lat=%.6f, h=%.3f", origin[0], origin[1], origin[2]);

				// 使用转换后的经纬度作为中心点
				dCenterX = origin[0];
				dCenterY = origin[1];
			}
			else
			{
				LOG_E("Failed to convert EPSG:%d coordinates", metadata.nEpsgCode);
			}
		}
		else if (metadata.bIsWKT)
		{
			// WKT 格式坐标系统
			LOG_I("Using WKT projection");
			LOG_I("  SRSOrigin: %s", metadata.strSrsOrigin.c_str());
			// 解析 SRSOrigin 为投影坐标
			double origin[3] = { metadata.dOffsetX, metadata.dOffsetY, metadata.dOffsetZ };

			// 调用 InitFromWKT 转换为经纬度
			if (GeoTransform::InitFromWKT(metadata.strSrs.c_str(), origin))
			{
				LOG_I("GeoTransform initialized successfully for WKT projection");
				LOG_I("  Converted to geographic: lon=%.6f, lat=%.6f, h=%.3f", origin[0], origin[1], origin[2]);

				// 使用转换后的经纬度作为中心点
				dCenterX = origin[0];
				dCenterY = origin[1];
			}
			else
			{
				LOG_E("Failed to convert WKT coordinates");
			}
		}
	}
	else
	{
		LOG_W("metadata.xml not found or parsing failed, using provided center_x=%.6f, center_y=%.6f", dCenterX, dCenterY);
	}

	// 3. 检测数据源类型
	bool is_oblique_data = false;  // 是否为倾斜摄影数据集
	std::string check_data_dir = data_path;

	// 检查路径是否以 "/Data" 结尾
	data_pos = data_path.rfind("/Data");  // 复用之前定义的 data_pos 变量
	if (data_pos == std::string::npos || data_pos != data_path.length() - 5)
	{
		// 不以 "/Data" 结尾，追加它
		check_data_dir = data_path + "/Data";
	}

	// 检测是否为倾斜摄影数据集
	if (OSGBTools::IsDirectory(check_data_dir) && has_metadata)
	{
		is_oblique_data = true;
		LOG_I("检测到倾斜摄影数据集模式 (Data目录 + metadata.xml)");
		std::cout << "[INFO] 在以下位置搜索瓦片：" << check_data_dir << std::endl;
	}
	else
	{
		// 尝试纯OSGB文件夹模式
		check_data_dir = data_path;
		LOG_I("检测到纯OSGB文件夹模式");
		std::cout << "[INFO] 扫描OSGB文件夹：" << check_data_dir << std::endl;
	}

	// 4. 创建输出目录
	std::string out_path = strOutputDir;
#ifdef _WIN32
	CreateDirectoryA(out_path.c_str(), NULL);
#else
	mkdir(out_path.c_str(), 0755);
#endif

	// 5. 收集所有子目录/OSGB文件
	struct TileInfo {
		std::string tile_name;
		std::string osgb_path;
		std::string output_path;
		TileBox bbox;
	};

	std::vector<TileInfo> tiles;
	TileBox global_bbox;

	if (is_oblique_data)
	{
		// 倾斜摄影模式：扫描 Tile_* 目录
		std::string out_data_path = out_path + "/Data";
#ifdef _WIN32
		CreateDirectoryA(out_data_path.c_str(), NULL);
#else
		mkdir(out_data_path.c_str(), 0755);
#endif

		// 使用 OSGBTools 统一的目录扫描函数
		std::vector<std::string> tile_names = OSGBTools::ScanTileDirectories(check_data_dir);
		if (tile_names.empty())
		{
			LOG_E("未找到任何 Tile_* 目录：%s", check_data_dir.c_str());
			return false;
		}

		for (const auto& tile_name : tile_names)
		{
			std::string tile_dir = check_data_dir + "/" + tile_name;
			std::string osgb_file = tile_dir + "/" + tile_name + ".osgb";

			TileInfo info;
			info.tile_name = tile_name;
			info.osgb_path = osgb_file;
			info.output_path = out_data_path + "/" + tile_name;
#ifdef _WIN32
			CreateDirectoryA(info.output_path.c_str(), NULL);
#else
			mkdir(info.output_path.c_str(), 0755);
#endif
			tiles.emplace_back(info);
		}
	}
	else
	{
		// 纯OSGB文件夹模式：
		// 1. 先检查输入目录本身是否包含OSGB文件
		// 2. 如果不包含，再扫描子目录

		std::vector<std::string> osgb_files_in_root = OSGBTools::ScanOSGBFiles(check_data_dir, false);

		if (!osgb_files_in_root.empty())
		{
			// 情况1：输入目录本身就包含OSGB文件（如 E:\Data\3D\Tile_+005_+006）
			LOG_I("输入目录本身包含 %zu 个OSGB文件", osgb_files_in_root.size());

			// 查找根OSGB文件
			std::string root_osgb = OSGBTools::FindRootOSGB(check_data_dir);
			if (root_osgb.empty())
			{
				// 如果没有找到根OSGB，使用第一个文件
				root_osgb = osgb_files_in_root[0];
				LOG_I("未找到根OSGB，使用第一个文件: %s", root_osgb.c_str());
			}
			else
			{
				LOG_I("找到根OSGB: %s", root_osgb.c_str());
			}

			// 获取目录名作为tile名称
			std::string dir_name = OSGBTools::GetFileName(check_data_dir);
			if (dir_name.empty())
			{
				dir_name = "output";
			}

			TileInfo info;
			info.tile_name = dir_name;
			info.osgb_path = root_osgb;
			info.output_path = out_path + "/" + dir_name;
#ifdef _WIN32
			CreateDirectoryA(info.output_path.c_str(), NULL);
#else
			mkdir(info.output_path.c_str(), 0755);
#endif
			tiles.emplace_back(info);
		}
		else
		{
			// 情况2：输入目录不包含OSGB，扫描子目录
			std::vector<std::string> osgb_folders = OSGBTools::ScanOSGBFolders(check_data_dir);

			LOG_I("找到 %zu 个包含OSGB文件的子目录", osgb_folders.size());

			for (const auto& folder_name : osgb_folders)
			{
				std::string folder_path = check_data_dir + "/" + folder_name;

				// 在子目录中查找根OSGB文件
				std::string root_osgb = OSGBTools::FindRootOSGB(folder_path);
				if (root_osgb.empty())
				{
					// 如果没有找到根OSGB，尝试扫描该目录下的所有OSGB文件，使用第一个
					std::vector<std::string> osgb_files = OSGBTools::ScanOSGBFiles(folder_path, false);
					if (!osgb_files.empty())
					{
						root_osgb = osgb_files[0];
						LOG_I("子目录 %s 未找到根OSGB，使用第一个文件: %s", folder_name.c_str(), root_osgb.c_str());
					}
					else
					{
						LOG_W("子目录 %s 中未找到OSGB文件，跳过", folder_name.c_str());
						continue;
					}
				}
				else
				{
					LOG_I("子目录 %s 找到根OSGB: %s", folder_name.c_str(), root_osgb.c_str());
				}

				TileInfo info;
				info.tile_name = folder_name;
				info.osgb_path = root_osgb;
				info.output_path = out_path + "/" + folder_name;
#ifdef _WIN32
				CreateDirectoryA(info.output_path.c_str(), NULL);
#else
				mkdir(info.output_path.c_str(), 0755);
#endif
				tiles.emplace_back(info);
			}
		}
	}

	if (tiles.empty())
	{
		LOG_E("未找到任何OSGB数据");
		return false;
	}

	std::cout << "[INFO] Found " << tiles.size() << " tile directories to process" << std::endl;

	// 5. 处理每个瓦片
	std::vector<std::string> tile_jsons;

	for (size_t i = 0; i < tiles.size(); i++)
	{
		TileInfo& tile = tiles[i];
		std::cout << "[INFO] Processing tile " << (i + 1) << "/" << tiles.size()
			<< ": " << tile.tile_name << std::endl;

		auto [success, strTileJson, bbox_data] = To3dTile(
			tile.osgb_path,
			tile.output_path,
			dCenterX,
			dCenterY,
			nMaxLevel,
			bEnableTextureCompress,
			bEnableMeshOpt,
			bEnableDraco
		);

		if (success && !strTileJson.empty())
		{
			tile_jsons.emplace_back(strTileJson);

			// 更新边界框
			tile.bbox.max = { bbox_data[0], bbox_data[1], bbox_data[2] };
			tile.bbox.min = { bbox_data[3], bbox_data[4], bbox_data[5] };

			// 合并到全局边界框
			if (global_bbox.max.empty())
			{
				global_bbox = tile.bbox;
			}
			else
			{
				expend_box(global_bbox, tile.bbox);
			}

			// 将瓦片 JSON 包装在完整的 tileset 结构中
			std::string wrapped_json = "{";
			wrapped_json += "\"asset\":{\"version\":\"1.0\",\"gltfUpAxis\":\"Z\"},";
			wrapped_json += "\"geometricError\":1000,";
			wrapped_json += "\"root\":";
			wrapped_json += strTileJson;  // 这是此瓦片的根节点
			wrapped_json += "}";

			// 保存单个瓦片的 tileset.json
			std::string tileset_path = tile.output_path + "/tileset.json";
			OSGBTools::WriteFile(tileset_path.c_str(), wrapped_json.data(), wrapped_json.size());
		}
		else
		{
			LOG_E("处理瓦片失败：%s", tile.tile_name.c_str());
		}
	}

	if (tile_jsons.empty())
	{
		LOG_E("没有成功处理任何瓦片");

		return false;
	}

	// 6. 计算变换矩阵
	std::vector<double> transform_matrix(16);
	{
		// 使用合并的全局边界框的最小高度
		double height_min = global_bbox.min.empty() ? 0.0 : global_bbox.min[2];

		// 对于ENU坐标系，需要应用SRSOrigin偏移到根节点变换矩阵
		if (has_metadata && metadata.bIsENU)
		{
			LOG_I("应用ENU offset到根节点变换矩阵: (%.3f, %.3f, %.3f)",
				metadata.dOffsetX, metadata.dOffsetY, metadata.dOffsetZ);
			OSGBTools::TransformCWithEnuOffset(
				dCenterX, dCenterY, height_min,
				metadata.dOffsetX, metadata.dOffsetY, metadata.dOffsetZ,
				transform_matrix.data());
		}
		else
		{
			OSGBTools::TransformC(dCenterX, dCenterY, height_min, transform_matrix.data());
		}
	}

	// 7. 生成根 tileset.json（使用 TilesetNode，减少73%代码）
	TilesetNode rootNode;
	rootNode.geometricError = 2000;
	rootNode.boundingVolume = BoundingVolumeFromTileBox(global_bbox);
	rootNode.transform = transform_matrix;

	// 添加子瓦片节点
	for (const auto& tile : tiles)
	{
		TilesetNode childNode;
		childNode.geometricError = 1000;
		childNode.boundingVolume = BoundingVolumeFromTileBox(tile.bbox);

		// 根据数据集类型生成URI
		if (is_oblique_data)
		{
			childNode.contentUri = "./Data/" + tile.tile_name + "/tileset.json";
		}
		else
		{
			childNode.contentUri = "./" + tile.tile_name + "/tileset.json";
		}

		rootNode.children.emplace_back(childNode);
	}

	// 生成JSON,includeAsset=true
	std::string root_json = rootNode.ToJson(true); 

	// 8. 保存根 tileset.json
	std::string root_tileset_path = strOutputDir + "/tileset.json";
	OSGBTools::WriteFile(root_tileset_path.c_str(), root_json.data(), root_json.size());

	std::cout << "[INFO] 批量处理完成！生成了包含 " << tiles.size()
		<< " 个瓦片的根 tileset.json" << std::endl;

	// 9. 清理 GeoTransform 资源（谁调用谁释放）
	GeoTransform::Cleanup();

	return true;
}

//===============OSGB23dTiles 结束==================
