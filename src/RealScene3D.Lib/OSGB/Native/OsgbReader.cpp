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
#include <osg/ComputeBoundsVisitor>
#include <sstream>
#include <unordered_map>
#include <map>

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

// 访问器 - 直接提取原始数据
class MeshExtractorVisitor : public osg::NodeVisitor {
public:
    MeshData meshData;
    std::unordered_map<osg::Texture*, int> textureIndexMap;

    MeshExtractorVisitor() : osg::NodeVisitor(osg::NodeVisitor::TRAVERSE_ALL_CHILDREN) {
        meshData.BBoxMinX = meshData.BBoxMinY = meshData.BBoxMinZ = std::numeric_limits<float>::max();
        meshData.BBoxMaxX = meshData.BBoxMaxY = meshData.BBoxMaxZ = std::numeric_limits<float>::lowest();
        meshData.VertexCount = 0;
        meshData.FaceCount = 0;
    }

    void apply(osg::Geode& geode) override {
        for (unsigned int i = 0; i < geode.getNumDrawables(); ++i) {
            osg::Geometry* geom = geode.getDrawable(i)->asGeometry();
            if (geom) {
                extractGeometry(geom);
            }
        }
        traverse(geode);
    }

private:
    void extractGeometry(osg::Geometry* geom) {
        osg::Vec3Array* vertices = dynamic_cast<osg::Vec3Array*>(geom->getVertexArray());
        if (!vertices || vertices->empty()) return;

        unsigned int baseIndex = meshData.Vertices.size() / 3;
        unsigned int vertexCount = vertices->size();

        // 提取顶点
        for (const auto& v : *vertices) {
            meshData.Vertices.push_back(v.x());
            meshData.Vertices.push_back(v.y());
            meshData.Vertices.push_back(v.z());

            meshData.BBoxMinX = std::min(meshData.BBoxMinX, v.x());
            meshData.BBoxMinY = std::min(meshData.BBoxMinY, v.y());
            meshData.BBoxMinZ = std::min(meshData.BBoxMinZ, v.z());
            meshData.BBoxMaxX = std::max(meshData.BBoxMaxX, v.x());
            meshData.BBoxMaxY = std::max(meshData.BBoxMaxY, v.y());
            meshData.BBoxMaxZ = std::max(meshData.BBoxMaxZ, v.z());
        }

        // 提取法线 - 正确处理绑定模式
        osg::Vec3Array* normals = dynamic_cast<osg::Vec3Array*>(geom->getNormalArray());
        if (normals && !normals->empty()) {
            osg::Geometry::AttributeBinding normalBinding = geom->getNormalBinding();

            if (normalBinding == osg::Geometry::BIND_PER_VERTEX) {
                // 每个顶点一个法线
                for (const auto& n : *normals) {
                    meshData.Normals.push_back(n.x());
                    meshData.Normals.push_back(n.y());
                    meshData.Normals.push_back(n.z());
                }
            } else if (normalBinding == osg::Geometry::BIND_OVERALL && normals->size() > 0) {
                // 整个geometry共用一个法线，需要扩展到所有顶点
                const osg::Vec3& n = (*normals)[0];
                for (unsigned int i = 0; i < vertexCount; ++i) {
                    meshData.Normals.push_back(n.x());
                    meshData.Normals.push_back(n.y());
                    meshData.Normals.push_back(n.z());
                }
            }
        }

        // 法线数量不足时补充默认法线
        while (meshData.Normals.size() < (meshData.Vertices.size())) {
            meshData.Normals.push_back(0.0f);
            meshData.Normals.push_back(1.0f);
            meshData.Normals.push_back(0.0f);
        }

        // 提取纹理坐标 - 正确处理绑定模式
        osg::Vec2Array* texCoords = dynamic_cast<osg::Vec2Array*>(geom->getTexCoordArray(0));
        if (texCoords && !texCoords->empty()) {
            // OSG 2.x 使用 getTexCoordArrayList，OSG 3.x 使用不同API
            // 纹理坐标通常是 BIND_PER_VERTEX
            for (const auto& tc : *texCoords) {
                meshData.TexCoords.push_back(tc.x());
                meshData.TexCoords.push_back(tc.y());
            }
        }

        // 纹理坐标数量不足时补充默认值
        while (meshData.TexCoords.size() < (meshData.Vertices.size() / 3 * 2)) {
            meshData.TexCoords.push_back(0.0f);
            meshData.TexCoords.push_back(0.0f);
        }

        // 提取索引 - 直接遍历PrimitiveSet
        for (unsigned int i = 0; i < geom->getNumPrimitiveSets(); ++i) {
            osg::PrimitiveSet* ps = geom->getPrimitiveSet(i);
            if (!ps) continue;

            GLenum mode = ps->getMode();

            // 处理DrawElementsUInt
            osg::DrawElementsUInt* deui = dynamic_cast<osg::DrawElementsUInt*>(ps);
            if (deui) {
                if (mode == GL_TRIANGLES) {
                    for (size_t j = 0; j < deui->size(); ++j) {
                        meshData.Indices.push_back(baseIndex + (*deui)[j]);
                    }
                } else if (mode == GL_TRIANGLE_STRIP) {
                    for (size_t j = 0; j + 2 < deui->size(); ++j) {
                        if (j % 2 == 0) {
                            meshData.Indices.push_back(baseIndex + (*deui)[j]);
                            meshData.Indices.push_back(baseIndex + (*deui)[j + 1]);
                            meshData.Indices.push_back(baseIndex + (*deui)[j + 2]);
                        } else {
                            meshData.Indices.push_back(baseIndex + (*deui)[j]);
                            meshData.Indices.push_back(baseIndex + (*deui)[j + 2]);
                            meshData.Indices.push_back(baseIndex + (*deui)[j + 1]);
                        }
                    }
                } else if (mode == GL_TRIANGLE_FAN) {
                    for (size_t j = 1; j + 1 < deui->size(); ++j) {
                        meshData.Indices.push_back(baseIndex + (*deui)[0]);
                        meshData.Indices.push_back(baseIndex + (*deui)[j]);
                        meshData.Indices.push_back(baseIndex + (*deui)[j + 1]);
                    }
                }
                continue;
            }

            // 处理DrawElementsUShort
            osg::DrawElementsUShort* deus = dynamic_cast<osg::DrawElementsUShort*>(ps);
            if (deus) {
                if (mode == GL_TRIANGLES) {
                    for (size_t j = 0; j < deus->size(); ++j) {
                        meshData.Indices.push_back(baseIndex + (*deus)[j]);
                    }
                } else if (mode == GL_TRIANGLE_STRIP) {
                    for (size_t j = 0; j + 2 < deus->size(); ++j) {
                        if (j % 2 == 0) {
                            meshData.Indices.push_back(baseIndex + (*deus)[j]);
                            meshData.Indices.push_back(baseIndex + (*deus)[j + 1]);
                            meshData.Indices.push_back(baseIndex + (*deus)[j + 2]);
                        } else {
                            meshData.Indices.push_back(baseIndex + (*deus)[j]);
                            meshData.Indices.push_back(baseIndex + (*deus)[j + 2]);
                            meshData.Indices.push_back(baseIndex + (*deus)[j + 1]);
                        }
                    }
                } else if (mode == GL_TRIANGLE_FAN) {
                    for (size_t j = 1; j + 1 < deus->size(); ++j) {
                        meshData.Indices.push_back(baseIndex + (*deus)[0]);
                        meshData.Indices.push_back(baseIndex + (*deus)[j]);
                        meshData.Indices.push_back(baseIndex + (*deus)[j + 1]);
                    }
                }
                continue;
            }

            // 处理DrawArrays
            osg::DrawArrays* da = dynamic_cast<osg::DrawArrays*>(ps);
            if (da) {
                unsigned int first = da->getFirst();
                unsigned int count = da->getCount();

                if (mode == GL_TRIANGLES) {
                    for (unsigned int j = 0; j < count; ++j) {
                        meshData.Indices.push_back(baseIndex + first + j);
                    }
                } else if (mode == GL_TRIANGLE_STRIP) {
                    for (unsigned int j = 0; j + 2 < count; ++j) {
                        if (j % 2 == 0) {
                            meshData.Indices.push_back(baseIndex + first + j);
                            meshData.Indices.push_back(baseIndex + first + j + 1);
                            meshData.Indices.push_back(baseIndex + first + j + 2);
                        } else {
                            meshData.Indices.push_back(baseIndex + first + j);
                            meshData.Indices.push_back(baseIndex + first + j + 2);
                            meshData.Indices.push_back(baseIndex + first + j + 1);
                        }
                    }
                } else if (mode == GL_TRIANGLE_FAN) {
                    for (unsigned int j = 1; j + 1 < count; ++j) {
                        meshData.Indices.push_back(baseIndex + first);
                        meshData.Indices.push_back(baseIndex + first + j);
                        meshData.Indices.push_back(baseIndex + first + j + 1);
                    }
                }
            }
        }

        meshData.FaceCount = meshData.Indices.size() / 3;
        meshData.VertexCount = meshData.Vertices.size() / 3;

        // 提取纹理
        osg::StateSet* ss = geom->getStateSet();
        if (ss) {
            extractTextures(ss);
        }
    }

