#include "MeshProcessor.h"
#include <osg/Texture>
#include <osg/Image>
#include <osg/Array>
#include <vector>
#include <cstdlib>

#ifdef ENABLE_KTX2
// Basis Universal头文件用于KTX2压缩
#include <basisu/encoder/basisu_comp.h>
#include <basisu/transcoder/basisu_transcoder.h>
#endif

#ifdef OPTIMIZER
// meshoptimizer用于网格简化
#include <meshoptimizer.h>
#endif

#ifdef ENABLE_DRACO
// Draco压缩头文件
#include "draco/compression/encode.h"
#include "draco/core/encoder_buffer.h"
#include "draco/mesh/mesh.h"
#endif

#include <stb_image_write.h>

// KTX2压缩标志
static bool m_bUseKtx2Compression = true;

// 使用Basis Universal将图像数据压缩为KTX2的函数
bool MeshProcessor::CompressToKtx2(const std::vector<unsigned char>& rgbaData, int nWidth, int nHeight, std::vector<unsigned char>& ktx2Data)
{
	try
	{
		// 验证输入参数
		if (rgbaData.empty() || nWidth <= 0 || nHeight <= 0)
		{
			return false;
		}

		// 初始化Basis Universal编码器
		static bool basisu_initialized = false;
		if (!basisu_initialized)
		{
			basisu::basisu_encoder_init();
			basisu_initialized = true;
		}

		// 从RGBA数据创建basisu图像
		basisu::vector<basisu::image> sourceImages;
		sourceImages.resize(1);
		sourceImages[0].init(rgbaData.data(), nWidth, nHeight, 4);

		// 使用简化API进行压缩
		size_t nCompressedSize = 0;
		// 使用ETC1S格式进行KTX2压缩
		// 标志：
		// - Quality 128 (0-255): 压缩质量
		// - cFlagKTX2: 输出KTX2格式
		// - cFlagGenMipsWrap: 生成mipmap并包装
		unsigned int nCompressionFlags = 64 | basisu::cFlagKTX2 | basisu::cFlagGenMipsWrap;

		void* pCompressedData = basisu::basis_compress(
			basist::basis_tex_format::cUASTC4x4,
			sourceImages,
			static_cast<uint32_t>(nCompressionFlags),
			1.0f,
			&nCompressedSize
		);

		// 检查压缩是否成功
		if (!pCompressedData || nCompressedSize == 0)
		{
			return false;
		}

		// 将压缩数据复制到输出向量
		ktx2Data.resize(nCompressedSize);
		memcpy(ktx2Data.data(), pCompressedData, nCompressedSize);

		// 释放压缩数据
		basisu::basis_free_data(pCompressedData);
		return true;
	}
	catch (const std::exception& e)
	{
		return false;
	}
	catch (...)
	{
		return false;
	}
}

// 设置KTX2压缩标志的函数
void MeshProcessor::SetKtx2CompressionFlag(bool bEnable)
{
	m_bUseKtx2Compression = bEnable;
}

// 辅助函数写入缓冲区数据（静态以避免重复符号）
static void write_buf(void* pContext, void* pData, int nlen)
{
	std::vector<char>* buf = (std::vector<char>*)pContext;
	buf->insert(buf->end(), (char*)pData, (char*)pData + nlen);
}

