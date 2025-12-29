#include "OsgbReader.h"
#include <osg/Node>
#include <osg/Geode>
#include <osg/Geometry>
#include <osg/Texture2D>
#include <osg/Image>
#include <osg/Material>
#include <osg/TriangleIndexFunctor>
#include <osgDB/ReadFile>
#include <osgDB/WriteFile>
#include <osgDB/FileUtils>
#include <osgDB/Registry>
#include <osg/ComputeBoundsVisitor>
#include <osg/PagedLOD>
#include <osg/ProxyNode>
#include <sstream>
#include <unordered_map>
#include <unordered_set>
#include <map>
#include <set>
#include <locale>
#include <filesystem>
#include <functional>

// 包含所有PrimitiveSet类型
#include <osg/PrimitiveSet>
#include <osg/MatrixTransform>

namespace RealScene3D
{
    namespace Native
    {

        // 内部实现类（PIMPL 模式）
        class OsgbReader::Impl
        {
        public:
            osg::ref_ptr<osg::Node> rootNode;

            Impl() : rootNode(nullptr) {}
            ~Impl()
            {
                rootNode = nullptr;
            }
        };

        OsgbReader::OsgbReader()
            : m_impl(std::make_unique<Impl>()),
              m_charset(Charset::Default)
        {
        }

        OsgbReader::~OsgbReader() = default;

        void OsgbReader::SetCharset(Charset charset)
        {
            m_charset = charset;
        }

        bool OsgbReader::ValidateFile(const std::string &filePath)
        {
            if (filePath.empty())
            {
                m_lastError = "File path is empty";
                return false;
            }

            if (!osgDB::fileExists(filePath))
            {
                m_lastError = "File does not exist: " + filePath;
                return false;
            }

            return true;
        }

        // PagedLOD文件引用提取访问器
        class PagedLODVisitor : public osg::NodeVisitor
        {
        public:
            std::set<std::string> childFiles; // 使用set自动去重
            std::string basePath;             // 基础路径

            PagedLODVisitor(const std::string &baseDir)
                : osg::NodeVisitor(osg::NodeVisitor::TRAVERSE_ALL_CHILDREN),
                  basePath(baseDir) {}

            void apply(osg::PagedLOD &node) override
            {
                // PagedLOD节点包含多个子文件引用
                for (unsigned int i = 0; i < node.getNumFileNames(); ++i)
                {
                    std::string filename = node.getFileName(i);
                    if (!filename.empty())
                    {
                        // 构造完整路径
                        std::filesystem::path fullPath = std::filesystem::path(basePath) / filename;
                        if (std::filesystem::exists(fullPath))
                        {
                            childFiles.insert(fullPath.string());
                        }
                    }
                }
                traverse(node);
            }

            void apply(osg::ProxyNode &node) override
            {
                // ProxyNode也可能包含外部引用
                for (unsigned int i = 0; i < node.getNumFileNames(); ++i)
                {
                    std::string filename = node.getFileName(i);
                    if (!filename.empty())
                    {
                        std::filesystem::path fullPath = std::filesystem::path(basePath) / filename;
                        if (std::filesystem::exists(fullPath))
                        {
                            childFiles.insert(fullPath.string());
                        }
                    }
                }
                traverse(node);
            }
        };

        // 访问器 - 直接提取原始数据
        class MeshExtractorVisitor : public osg::NodeVisitor
        {
        public:
            MeshData meshData;
            std::unordered_map<osg::Texture *, int> textureIndexMap;
            std::unordered_set<osg::Geometry *> processedGeometries; // 已处理的 Geometry 去重
            osg::Matrix currentTransform;                            // 当前累积的变换矩阵
            bool hasTransform;                                       // 是否检测到变换

            MeshExtractorVisitor() : osg::NodeVisitor(osg::NodeVisitor::TRAVERSE_ALL_CHILDREN),
                                     hasTransform(false)
            {
                meshData.BBoxMinX = meshData.BBoxMinY = meshData.BBoxMinZ = std::numeric_limits<float>::max();
                meshData.BBoxMaxX = meshData.BBoxMaxY = meshData.BBoxMaxZ = std::numeric_limits<float>::lowest();
                meshData.VertexCount = 0;
                meshData.FaceCount = 0;

                // 初始化为单位矩阵
                currentTransform.makeIdentity();

                // 预分配内存（性能优化）
                meshData.Vertices.reserve(10000 * 3); // 预留1万顶点
                meshData.Normals.reserve(10000 * 3);
                meshData.TexCoords.reserve(10000 * 2);
                meshData.Indices.reserve(10000 * 3); // 预留1万三角形
                meshData.Textures.reserve(10);
                meshData.Materials.reserve(10);
            }

            // 处理 MatrixTransform 节点
            void apply(osg::MatrixTransform &transform) override
            {
                // 保存当前变换
                osg::Matrix savedTransform = currentTransform;

                // 累积变换矩阵
                currentTransform = currentTransform * transform.getMatrix();
                hasTransform = true;

                // 遍历子节点
                traverse(transform);

                // 恢复变换
                currentTransform = savedTransform;
            }

            void apply(osg::Geode &geode) override
            {
                for (unsigned int i = 0; i < geode.getNumDrawables(); ++i)
                {
                    osg::Geometry *geom = geode.getDrawable(i)->asGeometry();
                    if (geom && processedGeometries.find(geom) == processedGeometries.end())
                    {
                        processedGeometries.insert(geom);
                        extractGeometry(geom);
                    }
                }
                // OSG 3.4+: Geode 可能有子节点，需要遍历
                traverse(geode);
            }

            // OSG 3.4+ 支持：Drawable 可以直接作为节点添加到场景图
            void apply(osg::Drawable &drawable) override
            {
                osg::Geometry *geom = drawable.asGeometry();
                if (geom && processedGeometries.find(geom) == processedGeometries.end())
                {
                    processedGeometries.insert(geom);
                    extractGeometry(geom);
                }
            }

            // 显式处理 Group 节点，确保正确遍历所有子节点
            void apply(osg::Group &group) override
            {
                traverse(group);
            }

            // 完成时保存变换信息
            void finalize()
            {
                if (hasTransform)
                {
                    meshData.Transform.HasTransform = true;
                    for (int row = 0; row < 4; ++row)
                    {
                        for (int col = 0; col < 4; ++col)
                        {
                            meshData.Transform.Matrix[row * 4 + col] = currentTransform(row, col);
                        }
                    }
                }
            }

