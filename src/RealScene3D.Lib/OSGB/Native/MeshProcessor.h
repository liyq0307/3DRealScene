#ifndef MESH_PROCESSOR_H
#define MESH_PROCESSOR_H

#include <vector>
#include <string>
#include <osg/Geometry>

// Draco声明
namespace draco
{
	class Mesh;
}

// 网格处理顶点数据
struct VertexData
{
	float x, y, z;          // 位置
	float nx, ny, nz;       // 法线
	float u, v;             // 纹理坐标

	VertexData() : x(0), y(0), z(0), nx(0), ny(0), nz(0), u(0), v(0) {}
};

// 网格简化参数
struct SimplificationParams
{
	float target_error = 0.01f;           // 目标误差 (0.01 = 1%)
	float target_ratio = 0.5f;            // 三角形目标比例 (0.5 = 50%)
	bool enable_simplification = false;   // 是否启用网格简化
	bool preserve_texture_coords = true;  // 是否保留纹理坐标
	bool preserve_normals = true;         // 是否保留法线
};

// Draco 压缩参数
struct DracoCompressionParams
{
	int position_quantization_bits = 11;  // 位置的量化位数 (10-16)
	int normal_quantization_bits = 10;    // 法线的量化位数 (8-16)
	int tex_coord_quantization_bits = 12; // 纹理坐标的量化位数 (8-16)
	int generic_quantization_bits = 8;    // 其他属性的量化位数 (8-16)
	bool enable_compression = false;      // 是否启用 Draco 压缩
};


class MeshProcessor
{
	public:
	MeshProcessor() = default;
	~MeshProcessor() = default;

	// 使用 Basis Universal 将图像数据压缩为 KTX2 的函数
	static bool CompressToKtx2(const std::vector<unsigned char>& rgba_data, int width, int height,
		std::vector<unsigned char>& ktx2_data);

	// 设置 KTX2 压缩标志的函数
	static void SetKtx2CompressionFlag(bool enable);

	// 使用 meshoptimizer 优化和简化网格数据的函数
	// 输入: 顶点、索引和优化参数
	// 输出: 优化后的顶点和简化后的索引
	static bool OptimizeAndSimplifyMesh(
		std::vector<VertexData>& vertices,
		size_t& vertex_count,
		std::vector<unsigned int>& indices,
		size_t original_index_count,
		std::vector<unsigned int>& simplified_indices,
		size_t& simplified_index_count,
		const SimplificationParams& params);

	// 使用 meshoptimizer 简化网格几何体的函数
	static bool SimplifyMeshGeometry(osg::Geometry* geometry, const SimplificationParams& params);

	// 使用 Draco 压缩网格几何体的函数
	// 可选的输出参数允许调用者检索 Draco 属性 ID 以进行 glTF 扩展映射
	static bool CompressMeshGeometry(osg::Geometry* geometry, const DracoCompressionParams& params,
		std::vector<unsigned char>& compressed_data, size_t& compressed_size,
		int* out_position_att_id = nullptr, int* out_normal_att_id = nullptr);

	// 处理纹理的函数 (KTX2 压缩)
	static bool ProcessTexture(osg::Texture* tex, std::vector<unsigned char>& image_data, std::string& mime_type, bool enable_texture_compress = false);
};


#endif // MESH_PROCESSOR_H