// 处理纹理的函数（KTX2压缩）
bool MeshProcessor::ProcessTexture(osg::Texture* tex, std::vector<unsigned char>& image_data, std::string& mime_type, bool enable_texture_compress)
{
	// 检查是否启用KTX2压缩
	if (enable_texture_compress)
	{
		// 使用Basis Universal处理KTX2压缩
		std::vector<unsigned char> ktx2_buf;
		int width, height;

		if (tex)
		{
			if (tex->getNumImages() > 0)
			{
				osg::Image* img = tex->getImage(0);
				if (img)
				{
					width = img->s();
					height = img->t();

					// 提取原始RGBA数据用于压缩
					std::vector<unsigned char> rgba_data;
					const GLenum format = img->getPixelFormat();
					const unsigned char* source_data = img->data();
					size_t data_size = img->getTotalSizeInBytes();

					// 检查是否需要处理行填充
					unsigned int rowStep = img->getRowStepInBytes();
					unsigned int rowSize = img->getRowSizeInBytes();
					bool hasRowPadding = (rowStep != rowSize);

					// 如需要转换为RGBA
					if (format == GL_RGBA)
					{
						if (hasRowPadding)
						{
							// 处理行填充
							rgba_data.resize(width * height * 4);
							for (int row = 0; row < height; row++)
							{
								memcpy(&rgba_data[row * width * 4],
									&source_data[row * rowStep],
									width * 4);
							}
						}
						else
						{
							rgba_data.assign(source_data, source_data + data_size);
						}
					}
					else if (format == GL_RGB)
					{
						// 将RGB转换为RGBA
						rgba_data.resize(width * height * 4);
						if (hasRowPadding)
						{
							// 处理RGB的行填充
							for (int row = 0; row < height; row++)
							{
								for (int col = 0; col < width; col++) {
									int src_idx = row * rowStep + col * 3;
									int dst_idx = (row * width + col) * 4;
									rgba_data[dst_idx + 0] = source_data[src_idx + 0];
									rgba_data[dst_idx + 1] = source_data[src_idx + 1];
									rgba_data[dst_idx + 2] = source_data[src_idx + 2];
									rgba_data[dst_idx + 3] = 255;
								}
							}
						}
						else
						{
							for (int i = 0; i < width * height; i++)
							{
								rgba_data[i * 4 + 0] = source_data[i * 3 + 0];
								rgba_data[i * 4 + 1] = source_data[i * 3 + 1];
								rgba_data[i * 4 + 2] = source_data[i * 3 + 2];
								rgba_data[i * 4 + 3] = 255;
							}
						}
					}
					else if (format == GL_BGRA)
					{
						// 将BGRA转换为RGBA
						rgba_data.resize(width * height * 4);
						if (hasRowPadding)
						{
							for (int row = 0; row < height; row++)
							{
								for (int col = 0; col < width; col++)
								{
									int src_idx = row * rowStep + col * 4;
									int dst_idx = (row * width + col) * 4;
									rgba_data[dst_idx + 0] = source_data[src_idx + 2]; // R
									rgba_data[dst_idx + 1] = source_data[src_idx + 1]; // G
									rgba_data[dst_idx + 2] = source_data[src_idx + 0]; // B
									rgba_data[dst_idx + 3] = source_data[src_idx + 3]; // A
								}
							}
						}
						else
						{
							for (int i = 0; i < width * height; i++)
							{
								rgba_data[i * 4 + 0] = source_data[i * 4 + 2]; // R
								rgba_data[i * 4 + 1] = source_data[i * 4 + 1]; // G
								rgba_data[i * 4 + 2] = source_data[i * 4 + 0]; // B
								rgba_data[i * 4 + 3] = source_data[i * 4 + 3]; // A
							}
						}
					}

					// 使用Basis Universal压缩为KTX2
					if (!rgba_data.empty())
					{
						if (CompressToKtx2(rgba_data, width, height, ktx2_buf))
						{
							// 成功压缩为KTX2
							image_data = ktx2_buf;
							mime_type = "image/ktx2";
							return true;
						}
					}
				}
			}
		}

		// 如果KTX2压缩失败，退回到JPEG
	}

	// 退回到JPEG压缩
	std::vector<unsigned char> jpeg_buf;
	int width, height;
	if (tex)
	{
		if (tex->getNumImages() > 0)
		{
			osg::Image* img = tex->getImage(0);
			if (img)
			{
				width = img->s();
				height = img->t();

				const GLenum format = img->getPixelFormat();
				const char* rgb = (const char*)(img->data());
				uint32_t rowStep = img->getRowStepInBytes();
				uint32_t rowSize = img->getRowSizeInBytes();
				switch (format)
				{
				case GL_RGBA:
					jpeg_buf.resize(width * height * 3);
					for (int i = 0; i < height; i++)
					{
						for (int j = 0; j < width; j++)
						{
							jpeg_buf[i * width * 3 + j * 3] = rgb[i * width * 4 + j * 4];
							jpeg_buf[i * width * 3 + j * 3 + 1] = rgb[i * width * 4 + j * 4 + 1];
							jpeg_buf[i * width * 3 + j * 3 + 2] = rgb[i * width * 4 + j * 4 + 2];
						}
					}
					break;
				case GL_BGRA:
					jpeg_buf.resize(width * height * 3);
					for (int i = 0; i < height; i++)
					{
						for (int j = 0; j < width; j++)
						{
							jpeg_buf[i * width * 3 + j * 3] = rgb[i * width * 4 + j * 4 + 2];
							jpeg_buf[i * width * 3 + j * 3 + 1] = rgb[i * width * 4 + j * 4 + 1];
							jpeg_buf[i * width * 3 + j * 3 + 2] = rgb[i * width * 4 + j * 4];
						}
					}
					break;
				case GL_RGB:
					for (int i = 0; i < height; i++)
					{
						for (int j = 0; j < rowSize; j++)
						{
							jpeg_buf.emplace_back(rgb[rowStep * i + j]);
						}
					}
					break;
				default:
					break;
				}
			}
		}
	}
	if (!jpeg_buf.empty())
	{
		std::vector<char> buffer_data;
		stbi_write_jpg_to_func(write_buf, &buffer_data, width, height, 3, jpeg_buf.data(), 80);
		image_data.assign(buffer_data.begin(), buffer_data.end());
		mime_type = "image/jpeg";
		return true;
	}
	else
	{
		std::vector<char> v_data(256 * 256 * 3, 255);
		width = height = 256;
		std::vector<char> buffer_data;
		stbi_write_jpg_to_func(write_buf, &buffer_data, width, height, 3, v_data.data(), 80);
		image_data.assign(buffer_data.begin(), buffer_data.end());
		mime_type = "image/jpeg";
		return true;
	}

	return false;
}