        private:
            void extractGeometry(osg::Geometry *geom)
            {
                osg::Vec3Array *vertices = dynamic_cast<osg::Vec3Array *>(geom->getVertexArray());
                if (!vertices || vertices->empty())
                    return;

                unsigned int baseIndex = meshData.Vertices.size() / 3;
                unsigned int vertexCount = vertices->size();

                // 边界检查：防止顶点数过大
                if (vertexCount > 10000000)
                { // 1000万顶点上限
                    // 跳过异常大的几何体
                    return;
                }

                // 记录当前几何体开始前的面数量（用于材质索引映射）
                unsigned int startFaceIndex = meshData.Indices.size() / 3;

                // 提取顶点（应用变换矩阵）
                for (const auto &v : *vertices)
                {
                    // 应用当前变换矩阵
                    osg::Vec3 transformedV = v * currentTransform;

                    meshData.Vertices.push_back(transformedV.x());
                    meshData.Vertices.push_back(transformedV.y());
                    meshData.Vertices.push_back(transformedV.z());

                    meshData.BBoxMinX = std::min(meshData.BBoxMinX, transformedV.x());
                    meshData.BBoxMinY = std::min(meshData.BBoxMinY, transformedV.y());
                    meshData.BBoxMinZ = std::min(meshData.BBoxMinZ, transformedV.z());
                    meshData.BBoxMaxX = std::max(meshData.BBoxMaxX, transformedV.x());
                    meshData.BBoxMaxY = std::max(meshData.BBoxMaxY, transformedV.y());
                    meshData.BBoxMaxZ = std::max(meshData.BBoxMaxZ, transformedV.z());
                }

                // 提取法线 - 正确处理绑定模式
                osg::Vec3Array *normals = dynamic_cast<osg::Vec3Array *>(geom->getNormalArray());
                if (normals && !normals->empty())
                {
                    osg::Geometry::AttributeBinding normalBinding = geom->getNormalBinding();

                    if (normalBinding == osg::Geometry::BIND_PER_VERTEX)
                    {
                        // 每个顶点一个法线（最常见）
                        if (normals->size() == vertexCount)
                        {
                            for (const auto &n : *normals)
                            {
                                meshData.Normals.push_back(n.x());
                                meshData.Normals.push_back(n.y());
                                meshData.Normals.push_back(n.z());
                            }
                        }
                        else
                        {
                            // 法线数量不匹配，填充默认法线
                            for (unsigned int i = 0; i < vertexCount; ++i)
                            {
                                if (i < normals->size())
                                {
                                    const osg::Vec3 &n = (*normals)[i];
                                    meshData.Normals.push_back(n.x());
                                    meshData.Normals.push_back(n.y());
                                    meshData.Normals.push_back(n.z());
                                }
                                else
                                {
                                    // 默认法线 (0, 1, 0)
                                    meshData.Normals.push_back(0.0f);
                                    meshData.Normals.push_back(1.0f);
                                    meshData.Normals.push_back(0.0f);
                                }
                            }
                        }
                    }
                    else if (normalBinding == osg::Geometry::BIND_OVERALL && normals->size() > 0)
                    {
                        // 整个geometry共用一个法线，需要扩展到所有顶点
                        const osg::Vec3 &n = (*normals)[0];
                        for (unsigned int i = 0; i < vertexCount; ++i)
                        {
                            meshData.Normals.push_back(n.x());
                            meshData.Normals.push_back(n.y());
                            meshData.Normals.push_back(n.z());
                        }
                    }
                    else if (normalBinding == osg::Geometry::BIND_PER_PRIMITIVE_SET)
                    {
                        // 每个 primitive set 一个法线
                        // 这种情况较复杂，需要根据图元展开法线
                        // 为简化处理，这里使用第一个法线作为默认值
                        if (normals->size() > 0)
                        {
                            const osg::Vec3 &n = (*normals)[0];
                            for (unsigned int i = 0; i < vertexCount; ++i)
                            {
                                meshData.Normals.push_back(n.x());
                                meshData.Normals.push_back(n.y());
                                meshData.Normals.push_back(n.z());
                            }
                        }
                    }
                    else
                    {
                        // 其他绑定模式：使用第一个法线作为默认值
                        if (normals->size() > 0)
                        {
                            const osg::Vec3 &n = (*normals)[0];
                            for (unsigned int i = 0; i < vertexCount; ++i)
                            {
                                meshData.Normals.push_back(n.x());
                                meshData.Normals.push_back(n.y());
                                meshData.Normals.push_back(n.z());
                            }
                        }
                    }
                }

                // 法线数量不足时补充默认法线
                while (meshData.Normals.size() < (meshData.Vertices.size()))
                {
                    meshData.Normals.push_back(0.0f);
                    meshData.Normals.push_back(1.0f);
                    meshData.Normals.push_back(0.0f);
                }

                // 提取纹理坐标 - 正确处理绑定模式
                osg::Vec2Array *texCoords = dynamic_cast<osg::Vec2Array *>(geom->getTexCoordArray(0));
                if (texCoords && !texCoords->empty())
                {
                    // OSG 2.x 使用 getTexCoordArrayList，OSG 3.x 使用不同API
                    // 纹理坐标通常是 BIND_PER_VERTEX
                    size_t texCoordCount = std::min(static_cast<size_t>(vertexCount), texCoords->size());
                    for (size_t i = 0; i < texCoordCount; ++i)
                    {
                        const osg::Vec2 &tc = (*texCoords)[i];
                        meshData.TexCoords.push_back(tc.x());
                        meshData.TexCoords.push_back(tc.y());
                    }
                }

                // 纹理坐标数量不足时补充默认值
                while (meshData.TexCoords.size() < (meshData.Vertices.size() / 3 * 2))
                {
                    meshData.TexCoords.push_back(0.0f);
                    meshData.TexCoords.push_back(0.0f);
                }

                // 提取索引 - 支持多种 PrimitiveSet 类型
                for (unsigned int i = 0; i < geom->getNumPrimitiveSets(); ++i)
                {
                    osg::PrimitiveSet *ps = geom->getPrimitiveSet(i);
                    if (!ps)
                        continue;

                    GLenum mode = ps->getMode();

                    // 处理 DrawElementsUInt
                    osg::DrawElementsUInt *deui = dynamic_cast<osg::DrawElementsUInt *>(ps);
                    if (deui)
                    {
                        processDrawElements(deui, baseIndex, mode);
                        continue;
                    }

                    // 处理 DrawElementsUShort
                    osg::DrawElementsUShort *deus = dynamic_cast<osg::DrawElementsUShort *>(ps);
                    if (deus)
                    {
                        processDrawElements(deus, baseIndex, mode);
                        continue;
                    }

                    // 处理 DrawElementsUByte
                    osg::DrawElementsUByte *deub = dynamic_cast<osg::DrawElementsUByte *>(ps);
                    if (deub)
                    {
                        processDrawElements(deub, baseIndex, mode);
                        continue;
                    }

                    // 处理 DrawArrays
                    osg::DrawArrays *da = dynamic_cast<osg::DrawArrays *>(ps);
                    if (da)
                    {
                        processDrawArrays(da, baseIndex, mode);
                        continue;
                    }

                    // 处理 DrawArrayLengths（高级模式）
                    osg::DrawArrayLengths *dal = dynamic_cast<osg::DrawArrayLengths *>(ps);
                    if (dal)
                    {
                        processDrawArrayLengths(dal, baseIndex, mode);
                        continue;
                    }
                }

                meshData.FaceCount = meshData.Indices.size() / 3;
                meshData.VertexCount = meshData.Vertices.size() / 3;

                // 提取纹理和材质
                osg::StateSet *ss = geom->getStateSet();
                int currentMaterialIndex = -1; // 默认无材质

                if (ss)
                {
                    // 记录提取材质前的材质数量
                    int materialCountBefore = meshData.Materials.size();

                    extractTextures(ss);

                    // 如果添加了新材质，使用新材质的索引
                    if (meshData.Materials.size() > materialCountBefore)
                    {
                        currentMaterialIndex = materialCountBefore; // 新添加的材质索引
                    }
                }

                // 为当前几何体的所有面记录材质索引（方案B修复）
                unsigned int endFaceIndex = meshData.Indices.size() / 3;
                for (unsigned int i = startFaceIndex; i < endFaceIndex; ++i)
                {
                    meshData.FaceMaterialIndices.push_back(currentMaterialIndex);
                }
            }

