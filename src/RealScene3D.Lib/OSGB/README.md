# RealScene3D.Lib.OSGB - OSGB转GLB转换库

## 📦 概述

基于 OpenSceneGraph 的 OSGB 到 GLB 格式转换库，支持倾斜摄影数据转换为3D Tiles。

**核心功能：**
- ✅ OSGB 格式读取（倾斜摄影）
- ✅ GLB 格式输出（3D Tiles）
- ✅ 坐标系转换（PROJ支持）
- ✅ LOD多级细节支持
- ✅ 网格优化和压缩

---

## 🚀 快速开始

### 推荐方式：vcpkg 自动构建

```bash
cd F:/Workspace/CSharp/3DRealScene/src/RealScene3D.Lib/OSGB

# 配置（首次自动安装vcpkg和依赖，约35分钟）
mkdir build && cd build
cmake .. -G "Visual Studio 17 2022" -A x64

# 编译（后续构建5-10分钟）
cmake --build . --config Release
```

**输出位置：**
```
../../../bin/Release/RealScene3D.Lib.OSGB.dll
```

### 备选方式：使用预编译OSG

如果已有 OSG 预编译包：

```bash
# 1. 下载 OSG 并解压到：
#    3dParty/osg/

# 2. 配置
cd build
cmake .. -G "Visual Studio 17 2022" -A x64 -DUSE_EXTERNAL_OSG=ON

# 3. 编译
cmake --build . --config Release
```

**辅助脚本：**
```bash
# 检查OSG安装状态和获取下载指引
setup-osg.bat
```

---

## 📊 构建方案对比

| 方案 | 首次构建 | 后续构建 | 磁盘占用 | 难度 | 推荐度 |
|------|---------|---------|---------|------|--------|
| **vcpkg自动** | 35分钟 | 5-10分钟 | ~2GB | ⭐ | ✅ **推荐** |
| **手动OSG** | 5分钟 | 2-3分钟 | ~500MB | ⭐⭐⭐ | 可选 |

### vcpkg 方案（推荐）

**优点：**
- ✅ 完全自动化，无需手动下载
- ✅ 版本兼容性有保证
- ✅ 后续构建有二进制缓存
- ✅ 依赖自动解决

**缺点：**
- ⚠️ 首次构建时间较长（35分钟）

### 手动 OSG 方案

**优点：**
- ✅ 构建快速（5分钟）
- ✅ 磁盘占用小

**缺点：**
- ⚠️ OSG 官方无预编译包
- ⚠️ 需要手动下载和管理
- ⚠️ 版本兼容性需自行验证

**OSG 下载源：**
- http://download.osgvisual.org/3rdParty_x64/
- https://forum.openscenegraph.org/ (搜索 "Windows prebuilt")

---

## 📁 项目结构

```
OSGB/
├── CMakeLists.txt              # CMake配置
├── vcpkg.json                  # vcpkg依赖清单
├── setup-vcpkg.bat             # vcpkg自动安装
├── setup-osg.bat               # OSG安装检查
├── README.md                   # 本文档
│
├── Native/                     # C++源代码
│   ├── OsgbReaderCApi.h/cpp   # C API接口
│   ├── Osgb23dTile.cpp        # 核心转换逻辑
│   ├── MeshProcessor.h/cpp    # 网格处理
│   ├── LodPipeline.h/cpp      # LOD管线
│   ├── GeoTransform.h/cpp     # 坐标转换
│   ├── CoordinateApi.h/cpp    # 坐标系API
│   ├── Extern.h               # 工具函数
│   └── OsgFix.h               # Windows兼容
│
├── Interop/                    # C# P/Invoke
│   └── OsgbReaderNative.cs
│
└── ../3dParty/                 # 第三方库
    ├── include/                # 头文件库
    │   ├── glm/
    │   ├── Eigen/
    │   ├── nlohmann/
    │   ├── tiny_gltf.h
    │   └── stb_*.h
    ├── vcpkg/                  # vcpkg（自动）
    └── osg/                    # 预编译OSG（手动）
```

---

## 🛠️ 系统要求

### 必需组件

- **CMake** >= 3.22
- **C++编译器** 支持C++17
  - Windows: Visual Studio 2019/2022
  - Linux: GCC 9+ 或 Clang 10+
- **Git**（用于克隆vcpkg）

### 运行时依赖

**vcpkg 自动安装：**
- OpenSceneGraph 3.6.5
- 所有OSG依赖（Boost等）

**头文件库（已包含在3dParty/include/）：**
- GLM 1.0.1 - 数学库
- Eigen3 3.4.0 - 线性代数
- nlohmann-json - JSON解析
- tinygltf - GLTF/GLB读写
- stb_image/stb_image_write - 图像处理

**可选特性（通过CMake选项启用）：**
- PROJ - 坐标转换
- meshoptimizer - 网格优化
- Draco - 网格压缩
- Basis Universal - 纹理压缩

---

## 🔧 CMake 配置选项

