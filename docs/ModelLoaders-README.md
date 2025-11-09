# 3D模型加载器文档

## 概述

RealScene3D项目支持多种3D模型格式的加载和处理，采用**组合模式**设计，通过`CompositeModelLoader`自动根据文件扩展名选择合适的加载器。

模型加载器位于：`src/RealScene3D.Application/Services/Loaders/`

---

## 支持的格式

### 通用格式（已完整实现）

这些格式已完整实现，可直接使用，无需额外依赖。

| 格式 | 扩展名 | 加载器 | 应用场景 | 实现状态 |
|-----|--------|--------|---------|---------|
| OBJ | `.obj` | `ObjModelLoader` | 通用3D模型、教学、原型设计 | ✅ 完整实现 |
| glTF | `.gltf`, `.glb` | `GltfModelLoader` | Web3D、AR/VR、游戏引擎 | ✅ 完整实现 |
| STL | `.stl` | `StlModelLoader` | 3D打印、CAD导出 | ✅ 完整实现 |
| PLY | `.ply` | `PlyModelLoader` | 点云、三维扫描 | ✅ 完整实现 |

### 专业格式（框架已建立）

这些格式的加载器框架已建立，需要集成第三方库才能完整实现。

| 格式 | 扩展名 | 加载器 | 应用场景 | 依赖库 | 实现状态 |
|-----|--------|--------|---------|-------|---------|
| FBX | `.fbx` | `FbxModelLoader` | 游戏开发、影视制作、工业设计 | Assimp.NET | ⚠️ 需集成 |
| IFC | `.ifc`, `.ifcxml`, `.ifczip` | `IfcModelLoader` | BIM建筑信息模型、建筑设计 | xBIM Toolkit | ⚠️ 需集成 |
| OSGB | `.osgb`, `.osg` | `OsgbModelLoader` | 倾斜摄影、实景三维重建 | OSG或格式转换 | ⚠️ 需集成 |

---

## 通用格式详细说明

### 1. OBJ格式加载器 (`ObjModelLoader`)

**特点**:
- 最广泛使用的3D模型交换格式
- 文本格式，易于阅读和编辑
- 支持MTL材质定义文件
- 支持顶点、法线、纹理坐标

**使用示例**:
```csharp
var loader = serviceProvider.GetRequiredService<IModelLoader>();
var (triangles, boundingBox, materials) = await loader.LoadModelAsync("model.obj");
```

**支持的MTL材质属性**:
- 漫反射颜色 (`Kd`)
- 高光颜色 (`Ks`)
- 环境光颜色 (`Ka`)
- 光泽度 (`Ns`)
- 漫反射贴图 (`map_Kd`)

---

### 2. glTF格式加载器 (`GltfModelLoader`)

**特点**:
- Khronos Group制定的现代3D传输格式
- "3D界的JPEG"，高效紧凑
- 支持二进制格式(.glb)和JSON格式(.gltf)
- 内置PBR材质系统
- Web3D和AR/VR的首选格式

**使用示例**:
```csharp
var loader = serviceProvider.GetRequiredService<IModelLoader>();

// 加载GLTF文件
var (triangles, boundingBox, materials) = await loader.LoadModelAsync("model.gltf");

// 加载GLB文件
var (triangles, boundingBox, materials) = await loader.LoadModelAsync("model.glb");
```

**优势**:
- 支持完整的场景层次结构
- 内置动画和骨骼系统
- 优化的传输和加载性能
- 广泛的工具链支持

---

### 3. STL格式加载器 (`StlModelLoader`)

**特点**:
- 3D打印行业标准格式
- 仅包含三角形网格，无材质信息
- 支持ASCII和二进制两种编码
- 自动检测格式类型

**使用示例**:
```csharp
var loader = serviceProvider.GetRequiredService<IModelLoader>();
var (triangles, boundingBox, materials) = await loader.LoadModelAsync("model.stl");
// 返回的materials包含默认灰色材质
```

**格式自动检测**:
- ASCII格式：以`solid`关键字开头
- 二进制格式：80字节头部 + 三角形数量 + 三角形数据

**应用场景**:
- 3D打印切片准备
- CAD模型导出
- 快速原型制造

---

### 4. PLY格式加载器 (`PlyModelLoader`)