            void extractTextures(osg::StateSet *ss)
            {
                // 支持多纹理单元（最多8个）
                for (unsigned int texUnit = 0; texUnit < 8; ++texUnit)
                {
                    osg::Texture2D *tex = dynamic_cast<osg::Texture2D *>(
                        ss->getTextureAttribute(texUnit, osg::StateAttribute::TEXTURE));

                    if (tex && tex->getImage())
                    {
                        // 检查是否已经添加过该纹理
                        if (textureIndexMap.find(tex) == textureIndexMap.end())
                        {
                            osg::Image *img = tex->getImage();

                            // 检查是否有外部文件（参考 UGOSGToolkit.cpp:363）
                            std::string fileName = img->getFileName();
                            if (!fileName.empty())
                            {
                                // TODO: 如果有外部文件，应该使用文件路径
                                // 当前简化处理：仍然提取buffer数据
                            }

                            TextureData td;

                            td.Width = img->s();
                            td.Height = img->t();

                            // 获取像素格式
                            GLenum pixelFormat = img->getPixelFormat();

                            // 检查是否为压缩格式
                            switch (pixelFormat)
                            {
                            case GL_COMPRESSED_RGB_S3TC_DXT1_EXT:
                                td.IsCompressed = true;
                                td.CompressionType = 1;
                                td.Format = "DXT1";
                                td.Components = 3;
                                break;

                            case GL_COMPRESSED_RGBA_S3TC_DXT1_EXT:
                                td.IsCompressed = true;
                                td.CompressionType = 1;
                                td.Format = "DXT1";
                                td.Components = 4;
                                break;

                            case GL_COMPRESSED_RGBA_S3TC_DXT3_EXT:
                                td.IsCompressed = true;
                                td.CompressionType = 3;
                                td.Format = "DXT3";
                                td.Components = 4;
                                break;

                            case GL_COMPRESSED_RGBA_S3TC_DXT5_EXT:
                                td.IsCompressed = true;
                                td.CompressionType = 5;
                                td.Format = "DXT5";
                                td.Components = 4;
                                break;

                            case GL_RGBA:
                                td.IsCompressed = false;
                                td.Format = "RGBA";
                                td.Components = 4;
                                break;

                            case GL_RGB:
                                td.IsCompressed = false;
                                td.Format = "RGB";
                                td.Components = 3;
                                break;

                            case GL_LUMINANCE:
                                td.IsCompressed = false;
                                td.Format = "L";
                                td.Components = 1;
                                break;

                            case GL_LUMINANCE_ALPHA:
                                td.IsCompressed = false;
                                td.Format = "LA";
                                td.Components = 2;
                                break;

                            default:
                                // 不支持的格式，跳过
                                continue;
                            }

                            // 复制纹理数据
                            size_t dataSize = img->getTotalSizeInBytes();
                            if (dataSize == 0 || !img->data())
                            {
                                // 数据无效，跳过
                                continue;
                            }

                            td.ImageData.resize(dataSize);
                            std::memcpy(td.ImageData.data(), img->data(), dataSize);

                            // 生成纹理名称（使用原始文件名或生成名称）
                            if (!fileName.empty())
                            {
                                // 提取文件名（不含路径）
                                size_t pos = fileName.find_last_of("/\\");
                                if (pos != std::string::npos)
                                {
                                    fileName = fileName.substr(pos + 1);
                                }
                                td.Name = fileName;
                            }
                            else
                            {
                                td.Name = "texture_" + std::to_string(meshData.Textures.size());
                            }

                            // 添加到纹理列表
                            int textureIndex = meshData.Textures.size();
                            textureIndexMap[tex] = textureIndex;
                            meshData.Textures.push_back(td);
                        }
                    }
                }

                // 提取材质属性
                extractMaterials(ss);
            }

            void extractMaterials(osg::StateSet *ss)
            {
                osg::Material *mat = dynamic_cast<osg::Material *>(
                    ss->getAttribute(osg::StateAttribute::MATERIAL));

                MaterialData matData;

                if (mat)
                {
                    // 环境光
                    osg::Vec4 ambient = mat->getAmbient(osg::Material::FRONT_AND_BACK);
                    matData.AmbientR = ambient.r();
                    matData.AmbientG = ambient.g();
                    matData.AmbientB = ambient.b();
                    matData.AmbientA = ambient.a();

                    // 漫反射
                    osg::Vec4 diffuse = mat->getDiffuse(osg::Material::FRONT_AND_BACK);
                    matData.DiffuseR = diffuse.r();
                    matData.DiffuseG = diffuse.g();
                    matData.DiffuseB = diffuse.b();
                    matData.DiffuseA = diffuse.a();

                    // 镜面反射
                    osg::Vec4 specular = mat->getSpecular(osg::Material::FRONT_AND_BACK);
                    matData.SpecularR = specular.r();
                    matData.SpecularG = specular.g();
                    matData.SpecularB = specular.b();
                    matData.SpecularA = specular.a();

                    // 自发光
                    osg::Vec4 emission = mat->getEmission(osg::Material::FRONT_AND_BACK);
                    matData.EmissionR = emission.r();
                    matData.EmissionG = emission.g();
                    matData.EmissionB = emission.b();
                    matData.EmissionA = emission.a();

                    // 光泽度
                    matData.Shininess = mat->getShininess(osg::Material::FRONT_AND_BACK);
                }

                // 设置材质名称
                matData.Name = "material_" + std::to_string(meshData.Materials.size());

                // 查找关联的纹理索引
                matData.TextureIndex = -1;
                osg::Texture2D *tex = dynamic_cast<osg::Texture2D *>(
                    ss->getTextureAttribute(0, osg::StateAttribute::TEXTURE));

                if (tex && textureIndexMap.find(tex) != textureIndexMap.end())
                {
                    matData.TextureIndex = textureIndexMap[tex];
                }

                meshData.Materials.push_back(matData);
            }

