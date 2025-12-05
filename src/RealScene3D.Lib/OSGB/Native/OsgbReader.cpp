#include "OsgbReader.h"
#include <osg/Node>
#include <osg/Geode>
#include <osg/Geometry>
#include <osg/Texture2D>
#include <osg/Image>
#include <osg/Material>
#include <osgDB/ReadFile>
#include <osgDB/WriteFile>
#include <osgUtil/Optimizer>
#include <osg/ComputeBoundsVisitor>
#include <sstream>
#include <unordered_map>

namespace RealScene3D {
namespace Native {

// 内部实现类（PIMPL 模式）
class OsgbReader::Impl {
public:
    osg::ref_ptr<osg::Node> rootNode;

    Impl() : rootNode(nullptr) {}
    ~Impl() {
        rootNode = nullptr;
    }
};

OsgbReader::OsgbReader() : m_impl(std::make_unique<Impl>()) {
}

OsgbReader::~OsgbReader() = default;

// 完整网格提取访问器
class MeshExtractorVisitor : public osg::NodeVisitor {
public:
    MeshData meshData;
    std::unordered_map<osg::Texture*, int> textureIndexMap;
    std::unordered_map<osg::Material*, int> materialIndexMap;

    MeshExtractorVisitor() : osg::NodeVisitor(osg::NodeVisitor::TRAVERSE_ALL_CHILDREN) {
        // 初始化包围盒
        meshData.BBoxMinX = meshData.BBoxMinY = meshData.BBoxMinZ = std::numeric_limits<float>::max();
        meshData.BBoxMaxX = meshData.BBoxMaxY = meshData.BBoxMaxZ = std::numeric_limits<float>::lowest();
        meshData.VertexCount = 0;
        meshData.FaceCount = 0;
    }

    void apply(osg::Geode& geode) override {
        for (unsigned int i = 0; i < geode.getNumDrawables(); ++i) {
            osg::Geometry* geometry = geode.getDrawable(i)->asGeometry();
            if (geometry) {
                extractGeometry(geometry);
            }
        }
        traverse(geode);
    }

private:
    void extractGeometry(osg::Geometry* geometry) {
        unsigned int baseIndex = meshData.Vertices.size() / 3;

        // 提取顶点
        osg::Vec3Array* vertices = dynamic_cast<osg::Vec3Array*>(geometry->getVertexArray());
        if (vertices) {
            for (const auto& v : *vertices) {
                meshData.Vertices.push_back(v.x());
                meshData.Vertices.push_back(v.y());
                meshData.Vertices.push_back(v.z());

                // 更新包围盒
                meshData.BBoxMinX = std::min(meshData.BBoxMinX, v.x());
                meshData.BBoxMinY = std::min(meshData.BBoxMinY, v.y());
                meshData.BBoxMinZ = std::min(meshData.BBoxMinZ, v.z());
                meshData.BBoxMaxX = std::max(meshData.BBoxMaxX, v.x());
                meshData.BBoxMaxY = std::max(meshData.BBoxMaxY, v.y());
                meshData.BBoxMaxZ = std::max(meshData.BBoxMaxZ, v.z());
            }
            meshData.VertexCount += vertices->size();
        }

        // 提取法线
        osg::Vec3Array* normals = dynamic_cast<osg::Vec3Array*>(geometry->getNormalArray());
        if (normals) {
            for (const auto& n : *normals) {
                meshData.Normals.push_back(n.x());
                meshData.Normals.push_back(n.y());
                meshData.Normals.push_back(n.z());
            }
        } else {
            // 如果没有法线，填充默认值
            for (size_t i = 0; i < vertices->size(); ++i) {
                meshData.Normals.push_back(0.0f);
                meshData.Normals.push_back(1.0f);
                meshData.Normals.push_back(0.0f);
            }
        }

        // 提取纹理坐标
        osg::Vec2Array* texCoords = dynamic_cast<osg::Vec2Array*>(geometry->getTexCoordArray(0));
        if (texCoords) {
            for (const auto& tc : *texCoords) {
                meshData.TexCoords.push_back(tc.x());
                meshData.TexCoords.push_back(tc.y());
            }
        } else {
            // 如果没有纹理坐标，填充默认值
            for (size_t i = 0; i < vertices->size(); ++i) {
                meshData.TexCoords.push_back(0.0f);
                meshData.TexCoords.push_back(0.0f);
            }
        }

        // 提取索引
        for (unsigned int i = 0; i < geometry->getNumPrimitiveSets(); ++i) {
            osg::PrimitiveSet* primitiveSet = geometry->getPrimitiveSet(i);
            if (primitiveSet) {
                for (unsigned int j = 0; j < primitiveSet->getNumIndices(); ++j) {
                    meshData.Indices.push_back(baseIndex + primitiveSet->index(j));
                }
                meshData.FaceCount += primitiveSet->getNumIndices() / 3;
            }
        }

        // 提取材质和纹理
        osg::StateSet* stateSet = geometry->getStateSet();
        if (stateSet) {
            extractMaterialAndTexture(stateSet);
        }
    }