**特点**:
- Stanford大学开发的多边形文件格式
- 广泛用于三维扫描和点云数据
- 支持ASCII和二进制格式
- 可扩展的属性系统

**使用示例**:
```csharp
var loader = serviceProvider.GetRequiredService<IModelLoader>();
var (triangles, boundingBox, materials) = await loader.LoadModelAsync("scan.ply");
```

**支持的面类型**:
- 三角形（直接加载）
- 四边形（自动分割为两个三角形）
- 多边形（扇形三角剖分）

**当前实现**:
- ✅ ASCII格式完整支持
- ⚠️ 二进制格式待实现

**应用场景**:
- 三维激光扫描
- 点云数据处理
- 学术研究和计算几何

---

## 专业格式详细说明

### 1. FBX格式加载器 (`FbxModelLoader`)

**格式介绍**:
- Autodesk开发的通用3D交换格式
- 游戏开发和影视制作行业标准
- 支持复杂的场景层次、动画、骨骼、材质

**应用场景**:
- Unity、Unreal Engine等游戏引擎资产
- Maya、3ds Max模型导出
- 影视特效和动画制作

**集成方案**:

#### 方案1: 使用Assimp.NET（推荐）

**安装依赖**:
```bash
dotnet add package AssimpNet
```

**实现步骤**:
1. 在`FbxModelLoader.cs`中取消注释示例代码
2. 引入命名空间：`using Assimp;`
3. 使用`AssimpContext`导入FBX场景
4. 提取网格、材质、纹理数据

**代码示例**:
```csharp
using Assimp;

var importer = new AssimpContext();
var scene = importer.ImportFile(modelPath,
    PostProcessSteps.Triangulate |
    PostProcessSteps.GenerateNormals |
    PostProcessSteps.JoinIdenticalVertices);

// 提取网格和材质
foreach (var mesh in scene.Meshes)
{
    // 处理顶点、面、法线、纹理坐标
}
```

#### 方案2: 使用Autodesk FBX SDK

需要C++/CLI包装，实现复杂度较高，不推荐。

#### 方案3: 格式转换

使用外部工具转换为GLTF后加载：
```bash
# 使用Assimp命令行工具
assimp export model.fbx model.gltf

# 或使用Blender脚本批量转换
blender --background --python fbx_to_gltf.py
```

---

### 2. IFC格式加载器 (`IfcModelLoader`)

**格式介绍**:
- Industry Foundation Classes（工业基础类）
- buildingSMART制定的BIM国际标准
- 包含完整的建筑语义信息和几何数据

**应用场景**:
- Revit、ArchiCAD等BIM软件导出
- 建筑施工图纸转换
- 建筑信息管理和维护
- 建筑性能分析

**集成方案**:

#### 使用xBIM Toolkit（推荐）

**安装依赖**:
```bash
dotnet add package Xbim.Essentials
dotnet add package Xbim.Geometry.Engine.Interop
```

**实现步骤**:
1. 在`IfcModelLoader.cs`中取消注释示例代码
2. 引入命名空间：`using Xbim.Ifc;`
3. 使用`IfcStore`打开IFC模型
4. 遍历建筑构件提取几何数据

**代码示例**:
```csharp
using Xbim.Ifc;
using Xbim.ModelGeometry.Scene;

using (var model = IfcStore.Open(modelPath))
{
    var context = new Xbim3DModelContext(model);
    context.CreateContext();

    // 遍历所有3D表示
    foreach (var shapeInstance in context.ShapeInstances())
    {
        var geometry = context.ShapeGeometry(shapeInstance.ShapeGeometryLabel);
        // 提取三角形网格
    }
}
```

**支持的IFC版本**:
- IFC2x3
- IFC4
- IFC4x1（部分支持）

**推荐工作流程**:
1. **几何提取**：使用xBIM提取三角形网格
2. **语义存储**：将IFC构件属性存储到MongoDB的`BimModelMetadata`
3. **关联数据**：建立几何数据和语义数据的关联
4. **可视化**：生成3D Tiles用于Web展示

**BIM数据存储架构**:
```
PostgreSQL (几何数据)
    ↓
Triangle列表、BoundingBox
    ↓
3D Tiles生成
    ↓
MinIO (瓦片文件)

MongoDB (语义数据)
    ↓
BimModelMetadata集合
    ↓
{
    "IfcGuid": "...",
    "IfcType": "IfcWall",
    "Properties": {...},
    "GeometryRef": "..."
}
```