            // 辅助方法：处理DrawElements类型（模板方法）
            template <typename T>
            void processDrawElements(T *elements, unsigned int baseIndex, GLenum mode)
            {
                // 获取重启索引（通常是类型最大值，如 65535 或 4294967295）
                typename T::value_type restartIndex = std::numeric_limits<typename T::value_type>::max();

                if (mode == GL_TRIANGLES)
                {
                    // 三角形列表
                    for (size_t j = 0; j + 2 < elements->size(); j += 3)
                    {
                        typename T::value_type i0 = (*elements)[j];
                        typename T::value_type i1 = (*elements)[j + 1];
                        typename T::value_type i2 = (*elements)[j + 2];

                        if (i0 == restartIndex || i1 == restartIndex || i2 == restartIndex)
                            continue;

                        meshData.Indices.push_back(baseIndex + i0);
                        meshData.Indices.push_back(baseIndex + i1);
                        meshData.Indices.push_back(baseIndex + i2);
                    }
                }
                else if (mode == GL_TRIANGLE_STRIP)
                {
                    // 三角形带：支持 Restart Index
                    size_t currentRun = 0; // 当前连续有效顶点数

                    for (size_t j = 0; j < elements->size(); ++j)
                    {
                        typename T::value_type idx = (*elements)[j];

                        // 检查 Restart Index
                        if (idx == restartIndex)
                        {
                            currentRun = 0; // 重置序列
                            continue;
                        }

                        currentRun++;

                        if (currentRun >= 3)
                        {
                            // 可以构建三角形
                            typename T::value_type i0 = (*elements)[j - 2];
                            typename T::value_type i1 = (*elements)[j - 1];
                            typename T::value_type i2 = (*elements)[j];

                            // 判断奇偶性来决定顶点顺序
                            // 第1个三角形(currentRun=3)是偶数(0)，顺序 i0,i1,i2
                            // 第2个三角形(currentRun=4)是奇数(1)，顺序 i0,i2,i1 (翻转后两个)
                            if ((currentRun - 3) % 2 == 0)
                            {
                                meshData.Indices.push_back(baseIndex + i0);
                                meshData.Indices.push_back(baseIndex + i1);
                                meshData.Indices.push_back(baseIndex + i2);
                            }
                            else
                            {
                                meshData.Indices.push_back(baseIndex + i0);
                                meshData.Indices.push_back(baseIndex + i2);
                                meshData.Indices.push_back(baseIndex + i1);
                            }
                        }
                    }
                }
                else if (mode == GL_TRIANGLE_FAN)
                {
                    // 三角形扇：支持 Restart Index
                    size_t currentRun = 0;
                    typename T::value_type centerIdx = 0; // 扇形的中心点

                    for (size_t j = 0; j < elements->size(); ++j)
                    {
                        typename T::value_type idx = (*elements)[j];

                        if (idx == restartIndex)
                        {
                            currentRun = 0;
                            continue;
                        }

                        currentRun++;

                        if (currentRun == 1)
                        {
                            centerIdx = idx; // 记录新的中心点
                        }
                        else if (currentRun >= 3)
                        {
                            // 任何 >= 3 的点都与中心点和前一个点构成三角形
                            typename T::value_type prevIdx = (*elements)[j - 1];

                            meshData.Indices.push_back(baseIndex + centerIdx);
                            meshData.Indices.push_back(baseIndex + prevIdx);
                            meshData.Indices.push_back(baseIndex + idx);
                        }
                    }
                }
                else if (mode == GL_QUADS)
                {
                    // 四边形转三角形
                    size_t quadCount = elements->size() / 4;
                    for (size_t q = 0; q < quadCount; ++q)
                    {
                        typename T::value_type i0 = (*elements)[q * 4 + 0];
                        typename T::value_type i1 = (*elements)[q * 4 + 1];
                        typename T::value_type i2 = (*elements)[q * 4 + 2];
                        typename T::value_type i3 = (*elements)[q * 4 + 3];

                        if (i0 == restartIndex || i1 == restartIndex || i2 == restartIndex || i3 == restartIndex)
                            continue;

                        // 保持原始的缠绕顺序逻辑
                        // 第一个三角形 (0,1,3) -> 使用索引 (i1,i0,i3)
                        meshData.Indices.push_back(baseIndex + i1);
                        meshData.Indices.push_back(baseIndex + i0);
                        meshData.Indices.push_back(baseIndex + i3);

                        // 第二个三角形 (1,2,3) -> 使用索引 (i1,i3,i2)
                        meshData.Indices.push_back(baseIndex + i1);
                        meshData.Indices.push_back(baseIndex + i3);
                        meshData.Indices.push_back(baseIndex + i2);
                    }
                }
                else if (mode == GL_POLYGON)
                {
                    // 多边形转三角形扇
                    if (elements->size() >= 3)
                    {
                        for (size_t j = 1; j + 1 < elements->size(); ++j)
                        {
                            typename T::value_type i0 = (*elements)[0];
                            typename T::value_type i1 = (*elements)[j];
                            typename T::value_type i2 = (*elements)[j + 1];

                            if (i0 == restartIndex || i1 == restartIndex || i2 == restartIndex)
                                continue;

                            meshData.Indices.push_back(baseIndex + i0);
                            meshData.Indices.push_back(baseIndex + i1);
                            meshData.Indices.push_back(baseIndex + i2);
                        }
                    }
                }
                else if (mode == GL_QUAD_STRIP)
                {
                    // 四边形带转三角形（每2个顶点与前2个顶点构成一个四边形）
                    for (size_t j = 0; j + 3 < elements->size(); j += 2)
                    {
                        typename T::value_type i0 = (*elements)[j];
                        typename T::value_type i1 = (*elements)[j + 1];
                        typename T::value_type i2 = (*elements)[j + 3];
                        typename T::value_type i3 = (*elements)[j + 2];

                        if (i0 == restartIndex || i1 == restartIndex || i2 == restartIndex || i3 == restartIndex)
                            continue;

                        // 第一个三角形 (0,1,2)
                        meshData.Indices.push_back(baseIndex + i0);
                        meshData.Indices.push_back(baseIndex + i1);
                        meshData.Indices.push_back(baseIndex + i2);

                        // 第二个三角形 (0,2,3)
                        meshData.Indices.push_back(baseIndex + i0);
                        meshData.Indices.push_back(baseIndex + i2);
                        meshData.Indices.push_back(baseIndex + i3);
                    }
                }
            }

