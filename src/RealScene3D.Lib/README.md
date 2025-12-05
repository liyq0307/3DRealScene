# RealScene3D.Lib - 3D 格式 C++/CLI 封装库集合

## 📦 概述

RealScene3D.Lib 是一个包含多个 3D 格式封装库的容器项目，为 .NET 应用程序提供原生 C++ 库的托管接口。

## 🗂️ 封装库

### OSGB - OpenSceneGraph 封装

完整封装 OpenSceneGraph (OSG) 核心功能，提供**直接读取 OSGB 文件**的能力。

**特性：**
- ✅ 零依赖外部工具，直接读取 OSGB
- ✅ 原生高质量纹理，无损提取
- ✅ 完整网格数据：顶点、法线、纹理坐标、材质
- ✅ .NET 无缝集成

**文档：** [OSGB/README.md](OSGB/README.md)

**快速开始：**
```cmd
cd OSGB
build.bat "C:\Program Files\OpenSceneGraph"
```

---

## 🏗️ 项目结构

```
RealScene3D.Lib/
├── OSGB/                           # OpenSceneGraph 封装
│   ├── Native/                     # C++ 原生层
│   ├── Managed/                    # C++/CLI 托管层
│   ├── Examples/                   # 使用示例
│   ├── build.bat                   # 自动化构建脚本（编译+部署+验证）
│   ├── README.md                   # 完整文档
│   └── RealScene3D.Lib.OSGB.vcxproj
│
└── README.md                       # 本文件（容器索引）
```

---

## 💡 设计原则

### 独立性
每个格式封装都是完全独立的：
- 独立的 .vcxproj 项目文件
- 独立的构建和部署脚本
- 独立的文档和示例
- 独立的依赖管理

### 统一性
所有格式封装遵循统一的架构：
```
格式名称/
├── Native/          # C++ 原生实现
├── Managed/         # C++/CLI 托管封装
├── Examples/        # 使用示例
├── build.bat        # 自动化构建脚本（编译+部署+验证）
├── README.md        # 完整文档
└── *.vcxproj        # Visual Studio 项目
```

### 可扩展性
未来可以轻松添加新的格式封装：
- FBX
- LAS/LAZ (点云)
- 其他...