---

### 3. OSGB格式加载器 (`OsgbModelLoader`)

**格式介绍**:
- OpenSceneGraph Binary格式
- 倾斜摄影领域最常用的三维模型格式
- 支持大规模场景的LOD层次结构
- 二进制格式，高压缩比

**应用场景**:
- 无人机倾斜摄影建模
- 实景三维重建
- 数字孪生城市
- 大范围地形可视化

**集成方案**:

#### 方案1: 格式转换（推荐）

**推荐工具**:

1. **osgconv** - OSG官方转换工具
```bash
# OSGB转GLTF
osgconv input.osgb output.gltf

# 批量转换
for file in *.osgb; do
    osgconv "$file" "${file%.osgb}.gltf"
done
```

2. **obj23dtiles** - OSGB转3D Tiles
```bash
# 安装
npm install -g obj23dtiles

# 转换OSGB为3D Tiles
obj23dtiles -i input.osgb -o output/

# 生成tileset.json
obj23dtiles -i input.osgb -o output/ --tileset
```

3. **Cesiumlab** - 商业化倾斜摄影处理工具
   - 可视化界面
   - 支持OSGB到3D Tiles转换
   - 自动优化和切片

#### 方案2: 直接集成OSG库

需要P/Invoke包装OpenSceneGraph C++库，实现复杂。

#### 方案3: 使用项目现有架构（最推荐）

**推荐工作流程**:

```
OSGB原始数据
    ↓
obj23dtiles转换
    ↓
3D Tiles (b3dm + tileset.json)
    ↓
上传到MinIO
    ↓
元数据存储到MongoDB (TiltPhotographyMetadata)
    ↓
前端Cesium加载展示
```

**实施步骤**:

1. **预处理OSGB数据**
```bash
# 使用obj23dtiles批量转换
obj23dtiles -i data/aerial/*.osgb \
            -o output/tiles/ \
            --tileset \
            --maxLevel 18
```

2. **上传到MinIO**
```csharp
var minioService = serviceProvider.GetRequiredService<IMinioStorageService>();

// 上传瓦片文件
await minioService.UploadFileAsync(
    MinioBuckets.TILT_PHOTOGRAPHY,
    "project1/tiles/tileset.json",
    tilesetPath
);
```

3. **存储元数据到MongoDB**
```csharp
var metadata = new TiltPhotographyMetadata
{
    ProjectName = "城市航拍项目",
    DataSource = "无人机倾斜摄影",
    OriginalFormat = "OSGB",
    ConvertedFormat = "3DTiles",
    TilesetUrl = "minio://tilt-photography/project1/tiles/tileset.json",
    BoundingBox = ...,
    CaptureDate = DateTime.UtcNow,
    Resolution = 0.05, // 5cm分辨率
    Tags = new[] { "城市", "建筑", "2024" }
};

await repository.CreateAsync(metadata);
```

4. **前端加载**
```javascript
// Cesium加载3D Tiles
const tileset = viewer.scene.primitives.add(
    new Cesium.Cesium3DTileset({
        url: 'http://minio.example.com/tilt-photography/project1/tiles/tileset.json'
    })
);
```

---

## 使用CompositeModelLoader

`CompositeModelLoader`是一个智能的组合加载器，会根据文件扩展名自动选择合适的加载器。

### 基本使用

```csharp
// 通过依赖注入获取
var loader = serviceProvider.GetRequiredService<IModelLoader>();

// 自动识别格式并加载
var (triangles, boundingBox, materials) = await loader.LoadModelAsync("path/to/model.obj");
var (triangles, boundingBox, materials) = await loader.LoadModelAsync("path/to/model.gltf");
var (triangles, boundingBox, materials) = await loader.LoadModelAsync("path/to/model.stl");
```

### 检查格式支持

```csharp
var loader = serviceProvider.GetRequiredService<CompositeModelLoader>();

// 检查是否支持某个格式
if (loader.SupportsFormat(".fbx"))
{
    Console.WriteLine("FBX格式已支持");
}

// 获取所有支持的格式
var formats = loader.GetSupportedFormats();
Console.WriteLine($"支持的格式: {string.Join(", ", formats)}");
```