            // 辅助方法：处理DrawArrays
            void processDrawArrays(osg::DrawArrays *da, unsigned int baseIndex, GLenum mode)
            {
                unsigned int first = da->getFirst();
                unsigned int count = da->getCount();

                if (mode == GL_TRIANGLES)
                {
                    for (unsigned int j = 0; j < count; ++j)
                    {
                        meshData.Indices.push_back(baseIndex + first + j);
                    }
                }
                else if (mode == GL_TRIANGLE_STRIP)
                {
                    // 三角形带：需要至少3个顶点
                    if (count >= 3)
                    {
                        for (unsigned int j = 0; j <= count - 3; ++j)
                        {
                            if (j % 2 == 0)
                            {
                                // 偶数三角形：正常顺序
                                meshData.Indices.push_back(baseIndex + first + j);
                                meshData.Indices.push_back(baseIndex + first + j + 1);
                                meshData.Indices.push_back(baseIndex + first + j + 2);
                            }
                            else
                            {
                                // 奇数三角形：翻转顺序以保持正确的缠绕方向
                                meshData.Indices.push_back(baseIndex + first + j);
                                meshData.Indices.push_back(baseIndex + first + j + 2);
                                meshData.Indices.push_back(baseIndex + first + j + 1);
                            }
                        }
                    }
                }
                else if (mode == GL_TRIANGLE_FAN)
                {
                    // 三角形扇：需要至少3个顶点
                    if (count >= 3)
                    {
                        for (unsigned int j = 1; j <= count - 2; ++j)
                        {
                            meshData.Indices.push_back(baseIndex + first);
                            meshData.Indices.push_back(baseIndex + first + j);
                            meshData.Indices.push_back(baseIndex + first + j + 1);
                        }
                    }
                }
                else if (mode == GL_QUADS)
                {
                    // 四边形转三角形
                    unsigned int quadCount = count / 4;
                    for (unsigned int q = 0; q < quadCount; ++q)
                    {
                        unsigned int i0 = first + q * 4 + 0;
                        unsigned int i1 = first + q * 4 + 1;
                        unsigned int i2 = first + q * 4 + 2;
                        unsigned int i3 = first + q * 4 + 3;

                        meshData.Indices.push_back(baseIndex + i1);
                        meshData.Indices.push_back(baseIndex + i0);
                        meshData.Indices.push_back(baseIndex + i3);

                        meshData.Indices.push_back(baseIndex + i1);
                        meshData.Indices.push_back(baseIndex + i3);
                        meshData.Indices.push_back(baseIndex + i2);
                    }
                }
                else if (mode == GL_POLYGON)
                {
                    // 多边形转三角形扇
                    if (count >= 3)
                    {
                        for (unsigned int j = 1; j + 1 < count; ++j)
                        {
                            meshData.Indices.push_back(baseIndex + first);
                            meshData.Indices.push_back(baseIndex + first + j);
                            meshData.Indices.push_back(baseIndex + first + j + 1);
                        }
                    }
                }
                else if (mode == GL_QUAD_STRIP)
                {
                    // 四边形带转三角形（每2个顶点与前2个顶点构成一个四边形）
                    for (unsigned int j = 0; j + 3 < count; j += 2)
                    {
                        unsigned int i0 = first + j;
                        unsigned int i1 = first + j + 1;
                        unsigned int i2 = first + j + 3;
                        unsigned int i3 = first + j + 2;

                        // 第一个三角形 (0,1,2)
                        meshData.Indices.push_back(baseIndex + i0);
                        meshData.Indices.push_back(baseIndex + i1);
                        meshData.Indices.push_back(baseIndex + i2);

                        // 第二个三角形 (0,2,3)
                        meshData.Indices.push_back(baseIndex + i0);
                        meshData.Indices.push_back(baseIndex + i2);
                        meshData.Indices.push_back(baseIndex + i3);
                    }
                }
            }

            // 辅助方法：处理DrawArrayLengths（高级模式）
            void processDrawArrayLengths(osg::DrawArrayLengths *dal, unsigned int baseIndex, GLenum mode)
            {
                unsigned int offset = dal->getFirst();

                if (mode == GL_TRIANGLE_STRIP)
                {
                    // 三角形带模式
                    for (auto it = dal->begin(); it != dal->end(); ++it)
                    {
                        unsigned int length = *it;
                        if (length >= 3)
                        {
                            for (unsigned int j = 0; j <= length - 3; ++j)
                            {
                                if (j % 2 == 0)
                                {
                                    meshData.Indices.push_back(baseIndex + offset + j);
                                    meshData.Indices.push_back(baseIndex + offset + j + 1);
                                    meshData.Indices.push_back(baseIndex + offset + j + 2);
                                }
                                else
                                {
                                    meshData.Indices.push_back(baseIndex + offset + j);
                                    meshData.Indices.push_back(baseIndex + offset + j + 2);
                                    meshData.Indices.push_back(baseIndex + offset + j + 1);
                                }
                            }
                        }
                        offset += length;
                    }
                }
                else if (mode == GL_POLYGON || mode == GL_TRIANGLE_FAN)
                {
                    // 多边形/三角形扇模式
                    for (auto it = dal->begin(); it != dal->end(); ++it)
                    {
                        unsigned int length = *it;
                        if (length >= 3)
                        {
                            for (unsigned int j = 1; j <= length - 2; ++j)
                            {
                                meshData.Indices.push_back(baseIndex + offset);
                                meshData.Indices.push_back(baseIndex + offset + j);
                                meshData.Indices.push_back(baseIndex + offset + j + 1);
                            }
                        }
                        offset += length;
                    }
                }
                else if (mode == GL_QUADS)
                {
                    // 四边形模式（标准拆分方式，保持正确的缠绕顺序）
                    for (auto it = dal->begin(); it != dal->end(); ++it)
                    {
                        unsigned int length = *it;
                        unsigned int quadCount = length / 4;
                        for (unsigned int q = 0; q < quadCount; ++q)
                        {
                            unsigned int i0 = offset + q * 4 + 0;
                            unsigned int i1 = offset + q * 4 + 1;
                            unsigned int i2 = offset + q * 4 + 2;
                            unsigned int i3 = offset + q * 4 + 3;

                            // 第一个三角形 (0,1,2)
                            meshData.Indices.push_back(baseIndex + i0);
                            meshData.Indices.push_back(baseIndex + i1);
                            meshData.Indices.push_back(baseIndex + i2);

                            // 第二个三角形 (0,2,3)
                            meshData.Indices.push_back(baseIndex + i0);
                            meshData.Indices.push_back(baseIndex + i2);
                            meshData.Indices.push_back(baseIndex + i3);
                        }
                        offset += length;
                    }
                }
                else if (mode == GL_QUAD_STRIP)
                {
                    // 四边形带模式
                    for (auto it = dal->begin(); it != dal->end(); ++it)
                    {
                        unsigned int length = *it;
                        // 每4个顶点组成一个四边形带段
                        for (unsigned int j = 0; j + 3 < length; j += 2)
                        {
                            unsigned int i0 = offset + j;
                            unsigned int i1 = offset + j + 1;
                            unsigned int i2 = offset + j + 3;
                            unsigned int i3 = offset + j + 2;

                            // 第一个三角形 (0,1,2)
                            meshData.Indices.push_back(baseIndex + i0);
                            meshData.Indices.push_back(baseIndex + i1);
                            meshData.Indices.push_back(baseIndex + i2);

                            // 第二个三角形 (0,2,3)
                            meshData.Indices.push_back(baseIndex + i0);
                            meshData.Indices.push_back(baseIndex + i2);
                            meshData.Indices.push_back(baseIndex + i3);
                        }
                        offset += length;
                    }
                }
                else if (mode == GL_TRIANGLES)
                {
                    // 三角形模式：直接复制索引
                    for (auto it = dal->begin(); it != dal->end(); ++it)
                    {
                        unsigned int length = *it;
                        for (unsigned int j = 0; j < length; ++j)
                        {
                            meshData.Indices.push_back(baseIndex + offset + j);
                        }
                        offset += length;
                    }
                }
                else
                {
                    // 其他未知模式：直接复制索引（可能需要后续处理）
                    for (auto it = dal->begin(); it != dal->end(); ++it)
                    {
                        unsigned int length = *it;
                        for (unsigned int j = 0; j < length; ++j)
                        {
                            meshData.Indices.push_back(baseIndex + offset + j);
                        }
                        offset += length;
                    }
                }
            }
        };

