#include "OsgbReaderWrapper.h"

namespace RealScene3D {
namespace Managed {

OsgbReaderWrapper::OsgbReaderWrapper() {
    m_nativeReader = new Native::OsgbReader();
}

OsgbReaderWrapper::~OsgbReaderWrapper() {
    this->!OsgbReaderWrapper();
}

OsgbReaderWrapper::!OsgbReaderWrapper() {
    if (m_nativeReader != nullptr) {
        delete m_nativeReader;
        m_nativeReader = nullptr;
    }
}

ManagedMeshData^ OsgbReaderWrapper::LoadAndConvertToMesh(String^ filePath) {
    msclr::interop::marshal_context context;
    std::string nativePath = context.marshal_as<std::string>(filePath);

    // 直接调用原生读取器，一次性完成转换
    auto nativeMesh = m_nativeReader->LoadAndConvertToMesh(nativePath);

    // 转换为托管类型
    return ConvertMesh(nativeMesh);
}

List<ManagedTextureData^>^ OsgbReaderWrapper::ExtractTexturesOnly(String^ filePath) {
    msclr::interop::marshal_context context;
    std::string nativePath = context.marshal_as<std::string>(filePath);

    auto nativeTextures = m_nativeReader->ExtractTexturesOnly(nativePath);
    auto managedTextures = gcnew List<ManagedTextureData^>();

    for (const auto& nativeTexture : nativeTextures) {
        managedTextures->Add(ConvertTexture(nativeTexture));
    }

    return managedTextures;
}

bool OsgbReaderWrapper::SaveTexture(ManagedTextureData^ texture, String^ outputPath) {
    msclr::interop::marshal_context context;
    std::string nativePath = context.marshal_as<std::string>(outputPath);

    // 转换托管纹理数据为原生数据
    Native::TextureData nativeTexture;
    nativeTexture.Width = texture->Width;
    nativeTexture.Height = texture->Height;
    nativeTexture.Components = texture->Components;
    nativeTexture.Format = context.marshal_as<std::string>(texture->Format);
    nativeTexture.Name = context.marshal_as<std::string>(texture->Name);

    // 复制图像数据
    nativeTexture.ImageData.resize(texture->ImageData->Length);
    Marshal::Copy(
        texture->ImageData,
        0,
        IntPtr((void*)nativeTexture.ImageData.data()),
        texture->ImageData->Length
    );

    return m_nativeReader->SaveTexture(nativeTexture, nativePath);
}

String^ OsgbReaderWrapper::GetLastError() {
    return gcnew String(m_nativeReader->GetLastError().c_str());
}

// 私有转换方法实现

ManagedTextureData^ OsgbReaderWrapper::ConvertTexture(const Native::TextureData& nativeTexture) {
    auto managedTexture = gcnew ManagedTextureData();

    managedTexture->Width = nativeTexture.Width;
    managedTexture->Height = nativeTexture.Height;
    managedTexture->Components = nativeTexture.Components;
    managedTexture->Format = gcnew String(nativeTexture.Format.c_str());
    managedTexture->Name = gcnew String(nativeTexture.Name.c_str());

    // 复制图像数据
    managedTexture->ImageData = gcnew array<Byte>((int)nativeTexture.ImageData.size());
    Marshal::Copy(
        IntPtr((void*)nativeTexture.ImageData.data()),
        managedTexture->ImageData,
        0,
        managedTexture->ImageData->Length
    );

    return managedTexture;
}

ManagedMaterialData^ OsgbReaderWrapper::ConvertMaterial(const Native::MaterialData& nativeMaterial) {
    auto managedMaterial = gcnew ManagedMaterialData();

    managedMaterial->Name = gcnew String(nativeMaterial.Name.c_str());
    managedMaterial->DiffuseR = nativeMaterial.DiffuseR;
    managedMaterial->DiffuseG = nativeMaterial.DiffuseG;
    managedMaterial->DiffuseB = nativeMaterial.DiffuseB;
    managedMaterial->SpecularR = nativeMaterial.SpecularR;
    managedMaterial->SpecularG = nativeMaterial.SpecularG;
    managedMaterial->SpecularB = nativeMaterial.SpecularB;
    managedMaterial->Shininess = nativeMaterial.Shininess;
    managedMaterial->TextureIndex = nativeMaterial.TextureIndex;

    return managedMaterial;
}

ManagedMeshData^ OsgbReaderWrapper::ConvertMesh(const Native::MeshData& nativeMesh) {
    auto managedMesh = gcnew ManagedMeshData();

    // 转换顶点
    if (!nativeMesh.Vertices.empty()) {
        managedMesh->Vertices = gcnew array<float>((int)nativeMesh.Vertices.size());
        Marshal::Copy(
            IntPtr((void*)nativeMesh.Vertices.data()),
            managedMesh->Vertices,
            0,
            managedMesh->Vertices->Length
        );
    }

    // 转换法线
    if (!nativeMesh.Normals.empty()) {
        managedMesh->Normals = gcnew array<float>((int)nativeMesh.Normals.size());
        Marshal::Copy(
            IntPtr((void*)nativeMesh.Normals.data()),
            managedMesh->Normals,
            0,
            managedMesh->Normals->Length
        );
    }

    // 转换纹理坐标
    if (!nativeMesh.TexCoords.empty()) {
        managedMesh->TexCoords = gcnew array<float>((int)nativeMesh.TexCoords.size());
        Marshal::Copy(
            IntPtr((void*)nativeMesh.TexCoords.data()),
            managedMesh->TexCoords,
            0,
            managedMesh->TexCoords->Length
        );
    }

    // 转换索引
    if (!nativeMesh.Indices.empty()) {
        managedMesh->Indices = gcnew array<unsigned int>((int)nativeMesh.Indices.size());
        // Marshal::Copy 不支持 unsigned int，使用 pin_ptr 手动复制
        pin_ptr<unsigned int> pinnedIndices = &managedMesh->Indices[0];
        memcpy(pinnedIndices, nativeMesh.Indices.data(), nativeMesh.Indices.size() * sizeof(unsigned int));
    }

    // 转换纹理
    for (const auto& nativeTexture : nativeMesh.Textures) {
        managedMesh->Textures->Add(ConvertTexture(nativeTexture));
    }

    // 转换材质
    for (const auto& nativeMaterial : nativeMesh.Materials) {
        managedMesh->Materials->Add(ConvertMaterial(nativeMaterial));
    }

    // 复制包围盒
    managedMesh->BBoxMinX = nativeMesh.BBoxMinX;
    managedMesh->BBoxMinY = nativeMesh.BBoxMinY;
    managedMesh->BBoxMinZ = nativeMesh.BBoxMinZ;
    managedMesh->BBoxMaxX = nativeMesh.BBoxMaxX;
    managedMesh->BBoxMaxY = nativeMesh.BBoxMaxY;
    managedMesh->BBoxMaxZ = nativeMesh.BBoxMaxZ;

    // 复制统计信息
    managedMesh->VertexCount = nativeMesh.VertexCount;
    managedMesh->FaceCount = nativeMesh.FaceCount;
    managedMesh->TextureCount = nativeMesh.TextureCount;
    managedMesh->MaterialCount = nativeMesh.MaterialCount;

    return managedMesh;
}

} // namespace Managed
} // namespace RealScene3D