### 获取特定加载器

```csharp
var compositeLoader = serviceProvider.GetRequiredService<CompositeModelLoader>();

// 获取特定格式的加载器
var objLoader = compositeLoader.GetLoader(".obj");
if (objLoader != null)
{
    var result = await objLoader.LoadModelAsync("model.obj");
}
```

---

## 返回数据结构

所有加载器返回相同的数据结构：

```csharp
(
    List<Triangle> Triangles,           // 三角形列表
    BoundingBox3D BoundingBox,          // 包围盒
    Dictionary<string, Material> Materials  // 材质字典
)
```

### Triangle结构

```csharp
public class Triangle
{
    public Vector3D[] Vertices { get; set; }  // 3个顶点
    public Vector3D Normal { get; set; }      // 法线
    public string MaterialName { get; set; }   // 材质名称
}
```

### BoundingBox3D结构

```csharp
public class BoundingBox3D
{
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MinZ { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }
    public double MaxZ { get; set; }
}
```

### Material结构

```csharp
public class Material
{
    public string Name { get; set; }
    public Color3D DiffuseColor { get; set; }   // 漫反射颜色
    public Color3D SpecularColor { get; set; }  // 高光颜色
    public double Shininess { get; set; }        // 光泽度
    public string DiffuseTexture { get; set; }   // 漫反射贴图路径
}
```

---

## 与切片系统集成

模型加载器与3D Tiles切片系统无缝集成：

### 工作流程

```
模型文件 (.obj, .gltf, .stl, etc.)
    ↓
IModelLoader.LoadModelAsync()
    ↓
Triangle列表 + BoundingBox + Materials
    ↓
ISlicingStrategy (网格切片、八叉树切片等)
    ↓
Tile节点层次结构
    ↓
ITileGenerator (B3dm, GLTF, PNTS生成器)
    ↓
3D Tiles瓦片文件
    ↓
MinIO对象存储
```

### 使用示例

```csharp
// 1. 加载模型
var loader = serviceProvider.GetRequiredService<IModelLoader>();
var (triangles, boundingBox, materials) = await loader.LoadModelAsync("large-model.obj");

// 2. 创建切片任务
var slicingService = serviceProvider.GetRequiredService<ISlicingAppService>();
var task = await slicingService.CreateSlicingTaskAsync(new CreateSlicingTaskDto
{
    ModelPath = "large-model.obj",
    OutputPath = "output/tiles/",
    Strategy = SlicingStrategy.Octree,
    TileFormat = TileFormat.B3dm,
    MaxTrianglesPerTile = 50000
});

// 3. 执行切片
await slicingService.ExecuteSlicingTaskAsync(task.Id);

// 4. 生成tileset.json
var tilesetGenerator = serviceProvider.GetRequiredService<TilesetGenerator>();
await tilesetGenerator.GenerateTilesetAsync("output/tiles/");
```

---

## 配置和依赖注入

模型加载器在`Program.cs`中注册：

```csharp
// 通用格式加载器（已完整实现）
builder.Services.AddScoped<MtlParser>();
builder.Services.AddScoped<ObjModelLoader>();
builder.Services.AddScoped<GltfModelLoader>();
builder.Services.AddScoped<StlModelLoader>();
builder.Services.AddScoped<PlyModelLoader>();

// 专业格式加载器（需要集成第三方库）
builder.Services.AddScoped<FbxModelLoader>();
builder.Services.AddScoped<IfcModelLoader>();
builder.Services.AddScoped<OsgbModelLoader>();

// 组合加载器
builder.Services.AddScoped<CompositeModelLoader>();

// 统一接口
builder.Services.AddScoped<IModelLoader, CompositeModelLoader>();
```

---

## 性能优化建议

### 1. 大文件处理

对于超大模型文件（>1GB），建议：

```csharp
// 使用取消令牌
var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
var result = await loader.LoadModelAsync(path, cts.Token);

// 分块处理
var processor = serviceProvider.GetRequiredService<ISlicingProcessor>();
await processor.ProcessLargeModelAsync(path, chunkSize: 100000);
```

### 2. 并行加载

加载多个模型时使用并行处理：