    void extractTextures(osg::StateSet* ss) {
        for (unsigned int i = 0; i < 8; ++i) {
            osg::Texture2D* tex = dynamic_cast<osg::Texture2D*>(
                ss->getTextureAttribute(i, osg::StateAttribute::TEXTURE));

            if (tex && tex->getImage()) {
                if (textureIndexMap.find(tex) == textureIndexMap.end()) {
                    osg::Image* img = tex->getImage();
                    TextureData td;
                    td.Width = img->s();
                    td.Height = img->t();
                    td.Components = img->getPixelFormat() == GL_RGBA ? 4 : 3;
                    td.Format = td.Components == 4 ? "RGBA" : "RGB";

                    size_t dataSize = img->getTotalSizeInBytes();
                    td.ImageData.resize(dataSize);
                    std::memcpy(td.ImageData.data(), img->data(), dataSize);

                    td.Name = "texture_" + std::to_string(meshData.Textures.size()) + ".jpg";

                    textureIndexMap[tex] = meshData.Textures.size();
                    meshData.Textures.push_back(td);
                }
            }
        }
    }
};

MeshData OsgbReader::LoadAndConvertToMesh(const std::string& filePath) {
    try {
        m_impl->rootNode = osgDB::readNodeFile(filePath);

        if (!m_impl->rootNode) {
            m_lastError = "Failed to load file: " + filePath;
            return MeshData();
        }

        MeshExtractorVisitor visitor;
        m_impl->rootNode->accept(visitor);

        visitor.meshData.TextureCount = visitor.meshData.Textures.size();
        visitor.meshData.MaterialCount = 0;

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