    void extractMaterialAndTexture(osg::StateSet* stateSet) {
        // 提取纹理
        for (unsigned int unit = 0; unit < 8; ++unit) {
            osg::Texture* texture = dynamic_cast<osg::Texture*>(
                stateSet->getTextureAttribute(unit, osg::StateAttribute::TEXTURE)
            );

            if (texture) {
                // 检查是否已经添加过这个纹理
                if (textureIndexMap.find(texture) == textureIndexMap.end()) {
                    osg::Texture2D* tex2D = dynamic_cast<osg::Texture2D*>(texture);
                    if (tex2D) {
                        osg::Image* image = tex2D->getImage();
                        if (image && image->valid()) {
                            TextureData texData;
                            texData.Width = image->s();
                            texData.Height = image->t();

                            // 确定格式
                            if (image->getPixelFormat() == GL_RGBA) {
                                texData.Components = 4;
                                texData.Format = "RGBA";
                            } else if (image->getPixelFormat() == GL_RGB) {
                                texData.Components = 3;
                                texData.Format = "RGB";
                            } else {
                                texData.Components = 3;
                                texData.Format = "RGB";
                            }

                            // 复制图像数据
                            size_t dataSize = image->getTotalSizeInBytes();
                            texData.ImageData.resize(dataSize);
                            std::memcpy(texData.ImageData.data(), image->data(), dataSize);

                            // 生成纹理名称
                            std::ostringstream oss;
                            oss << "texture_" << meshData.Textures.size() << ".jpg";
                            texData.Name = oss.str();

                            int texIndex = meshData.Textures.size();
                            meshData.Textures.push_back(texData);
                            textureIndexMap[texture] = texIndex;
                        }
                    }
                }
            }
        }

        // 提取材质
        osg::Material* material = dynamic_cast<osg::Material*>(
            stateSet->getAttribute(osg::StateAttribute::MATERIAL)
        );

        if (material) {
            if (materialIndexMap.find(material) == materialIndexMap.end()) {
                MaterialData matData;

                auto diffuse = material->getDiffuse(osg::Material::FRONT);
                matData.DiffuseR = diffuse.r();
                matData.DiffuseG = diffuse.g();
                matData.DiffuseB = diffuse.b();

                auto specular = material->getSpecular(osg::Material::FRONT);
                matData.SpecularR = specular.r();
                matData.SpecularG = specular.g();
                matData.SpecularB = specular.b();

                matData.Shininess = material->getShininess(osg::Material::FRONT);

                // 关联纹理索引
                matData.TextureIndex = -1;
                for (auto& pair : textureIndexMap) {
                    matData.TextureIndex = pair.second;
                    break; // 使用第一个纹理
                }

                std::ostringstream oss;
                oss << "material_" << meshData.Materials.size();
                matData.Name = oss.str();

                materialIndexMap[material] = meshData.Materials.size();
                meshData.Materials.push_back(matData);
            }
        }
    }
};

MeshData OsgbReader::LoadAndConvertToMesh(const std::string& filePath) {
    try {
        // 读取 OSGB 文件
        m_impl->rootNode = osgDB::readNodeFile(filePath);

        if (!m_impl->rootNode) {
            m_lastError = "Failed to load file: " + filePath;
            return MeshData();
        }

        // 优化场景图
        osgUtil::Optimizer optimizer;
        optimizer.optimize(m_impl->rootNode.get(),
            osgUtil::Optimizer::FLATTEN_STATIC_TRANSFORMS |
            osgUtil::Optimizer::MERGE_GEOMETRY |
            osgUtil::Optimizer::SHARE_DUPLICATE_STATE);

        // 提取网格数据
        MeshExtractorVisitor visitor;
        m_impl->rootNode->accept(visitor);

        // 更新统计信息
        visitor.meshData.TextureCount = visitor.meshData.Textures.size();
        visitor.meshData.MaterialCount = visitor.meshData.Materials.size();

        return visitor.meshData;
    }
    catch (const std::exception& e) {
        m_lastError = std::string("Exception: ") + e.what();
        return MeshData();
    }
}

std::vector<TextureData> OsgbReader::ExtractTexturesOnly(const std::string& filePath) {
    try {
        m_impl->rootNode = osgDB::readNodeFile(filePath);
        if (!m_impl->rootNode) {
            m_lastError = "Failed to load file: " + filePath;
            return {};
        }

        MeshExtractorVisitor visitor;
        m_impl->rootNode->accept(visitor);
        return visitor.meshData.Textures;
    }
    catch (const std::exception& e) {
        m_lastError = std::string("Exception: ") + e.what();
        return {};
    }
}

bool OsgbReader::SaveTexture(const TextureData& texture, const std::string& outputPath) {
    try {
        osg::ref_ptr<osg::Image> image = new osg::Image();

        GLenum pixelFormat = texture.Components == 4 ? GL_RGBA : GL_RGB;
        GLenum dataType = GL_UNSIGNED_BYTE;

        image->setImage(
            texture.Width,
            texture.Height,
            1,
            pixelFormat,
            pixelFormat,
            dataType,
            const_cast<unsigned char*>(texture.ImageData.data()),
            osg::Image::NO_DELETE
        );

        bool success = osgDB::writeImageFile(*image, outputPath);

        if (!success) {
            m_lastError = "Failed to write texture: " + outputPath;
        }

        return success;
    }
    catch (const std::exception& e) {
        m_lastError = std::string("Exception saving texture: ") + e.what();
        return false;
    }
}

} // namespace Native
} // namespace RealScene3D