        // 辅助函数:合并两个MeshData
        static void MergeMeshData(MeshData &target, const MeshData &source)
        {
            if (source.VertexCount == 0)
                return; // 空数据,跳过

            unsigned int baseIndex = target.Vertices.size() / 3;

            // 合并顶点
            target.Vertices.insert(target.Vertices.end(), source.Vertices.begin(), source.Vertices.end());
            target.Normals.insert(target.Normals.end(), source.Normals.begin(), source.Normals.end());
            target.TexCoords.insert(target.TexCoords.end(), source.TexCoords.begin(), source.TexCoords.end());

            // 合并索引(需要偏移)
            for (auto idx : source.Indices)
            {
                target.Indices.push_back(baseIndex + idx);
            }

            // 合并纹理(需要建立映射)
            std::map<int, int> textureIndexMap;
            for (size_t i = 0; i < source.Textures.size(); ++i)
            {
                textureIndexMap[i] = target.Textures.size();
                target.Textures.push_back(source.Textures[i]);
            }

            // 合并材质(需要更新纹理索引映射)
            std::map<int, int> materialIndexMap;
            for (size_t i = 0; i < source.Materials.size(); ++i)
            {
                MaterialData mat = source.Materials[i];
                // 更新纹理索引
                if (mat.TextureIndex >= 0 && textureIndexMap.count(mat.TextureIndex))
                {
                    mat.TextureIndex = textureIndexMap[mat.TextureIndex];
                }
                materialIndexMap[i] = target.Materials.size();
                target.Materials.push_back(mat);
            }

            // 合并面材质索引(需要更新材质索引映射)
            for (auto matIdx : source.FaceMaterialIndices)
            {
                if (matIdx >= 0 && materialIndexMap.count(matIdx))
                {
                    target.FaceMaterialIndices.push_back(materialIndexMap[matIdx]);
                }
                else
                {
                    target.FaceMaterialIndices.push_back(matIdx);
                }
            }

            // 更新包围盒
            target.BBoxMinX = std::min(target.BBoxMinX, source.BBoxMinX);
            target.BBoxMinY = std::min(target.BBoxMinY, source.BBoxMinY);
            target.BBoxMinZ = std::min(target.BBoxMinZ, source.BBoxMinZ);
            target.BBoxMaxX = std::max(target.BBoxMaxX, source.BBoxMaxX);
            target.BBoxMaxY = std::max(target.BBoxMaxY, source.BBoxMaxY);
            target.BBoxMaxZ = std::max(target.BBoxMaxZ, source.BBoxMaxZ);

            // 更新统计信息
            target.VertexCount = target.Vertices.size() / 3;
            target.FaceCount = target.Indices.size() / 3;
            target.TextureCount = target.Textures.size();
            target.MaterialCount = target.Materials.size();
        }

