#pragma once

#include <string>
#include <vector>
#include <memory>

namespace RealScene3D {
namespace Native {

/// <summary>
/// 纹理数据结构
/// </summary>
struct TextureData {
    std::vector<unsigned char> ImageData;  // 图像原始数据
    int Width;                              // 宽度
    int Height;                             // 高度
    int Components;                         // 颜色通道数（3=RGB, 4=RGBA）
    std::string Format;                     // 格式（"RGB", "RGBA" 等）
    std::string Name;                       // 纹理名称
};

/// <summary>
/// 材质数据结构
/// </summary>
struct MaterialData {
    std::string Name;                       // 材质名称
    float DiffuseR, DiffuseG, DiffuseB;    // 漫反射颜色
    float SpecularR, SpecularG, SpecularB; // 镜面反射颜色
    float Shininess;                        // 光泽度
    int TextureIndex;                       // 纹理索引（-1 表示无纹理）
};

/// <summary>
/// 完整网格数据结构（可直接转换为 C# IMesh）
/// </summary>
struct MeshData {
    std::vector<float> Vertices;            // 顶点坐标 (x,y,z)
    std::vector<float> Normals;             // 法线 (nx,ny,nz)
    std::vector<float> TexCoords;           // 纹理坐标 (u,v)
    std::vector<unsigned int> Indices;      // 面索引
    std::vector<TextureData> Textures;      // 纹理列表
    std::vector<MaterialData> Materials;    // 材质列表

    // 包围盒
    float BBoxMinX, BBoxMinY, BBoxMinZ;
    float BBoxMaxX, BBoxMaxY, BBoxMaxZ;

    // 统计信息
    int VertexCount;
    int FaceCount;
    int TextureCount;
    int MaterialCount;
};

/// <summary>
/// OSGB 文件读取器（C++ 原生实现）
/// 完整封装 OpenSceneGraph，提供直接读取 OSGB 为网格数据的功能
/// </summary>
class OsgbReader {
public:
    OsgbReader();
    ~OsgbReader();

    /// <summary>
    /// 直接加载 OSGB 文件并转换为网格数据
    /// 这是主要接口，一次性完成读取和转换
    /// </summary>
    MeshData LoadAndConvertToMesh(const std::string& filePath);

    /// <summary>
    /// 仅提取纹理数据（如果只需要纹理）
    /// </summary>
    std::vector<TextureData> ExtractTexturesOnly(const std::string& filePath);

    /// <summary>
    /// 保存纹理到文件
    /// </summary>
    bool SaveTexture(const TextureData& texture, const std::string& outputPath);

    /// <summary>
    /// 获取最后的错误信息
    /// </summary>
    std::string GetLastError() const { return m_lastError; }

private:
    class Impl;
    std::unique_ptr<Impl> m_impl;
    std::string m_lastError;
};

} // namespace Native
} // namespace RealScene3D
