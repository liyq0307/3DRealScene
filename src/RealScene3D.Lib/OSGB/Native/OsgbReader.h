#pragma once

#include <string>
#include <vector>
#include <memory>

namespace RealScene3D
{
    namespace Native
    {

        /// <summary>
        /// 字符编码类型
        /// </summary>
        enum class Charset
        {
            Default,
            GB18030, // 中文简体
            ShiftJIS // 日文
        };

        /// <summary>
        /// 纹理数据结构（增强版）
        /// </summary>
        struct TextureData
        {
            std::vector<unsigned char> ImageData; // 图像原始数据
            int Width;                            // 宽度
            int Height;                           // 高度
            int Components;                       // 颜色通道数（3=RGB, 4=RGBA）
            std::string Format;                   // 格式（"RGB", "RGBA", "DXT1", "DXT3", "DXT5"）
            std::string Name;                     // 纹理名称
            bool IsCompressed;                    // 是否为压缩格式
            int CompressionType;                  // 压缩类型（1=DXT1, 3=DXT3, 5=DXT5）

            TextureData() : Width(0), Height(0), Components(0),
                            IsCompressed(false), CompressionType(0) {}
        };

        /// <summary>
        /// 材质数据结构（完整版）
        /// </summary>
        struct MaterialData
        {
            std::string Name; // 材质名称

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

            // 纹理索引（-1 表示无纹理）
            int TextureIndex;

            MaterialData() : AmbientR(0.2f), AmbientG(0.2f), AmbientB(0.2f), AmbientA(1.0f),
                             DiffuseR(0.8f), DiffuseG(0.8f), DiffuseB(0.8f), DiffuseA(1.0f),
                             SpecularR(0.0f), SpecularG(0.0f), SpecularB(0.0f), SpecularA(1.0f),
                             EmissionR(0.0f), EmissionG(0.0f), EmissionB(0.0f), EmissionA(1.0f),
                             Shininess(0.0f), TextureIndex(-1) {}
        };

        /// <summary>
        /// 矩阵变换信息
        /// </summary>
        struct TransformInfo
        {
            bool HasTransform; // 是否包含变换
            double Matrix[16]; // 4x4 矩阵（行优先）

            TransformInfo() : HasTransform(false)
            {
                // 初始化为单位矩阵
                for (int i = 0; i < 16; ++i)
                {
                    Matrix[i] = (i % 5 == 0) ? 1.0 : 0.0;
                }
            }
        };

        /// <summary>
        /// 完整网格数据结构（可直接转换为 C# IMesh）
        /// </summary>
        struct MeshData
        {
            std::vector<float> Vertices;          // 顶点坐标 (x,y,z)
            std::vector<float> Normals;           // 法线 (nx,ny,nz)
            std::vector<float> TexCoords;         // 纹理坐标 (u,v)
            std::vector<unsigned int> Indices;    // 面索引
            std::vector<TextureData> Textures;    // 纹理列表
            std::vector<MaterialData> Materials;  // 材质列表
            std::vector<int> FaceMaterialIndices; // 每个面的材质索引（方案B修复）

            // 包围盒
            float BBoxMinX, BBoxMinY, BBoxMinZ;
            float BBoxMaxX, BBoxMaxY, BBoxMaxZ;

            // 统计信息
            int VertexCount;
            int FaceCount;
            int TextureCount;
            int MaterialCount;

            // 内存使用统计（字节）
            size_t VerticesMemory;
            size_t NormalsMemory;
            size_t TexCoordsMemory;
            size_t IndicesMemory;
            size_t TexturesMemory;
            size_t TotalMemory;

            // 变换信息
            TransformInfo Transform;

            MeshData() : BBoxMinX(0), BBoxMinY(0), BBoxMinZ(0),
                         BBoxMaxX(0), BBoxMaxY(0), BBoxMaxZ(0),
                         VertexCount(0), FaceCount(0), TextureCount(0), MaterialCount(0),
                         VerticesMemory(0), NormalsMemory(0), TexCoordsMemory(0),
                         IndicesMemory(0), TexturesMemory(0), TotalMemory(0) {}

            // 计算内存使用
            void CalculateMemoryUsage()
            {
                VerticesMemory = Vertices.size() * sizeof(float);
                NormalsMemory = Normals.size() * sizeof(float);
                TexCoordsMemory = TexCoords.size() * sizeof(float);
                IndicesMemory = Indices.size() * sizeof(unsigned int);

                TexturesMemory = 0;
                for (const auto &tex : Textures)
                {
                    TexturesMemory += tex.ImageData.size();
                }

                TotalMemory = VerticesMemory + NormalsMemory + TexCoordsMemory +
                              IndicesMemory + TexturesMemory;
            }
        };

        /// <summary>
        /// OSGB 文件读取器（C++ 原生实现 - 增强版）
        /// 完整封装 OpenSceneGraph，提供直接读取 OSGB 为网格数据的功能
        ///
        /// 优化特性：
        /// - 完整的错误处理（包括 OSG 专用异常）
        /// - 字符编码支持（中文路径）
        /// - 完整材质属性提取
        /// - DXT 压缩纹理支持
        /// - 多纹理单元支持（最多8个）
        /// </summary>
        class OsgbReader
        {
        public:
            OsgbReader();
            ~OsgbReader();

            /// <summary>
            /// 直接加载 OSGB 文件并转换为网格数据
            /// 这是主要接口，一次性完成读取和转换
            /// </summary>
            /// <param name="filePath">文件路径（支持中文路径）</param>
            /// <param name="loadAllLevels">是否递归加载所有LOD层级（默认false，仅加载当前文件）</param>
            /// <param name="maxDepth">最大递归深度（0=无限制，默认0）</param>
            /// <returns>网格数据，如果失败则返回空数据</returns>
            MeshData LoadAndConvertToMesh(const std::string &filePath, bool loadAllLevels = false, int maxDepth = 0);

            /// <summary>
            /// 仅提取纹理数据（如果只需要纹理）
            /// </summary>
            std::vector<TextureData> ExtractTexturesOnly(const std::string &filePath);

            /// <summary>
            /// 保存纹理到文件
            /// </summary>
            bool SaveTexture(const TextureData &texture, const std::string &outputPath);

            /// <summary>
            /// 设置字符编码（用于处理非 ASCII 路径）
            /// </summary>
            /// <param name="charset">字符编码类型</param>
            void SetCharset(Charset charset);

            /// <summary>
            /// 获取当前字符编码设置
            /// </summary>
            Charset GetCharset() const { return m_charset; }

            /// <summary>
            /// 获取最后的错误信息
            /// </summary>
            std::string GetLastError() const { return m_lastError; }

            /// <summary>
            /// 验证文件是否存在且可读
            /// </summary>
            bool ValidateFile(const std::string &filePath);

        private:
            class Impl;
            std::unique_ptr<Impl> m_impl;
            std::string m_lastError;
            Charset m_charset;
        };

    } // namespace Native
} // namespace RealScene3D