        MeshData OsgbReader::LoadAndConvertToMesh(const std::string &filePath, bool loadAllLevels, int maxDepth)
        {
#ifdef WIN32
            // 保存当前 locale
            std::locale oldLocale;

            // 根据字符编码设置 locale（用于处理中文路径等）
            if (m_charset == Charset::GB18030)
            {
                oldLocale = std::locale::global(std::locale(".936"));
            }
            else if (m_charset == Charset::ShiftJIS)
            {
                oldLocale = std::locale::global(std::locale(".932"));
            }
#endif

            try
            {
                // 1. 文件验证
                if (!ValidateFile(filePath))
                {
                    return MeshData();
                }

                // 2. 设置读取选项
                osg::ref_ptr<osgDB::Options> options = new osgDB::Options();
                options->setOptionString("noTriStripPolygons");

                // 3. 读取文件
                m_impl->rootNode = osgDB::readNodeFile(filePath, options.get());

                if (!m_impl->rootNode)
                {
                    m_lastError = "Failed to load file: " + filePath;
#ifdef WIN32
                    std::locale::global(oldLocale);
#endif
                    return MeshData();
                }

                // 4. 提取网格数据
                MeshExtractorVisitor visitor;
                m_impl->rootNode->accept(visitor);

                // 5. 完成处理（保存变换信息）
                visitor.finalize();

                // 6. 统计信息
                visitor.meshData.TextureCount = visitor.meshData.Textures.size();
                visitor.meshData.MaterialCount = visitor.meshData.Materials.size();

                // 7. 递归加载子层级(如果启用)
                if (loadAllLevels)
                {
                    // 提取基础目录
                    std::filesystem::path fullPath(filePath);
                    std::string baseDir = fullPath.parent_path().string();

                    // 使用PagedLODVisitor提取所有子文件引用
                    PagedLODVisitor lodVisitor(baseDir);
                    m_impl->rootNode->accept(lodVisitor);

                    // 递归加载每个子文件
                    std::set<std::string> processedFiles; // 防止循环引用
                    processedFiles.insert(filePath);      // 标记当前文件已处理

                    int currentDepth = 1;
                    std::set<std::string> currentLevelFiles = lodVisitor.childFiles;

                    while (!currentLevelFiles.empty() && (maxDepth == 0 || currentDepth <= maxDepth))
                    {
                        std::set<std::string> nextLevelFiles;

                        for (const auto &childFile : currentLevelFiles)
                        {
                            if (processedFiles.count(childFile))
                                continue; // 跳过已处理的文件
                            processedFiles.insert(childFile);

                            // 读取子文件
                            osg::ref_ptr<osg::Node> childNode = osgDB::readNodeFile(childFile, options.get());
                            if (!childNode)
                                continue;

                            // 提取子文件的网格数据
                            MeshExtractorVisitor childVisitor;
                            childNode->accept(childVisitor);
                            childVisitor.finalize();

                            // 合并到主网格数据
                            MergeMeshData(visitor.meshData, childVisitor.meshData);

                            // 提取子文件的PagedLOD引用
                            PagedLODVisitor childLodVisitor(baseDir);
                            childNode->accept(childLodVisitor);
                            for (const auto &grandChild : childLodVisitor.childFiles)
                            {
                                if (!processedFiles.count(grandChild))
                                {
                                    nextLevelFiles.insert(grandChild);
                                }
                            }
                        }

                        currentLevelFiles = nextLevelFiles;
                        currentDepth++;
                    }

                    // 重新计算统计信息
                    visitor.meshData.TextureCount = visitor.meshData.Textures.size();
                    visitor.meshData.MaterialCount = visitor.meshData.Materials.size();
                }

                // 8. 计算内存使用
                visitor.meshData.CalculateMemoryUsage();

#ifdef WIN32
                // 恢复 locale
                std::locale::global(oldLocale);
#endif

                return visitor.meshData;
            }
            catch (const osg::ref_ptr<osgDB::InputException> &)
            {
                // OSG 专用异常 - 通常表示文件数据损坏
                m_lastError = "OSG Exception: File data is corrupted or invalid";

                // 清理资源
                if (m_impl->rootNode)
                {
                    m_impl->rootNode = nullptr;
                }

#ifdef WIN32
                std::locale::global(oldLocale);
#endif

                return MeshData();
            }
            catch (const std::exception &e)
            {
                m_lastError = std::string("Exception: ") + e.what();

#ifdef WIN32
                std::locale::global(oldLocale);
#endif

                return MeshData();
            }
            catch (...)
            {
                m_lastError = "Unknown exception occurred";

#ifdef WIN32
                std::locale::global(oldLocale);
#endif

                return MeshData();
            }
        }

        std::vector<TextureData> OsgbReader::ExtractTexturesOnly(const std::string &filePath)
        {
            try
            {
                // 文件验证
                if (!ValidateFile(filePath))
                {
                    return {};
                }

                // 读取文件
                m_impl->rootNode = osgDB::readNodeFile(filePath);

                if (!m_impl->rootNode)
                {
                    m_lastError = "Failed to load file: " + filePath;
                    return {};
                }

                // 提取纹理
                MeshExtractorVisitor visitor;
                m_impl->rootNode->accept(visitor);

                return visitor.meshData.Textures;
            }
            catch (const osg::ref_ptr<osgDB::InputException> &)
            {
                m_lastError = "OSG Exception: File data is corrupted or invalid";
                return {};
            }
            catch (const std::exception &e)
            {
                m_lastError = std::string("Exception: ") + e.what();
                return {};
            }
        }

        bool OsgbReader::SaveTexture(const TextureData &texture, const std::string &outputPath)
        {
            try
            {
                osg::ref_ptr<osg::Image> image = new osg::Image();

                // 根据纹理格式设置图像格式
                GLenum pixelFormat;
                GLenum dataType = GL_UNSIGNED_BYTE;

                if (texture.IsCompressed)
                {
                    // 压缩纹理格式
                    switch (texture.CompressionType)
                    {
                    case 1:
                        pixelFormat = texture.Components == 4 ? GL_COMPRESSED_RGBA_S3TC_DXT1_EXT : GL_COMPRESSED_RGB_S3TC_DXT1_EXT;
                        break;
                    case 3:
                        pixelFormat = GL_COMPRESSED_RGBA_S3TC_DXT3_EXT;
                        break;
                    case 5:
                        pixelFormat = GL_COMPRESSED_RGBA_S3TC_DXT5_EXT;
                        break;
                    default:
                        m_lastError = "Unsupported compression type: " +
                                      std::to_string(texture.CompressionType);
                        return false;
                    }
                }
                else
                {
                    // 未压缩格式
                    switch (texture.Components)
                    {
                    case 1:
                        pixelFormat = GL_LUMINANCE;
                        break;
                    case 2:
                        pixelFormat = GL_LUMINANCE_ALPHA;
                        break;
                    case 3:
                        pixelFormat = GL_RGB;
                        break;
                    case 4:
                        pixelFormat = GL_RGBA;
                        break;
                    default:
                        m_lastError = "Unsupported component count: " +
                                      std::to_string(texture.Components);
                        return false;
                    }
                }

                // 设置图像数据
                image->setImage(
                    texture.Width,
                    texture.Height,
                    1,
                    pixelFormat,
                    pixelFormat,
                    dataType,
                    const_cast<unsigned char *>(texture.ImageData.data()),
                    osg::Image::NO_DELETE);

                // 写入文件
                bool success = osgDB::writeImageFile(*image, outputPath);

                if (!success)
                {
                    m_lastError = "Failed to write texture: " + outputPath;
                }

                return success;
            }
            catch (const std::exception &e)
            {
                m_lastError = std::string("Exception saving texture: ") + e.what();
                return false;
            }
        }

        // ==================== PagedLOD 层次结构加载功能 ====================

        /// <summary>
        /// 辅助函数：从 OSGB 文件名提取 LOD 层级编号
        /// 参考 E:\Code\3dtiles\src\osgb23dtile.cpp 的 get_lvl_num 实现
        /// 示例：Tile_L17_X123_Y456.osgb -> 17
        ///       Data/Tile_L02_X0_Y0.osgb -> 2
        ///       Tile.osgb -> -1 (未找到)
        /// </summary>
        static int ExtractLODLevel(const std::string &filePath)
        {
            // 提取文件名（不含路径）
            std::filesystem::path path(filePath);
            std::string filename = path.stem().string(); // 不含扩展名

            // 查找 "_L" 标记
            size_t p0 = filename.find("_L");
            if (p0 == std::string::npos)
            {
                // 如果没有找到 "_L"，返回 -1
                return -1;
            }

            // 查找下一个 "_" 标记
            size_t p1 = filename.find("_", p0 + 2);
            if (p1 == std::string::npos)
            {
                return -1;
            }

            // 提取 "_L" 和下一个 "_" 之间的数字
            std::string substr = filename.substr(p0 + 2, p1 - p0 - 2);

            try
            {
                return std::stol(substr);
            }
            catch (...)
            {
                return -1;
            }
        }