// 使用meshoptimizer优化和简化网格数据的函数
bool MeshProcessor::OptimizeAndSimplifyMesh(
	std::vector<VertexData>& vertices,
	size_t& nVertexCount,
	std::vector<unsigned int>& indices,
	size_t nOriginalIndexCount,
	std::vector<unsigned int>& simplifiedIndices,
	size_t& nSimplifiedIndexCount,
	const SimplificationParams& params)
{

	// 根据比率计算目标索引数量
	size_t nTargetIndexCount = static_cast<size_t>(nOriginalIndexCount * params.dTargetRatio);

	// 通过检查是否有顶点具有非零法线来自动检测法线是否存在
	bool hasNormals = false;
	if (params.bPreserveNormals && nVertexCount > 0)
	{
		for (size_t i = 0; i < nVertexCount; ++i)
		{
			if (vertices[i].nX != 0.0f || vertices[i].nY != 0.0f || vertices[i].nZ != 0.0f)
			{
				hasNormals = true;
				break;
			}
		}
	}

	// ============================================================================
	// 步骤1：生成顶点重映射以移除重复顶点
	// ============================================================================
	std::vector<unsigned int> remap(nVertexCount);
	size_t nUniqueVertexCount = meshopt_generateVertexRemap(
		remap.data(),
		indices.data(),
		nOriginalIndexCount,
		vertices.data(),
		nVertexCount,
		sizeof(VertexData)
	);

	// 重映射索引缓冲区
	meshopt_remapIndexBuffer(
		indices.data(),
		indices.data(),
		nOriginalIndexCount,
		remap.data()
	);

	// 重映射顶点缓冲区（位置、法线、UV一起）
	std::vector<VertexData> remappedVertices(nUniqueVertexCount);
	meshopt_remapVertexBuffer(
		remappedVertices.data(),
		vertices.data(),
		nVertexCount,
		sizeof(VertexData),
		remap.data()
	);

	// 更新顶点以使用重映射版本
	vertices = std::move(remappedVertices);
	nVertexCount = nUniqueVertexCount;

	// ============================================================================
	// 步骤2：优化顶点缓存
	// ============================================================================
	meshopt_optimizeVertexCache(
		indices.data(),
		indices.data(),
		nOriginalIndexCount,
		nVertexCount
	);

	// ============================================================================
	// 步骤3：优化过度绘制
	// ============================================================================
	meshopt_optimizeOverdraw(
		indices.data(),
		indices.data(),
		nOriginalIndexCount,
		&vertices[0].nX,
		nVertexCount,
		sizeof(VertexData),
		1.05f  // 阈值
	);

	// ============================================================================
	// 步骤4：优化顶点获取
	// ============================================================================
	meshopt_optimizeVertexFetch(
		vertices.data(),
		indices.data(),
		nOriginalIndexCount,
		vertices.data(),
		nVertexCount,
		sizeof(VertexData)
	);

	// ============================================================================
	// 步骤5：网格简化
	// ============================================================================
	// 为简化的索引分配内存（最坏情况）
	simplifiedIndices.resize(nOriginalIndexCount);

	// 使用meshopt_simplifyWithAttributes在简化期间保留法线
	float result_error = 0;

	if (hasNormals)
	{
		// 每个组件的法线权重（nx, ny, nz）
		float attribute_weights[3] = { 0.5f, 0.5f, 0.5f };

		nSimplifiedIndexCount = meshopt_simplifyWithAttributes(
			simplifiedIndices.data(),
			indices.data(),
			nOriginalIndexCount,
			&vertices[0].dX,          // 位置数据
			nVertexCount,
			sizeof(VertexData),       // 位置间距
			&vertices[0].nX,          // 法线数据
			sizeof(VertexData),       // 法线间距
			attribute_weights,
			3,                        // 3个法线组件
			nullptr,
			nTargetIndexCount,
			params.dTargetError,
			0,
			&result_error
		);
	}
	else
	{
		// 没有法线 - 使用标准简化
		nSimplifiedIndexCount = meshopt_simplify(
			simplifiedIndices.data(),
			indices.data(),
			nOriginalIndexCount,
			&vertices[0].dX,
			nVertexCount,
			sizeof(VertexData),
			nTargetIndexCount,
			params.dTargetError,
			0,
			&result_error
		);
	}

	// 调整为实际简化大小
	simplifiedIndices.resize(nSimplifiedIndexCount);

	return true;
}

