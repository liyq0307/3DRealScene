#pragma once

#include "../Native/OsgbReader.h"
#include <msclr/marshal_cppstd.h>

using namespace System;
using namespace System::Collections::Generic;
using namespace System::Runtime::InteropServices;

namespace RealScene3D {
namespace Managed {

/// <summary>
/// 托管纹理数据类（增强版）
/// </summary>
public ref class ManagedTextureData {
public:
    array<Byte>^ ImageData;
    int Width;
    int Height;
    int Components;
    String^ Format;
    String^ Name;
    bool IsCompressed;      // 新增：是否为压缩格式
    int CompressionType;    // 新增：压缩类型
};

/// <summary>
/// 托管材质数据类（完整版）
/// </summary>
public ref class ManagedMaterialData {
public:
    String^ Name;

    // 环境光
    float AmbientR, AmbientG, AmbientB, AmbientA;

    // 漫反射
    float DiffuseR, DiffuseG, DiffuseB, DiffuseA;

    // 镜面反射
    float SpecularR, SpecularG, SpecularB, SpecularA;

    // 自发光
    float EmissionR, EmissionG, EmissionB, EmissionA;

    // 光泽度
    float Shininess;

    // 纹理索引
    int TextureIndex;
};

/// <summary>
/// 托管变换信息类
/// </summary>
public ref class ManagedTransformInfo {
public:
    bool HasTransform;
    array<double>^ Matrix;  // 4x4 矩阵（16个元素）

    ManagedTransformInfo() {
        Matrix = gcnew array<double>(16);
        for (int i = 0; i < 16; i++) {
            Matrix[i] = (i % 5 == 0) ? 1.0 : 0.0;
        }
    }
};

/// <summary>
/// 托管完整网格数据类（对应 C# IMesh - 增强版）
/// </summary>
public ref class ManagedMeshData {
public:
    array<float>^ Vertices;
    array<float>^ Normals;
    array<float>^ TexCoords;
    array<unsigned int>^ Indices;
    array<int>^ FaceMaterialIndices;  // 每个面的材质索引（方案B修复）
    List<ManagedTextureData^>^ Textures;
    List<ManagedMaterialData^>^ Materials;

    // 包围盒
    float BBoxMinX, BBoxMinY, BBoxMinZ;
    float BBoxMaxX, BBoxMaxY, BBoxMaxZ;

    // 统计信息
    int VertexCount;
    int FaceCount;
    int TextureCount;
    int MaterialCount;

    // 内存使用统计（字节）
    long long VerticesMemory;
    long long NormalsMemory;
    long long TexCoordsMemory;
    long long IndicesMemory;
    long long TexturesMemory;
    long long TotalMemory;

    // 变换信息
    ManagedTransformInfo^ Transform;

    ManagedMeshData() {
        Textures = gcnew List<ManagedTextureData^>();
        Materials = gcnew List<ManagedMaterialData^>();
        Transform = gcnew ManagedTransformInfo();
    }
};

/// <summary>
/// OSGB 读取器托管封装
/// 提供直接读取 OSGB 文件为网格数据的功能，无需 osgconv 转换
/// </summary>
public ref class OsgbReaderWrapper {
public:
    OsgbReaderWrapper();
    ~OsgbReaderWrapper();
    !OsgbReaderWrapper();

    /// <summary>
    /// 直接加载 OSGB 文件并转换为网格数据
    /// 这是主要接口，一次性完成读取和转换，无需 osgconv
    /// </summary>
    ManagedMeshData^ LoadAndConvertToMesh(String^ filePath);

    /// <summary>
    /// 仅提取纹理数据（如果只需要纹理）
    /// </summary>
    List<ManagedTextureData^>^ ExtractTexturesOnly(String^ filePath);

    /// <summary>
    /// 保存纹理到文件
    /// </summary>
    bool SaveTexture(ManagedTextureData^ texture, String^ outputPath);

    /// <summary>
    /// 获取最后的错误信息
    /// </summary>
    String^ GetLastError();

private:
    Native::OsgbReader* m_nativeReader;

    // 转换辅助方法
    ManagedTextureData^ ConvertTexture(const Native::TextureData& nativeTexture);
    ManagedMaterialData^ ConvertMaterial(const Native::MaterialData& nativeMaterial);
    ManagedMeshData^ ConvertMesh(const Native::MeshData& nativeMesh);
};

} // namespace Managed
} // namespace RealScene3D