### 基础选项

```bash
# 使用外部OSG（跳过vcpkg自动构建OSG）
cmake .. -DUSE_EXTERNAL_OSG=ON

# 指定OSG路径
cmake .. -DUSE_EXTERNAL_OSG=ON -DOSG_DIR="D:/MyOSG"
```

### vcpkg 可选库构建

**默认情况下，PROJ、meshoptimizer、Draco、Basis Universal等库不通过vcpkg构建。**
如需通过vcpkg构建这些库，使用以下选项：

```bash
# 通过vcpkg构建PROJ（坐标转换库）
cmake .. -DBUILD_PROJ_WITH_VCPKG=ON

# 通过vcpkg构建Basis Universal（纹理压缩）
cmake .. -DBUILD_BASISU_WITH_VCPKG=ON

# 通过vcpkg构建meshoptimizer（网格优化）
cmake .. -DBUILD_MESHOPT_WITH_VCPKG=ON

# 通过vcpkg构建Draco（网格压缩）
cmake .. -DBUILD_DRACO_WITH_VCPKG=ON

# 组合使用多个选项
cmake .. \
  -DBUILD_PROJ_WITH_VCPKG=ON \
  -DBUILD_MESHOPT_WITH_VCPKG=ON \
  -DBUILD_DRACO_WITH_VCPKG=ON
```

**说明：**
- 这些选项控制vcpkg是否构建对应的库
- 即使不通过vcpkg构建，CMake仍会尝试查找系统中已安装的版本
- 如果库未找到，相关功能将被禁用（不影响核心功能）
- 使用vcpkg构建会增加首次编译时间，但保证版本兼容性

### 运行时功能开关

以下选项控制运行时是否启用特定功能（需要对应的库已安装）：

```bash
cmake .. \
  -DENABLE_TEXTURE_COMPRESSION=ON \   # 启用KTX2纹理压缩功能
  -DENABLE_MESH_OPTIMIZATION=ON \     # 启用网格优化功能
  -DENABLE_DRACO_COMPRESSION=ON       # 启用Draco压缩功能
```

**注意：**
- 这些选项与vcpkg构建选项是**独立**的
- `ENABLE_*` 控制是否使用功能（需要库已安装）
- `BUILD_*_WITH_VCPKG` 控制是否通过vcpkg构建库
- 完整示例：
  ```bash
  # 通过vcpkg构建PROJ并启用坐标转换功能
  cmake .. -DBUILD_PROJ_WITH_VCPKG=ON

  # 使用系统已安装的meshoptimizer并启用网格优化
  cmake .. -DENABLE_MESH_OPTIMIZATION=ON

  # 组合：通过vcpkg构建Draco并启用压缩功能
  cmake .. -DBUILD_DRACO_WITH_VCPKG=ON -DENABLE_DRACO_COMPRESSION=ON
  ```

### 构建类型

```bash
# Release版本（默认）
cmake .. -DCMAKE_BUILD_TYPE=Release

# Debug版本
cmake .. -DCMAKE_BUILD_TYPE=Debug
```

### 常见使用场景

**场景1：快速开始（仅核心功能）**
```bash
# 仅构建OSG和核心功能，不构建任何可选库
cmake .. -G "Visual Studio 17 2022" -A x64
cmake --build . --config Release
```

**场景2：完整功能（通过vcpkg）**
```bash
# 通过vcpkg构建所有可选库
cmake .. -G "Visual Studio 17 2022" -A x64 \
  -DBUILD_PROJ_WITH_VCPKG=ON \
  -DBUILD_BASISU_WITH_VCPKG=ON \
  -DBUILD_MESHOPT_WITH_VCPKG=ON \
  -DBUILD_DRACO_WITH_VCPKG=ON \
  -DENABLE_TEXTURE_COMPRESSION=ON \
  -DENABLE_MESH_OPTIMIZATION=ON \
  -DENABLE_DRACO_COMPRESSION=ON \
  -DUSE_EXTERNAL_OSG=ON
```

**场景3：使用预编译OSG + 系统库**
```bash
# 使用预编译OSG，其他库从系统查找
cmake .. -G "Visual Studio 17 2022" -A x64 \
  -DUSE_EXTERNAL_OSG=ON \
  -DENABLE_MESH_OPTIMIZATION=ON
```

**场景4：仅添加PROJ支持**
```bash
# 只通过vcpkg构建PROJ，其他保持默认
cmake .. -DBUILD_PROJ_WITH_VCPKG=ON
```

---


## 🔍 常见问题

### Q1: 首次构建时间太长？

**A:** vcpkg 首次需要编译 OSG 和依赖，约35分钟。

**优化方案：**
1. 并行编译：`cmake --build . -j 8`
2. 空闲时间运行，让其完成
3. 后续构建会使用缓存（5-10分钟）
4. 团队共享 vcpkg 缓存

### Q2: 找不到 OpenSceneGraph？

