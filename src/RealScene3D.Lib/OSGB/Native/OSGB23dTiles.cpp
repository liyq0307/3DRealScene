// ============================================================================
// OSGB23dTiles 实现
// 将 OSGB (OpenSceneGraph Binary) 文件转换为 GLB 和 3D Tiles 格式
// ============================================================================

#include <cstdint>
#include <limits>

#include <Eigen/Eigen>

#ifdef ENABLE_PROJ
#include <proj.h>  // PROJ coordinate transformation
#endif

// Basis Universal for KTX2 texture compression
#ifdef ENABLE_KTX2
#include <basisu/encoder/basisu_comp.h>
#include <basisu/transcoder/basisu_transcoder.h>
#endif

// OpenMP 用于并行处理
#ifdef _OPENMP
#include <omp.h>
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

using namespace OSGBLog;

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
		osg::ref_ptr<osg::Vec3Array> vertexArr = (osg::Vec3Array*)geometry.getVertexArray();
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

	if (auto state = geometry.getStateSet())
	{
		osg::ref_ptr<osg::Material> material = dynamic_cast<osg::Material*>(state->getAttribute(osg::StateAttribute::MATERIAL));
		if (material)
		{
			// 存储材质供后续使用                                                                                                                                                    
			material_set.insert(material);

			// 建立Geometry到Material的映射                                                                                                                                          
			material_map[&geometry] = material;
		}

		osg::ref_ptr<osg::Texture> texture = dynamic_cast<osg::Texture*>(state->getTextureAttribute(0, osg::StateAttribute::TEXTURE));
		if (texture)
		{
			if (is_pagedlod)
			{
				texture_array.insert(texture);
			}
			else
			{
				other_texture_array.insert(texture);
			}

			texture_map[&geometry] = texture;
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
void PutVal(std::vector<unsigned char>& buf, T val)
{
	buf.insert(buf.end(), (unsigned char*)&val, (unsigned char*)&val + sizeof(T));
}

template<class T>
void PutVal(std::string& buf, T val)
{
	buf.append((unsigned char*)&val, (unsigned char*)&val + sizeof(T));
}

void WriteBuf(void* context, void* data, int len)
{
	std::vector<char>* buf = (std::vector<char>*)context;
	buf->insert(buf->end(), (char*)data, (char*)data + len);
}

template<class T>
void AlignmentBuffer(std::vector<T>& buf)
{
	while (buf.size() % 4 != 0)
	{
		buf.emplace_back(0x00);
	}
}

void ExpandBbox3d(osg::Vec3f& point_max, osg::Vec3f& point_min, osg::Vec3f point)
{
	point_max.x() = std::max(point.x(), point_max.x());
	point_min.x() = std::min(point.x(), point_min.x());
	point_max.y() = std::max(point.y(), point_max.y());
	point_min.y() = std::min(point.y(), point_min.y());
	point_max.z() = std::max(point.z(), point_max.z());
	point_min.z() = std::min(point.z(), point_min.z());
}

void ExpandBbox2d(osg::Vec2f& point_max, osg::Vec2f& point_min, osg::Vec2f point)
{
	point_max.x() = std::max(point.x(), point_max.x());
	point_min.x() = std::min(point.x(), point_min.x());
	point_max.y() = std::max(point.y(), point_max.y());
	point_min.y() = std::min(point.y(), point_min.y());
}

void ExpandBox(TileBox& box, TileBox& box_new)
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

TileBox ExtendTileBox(OSGTree& tree)
{
	TileBox box = tree.bbox;
	for (auto& i : tree.sub_nodes)
	{
		TileBox sub_tile = ExtendTileBox(i);
		ExpandBox(box, sub_tile);
	}

	tree.bbox = box;

	return box;
}

void CalcGeometricError(OSGTree& tree)
{
	const double EPS = 1e-12;

	// 深度优先
	for (auto& i : tree.sub_nodes)
	{
		CalcGeometricError(i);
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

		auto GetGeometricError = [](TileBox& bbox)
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
			tree.geometricError = GetGeometricError(tree.bbox);
		}
		else
		{
			tree.geometricError = leaf.geometricError * 2.0;
		}
	}
}

/**
 * @brief 创建一个默认颜色的材质
 * @note 该材质使用 KHR_materials_unlit 扩展，适用于不需要光照计算的场景
 */
tinygltf::Material MakeDefaultColorMaterial(double r, double g, double b)
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

/**
 * @brief 将 OSG 材质转换为 PBR 材质
 * @note 针对已烘焙光照的OSGB纹理进行优化，大幅提升环境光贡献，确保显示亮度接近原始效果
 * @note 该方法适用于大多数常见材质类型，但可能无法完美保留所有复杂材质的外观
 */
tinygltf::Material ConvertOSGBMaterialToPBR(osg::Material* osgMaterial)
{
	tinygltf::Material gltfMaterial;
	gltfMaterial.name = "converted_pbr";

	// 1. 提取OSGB材质属性
	osg::Vec4 ambient = osgMaterial->getAmbient(osg::Material::FRONT_AND_BACK);
	osg::Vec4 diffuse = osgMaterial->getDiffuse(osg::Material::FRONT_AND_BACK);
	osg::Vec4 specular = osgMaterial->getSpecular(osg::Material::FRONT_AND_BACK);
	osg::Vec4 emission = osgMaterial->getEmission(osg::Material::FRONT_AND_BACK);
	float shininess = osgMaterial->getShininess(osg::Material::FRONT_AND_BACK);

	// 2. BaseColor转换：针对烘焙纹理，使用白色baseColor
	// OSGB模型的纹理已包含完整光照信息，材质颜色应为白色(1,1,1)以避免调制纹理
	// 使用unlit扩展时，baseColorFactor会直接影响纹理显示亮度
	gltfMaterial.pbrMetallicRoughness.baseColorFactor =
	{
		1.0, 1.0, 1.0,  // 固定白色，100%显示纹理原色
		diffuse[3]      // 保留alpha通道
	};

	// 注：原始diffuse和ambient信息已在纹理烘焙中体现，无需再次应用
	// 3. Roughness转换：从Shininess推导粗糙度
	// 对于已烘焙纹理，使用更高的roughness以减少额外的高光反射
	// Shininess范围通常是0-128，值越大越光滑
	// Roughness范围是0-1，值越大越粗糙
	float roughness = 1.0f - std::sqrt(shininess / 128.0f);
	roughness = std::clamp(roughness, 0.0f, 1.0f);

	// 针对烘焙纹理：提升roughness基准值，降低高光反射
	roughness = std::max(roughness, 0.6f);  // 最小粗糙度0.6（更接近漫反射）
	gltfMaterial.pbrMetallicRoughness.roughnessFactor = roughness;

	// 4. Metallic转换：根据Specular强度推导金属度（保守策略）
	// 对于OSGB实景数据，大部分是非金属材质
	float specularLuminance = (specular[0] + specular[1] + specular[2]) / 3.0f;

	// 更保守的金属度判断：提高阈值，降低最大金属度
	float metallic = 0.0f;
	if (specularLuminance > 0.7f)  // 阈值从0.5提升到0.7
	{
		// 检查Specular是否接近白色（金属特征）
		float colorVariance = std::abs(specular[0] - specular[1]) +
			std::abs(specular[1] - specular[2]) +
			std::abs(specular[0] - specular[2]);
		if (colorVariance < 0.15f)  // 从0.1放宽到0.15
		{
			// 限制最大金属度为0.3（实景数据很少有纯金属）
			metallic = std::min(specularLuminance * 0.5f, 0.3f);
		}
	}
	gltfMaterial.pbrMetallicRoughness.metallicFactor = metallic;

	// 5. Emissive转换：直接映射
	gltfMaterial.emissiveFactor =
	{
		emission[0], emission[1], emission[2]
	};

	// 6. 添加unlit扩展：禁用实时光照计算
	// OSGB模型的纹理已包含烘焙光照，使用unlit可以忠实还原原始效果
	tinygltf::Value::Object unlit_ext;
	gltfMaterial.extensions["KHR_materials_unlit"] = tinygltf::Value(unlit_ext);

	return gltfMaterial;
}

/**
 * @brief 将 OSG 材质转换为 PBR 材质，使用 KHR_materials_specular 扩展保留 Specular 信息
 * @note 针对已烘焙光照的OSGB纹理优化，添加环境光处理，改进金属度计算
 * @note 该方法允许更精确地保留原始材质的外观，适用于需要高保真转换的场景
 */
tinygltf::Material ConvertOSGBMaterialWithSpecularExt(osg::Material* osgMaterial)
{
	tinygltf::Material gltfMaterial;
	gltfMaterial.name = "converted_pbr_specular";

	// 1. 提取OSGB材质属性
	osg::Vec4 ambient = osgMaterial->getAmbient(osg::Material::FRONT_AND_BACK);
	osg::Vec4 diffuse = osgMaterial->getDiffuse(osg::Material::FRONT_AND_BACK);
	osg::Vec4 specular = osgMaterial->getSpecular(osg::Material::FRONT_AND_BACK);
	osg::Vec4 emission = osgMaterial->getEmission(osg::Material::FRONT_AND_BACK);
	float shininess = osgMaterial->getShininess(osg::Material::FRONT_AND_BACK);

	// 2. BaseColor转换：针对烘焙纹理，使用白色baseColor
	// OSGB模型的纹理已包含完整光照信息，材质颜色应为白色(1,1,1)以避免调制纹理
	// 使用unlit扩展时，baseColorFactor会直接影响纹理显示亮度
	gltfMaterial.pbrMetallicRoughness.baseColorFactor =
	{
		1.0, 1.0, 1.0,  // 固定白色，100%显示纹理原色
		diffuse[3]      // 保留alpha通道
	};

	// 注：原始diffuse和ambient信息已在纹理烘焙中体现，无需再次应用
	// 3. Roughness转换：从Shininess推导粗糙度
	// 对于已烘焙纹理，提升roughness基准值
	float roughness = 1.0f - std::sqrt(shininess / 128.0f);
	roughness = std::clamp(roughness, 0.0f, 1.0f);
	roughness = std::max(roughness, 0.6f);  // 最小粗糙度0.6
	gltfMaterial.pbrMetallicRoughness.roughnessFactor = roughness;

	// 4. Metallic转换：根据Specular强度动态计算（改进）
	// 原实现固定为0.0，现在根据specular强度动态判断
	float specularLuminance = (specular[0] + specular[1] + specular[2]) / 3.0f;
	float metallic = 0.0f;

	// 保守的金属度判断
	if (specularLuminance > 0.7f)
	{
		float colorVariance = std::abs(specular[0] - specular[1]) +
			std::abs(specular[1] - specular[2]) +
			std::abs(specular[0] - specular[2]);
		if (colorVariance < 0.15f)
		{
			// 因为使用了specular扩展，metallic可以更低
			metallic = std::min(specularLuminance * 0.3f, 0.2f);
		}
	}
	gltfMaterial.pbrMetallicRoughness.metallicFactor = metallic;

	// 5. 使用KHR_materials_specular扩展保留Specular
	tinygltf::Value::Object specularExt;

	// specularFactor: Specular强度
	float specularStrength = specularLuminance;
	specularExt["specularFactor"] = tinygltf::Value(specularStrength);

	// specularColorFactor: Specular颜色
	tinygltf::Value::Array specularColor =
	{
		tinygltf::Value(specular[0]),
		tinygltf::Value(specular[1]),
		tinygltf::Value(specular[2])
	};
	specularExt["specularColorFactor"] = tinygltf::Value(specularColor);

	gltfMaterial.extensions["KHR_materials_specular"] = tinygltf::Value(specularExt);

	// 6. Emissive转换：直接映射
	gltfMaterial.emissiveFactor = { emission[0], emission[1], emission[2] };

	// 7. 添加unlit扩展：禁用实时光照计算
	// OSGB模型的纹理已包含烘焙光照，使用unlit可以忠实还原原始效果
	tinygltf::Value::Object unlit_ext;
	gltfMaterial.extensions["KHR_materials_unlit"] = tinygltf::Value(unlit_ext);

	return gltfMaterial;
}
//===============工具结束==================

//===============OSGB23dTiles==================
B3DMResult OSGB23dTiles::ToB3DM(
	const std::string strInPath,
	const std::string& strOutPath,
	double dCenterX,
	double dCenterY,
	int nMaxLevel,
	bool bEnableTextureCompress,
	bool bEnableMeshOpt,
	bool bEnableDraco)
{
	B3DMResult result;
	result.success = false;

	std::string path = OSGBTools::OSGString(strInPath);

	// 自动检测目录并查找根 OSGB 文件
	if (OSGBTools::IsDirectory(path))
	{
		OSGBLog::LOG_I("[INFO] 输入是目录，正在搜索根 OSGB 文件...");
		std::string root_osgb = OSGBTools::FindRootOSGB(path);
		if (root_osgb.empty())
		{
			LOG_E("在目录 [{}] 中未找到根 OSGB 文件！", strInPath.c_str());
			return result;
		}
		OSGBLog::LOG_I("[INFO] 找到根 OSGB：{}", root_osgb);
		path = root_osgb;
	}

	OSGTree root = GetAllTree(path);
	if (root.file_name.empty())
	{
		LOG_E("打开文件 [{}] 失败！", strInPath.c_str());
		return result;
	}

	DoTileJob(root, strOutPath, nMaxLevel,
		bEnableTextureCompress, bEnableMeshOpt, bEnableDraco);

	ExtendTileBox(root);

	if (root.bbox.max.empty() || root.bbox.min.empty())
	{
		LOG_E("[{}] bbox 为空！", strInPath.c_str());
		return result;
	}

	CalcGeometricError(root);

	root.geometricError = 1000.0;
	std::string strJson = EncodeTileJSON(root, dCenterX, dCenterY);
	root.bbox.extend(0.2);

	// 构建返回结果
	result.success = true;
	result.tilesetJson = strJson;
	std::copy(root.bbox.max.begin(), root.bbox.max.end(), result.boundingBox.begin());
	std::copy(root.bbox.min.begin(), root.bbox.min.end(), result.boundingBox.begin() + 3);

	return result;
}

bool OSGB23dTiles::ToGLB(
	const std::string& strInPath,
	const std::string& strOutPath,
	bool bBinary/* = true*/,
	bool bEnableTextureCompress/* = false*/,
	bool bEnableMeshOpt/* = false*/,
	bool bEnableDraco/* = false*/)
{
	MeshInfo minfo;
	std::string glb_buf = "";
	std::string path = OSGBTools::OSGString(strInPath);

	// 自动检测目录并查找根 OSGB 文件
	if (OSGBTools::IsDirectory(path))
	{
		OSGBLog::LOG_I("[INFO] 输入是目录，正在搜索根 OSGB 文件...");
		std::string root_osgb = OSGBTools::FindRootOSGB(path);
		if (root_osgb.empty())
		{
			LOG_E("在目录 [{}] 中未找到根 OSGB 文件！", strInPath.c_str());
			return false;
		}
		OSGBLog::LOG_I("[INFO] 找到根 OSGB：{}", root_osgb);
		path = root_osgb;
	}

	bool ret = ToGLBBuf(path, glb_buf, minfo, -1, bBinary, bEnableTextureCompress, bEnableMeshOpt, bEnableDraco);
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

std::vector<uint8_t> OSGB23dTiles::ToGLBBuf(
	std::string strOsgbPath,
	int nNodeType,
	bool bBinary,
	bool bEnableTextureCompress,
	bool bEnableMeshOpt,
	bool bEnableDraco)
{
	std::string glb_buff;

	bool ret = ToGLBBuf(OSGBTools::OSGString(strOsgbPath), glb_buff, MeshInfo(),
		nNodeType, bBinary, bEnableTextureCompress, bEnableMeshOpt, bEnableDraco, false);

	if (!ret)
	{
		return std::vector<uint8_t>();
	}

	// 将 string 转换为 vector<uint8_t>
	std::vector<uint8_t> result(glb_buff.begin(), glb_buff.end());
	return result;
}

template<class T>
void OSGB23dTiles::WriteOsgIndecis(T* drawElements, OsgBuildState* osgState, int componentType)
{
	unsigned max_index = 0;
	unsigned min_index = 1 << 30;
	unsigned buffer_start = osgState->buffer->data.size();

	unsigned IndNum = drawElements->getNumIndices();
	for (unsigned m = 0; m < IndNum; m++)
	{
		auto idx = drawElements->at(m);
		PutVal(osgState->buffer->data, idx);
		if (idx > max_index) max_index = idx;
		if (idx < min_index) min_index = idx;
	}
	AlignmentBuffer(osgState->buffer->data);

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

void OSGB23dTiles::WriteVec3Array(osg::Vec3Array* v3f, OsgBuildState* osgState, osg::Vec3f& point_max, osg::Vec3f& point_min)
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
		PutVal(osgState->buffer->data, point.x());
		PutVal(osgState->buffer->data, point.y());
		PutVal(osgState->buffer->data, point.z());
		ExpandBbox3d(point_max, point_min, point);
	}
	AlignmentBuffer(osgState->buffer->data);

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

void OSGB23dTiles::WriteVec2Array(osg::Vec2Array* v2f, OsgBuildState* osgState)
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
		PutVal(osgState->buffer->data, point.x());
		PutVal(osgState->buffer->data, point.y());
		ExpandBbox2d(point_max, point_min, point);
	}
	AlignmentBuffer(osgState->buffer->data);

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

int OSGB23dTiles::WriteIndexVector(const std::vector<uint32_t>& indices, OsgBuildState* osgState, DracoState* dracoState)
{
	if (indices.empty())
	{
		return -1;
	}

	uint32_t max_index = 0;
	uint32_t min_index = std::numeric_limits<uint32_t>::max();
	for (auto idx : indices)
	{
		max_index = std::max(max_index, idx);
		min_index = std::min(min_index, idx);
	}

	// 选择可以容纳给定最大索引的最小 glTF 组件类型。
	// 根据 max_index 返回 UNSIGNED_BYTE、UNSIGNED_SHORT 或 UNSIGNED_INT。
	auto PickIndexComponentType = [](uint32_t max_index)
	{
		if (max_index <= std::numeric_limits<uint8_t>::max())
		{
			return TINYGLTF_COMPONENT_TYPE_UNSIGNED_BYTE;
		}

		if (max_index <= std::numeric_limits<uint16_t>::max())
		{
			return TINYGLTF_COMPONENT_TYPE_UNSIGNED_SHORT;
		}

		return TINYGLTF_COMPONENT_TYPE_UNSIGNED_INT;
	};

	const int componentType = PickIndexComponentType(max_index);
	if (dracoState && dracoState->compressed)
	{
		tinygltf::Accessor acc;
		acc.bufferView = -1;
		acc.type = TINYGLTF_TYPE_SCALAR;
		acc.componentType = componentType;
		acc.count = indices.size();
		acc.maxValues = { (double)max_index };
		acc.minValues = { (double)min_index };
		int accIdx = (int)osgState->model->accessors.size();
		osgState->model->accessors.emplace_back(acc);

		return accIdx;
	}

	unsigned buffer_start = osgState->buffer->data.size();
	switch (componentType)
	{
		case TINYGLTF_COMPONENT_TYPE_UNSIGNED_BYTE:
			for (auto idx : indices)
			{
				PutVal(osgState->buffer->data, static_cast<uint8_t>(idx));
			}
			break;
		case TINYGLTF_COMPONENT_TYPE_UNSIGNED_SHORT:
			for (auto idx : indices)
			{
				PutVal(osgState->buffer->data, static_cast<uint16_t>(idx));
			}
			break;
		default:
			for (auto idx : indices)
			{
				PutVal(osgState->buffer->data, static_cast<uint32_t>(idx));
			}
			break;
	}

	AlignmentBuffer(osgState->buffer->data);

	tinygltf::Accessor acc;
	acc.bufferView = osgState->model->bufferViews.size();
	acc.type = TINYGLTF_TYPE_SCALAR;
	acc.componentType = componentType;
	acc.count = indices.size();
	acc.maxValues = { (double)max_index };
	acc.minValues = { (double)min_index };
	int accIdx = (int)osgState->model->accessors.size();
	osgState->model->accessors.emplace_back(acc);

	tinygltf::BufferView bfv;
	bfv.buffer = 0;
	bfv.target = TINYGLTF_TARGET_ELEMENT_ARRAY_BUFFER;
	bfv.byteOffset = buffer_start;
	bfv.byteLength = osgState->buffer->data.size() - buffer_start;
	osgState->model->bufferViews.emplace_back(bfv);

	return accIdx;
}

// 将 GL_QUADS 或 GL_QUAD_STRIP 索引序列转换为三角形索引。
// GL_QUADS: 每4个索引形成一个四边形，分割成2个三角形。
// GL_QUAD_STRIP: 索引对形成条带顺序的四边形，每个四边形分割成2个三角形。
// 如果三角化成功并填充 'out'，返回 true，否则返回 false。
bool TriangulateQuadLike(const std::vector<uint32_t>& indices, GLenum mode, std::vector<uint32_t>& out)
{
	out.clear();

	if (mode == GL_QUADS)
	{
		if (indices.size() < 4)
		{
			return false;
		}

		if (indices.size() % 4 != 0)
		{
			LOG_E("GL_QUADS index count (%zu) is not divisible by 4, trailing vertices will be ignored", indices.size());
		}

		size_t quad_count = indices.size() / 4;
		out.reserve(quad_count * 6);
		for (size_t q = 0; q < quad_count; ++q)
		{
			size_t base = q * 4;
			out.emplace_back(indices[base]);
			out.emplace_back(indices[base + 1]);
			out.emplace_back(indices[base + 2]);
			out.emplace_back(indices[base]);
			out.emplace_back(indices[base + 2]);
			out.emplace_back(indices[base + 3]);
		}

		return !out.empty();
	}

	if (mode == GL_QUAD_STRIP)
	{
		if (indices.size() < 4)
		{
			return false;
		}

		if (indices.size() % 2 != 0)
		{
			LOG_E("GL_QUAD_STRIP index count (%zu) is not even, trailing vertex will be ignored",
				indices.size());
		}

		size_t pair_count = indices.size() / 2;
		if (pair_count < 2)
		{
			return false;
		}
		out.reserve((pair_count - 1) * 6);
		for (size_t i = 0; i + 1 < pair_count; ++i)
		{
			size_t base = i * 2;
			if (base + 3 >= indices.size())
			{
				break;
			}
			uint32_t a = indices[base];
			uint32_t b = indices[base + 1];
			uint32_t c = indices[base + 2];
			uint32_t d = indices[base + 3];
			out.emplace_back(a);
			out.emplace_back(b);
			out.emplace_back(c);
			out.emplace_back(b);
			out.emplace_back(d);
			out.emplace_back(c);
		}

		return !out.empty();
	}

	return false;
}

void OSGB23dTiles::WriteElementArrayPrimitive(osg::Geometry* g, osg::PrimitiveSet* ps, OsgBuildState* osgState, PrimitiveState* pmtState, DracoState* dracoState)
{
	tinygltf::Primitive primits;
	primits.indices = osgState->model->accessors.size();
	// reset draw_array state
	osgState->draw_array_first = -1;
	const GLenum gl_mode = ps->getMode();
	const bool needs_quad_triangulation = (gl_mode == GL_QUADS || gl_mode == GL_QUAD_STRIP);
	std::vector<uint32_t> triangulated_indices;

	auto collect_and_triangulate = [&](auto* drawElements)
	{
		std::vector<uint32_t> source;
		source.reserve(drawElements->getNumIndices());
		for (unsigned m = 0; m < drawElements->getNumIndices(); ++m)
		{
			source.emplace_back(drawElements->at(m));
		}

		return TriangulateQuadLike(source, gl_mode, triangulated_indices);
	};

	auto write_triangulated = [&](auto* drawElements, int componentType)
	{
		if (!needs_quad_triangulation)
		{
			if (dracoState && dracoState->compressed)
			{
				tinygltf::Accessor acc;
				acc.bufferView = -1;
				acc.type = TINYGLTF_TYPE_SCALAR;
				acc.componentType = componentType;
				acc.count = drawElements->getNumIndices();
				int accIdx = (int)osgState->model->accessors.size();
				osgState->model->accessors.emplace_back(acc);
				primits.indices = accIdx;
			}
			else
			{
				WriteOsgIndecis(drawElements, osgState, componentType);
			}

			return;
		}

		if (collect_and_triangulate(drawElements))
		{
			primits.indices = WriteIndexVector(triangulated_indices, osgState, dracoState);
		}
		else
		{
			primits.indices = -1;
		}
	};

	osg::PrimitiveSet::Type t = ps->getType();
	switch (t)
	{
		case (osg::PrimitiveSet::DrawElementsUBytePrimitiveType):
		{
			const osg::DrawElementsUByte* drawElements = static_cast<const osg::DrawElementsUByte*>(ps);
			write_triangulated(drawElements, TINYGLTF_COMPONENT_TYPE_UNSIGNED_BYTE);
			break;
		}
		case (osg::PrimitiveSet::DrawElementsUShortPrimitiveType):
		{
			const osg::DrawElementsUShort* drawElements = static_cast<const osg::DrawElementsUShort*>(ps);
			write_triangulated(drawElements, TINYGLTF_COMPONENT_TYPE_UNSIGNED_SHORT);
			break;
		}
		case (osg::PrimitiveSet::DrawElementsUIntPrimitiveType):
		{
			const osg::DrawElementsUInt* drawElements = static_cast<const osg::DrawElementsUInt*>(ps);
			write_triangulated(drawElements, TINYGLTF_COMPONENT_TYPE_UNSIGNED_INT);
			break;
		}
		case osg::PrimitiveSet::DrawArraysPrimitiveType:
		{
			primits.indices = -1;
			osg::DrawArrays* da = dynamic_cast<osg::DrawArrays*>(ps);
			osgState->draw_array_first = da->getFirst();
			osgState->draw_array_count = da->getCount();
			if (needs_quad_triangulation && da->getCount() > 0)
			{
				std::vector<uint32_t> source;
				source.reserve(da->getCount());
				for (int i = 0; i < da->getCount(); ++i)
				{
					source.emplace_back(i);
				}

				if (TriangulateQuadLike(source, gl_mode, triangulated_indices))
				{
					primits.indices = WriteIndexVector(triangulated_indices, osgState, dracoState);
				}
			}
			break;
		}
		default:
		{
			LOG_E("unsupport osg::PrimitiveSet::Type [{}]", static_cast<int>(t));
			exit(1);
			break;
		}
	}

	// 顶点：完整顶点和部分索引
	if (pmtState->vertexAccessor > -1 && osgState->draw_array_first == -1)
	{
		primits.attributes["POSITION"] = pmtState->vertexAccessor;
	}
	else
	{
		osg::Vec3Array* vertexArr = (osg::Vec3Array*)g->getVertexArray();
		if (dracoState && dracoState->compressed)
		{
			// 创建一个占位符访问器（无bufferView），并设置正确的计数/类型
			tinygltf::Accessor acc;
			acc.bufferView = -1;
			acc.componentType = TINYGLTF_COMPONENT_TYPE_FLOAT;
			acc.count = (osgState->draw_array_first >= 0) ? osgState->draw_array_count : (int)vertexArr->size();
			acc.type = TINYGLTF_TYPE_VEC3;

			// 计算最大、最小值包围盒
			osg::Vec3f point_max(-1e38, -1e38, -1e38);
			osg::Vec3f point_min(1e38, 1e38, 1e38);
			int vec_start = (osgState->draw_array_first >= 0) ? osgState->draw_array_first : 0;
			int vec_end = (osgState->draw_array_first >= 0) ? (osgState->draw_array_count + vec_start) : (int)vertexArr->size();
			for (int vidx = vec_start; vidx < vec_end; vidx++)
			{
				osg::Vec3f point = vertexArr->at(vidx);
				ExpandBbox3d(point_max, point_min, point);
			}
			acc.minValues = { point_min.x(), point_min.y(), point_min.z() };
			acc.maxValues = { point_max.x(), point_max.y(), point_max.z() };
			int accIdx = (int)osgState->model->accessors.size();
			osgState->model->accessors.emplace_back(acc);
			primits.attributes["POSITION"] = accIdx;
			if (pmtState->vertexAccessor == -1 && osgState->draw_array_first == -1)
			{
				pmtState->vertexAccessor = accIdx;
			}

			if (point_min.x() <= point_max.x() && point_min.y() <= point_max.y() && point_min.z() <= point_max.z())
			{
				ExpandBbox3d(osgState->point_max, osgState->point_min, point_max);
				ExpandBbox3d(osgState->point_max, osgState->point_min, point_min);
			}
		}
		else
		{
			osg::Vec3f point_max(-1e38, -1e38, -1e38);
			osg::Vec3f point_min(1e38, 1e38, 1e38);
			primits.attributes["POSITION"] = osgState->model->accessors.size();
			if (pmtState->vertexAccessor == -1 && osgState->draw_array_first == -1)
			{
				pmtState->vertexAccessor = osgState->model->accessors.size();
			}
			WriteVec3Array(vertexArr, osgState, point_max, point_min);
			if (point_min.x() <= point_max.x() && point_min.y() <= point_max.y() &&
				point_min.z() <= point_max.z())
			{
				ExpandBbox3d(osgState->point_max, osgState->point_min, point_max);
				ExpandBbox3d(osgState->point_max, osgState->point_min, point_min);
			}
		}
	}

	// 法线
	osg::Vec3Array* normalArr = (osg::Vec3Array*)g->getNormalArray();
	if (normalArr)
	{
		if (pmtState->normalAccessor > -1 && osgState->draw_array_first == -1)
		{
			primits.attributes["NORMAL"] = pmtState->normalAccessor;
		}
		else
		{
			if (dracoState && dracoState->compressed)
			{
				tinygltf::Accessor acc;
				acc.bufferView = -1;
				acc.componentType = TINYGLTF_COMPONENT_TYPE_FLOAT;
				acc.count = (osgState->draw_array_first >= 0) ? osgState->draw_array_count : (int)normalArr->size();
				acc.type = TINYGLTF_TYPE_VEC3;
				int accIdx = (int)osgState->model->accessors.size();
				osgState->model->accessors.emplace_back(acc);
				primits.attributes["NORMAL"] = accIdx;
				if (pmtState->normalAccessor == -1 && osgState->draw_array_first == -1)
				{
					pmtState->normalAccessor = accIdx;
				}
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
	}

	// 纹理坐标
	osg::Vec2Array* texArr = (osg::Vec2Array*)g->getTexCoordArray(0);
	if (texArr)
	{
		if (pmtState->textcdAccessor > -1 && osgState->draw_array_first == -1)
		{
			primits.attributes["TEXCOORD_0"] = pmtState->textcdAccessor;
		}
		else
		{
			if (dracoState && dracoState->compressed)
			{
				tinygltf::Accessor acc;
				acc.bufferView = -1;
				acc.componentType = TINYGLTF_COMPONENT_TYPE_FLOAT;
				acc.count = (osgState->draw_array_first >= 0) ? osgState->draw_array_count : (int)texArr->size();
				acc.type = TINYGLTF_TYPE_VEC2;
				int accIdx = (int)osgState->model->accessors.size();
				osgState->model->accessors.emplace_back(acc);
				primits.attributes["TEXCOORD_0"] = accIdx;
				if (pmtState->textcdAccessor == -1 && osgState->draw_array_first == -1)
				{
					pmtState->textcdAccessor = accIdx;
				}
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
	}

	// 材质
	primits.material = -1;

	switch (ps->getMode())
	{
		case GL_POINTS:
			primits.mode = TINYGLTF_MODE_POINTS;
			break;
		case GL_LINES:
			primits.mode = TINYGLTF_MODE_LINE;
			break;
		case GL_LINE_LOOP:
			primits.mode = TINYGLTF_MODE_LINE_LOOP;
			break;
		case GL_LINE_STRIP:
			primits.mode = TINYGLTF_MODE_LINE_STRIP;
			break;
		case GL_TRIANGLES:
			primits.mode = TINYGLTF_MODE_TRIANGLES;
			break;
		case GL_TRIANGLE_STRIP:
			primits.mode = TINYGLTF_MODE_TRIANGLE_STRIP;
			break;
		case GL_TRIANGLE_FAN:
			primits.mode = TINYGLTF_MODE_TRIANGLE_FAN;
			break;
		case GL_QUADS:
			primits.mode = TINYGLTF_MODE_TRIANGLES;
			break;
		case GL_QUAD_STRIP:
			primits.mode = TINYGLTF_MODE_TRIANGLES;
			break;
		default:
			LOG_E("Unsupport Primitive Mode: {}", static_cast<int>(ps->getMode()));
			exit(1);
			break;
	}

	osgState->model->meshes.back().primitives.emplace_back(primits);
	if (dracoState && dracoState->compressed)
	{
		tinygltf::Primitive& backPrim = osgState->model->meshes.back().primitives.back();
		tinygltf::Value::Object dracoExt;
		dracoExt["bufferView"] = tinygltf::Value(dracoState->bufferView);
		tinygltf::Value::Object dracoAttribs;
		if (dracoState->posId != -1)
		{
			dracoAttribs["POSITION"] = tinygltf::Value(dracoState->posId);
		}

		if (dracoState->normId != -1)
		{
			dracoAttribs["NORMAL"] = tinygltf::Value(dracoState->normId);
		}

		if (dracoState->texId != -1)
		{
			dracoAttribs["TEXCOORD_0"] = tinygltf::Value(dracoState->texId);
		}

		if (dracoState->batchId != -1)
		{
			dracoAttribs["_BATCHID"] = tinygltf::Value(dracoState->batchId);
		}

		dracoExt["attributes"] = tinygltf::Value(dracoAttribs);
		backPrim.extensions["KHR_draco_mesh_compression"] = tinygltf::Value(dracoExt);
	}
}

void OSGB23dTiles::WriteOsgGeometry(osg::Geometry* pGeometry, OsgBuildState* osgState, bool bEnableSimplification, bool bEnableDraco)
{
	if (bEnableSimplification)
	{
		SimplificationParams simplication_params;
		simplication_params.bEnableSimplification = true;
		MeshProcessor::SimplifyMeshGeometry(pGeometry, simplication_params);
	}

	DracoState dracoState = { false, -1, -1, -1, -1, -1 };

	if (bEnableDraco)
	{
		std::vector<unsigned char> compressed_data;
		size_t compressed_size = 0;
		DracoCompressionParams draco_params;
		draco_params.bEnableCompression = true;
		int dracoPosId = -1, dracoNormId = -1, dracoTexId = -1, dracoBatchId = -1;
		bool ok = MeshProcessor::CompressMeshGeometry(
			pGeometry, draco_params, compressed_data, compressed_size, &dracoPosId, &dracoNormId, &dracoTexId, &dracoBatchId, nullptr);
		if (ok && compressed_size > 0)
		{
			unsigned bufOffset = osgState->buffer->data.size();
			AlignmentBuffer(osgState->buffer->data);
			bufOffset = osgState->buffer->data.size();
			osgState->buffer->data.resize(bufOffset + compressed_size);
			std::memcpy(osgState->buffer->data.data() + bufOffset, compressed_data.data(), compressed_size);
			tinygltf::BufferView bv;
			bv.buffer = 0;
			bv.byteOffset = bufOffset;
			bv.byteLength = compressed_size;
			int bvIdx = (int)osgState->model->bufferViews.size();
			osgState->model->bufferViews.emplace_back(bv);
			dracoState.compressed = true;
			dracoState.bufferView = bvIdx;
			dracoState.posId = dracoPosId;
			dracoState.normId = dracoNormId;
			dracoState.texId = dracoTexId;
			dracoState.batchId = dracoBatchId;
		}
	}

	osg::PrimitiveSet::Type t = pGeometry->getPrimitiveSet(0)->getType();
	PrimitiveState pmtState = { -1, -1, -1 };
	for (unsigned int k = 0; k < pGeometry->getNumPrimitiveSets(); k++)
	{
		osg::PrimitiveSet* ps = pGeometry->getPrimitiveSet(k);
		//if (t != ps->getType())
		//{
		//	LOG_E("osgb 中 PrimitiveSets 类型不相同");
		//	exit(1);
		//}

		WriteElementArrayPrimitive(pGeometry, ps, osgState, &pmtState, &dracoState);
	}
}

bool OSGB23dTiles::ToGLBBuf(
	std::string path,
	std::string& glb_buff,
	MeshInfo& mesh_info,
	int node_type,
	bool bBinary,
	bool enable_texture_compress,
	bool enable_meshopt,
	bool enable_draco,
	bool need_mesh_info/* = true*/)
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
				AlignmentBuffer(buffer.data);
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

	// 
	model.extensionsRequired = { "KHR_materials_unlit" };
	model.extensionsUsed = { "KHR_materials_unlit" };

	if (enable_texture_compress)
	{
		model.extensionsRequired = { "KHR_materials_unlit", "KHR_texture_basisu" };
		model.extensionsUsed = { "KHR_materials_unlit", "KHR_texture_basisu" };
	}

	if (enable_draco)
	{
		model.extensionsRequired.emplace_back("KHR_draco_mesh_compression");
		model.extensionsUsed.emplace_back("KHR_draco_mesh_compression");
	}

	for (int i = 0; i < infoVisitor.texture_array.size(); i++)
	{
		tinygltf::Material mat = MakeDefaultColorMaterial(1.0, 1.0, 1.0);
		mat.pbrMetallicRoughness.baseColorTexture.index = i;
		model.materials.emplace_back(mat);
	}

	// RPB作为备选方案吧
	//for (auto geom : infoVisitor.geometry_array)
	//{
	//	tinygltf::Material mat;

	//	// 如果有材质属性，进行转换                                                                                                                                                  
	//	if (infoVisitor.material_map.find(geom) != infoVisitor.material_map.end())
	//	{
	//		osg::Material* osgMat = infoVisitor.material_map[geom];
	//		//mat = ConvertOSGBMaterialToPBR(osgMat);
	//		
	//		 model.extensionsUsed.emplace_back("KHR_materials_specular");  
	//		// 如果使用ConvertOSGBMaterialWithSpecularExt, 需要添加的扩展声明
	//		 mat = ConvertOSGBMaterialWithSpecularExt(osgMat);   
	//	}
	//	else
	//	{
	//		mat = MakeDefaultColorMaterial(1.0, 1.0, 1.0);
	//	}

	//	// 关联纹理
	//	if (infoVisitor.texture_map.find(geom) != infoVisitor.texture_map.end())
	//	{
	//		auto tex = infoVisitor.texture_map[geom];
	//		if (tex)
	//		{
	//			int texIndex = 0;
	//			for (auto texture : infoVisitor.texture_array)
	//			{
	//				if (tex == texture)
	//				{
	//					mat.pbrMetallicRoughness.baseColorTexture.index = texIndex;
	//					break;
	//				}
	//				texIndex++;
	//			}
	//		}
	//	}

	//	model.materials.emplace_back(mat);
	//}

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
	bool res = gltf.WriteGltfSceneToStream(&model, ss, false, bBinary);
	if (res)
	{
		glb_buff = ss.str();
	}

	return res;
}

bool OSGB23dTiles::ToB3DMBuf(
	std::string path,
	std::string& b3dm_buf,
	TileBox& tile_box,
	int node_type,
	bool enable_texture_compress,
	bool enable_meshopt,
	bool enable_draco)
{
	using nlohmann::json;

	std::string glb_buf;
	MeshInfo minfo;
	if (!ToGLBBuf(path, glb_buf, minfo, node_type, true, enable_texture_compress, enable_meshopt, enable_draco))
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
	PutVal(b3dm_buf, version);
	PutVal(b3dm_buf, total_len);
	PutVal(b3dm_buf, feature_json_len);
	PutVal(b3dm_buf, feature_bin_len);
	PutVal(b3dm_buf, batch_json_len);
	PutVal(b3dm_buf, batch_bin_len);
	b3dm_buf.append(feature_json_string.begin(), feature_json_string.end());
	b3dm_buf.append(batch_json_string.begin(), batch_json_string.end());
	b3dm_buf.append(glb_buf);

	return true;
}

void OSGB23dTiles::DoTileJob(
	OSGTree& tree,
	std::string out_path,
	int max_lvl,
	bool enable_texture_compress,
	bool enable_meshopt,
	bool enable_draco)
{
	if (tree.file_name.empty())
	{
		return;
	}

	int lvl = OSGBTools::GetLvlNum(tree.file_name);
	if (max_lvl != -1 && lvl > max_lvl)
	{
		return;
	}

	if (tree.type > 0)
	{
		std::string b3dm_buf;
		ToB3DMBuf(tree.file_name, b3dm_buf, tree.bbox, tree.type, enable_texture_compress, enable_meshopt, enable_draco);
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
			LOG_E("read node files [{}] fail!", name.c_str());
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

bool OSGB23dTiles::ToB3DMBatch(
	const std::string& pDataDir,
	const std::string& strOutputDir,
	double dCenterX,
	double dCenterY,
	int nMaxLevel,
	bool bEnableTextureCompress,
	bool bEnableMeshOpt,
	bool bEnableDraco)
{
	// 1. 构建 Data 目录路径
	std::string data_path = OSGBTools::OSGString(pDataDir);

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
			LOG_I("使用 ENU 坐标系统");
			LOG_I("  地理原点：纬度=%.6f，经度=%.6f", metadata.dCenterLat, metadata.dCenterLon);
			LOG_I("  SRSOrigin 偏移：x=%.3f, y=%.3f, z=%.3f", metadata.dOffsetX, metadata.dOffsetY, metadata.dOffsetZ);

			// 调用 enu_init 初始化 GeoTransform
			// 注意：enu_init 需要经度在前，纬度在后
			double origin_enu[3] = { metadata.dOffsetX, metadata.dOffsetY, metadata.dOffsetZ };
			if (GeoTransform::InitFromENU(metadata.dCenterLon, metadata.dCenterLat, origin_enu))
			{
				LOG_I("ENU 系统 GeoTransform 初始化成功");

				// 使用 metadata 中的坐标作为中心点
				dCenterX = metadata.dCenterLon;
				dCenterY = metadata.dCenterLat;
			}
			else
			{
				LOG_E("ENU 系统 GeoTransform 初始化失败");
			}
		}
		else if (metadata.bIsEPSG)
		{
			// EPSG 坐标系统
			LOG_I("使用 EPSG:{} 坐标系统", metadata.nEpsgCode);
			LOG_I("  SRSOrigin: {}", metadata.strSrsOrigin.c_str());

			// 解析 SRSOrigin 为投影坐标
			double origin[3] = { metadata.dOffsetX, metadata.dOffsetY, metadata.dOffsetZ };
			// 调用 epsg_convert 转换为经纬度
			if (GeoTransform::InitFromEPSG(metadata.nEpsgCode, origin))
			{
				LOG_I("EPSG:{} 系统 GeoTransform 初始化成功", metadata.nEpsgCode);
				LOG_I("  转换为地理坐标：经度={:.6f}，纬度={:.6f}，海拔={:.3f}", origin[0], origin[1], origin[2]);

				// 使用转换后的经纬度作为中心点
				dCenterX = origin[0];
				dCenterY = origin[1];
			}
			else
			{
				LOG_E("EPSG:{} 坐标转换失败", metadata.nEpsgCode);
			}
		}
		else if (metadata.bIsWKT)
		{
			// WKT 格式坐标系统
			LOG_I("使用 WKT 投影");
			LOG_I("  SRSOrigin: {}", metadata.strSrsOrigin.c_str());
			// 解析 SRSOrigin 为投影坐标
			double origin[3] = { metadata.dOffsetX, metadata.dOffsetY, metadata.dOffsetZ };

			// 调用 InitFromWKT 转换为经纬度
			if (GeoTransform::InitFromWKT(metadata.strSrs.c_str(), origin))
			{
				LOG_I("WKT 投影 GeoTransform 初始化成功");
				LOG_I("  转换为地理坐标：经度=%.6f，纬度=%.6f，海拔=%.3f", origin[0], origin[1], origin[2]);

				// 使用转换后的经纬度作为中心点
				dCenterX = origin[0];
				dCenterY = origin[1];
			}
			else
			{
				LOG_E("WKT 坐标转换失败");
			}
		}
	}
	else
	{
		LOG_W("metadata.xml 未找到或解析失败，使用提供的 center_x=%.6f, center_y=%.6f", dCenterX, dCenterY);
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
		OSGBLog::LOG_I("[INFO] 在以下位置搜索瓦片：{}", check_data_dir);
	}
	else
	{
		// 尝试纯OSGB文件夹模式
		check_data_dir = data_path;
		LOG_I("检测到纯OSGB文件夹模式");
		OSGBLog::LOG_I("[INFO] 扫描OSGB文件夹：{}", check_data_dir);
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
			LOG_E("未找到任何 Tile_* 目录：{}", check_data_dir.c_str());
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
				LOG_I("未找到根OSGB，使用第一个文件: {}", root_osgb.c_str());
			}
			else
			{
				LOG_I("找到根OSGB: {}", root_osgb.c_str());
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
						LOG_I("子目录 {} 未找到根OSGB，使用第一个文件: {}", folder_name.c_str(), root_osgb.c_str());
					}
					else
					{
						LOG_W("子目录 {} 中未找到OSGB文件，跳过", folder_name.c_str());
						continue;
					}
				}
				else
				{
					LOG_I("子目录 {} 找到根OSGB: {}", folder_name.c_str(), root_osgb.c_str());
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

	OSGBLog::LOG_I("[INFO] 找到 {} 个瓦片目录待处理", tiles.size());

	// 5. 处理每个瓦片（使用 OpenMP 并行加速）
	std::vector<std::string> tile_jsons;

#ifdef _OPENMP
	// 获取可用线程数
	int num_threads = omp_get_max_threads();
	OSGBLog::LOG_I("[INFO] 使用 OpenMP 并行处理，线程数：{}", num_threads);

	// 使用 dynamic 调度以平衡不同瓦片的处理时间差异
#pragma omp parallel for schedule(dynamic)
#endif
	for (int i = 0; i < static_cast<int>(tiles.size()); i++)
	{
		TileInfo& tile = tiles[i];

		// 线程安全的控制台输出
#ifdef _OPENMP
#pragma omp critical(console_output)
#endif
		{
			OSGBLog::LOG_I("[INFO] 处理瓦片 {}/{}：{}", i + 1, tiles.size(), tile.tile_name);
		}

		B3DMResult result = ToB3DM(
			tile.osgb_path,
			tile.output_path,
			dCenterX,
			dCenterY,
			nMaxLevel,
			bEnableTextureCompress,
			bEnableMeshOpt,
			bEnableDraco
		);

		if (result.success && !result.tilesetJson.empty())
		{
			// 临界区：保护共享数据（tile_jsons 和 global_bbox）
#ifdef _OPENMP
#pragma omp critical(data_update)
#endif
			{
				tile_jsons.emplace_back(result.tilesetJson);

				// 更新边界框
				tile.bbox.max = { result.boundingBox[0], result.boundingBox[1], result.boundingBox[2] };
				tile.bbox.min = { result.boundingBox[3], result.boundingBox[4], result.boundingBox[5] };

				// 合并到全局边界框
				if (global_bbox.max.empty())
				{
					global_bbox = tile.bbox;
				}
				else
				{
					ExpandBox(global_bbox, tile.bbox);
				}
			}

			// 将瓦片 JSON 包装在完整的 tileset 结构中
			std::string wrapped_json = "{";
			wrapped_json += "\"asset\":{\"version\":\"1.0\",\"gltfUpAxis\":\"Z\"},";
			wrapped_json += "\"geometricError\":1000,";
			wrapped_json += "\"root\":";
			wrapped_json += result.tilesetJson;  // 这是此瓦片的根节点
			wrapped_json += "}";

			// 保存单个瓦片的 tileset.json（文件写入通常是线程安全的）
			std::string tileset_path = tile.output_path + "/tileset.json";
			OSGBTools::WriteFile(tileset_path.c_str(), wrapped_json.data(), wrapped_json.size());
		}
		else
		{
			// 线程安全的错误日志输出
#ifdef _OPENMP
#pragma omp critical(console_output)
#endif
			{
				LOG_E("处理瓦片失败：{}", tile.tile_name.c_str());
			}
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

	OSGBLog::LOG_I("[INFO] 批量处理完成！生成了包含 {} 个瓦片的根 tileset.json", tiles.size());

	// 9. 清理 GeoTransform 资源（谁调用谁释放）
	GeoTransform::Cleanup();

	return true;
}

//===============OSGB23dTiles 结束==================
