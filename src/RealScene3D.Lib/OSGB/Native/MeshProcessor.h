#ifndef MESH_PROCESSOR_H
#define MESH_PROCESSOR_H

#include <vector>
#include <string>
#include <osg/Geometry>

// Draco提前声明
namespace draco
{
	class Mesh;
}

/**
 * @brief 顶点数据结构
 */
struct VertexData
{
	// 顶点数据结构
	float dX, dY, dZ;

	// 法线数据
	float nX, nY, nZ;  

	// 纹理坐标
	float dU, dV; 

	VertexData() : dX(0), dY(0), dZ(0), nX(0), nY(0), nZ(0), dU(0), dV(0) {}
};

/**
 * @brief 网格简化参数
 */
struct SimplificationParams
{
	// 目标误差 (0.01 = 1%) 
	float dTargetError = 0.01f;   
	
	// 三角形目标比例 (0.5 = 50%)
	float dTargetRatio = 0.5f; 
	
	 // 是否启用网格简化
	bool bEnableSimplification = false;  

	// 是否保留纹理坐标
	bool bPreserveTextureCoords = true;  
	
	// 是否保留法线
	bool bPreserveNormals = true;         
};

// Draco 压缩参数
struct DracoCompressionParams
{
	// 位置的量化位数 (10-16)
	int nPositionQuantizationBits = 11;  

	// 法线的量化位数 (8-16)
	int nNormalQuantizationBits = 10;   

	// 纹理坐标的量化位数 (8-16)
	int nTexCoordQuantizationBits = 12; 

	// 颜色的量化位数 (8-16)
	int nGenericQuantizationBits = 8;    

	// 是否启用 Draco 压缩
	bool bEnableCompression = false;  
};

/**
 * @brief LOD级别设置
 */
class MeshProcessor
{
public:
	// 构造函数
	MeshProcessor() = default;

	// 析构函数
	~MeshProcessor() = default;

	/**
	 * @brief 将RGBA图像数据压缩为KTX2格式
	 * @param rgbaData 输入的RGBA图像数据
	 * @param nWidth 图像宽度
	 * @param nHeight 图像高度
	 * @param ktx2Data 输出的KTX2压缩数据
	 * @return true=成功, false=失败
	 */
	static bool CompressToKtx2(const std::vector<unsigned char>& rgbaData, int nWidth, int nHeight,
		std::vector<unsigned char>& ktx2Data);

	/**
	 * @brief 设置是否启用 KTX2 压缩
	 * @param bEnable true=启用, false=禁用
	 */
	static void SetKtx2CompressionFlag(bool bEnable);

	/**
	 * @brief 使用 meshoptimizer 优化和简化网格数据
	 * @param vertices 输入/输出的顶点数据
	 * @param nVertexCount 输入/输出的顶点数量
	 * @param indices 输入的索引数据
	 * @param nOriginalIndexCount 输入的原始索引数量
	 * @param simplifiedIndices 输出的简化后索引数据
	 * @param nSimplifiedIndexCount 输出的简化后索引数量
	 * @param params 网格简化参数
	 * @return true=成功, false=失败
	 * 
	 * @note 1.顶点数据结构 VertexData 必须与 meshoptimizer 期望的格式兼容。
	 * 	 	   该函数会根据提供的简化参数调整索引数量，并更新顶点和索引数组， 调用者负责确保输入数据的有效性。
	 *  	 2.该函数假设输入索引是三角形列表格式, 目前仅支持三角形列表的简化。
	 *  	 3.该函数会尝试保留法线和纹理坐标（如果存在且参数允许）, 但如果简化过程导致顶点合并，可能会影响这些属性的准确性。
	 * 		 4.调用者负责在调用前后管理顶点和索引数据的内存。
	 */
	static bool OptimizeAndSimplifyMesh(
		std::vector<VertexData>& vertices,
		size_t& nVertexCount,
		std::vector<unsigned int>& indices,
		size_t nOriginalIndexCount,
		std::vector<unsigned int>& simplifiedIndices,
		size_t& nSimplifiedIndexCount,
		const SimplificationParams& params);

	// 使用 meshoptimizer 简化网格几何体的函数
	static bool SimplifyMeshGeometry(osg::Geometry* pGeometry, const SimplificationParams& params);

	// 使用 Draco 压缩网格几何体的函数
	// 可选的输出参数允许调用者检索 Draco 属性 ID 以进行 glTF 扩展映射
	static bool CompressMeshGeometry(osg::Geometry* pGeometry, const DracoCompressionParams& params,
		std::vector<unsigned char>& compressedData, size_t& nCompressedSize,
		int* pOutPositionAttId = nullptr, int* pOutNormalAttId = nullptr);

	// 处理纹理的函数 (KTX2 压缩)
	static bool ProcessTexture(osg::Texture* pTexture, std::vector<unsigned char>& imageData, std::string& strMimeType, bool bEnableTextureCompress = false);
};


#endif // MESH_PROCESSOR_H