**vcpkg 方式：**
```bash
# 删除build目录重新配置
cd ..
rm -rf build
mkdir build && cd build
cmake ..
```

**手动 OSG 方式：**
```bash
# 确认目录结构
dir 3dParty\osg\bin\osg*.dll
dir 3dParty\osg\lib\*.lib
dir 3dParty\osg\include\osg\

# 重新配置
cmake .. -DUSE_EXTERNAL_OSG=ON
```

### Q3: 如何切换构建方式？

**从 vcpkg 切换到外部 OSG：**
```bash
cd build
rm -rf *
cmake .. -DUSE_EXTERNAL_OSG=ON
```

**从外部 OSG 切换回 vcpkg：**
```bash
cd build
rm -rf *
cmake ..
```

### Q4: 编译错误：找不到头文件？

**A:** 确认头文件库已下载到 `3dParty/include/`：
```bash
dir 3dParty\include\glm\
dir 3dParty\include\Eigen\
dir 3dParty\include\nlohmann\json.hpp
dir 3dParty\include\tiny_gltf.h
```

如果缺失，参考 `3dParty/README.md` 手动下载。

### Q5: vcpkg 下载失败？

**A:** 检查网络连接，或配置代理：
```bash
# 设置Git代理
git config --global http.proxy http://127.0.0.1:7890

# 或使用国内镜像
set VCPKG_DOWNLOADS_MIRROR=https://mirrors.tuna.tsinghua.edu.cn/vcpkg-downloads
```

---

## 🏗️ 架构说明

### 核心模块

**1. OSGB 读取（Osgb23dTile.cpp）**
- 使用 OpenSceneGraph 读取 OSGB
- 提取几何、纹理、材质

**2. 格式转换**
- 几何数据 → GLTF/GLB 格式
- tinygltf 库输出

**3. 坐标转换（GeoTransform.cpp）**
- PROJ API 坐标系转换
- 支持 EPSG、ENU、WKT

**4. 网格处理（MeshProcessor.cpp）**
- 网格优化（meshoptimizer）
- Draco 压缩
- 纹理压缩（KTX2）

**5. LOD 管线（LodPipeline.cpp）**
- 多级细节生成
- 简化参数配置

### 数据流

```
OSGB 文件
    ↓
OpenSceneGraph 读取
    ↓
几何/纹理提取
    ↓
坐标转换 (PROJ)
    ↓
网格处理/优化
    ↓
GLTF/GLB 输出
```

---

## 🔗 C# 集成

### 1. 添加 Interop 文件

```xml
<ItemGroup>
  <Compile Include="..\RealScene3D.Lib\OSGB\Interop\OsgbReaderNative.cs"
           Link="Interop\OsgbReaderNative.cs" />
</ItemGroup>
```

### 2. 复制 DLL

```xml
<ItemGroup>
  <None Include="$(SolutionDir)bin\$(Configuration)\RealScene3D.Lib.OSGB.dll">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

### 3. C# 调用示例

```csharp
using RealScene3D.Lib.OSGB.Interop;

// 转换 OSGB 到 GLB
using var reader = new OsgbReaderNative();
bool success = reader.ConvertToGlb(
    osgbPath: "data/test.osgb",
    glbPath: "output/test.glb",
    enableTextureCompress: false,
    enableMeshopt: true,
    enableDraco: false
);

if (!success)
{
    Console.WriteLine($"Error: {reader.GetLastError()}");
}
```

---

## 🎯 最佳实践

### 团队协作

**Leader 首次设置：**
```bash
# 1. 构建
cmake .. && cmake --build . --config Release

# 2. 打包缓存
cd ../3dParty
7z a vcpkg-cache.7z vcpkg_cache/ vcpkg_installed/

# 3. 分享给团队
```

**Team Members：**
```bash
# 1. 解压缓存到 3dParty/
# 2. 直接构建（快速）
cmake .. && cmake --build . --config Release
```

### CI/CD

```yaml
# .github/workflows/build.yml
- name: Cache vcpkg
  uses: actions/cache@v3
  with:
    path: 3dParty/vcpkg_cache
    key: vcpkg-${{ hashFiles('OSGB/vcpkg.json') }}

- name: Build
  run: |
    cd src/RealScene3D.Lib/OSGB
    cmake -B build
    cmake --build build --config Release
```

---

## 📄 许可证

遵循 OpenSceneGraph 的 [OSGPL 许可证](http://www.openscenegraph.org/index.php/licensing)。

---

## 🔗 相关链接

- [OpenSceneGraph 官网](http://www.openscenegraph.org/)
- [vcpkg 包管理器](https://github.com/microsoft/vcpkg)
- [PROJ 坐标转换](https://proj.org/)
- [tinygltf](https://github.com/syoyo/tinygltf)

---

## 📝 更新日志

**2025-12-31**
- 完整的 CMake 构建系统
- vcpkg 集成和二进制缓存
- 支持外部 OSG 预编译包
- PROJ 坐标转换
- 头文件库统一管理
- LOD 多级细节支持