        /// <summary>
        /// PagedLOD 层次结构访问器
        /// 收集 PagedLOD 节点的子文件引用和几何误差信息
        /// </summary>
        class PagedLODHierarchyVisitor : public osg::NodeVisitor
        {
        public:
            struct LODNodeInfo
            {
                std::string fileName;       // 子文件路径
                double geometricError;      // 几何误差
                double minRange;            // 最小可见距离
                double maxRange;            // 最大可见距离
            };

            std::vector<LODNodeInfo> childNodes;
            std::string basePath;

            PagedLODHierarchyVisitor(const std::string &baseDir)
                : osg::NodeVisitor(osg::NodeVisitor::TRAVERSE_ALL_CHILDREN),
                  basePath(baseDir) {}

            void apply(osg::PagedLOD &node) override
            {
                // 遍历 PagedLOD 的子文件
                for (unsigned int i = 0; i < node.getNumFileNames(); ++i)
                {
                    std::string filename = node.getFileName(i);
                    if (!filename.empty())
                    {
                        // 构造完整路径
                        std::filesystem::path fullPath = std::filesystem::path(basePath) / filename;
                        std::string fullPathStr = fullPath.string();

                        // 检查文件是否存在
                        if (std::filesystem::exists(fullPath))
                        {
                            LODNodeInfo info;
                            info.fileName = fullPathStr;

                            // 提取范围信息
                            if (i < node.getNumRanges())
                            {
                                info.minRange = node.getMinRange(i);
                                info.maxRange = node.getMaxRange(i);

                                // 几何误差使用 maxRange（参考 osgb23dtile.cpp）
                                info.geometricError = info.maxRange;
                            }
                            else
                            {
                                info.minRange = 0.0;
                                info.maxRange = 0.0;
                                info.geometricError = 0.0;
                            }

                            childNodes.push_back(info);
                        }
                    }
                }

                // 继续遍历
                traverse(node);
            }
        };

        /// <summary>
        /// 递归加载 PagedLOD 层次结构
        /// 参考 E:\Code\3dtiles\src\osgb23dtile.cpp 的 get_all_tree 实现
        /// </summary>
        static PagedLODNodeData LoadLODNodeRecursive(
            const std::string &filePath,
            const std::string &rootPath,
            int currentDepth,
            int maxDepth,
            Charset charset,
            std::string &lastError,
            std::set<std::string> &visitedFiles)
        {
            PagedLODNodeData nodeData;
            nodeData.FileName = filePath;

            // 检查循环引用
            std::filesystem::path absPath = std::filesystem::absolute(filePath);
            std::string absPathStr = absPath.string();
            if (visitedFiles.find(absPathStr) != visitedFiles.end())
            {
                lastError = "Detected circular reference: " + absPathStr;
                return nodeData;
            }
            visitedFiles.insert(absPathStr);

            // 计算相对路径
            std::filesystem::path rootAbsPath = std::filesystem::absolute(rootPath);
            nodeData.RelativePath = std::filesystem::relative(absPath, rootAbsPath.parent_path()).string();

            // 提取 LOD 层级
            nodeData.Level = ExtractLODLevel(filePath);

            // 检查递归深度（maxDepth=0 表示无限制）
            if (maxDepth > 0 && currentDepth >= maxDepth)
            {
                return nodeData;
            }

            // 加载当前文件的网格数据
            try
            {
#ifdef WIN32
                // 保存当前 locale
                std::locale oldLocale;

                // 根据字符编码设置 locale（用于处理中文路径等）
                if (charset == Charset::GB18030)
                {
                    oldLocale = std::locale::global(std::locale(".936"));
                }
                else if (charset == Charset::ShiftJIS)
                {
                    oldLocale = std::locale::global(std::locale(".932"));
                }
#endif

                // 读取文件
                osg::ref_ptr<osgDB::Options> options = new osgDB::Options();
                options->setOptionString("noTriStripPolygons");

                osg::ref_ptr<osg::Node> node = osgDB::readNodeFile(filePath, options.get());

                if (!node)
                {
                    lastError = "Failed to load file: " + filePath;
#ifdef WIN32
                    std::locale::global(oldLocale);
#endif
                    return nodeData;
                }

                // 提取网格数据
                MeshExtractorVisitor visitor;
                node->accept(visitor);
                visitor.finalize();

                nodeData.MeshData = visitor.meshData;
                nodeData.MeshData.CalculateMemoryUsage();

                // 收集 PagedLOD 子节点信息
                std::filesystem::path parentPath = std::filesystem::path(filePath).parent_path();
                PagedLODHierarchyVisitor lodVisitor(parentPath.string());
                node->accept(lodVisitor);

                // 递归加载子节点
                for (const auto &childInfo : lodVisitor.childNodes)
                {
                    PagedLODNodeData childNode = LoadLODNodeRecursive(
                        childInfo.fileName,
                        rootPath,
                        currentDepth + 1,
                        maxDepth,
                        charset,
                        lastError,
                        visitedFiles);

                    // 几何误差使用 maxRange（参考 osgb23dtile.cpp）
                    childNode.GeometricError = childInfo.maxRange;
                    nodeData.Children.push_back(childNode);
                }

#ifdef WIN32
                std::locale::global(oldLocale);
#endif
            }
            catch (const std::exception &e)
            {
                lastError = std::string("Exception loading LOD node: ") + e.what();
            }

            return nodeData;
        }

        /// <summary>
        /// 加载 OSGB 文件的 PagedLOD 层次结构（用于 3DTiles 分层切片）
        /// 参考 E:\Code\3dtiles\src\osgb23dtile.cpp 的 get_all_tree 实现
        /// </summary>
        std::vector<PagedLODNodeData> OsgbReader::LoadWithLODHierarchy(
            const std::string &filePath,
            int maxDepth)
        {
            std::vector<PagedLODNodeData> result;
            std::set<std::string> visitedFiles; // 用于检测循环引用

            // 验证文件
            if (!ValidateFile(filePath))
            {
                return result;
            }

            try
            {
                // 递归加载层次结构
                PagedLODNodeData rootNode = LoadLODNodeRecursive(
                    filePath,
                    filePath,
                    0,
                    maxDepth,
                    m_charset,
                    m_lastError,
                    visitedFiles);

                // 将层次结构展开为扁平列表（前序遍历）
                std::function<void(const PagedLODNodeData &)> flattenHierarchy;
                flattenHierarchy = [&](const PagedLODNodeData &node)
                {
                    result.push_back(node);
                    for (const auto &child : node.Children)
                    {
                        flattenHierarchy(child);
                    }
                };

                flattenHierarchy(rootNode);
            }
            catch (const std::exception &e)
            {
                m_lastError = std::string("Exception loading LOD hierarchy: ") + e.what();
            }

            return result;
        }

    } // namespace Native
} // namespace RealScene3D
