# RealScene3D.Lib.OSGB - OpenSceneGraph C++/CLI 封装

## 📦 概述

完整封装 OpenSceneGraph (OSG) 核心功能，提供**直接读取 OSGB 文件**的能力，无需任何转换工具。

**核心优势：**
- ✅ 零依赖外部工具，直接读取 OSGB
- ✅ 原生高质量纹理，无损提取
- ✅ .NET 无缝集成，C# 直接调用
- ✅ 完整网格数据：顶点、法线、纹理坐标、材质

---

## 🚀 快速开始

### 1. 安装依赖

从 [OpenSceneGraph 官网](http://www.openscenegraph.org/) 下载并安装预编译包。

### 2. 一键构建

```cmd
cd src\RealScene3D.Lib\OSGB

# 自动完成：编译 → 部署 → 验证
build.bat "C:\Program Files\OpenSceneGraph"

# 或使用环境变量
set OSG_ROOT=C:\Program Files\OpenSceneGraph
build.bat
```

**build.bat 自动执行：**
1. ✅ 编译 Debug 和 Release 配置
2. ✅ 部署 DLL 到应用程序目录
3. ✅ 复制 OpenSceneGraph 依赖文件
4. ✅ 验证部署完整性

**支持的参数：**
```cmd
build.bat                                    # 使用环境变量，部署 Release
build.bat "C:\OSG"                          # 指定 OSG_ROOT，部署 Release
build.bat "C:\OSG" Debug                    # 指定 OSG_ROOT，部署 Debug
build.bat Debug                              # 使用环境变量，部署 Debug
```

### 3. 使用

```csharp
using RealScene3D.Application.Services.Loaders;

// 通过依赖注入获取
var loader = serviceProvider.GetRequiredService<OsgbModelLoader>();

// 直接加载 OSGB 文件
var (mesh, boundingBox) = await loader.LoadModelAsync("path/to/file.osgb");

Console.WriteLine($"顶点: {mesh.VertexCount}, 面: {mesh.FacesCount}");
```

---

## 📁 项目结构

```
OSGB/
├── Native/                      # C++ 原生层
│   ├── OsgbReader.h
│   └── OsgbReader.cpp
├── Managed/                     # C++/CLI 托管层
│   ├── OsgbReaderWrapper.h
│   └── OsgbReaderWrapper.cpp
├── Examples/                    # 使用示例
├── RealScene3D.Lib.OSGB.vcxproj
├── build.bat                    # 自动化构建脚本 ⭐
└── README.md                    # 本文件
```

---

## 🛠️ 系统要求

### 必需组件
- **Visual Studio 2022 或更高版本**
  - C++/CLI 支持
  - Windows SDK 10.0
  - Platform Toolset v143 或更高

- **OpenSceneGraph 3.6.x+**
  - 库文件：`osg.lib`, `osgDB.lib`, `osgUtil.lib`, `OpenThreads.lib`

### 环境变量（可选）
```cmd
set OSG_ROOT=C:\Program Files\OpenSceneGraph
```

---

## 🔧 常见问题

### Q1: 编译时找不到 OSG 头文件

**解决方案：**
```cmd
# 方式 1：作为参数传递（推荐）
build.bat "C:\Program Files\OpenSceneGraph"

# 方式 2：设置环境变量
set OSG_ROOT=C:\Program Files\OpenSceneGraph
build.bat
```

### Q2: 运行时提示 "RealScene3D.Lib 不可用"

**解决方案：**
```cmd
# 重新运行构建脚本
cd src\RealScene3D.Lib\OSGB
build.bat "C:\Program Files\OpenSceneGraph"
```

构建脚本会自动验证部署，确保以下文件存在：
- `RealScene3D.Lib.OSGB.dll`
- `osg.dll`, `osgDB.dll`, `osgUtil.dll`, `OpenThreads.dll`
- `osgPlugins-3.6.x/` 目录

### Q3: 找不到 OpenSceneGraph DLL

**解决方案：**
```cmd
# 方式 1：重新部署（推荐）
build.bat

# 方式 2：添加到 PATH
set PATH=%PATH%;%OSG_ROOT%\bin
```

---

## 🏗️ 架构说明

**四层架构：**
```
C# Application (OsgbModelLoader)
    ↓
C# Service (OsgbNativeReader)
    ↓
C++/CLI Managed (OsgbReaderWrapper)
    ↓
C++ Native (OsgbReader)
    ↓
OpenSceneGraph Library
```

**数据流：**
- **Native 层：** 使用 OSG API 读取 OSGB → 提取网格、纹理、材质
- **Managed 层：** C++ 数据 → .NET 托管类型
- **Service 层：** 托管数据 → C# IMesh 接口
- **Application 层：** 统一的模型加载接口

---

## 📄 许可证

遵循 OpenSceneGraph 的 [OSGPL 许可证](http://www.openscenegraph.org/index.php/about/licensing)。

---

## 🔗 相关链接

- [OpenSceneGraph 官网](http://www.openscenegraph.org/)
- [RealScene3D 项目](../../../README.md)
- [父级封装库说明](../README.md)

---

**需要帮助？** 查看 build.bat 脚本的详细输出或提交 Issue。