```csharp
var tasks = modelPaths.Select(async path =>
{
    var loader = serviceProvider.GetRequiredService<IModelLoader>();
    return await loader.LoadModelAsync(path);
});

var results = await Task.WhenAll(tasks);
```

### 3. 缓存策略

使用Redis缓存加载结果：

```csharp
var cacheService = serviceProvider.GetRequiredService<IRedisCacheService>();
var cacheKey = $"model:{modelHash}";

// 尝试从缓存获取
var cached = await cacheService.GetAsync<ModelData>(cacheKey);
if (cached != null)
    return cached;

// 加载并缓存
var result = await loader.LoadModelAsync(path);
await cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(24));
```

---

## 常见问题 (FAQ)

### Q: 为什么FBX/IFC/OSGB加载器抛出NotImplementedException？

**A**: 这些格式需要集成第三方库才能完整实现。当前代码提供了框架和示例，需要安装对应的NuGet包并取消注释实现代码。

推荐做法：
- **FBX**: 安装`AssimpNet`包
- **IFC**: 安装`Xbim.Essentials`包
- **OSGB**: 使用格式转换工具预处理为GLTF或3D Tiles

---

### Q: 如何处理包含纹理的模型？

**A**:
- **OBJ**: MTL文件中的`map_Kd`会自动解析，纹理路径存储在`Material.DiffuseTexture`
- **GLTF**: 纹理嵌入在GLB文件或引用外部图片
- **处理**: 纹理文件应上传到MinIO的`textures`桶，并更新引用路径

```csharp
// 上传纹理
var textureUrl = await minioService.UploadFileAsync(
    MinioBuckets.TEXTURES,
    $"model123/diffuse.jpg",
    texturePath
);

// 更新材质引用
material.DiffuseTexture = textureUrl;
```

---

### Q: 如何选择合适的切片策略？

**A**: 根据模型特点选择：

| 模型类型 | 推荐策略 | 理由 |
|---------|---------|------|
| 建筑模型 | 网格切片 | 空间分布均匀 |
| 地形数据 | 四叉树/网格 | 平面分布为主 |
| 复杂场景 | 八叉树 | 三维空间分布复杂 |
| 超大模型 | 自适应切片 | 根据密度动态调整 |

---

### Q: 支持哪些坐标系统？

**A**:
- 模型加载器保留原始坐标
- 坐标转换在切片阶段处理
- 支持的坐标系：WGS84、CGCS2000、自定义投影

配置坐标转换：
```csharp
var task = new CreateSlicingTaskDto
{
    ModelPath = "model.obj",
    SourceCRS = "EPSG:4326",  // WGS84
    TargetCRS = "EPSG:4549",  // CGCS2000
    TransformMatrix = transformMatrix
};
```

---

## 扩展开发

### 添加新的格式加载器

1. **创建加载器类**

```csharp
public class CustomModelLoader : IModelLoader
{
    public async Task<(List<Triangle>, BoundingBox3D, Dictionary<string, Material>)>
        LoadModelAsync(string modelPath, CancellationToken cancellationToken = default)
    {
        // 实现加载逻辑
    }

    public bool SupportsFormat(string extension)
    {
        return extension == ".custom";
    }

    public IEnumerable<string> GetSupportedFormats()
    {
        return new[] { ".custom" };
    }
}
```

2. **注册到DI容器**

```csharp
// Program.cs
builder.Services.AddScoped<CustomModelLoader>();
```

3. **更新CompositeModelLoader**

```csharp
public CompositeModelLoader(
    // ... 其他加载器
    CustomModelLoader customLoader)
{
    // ...
    RegisterLoader(customLoader);
}
```

---

## 相关文档

- [切片策略文档](./SlicingStrategies-README.md)
- [瓦片生成器文档](./TileGenerators-README.md)
- [异构存储架构](./HeterogeneousStorage-README.md)
- [API接口文档](./API-README.md)

---

## 技术支持

如遇到问题，请检查：

1. **日志输出**：查看`ILogger`输出的详细错误信息
2. **文件格式**：确认文件是否损坏或格式不正确
3. **依赖库**：检查是否安装了必要的NuGet包
4. **内存限制**：超大文件可能需要调整内存配置

---

**最后更新**: 2024年
**维护者**: RealScene3D团队