// 使用meshoptimizer简化网格几何体的函数
bool MeshProcessor::SimplifyMeshGeometry(osg::Geometry* pGeometry, const SimplificationParams& params)
{
	if (!params.bEnableSimplification || !pGeometry)
	{
		return false;
	}

	// 获取顶点数组
	osg::Vec3Array* vertexArray = dynamic_cast<osg::Vec3Array*>(pGeometry->getVertexArray());
	if (!vertexArray || vertexArray->empty())
	{
		return false;
	}

	// 获取索引数组（我们需要处理不同的原始集类型）
	if (pGeometry->getNumPrimitiveSets() == 0)
	{
		return false;
	}

	// 现在，我们只处理第一个原始集
	osg::PrimitiveSet* primitiveSet = pGeometry->getPrimitiveSet(0);
	if (!primitiveSet)
	{
		return false;
	}

	// 获取顶点属性
	size_t vertex_count = vertexArray->size();

	// 如果可用且应保留，则获取法线
	osg::Vec3Array* normalArray = dynamic_cast<osg::Vec3Array*>(pGeometry->getNormalArray());
	bool hasNormals = params.bPreserveNormals && normalArray && normalArray->size() == vertex_count;

	// 如果可用且应保留，则获取纹理坐标
	osg::Vec2Array* texCoordArray = dynamic_cast<osg::Vec2Array*>(pGeometry->getTexCoordArray(0));
	bool hasTexCoords = params.bPreserveTextureCoords && texCoordArray && texCoordArray->size() == vertex_count;
	// 将OSG顶点数据转换为VertexData结构
	std::vector<VertexData> vertices(vertex_count);

	for (size_t i = 0; i < vertex_count; ++i)
	{
		// 位置
		const osg::Vec3& vertex = vertexArray->at(i);
		vertices[i].dX = vertex.x();
		vertices[i].dY = vertex.y();
		vertices[i].dZ = vertex.z();

		// 法线
		if (hasNormals)
		{
			const osg::Vec3& normal = normalArray->at(i);
			vertices[i].nX = normal.x();
			vertices[i].nY = normal.y();
			vertices[i].nZ = normal.z();
		}
		else {
			vertices[i].nX = 0.0f;
			vertices[i].nY = 0.0f;
			vertices[i].nZ = 0.0f;
		}

		// 纹理坐标
		if (hasTexCoords)
		{
			const osg::Vec2& texcoord = texCoordArray->at(i);
			vertices[i].dU = texcoord.x();
			vertices[i].dV = texcoord.y();
		}
		else {
			vertices[i].dU = 0.0f;
			vertices[i].dV = 0.0f;
		}
	}

	// 处理不同的原始集类型
	std::vector<unsigned int> indices;
	size_t original_index_count = 0;

	switch (primitiveSet->getType())
	{
	case osg::PrimitiveSet::DrawElementsUBytePrimitiveType: {
		const osg::DrawElementsUByte* drawElements = static_cast<const osg::DrawElementsUByte*>(primitiveSet);
		original_index_count = drawElements->size();
		indices.resize(original_index_count);
		for (size_t i = 0; i < original_index_count; ++i)
		{
			indices[i] = static_cast<unsigned int>(drawElements->at(i));
		}
		break;
	}
	case osg::PrimitiveSet::DrawElementsUShortPrimitiveType:
	{
		const osg::DrawElementsUShort* drawElements = static_cast<const osg::DrawElementsUShort*>(primitiveSet);
		original_index_count = drawElements->size();
		indices.resize(original_index_count);
		for (size_t i = 0; i < original_index_count; ++i)
		{
			indices[i] = static_cast<unsigned int>(drawElements->at(i));
		}
		break;
	}
	case osg::PrimitiveSet::DrawElementsUIntPrimitiveType:
	{
		const osg::DrawElementsUInt* drawElements = static_cast<const osg::DrawElementsUInt*>(primitiveSet);
		original_index_count = drawElements->size();
		indices.resize(original_index_count);
		for (size_t i = 0; i < original_index_count; ++i)
		{
			indices[i] = drawElements->at(i);
		}
		break;
	}
	case osg::PrimitiveSet::DrawArraysPrimitiveType:
	{
		// 对于DrawArrays，我们需要创建索引
		osg::DrawArrays* drawArrays = static_cast<osg::DrawArrays*>(primitiveSet);
		unsigned int first = drawArrays->getFirst();
		unsigned int count = drawArrays->getCount();
		original_index_count = count;
		indices.resize(count);
		for (unsigned int i = 0; i < count; ++i)
		{
			indices[i] = first + i;
		}
		break;
	}
	default:
		// 不支持的原始类型
		return false;
	}

	// 根据比率计算目标索引数量
	size_t target_index_count = static_cast<size_t>(original_index_count * params.dTargetRatio);

	// 使用提取的优化和简化函数
	std::vector<unsigned int> simplified_indices;
	size_t simplified_index_count = 0;

	if (!OptimizeAndSimplifyMesh(
		vertices, vertex_count,
		indices, original_index_count,
		simplified_indices, simplified_index_count,
		params))
	{
		return false;
	}

	osg::ref_ptr<osg::Vec3Array> newVertexArray = new osg::Vec3Array();
	newVertexArray->reserve(vertex_count);

	for (size_t i = 0; i < vertex_count; ++i)
	{
		newVertexArray->push_back(osg::Vec3(vertices[i].dX, vertices[i].dY, vertices[i].dZ));
	}
	pGeometry->setVertexArray(newVertexArray);

	// 如果存在则更新法线
	if (hasNormals)
	{
		osg::ref_ptr<osg::Vec3Array> newNormalArray = new osg::Vec3Array();
		newNormalArray->reserve(vertex_count);

		for (size_t i = 0; i < vertex_count; ++i)
		{
			newNormalArray->push_back(osg::Vec3(vertices[i].nX, vertices[i].nY, vertices[i].nZ));
		}
		pGeometry->setNormalArray(newNormalArray);
		pGeometry->setNormalBinding(osg::Geometry::BIND_PER_VERTEX);
	}

	// 如果存在则更新纹理坐标
	if (hasTexCoords)
	{
		osg::ref_ptr<osg::Vec2Array> newTexCoordArray = new osg::Vec2Array();
		newTexCoordArray->reserve(vertex_count);

		for (size_t i = 0; i < vertex_count; ++i)
		{
			newTexCoordArray->push_back(osg::Vec2(vertices[i].dU, vertices[i].dV));
		}
		pGeometry->setTexCoordArray(0, newTexCoordArray);
	}

	// 创建具有简化索引的新原始集
	switch (primitiveSet->getType())
	{
	case osg::PrimitiveSet::DrawElementsUBytePrimitiveType:
	{
		osg::ref_ptr<osg::DrawElementsUByte> newDrawElements = new osg::DrawElementsUByte(primitiveSet->getMode());
		for (size_t i = 0; i < simplified_index_count; ++i)
		{
			newDrawElements->push_back(static_cast<osg::DrawElementsUByte::value_type>(simplified_indices[i]));
		}
		pGeometry->setPrimitiveSet(0, newDrawElements.get());
		break;
	}
	case osg::PrimitiveSet::DrawElementsUShortPrimitiveType:
	{
		osg::ref_ptr<osg::DrawElementsUShort> newDrawElements = new osg::DrawElementsUShort(primitiveSet->getMode());
		for (size_t i = 0; i < simplified_index_count; ++i)
		{
			newDrawElements->push_back(static_cast<osg::DrawElementsUShort::value_type>(simplified_indices[i]));
		}
		pGeometry->setPrimitiveSet(0, newDrawElements.get());
		break;
	}
	case osg::PrimitiveSet::DrawElementsUIntPrimitiveType:
	{
		osg::ref_ptr<osg::DrawElementsUInt> newDrawElements = new osg::DrawElementsUInt(primitiveSet->getMode());
		for (size_t i = 0; i < simplified_index_count; ++i)
		{
			newDrawElements->push_back(simplified_indices[i]);
		}
		pGeometry->setPrimitiveSet(0, newDrawElements.get());
		break;
	}
	case osg::PrimitiveSet::DrawArraysPrimitiveType:
	{
		// For DrawArrays, we need to create a new DrawElements
		osg::ref_ptr<osg::DrawElementsUInt> newDrawElements = new osg::DrawElementsUInt(primitiveSet->getMode());
		for (size_t i = 0; i < simplified_index_count; ++i)
		{
			newDrawElements->push_back(simplified_indices[i]);
		}
		pGeometry->setPrimitiveSet(0, newDrawElements.get());
		break;
	}
	}

	return true;
}

// 使用Draco压缩网格几何体的函数
bool MeshProcessor::CompressMeshGeometry(osg::Geometry* pGeometry, const DracoCompressionParams& params,
	std::vector<unsigned char>& compressedData, size_t& nCompressedSize,
	int* out_position_att_id, int* out_normal_att_id)
{
	if (!params.bEnableCompression || !pGeometry)
	{
		return false;
	}

	// 获取顶点数组
	osg::Vec3Array* vertexArray = dynamic_cast<osg::Vec3Array*>(pGeometry->getVertexArray());
	if (!vertexArray || vertexArray->empty())
	{
		return false;
	}

	// 创建Draco网格
	std::unique_ptr<draco::Mesh> dracoMesh(new draco::Mesh());

	// 设置顶点
	const size_t vertexCount = vertexArray->size();
	dracoMesh->set_num_points(vertexCount);

	// 添加位置属性
	draco::GeometryAttribute posAttr;
	posAttr.Init(draco::GeometryAttribute::POSITION, nullptr, 3, draco::DT_FLOAT32, false, sizeof(float) * 3, 0);
	int posAttId = dracoMesh->AddAttribute(posAttr, true, vertexCount);

	// 复制顶点位置
	for (size_t i = 0; i < vertexCount; ++i)
	{
		const osg::Vec3& vertex = vertexArray->at(i);
		const float pos[3] = { static_cast<float>(vertex.x()), static_cast<float>(vertex.y()), static_cast<float>(vertex.z()) };
		dracoMesh->attribute(posAttId)->SetAttributeValue(draco::AttributeValueIndex(i), &pos[0]);
	}

	// 如果存在则处理法线
	osg::Vec3Array* normalArray = dynamic_cast<osg::Vec3Array*>(pGeometry->getNormalArray());
	int normalAttId = -1;
	if (normalArray && normalArray->size() == vertexCount)
	{
		draco::GeometryAttribute normalAttr;
		normalAttr.Init(draco::GeometryAttribute::NORMAL, nullptr, 3, draco::DT_FLOAT32, false, sizeof(float) * 3, 0);
		normalAttId = dracoMesh->AddAttribute(normalAttr, true, vertexCount);

		// 复制法线
		for (size_t i = 0; i < vertexCount; ++i)
		{
			const osg::Vec3& normal = normalArray->at(i);
			const float norm[3] = { static_cast<float>(normal.x()), static_cast<float>(normal.y()), static_cast<float>(normal.z()) };
			dracoMesh->attribute(normalAttId)->SetAttributeValue(draco::AttributeValueIndex(i), &norm[0]);
		}
	}

	// 处理原始集（索引）
	if (pGeometry->getNumPrimitiveSets() > 0)
	{
		osg::PrimitiveSet* primitiveSet = pGeometry->getPrimitiveSet(0);
		unsigned int numIndices = primitiveSet->getNumIndices();

		if (numIndices > 0)
		{
			// 为网格创建面
			std::vector<uint32_t> indices(numIndices);
			for (unsigned int i = 0; i < numIndices; ++i)
			{
				indices[i] = primitiveSet->index(i);
			}

			// 将三角形列表转换为面
			const size_t faceCount = numIndices / 3;
			dracoMesh->SetNumFaces(faceCount);

			for (size_t i = 0; i < faceCount; ++i)
			{
				draco::Mesh::Face face;
				face[0] = indices[i * 3];
				face[1] = indices[i * 3 + 1];
				face[2] = indices[i * 3 + 2];
				dracoMesh->SetFace(draco::FaceIndex(i), face);
			}
		}
	}

	// 编码网格
	draco::Encoder encoder;

	// 设置编码选项
	encoder.SetSpeedOptions(5, 5); // 默认速度选项
	encoder.SetAttributeQuantization(draco::GeometryAttribute::POSITION, params.nPositionQuantizationBits);

	if (normalArray)
	{
		encoder.SetAttributeQuantization(draco::GeometryAttribute::NORMAL, params.nNormalQuantizationBits);
	}

	// 编码网格
	draco::EncoderBuffer buffer;
	draco::Status status = encoder.EncodeMeshToBuffer(*dracoMesh, &buffer);

	if (!status.ok())
	{
		return false;
	}

	// 复制压缩数据
	nCompressedSize = buffer.size();
	compressedData.resize(nCompressedSize);
	std::memcpy(compressedData.data(), buffer.data(), nCompressedSize);

	if (out_position_att_id)
	{
		*out_position_att_id = posAttId;
	}
	if (out_normal_att_id)
	{
		*out_normal_att_id = normalAttId;
	}

	return true;
}
