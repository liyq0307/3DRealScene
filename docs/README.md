# RealScene3D - 3D场景切片系统完整文档

**版本**: 2.0
**最后更新**: 2025-11-09
**状态**: ✅ 生产就绪

---

## 📋 目录

1. [项目概述](#项目概述)
2. [系统架构](#系统架构)
3. [核心功能模块](#核心功能模块)
4. [API使用指南](#api使用指南)
5. [技术实现细节](#技术实现细节)
6. [最佳实践](#最佳实践)

---

## 项目概述

RealScene3D 是一个完整的3D场景切片和瓦片生成系统，支持将大型3D模型（OBJ、GLTF/GLB）转换为优化的Cesium 3D Tiles格式，用于Web端高性能渲染。

### 核心特性

- ✅ **多种切片策略** - Grid、Octree、KdTree、Adaptive、Recursive
- ✅ **多格式输出** - B3DM、I3DM、GLTF、PNTS、CMPT
- ✅ **LOD支持** - 基于QEM的自动网格简化
- ✅ **材质系统** - 完整的PBR材质支持
- ✅ **纹理处理** - 自动纹理图集生成和GLB内嵌纹理导出
- ✅ **工厂模式** - 解耦的策略和生成器架构
- ✅ **异步处理** - 支持大规模模型的后台处理

---

## 系统架构

### 整体架构图

```
┌─────────────────────────────────────────────────────────────────┐
│                        WebApi Layer                             │
│  Controllers: SlicingController, SceneController                │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                    Application Layer                            │
│  ┌──────────────────┐  ┌──────────────────┐                     │
│  │ SlicingAppService│  │SlicingProcessor  │                     │
│  └────────┬─────────┘  └────────┬─────────┘                     │
│           │                      │                              │
│  ┌────────┴──────────────────────┴────────┐                     │
│  │      ISlicingStrategyFactory           │                     │
│  │  ┌────────────────────────────────┐    │                     │
│  │  │ GridSlicingStrategy            │    │                     │
│  │  │ OctreeSlicingStrategy          │    │                     │
│  │  │ KdTreeSlicingStrategy          │    │                     │
│  │  │ AdaptiveSlicingStrategy        │    │                     │
│  │  │ RecursiveSubdivisionStrategy   │    │                     │
│  │  └────────────────────────────────┘    │                     │
│  └─────────────────────────────────────────┘                    │
│                          │                                      │
│  ┌────────────────────────┴────────────────┐                    │
│  │      ITileGeneratorFactory              │                    │
│  │  ┌────────────────────────────────┐     │                    │
│  │  │ B3dmGenerator                  │     │                    │
│  │  │ GltfGenerator                  │     │                    │
│  │  │ I3dmGenerator                  │     │                    │
│  │  │ PntsGenerator                  │     │                    │
│  │  │ CmptGenerator                  │     │                    │
│  │  └────────────────────────────────┘     │                    │
│  └─────────────────────────────────────────┘                    │
│                          │                                      │
│  ┌────────────────────────┴────────────────┐                    │
│  │ Support Services                        │                    │
│  │ - ObjModelLoader / GltfModelLoader      │                    │
│  │ - MtlParser                             │                    │
│  │ - MeshDecimationService (QEM)           │                    │
│  │ - TextureAtlasGenerator                 │                    │
│  │ - TilesetGenerator                      │                    │
│  └─────────────────────────────────────────┘                    │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                      Domain Layer                               │
│  Entities: Triangle, Material, Geometry, BoundingBox3D          │
│  Interfaces: ISlicingStrategy, ITileGeneratorFactory            │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────┴───────────────────────────────────────┐
│                   Infrastructure Layer                          │
│  - PostgreSQL/PostGIS (空间数据)                                │
│  - MongoDB (元数据)                                             │
│  - Redis (缓存)                                                 │
│  - MinIO (文件存储)                                             │
└─────────────────────────────────────────────────────────────────┘
```

### 设计模式

#### 1. 工厂模式 (Factory Pattern)

**切片策略工厂**:
```csharp
public interface ISlicingStrategyFactory
{
    ISlicingStrategy CreateStrategy(SlicingStrategy strategy);
}
```

**瓦片生成器工厂**:
```csharp
public interface ITileGeneratorFactory
{
    object CreateGenerator(TileFormat format);
}
```

**代码简化效果**:

*之前（v1.5）- 需要30+行 switch 语句*:
```csharp
ISlicingStrategy strategy = config.Strategy switch
{
    SlicingStrategy.Grid => new GridSlicingStrategy(
        (ILogger)_logger,
        _tileGeneratorFactory,
        _modelLoader,
        _meshDecimationService),
    SlicingStrategy.Octree => new OctreeSlicingStrategy(...),
    // ... 更多策略
    _ => new OctreeSlicingStrategy(...)
};
```

*现在（v2.0）- 仅需1行代码*:
```csharp
ISlicingStrategy strategy = _slicingStrategyFactory.CreateStrategy(config.Strategy);
```

**优势**:
- ✅ 代码量减少 95%
- ✅ 单一职责原则
- ✅ 易于测试和维护
- ✅ 支持运行时动态扩展

#### 2. 模板方法模式 (Template Method Pattern)

**TileGenerator基类**:
```csharp
public abstract class TileGenerator
{
    public abstract byte[] GenerateTile(...);
    public abstract Task SaveTileAsync(...);  // v2.0 新增统一保存接口

    protected virtual void ValidateInput(...) { }
    protected byte[] PadTo4ByteBoundary(...) { }
}
```

**统一保存接口改进**:

所有瓦片生成器现在都实现了抽象的 `SaveTileAsync` 方法：

```csharp
// B3dmGenerator
public override async Task SaveTileAsync(
    List<Triangle> triangles,
    BoundingBox3D bounds,
    string outputPath,
    Dictionary<string, Material>? materials = null)
{
    await SaveB3DMFileAsync(triangles, bounds, outputPath, materials);
}
```

**优势**:
- ✅ 统一接口，多态调用
- ✅ 切片策略无需关心具体生成器类型
- ✅ 代码更简洁（从 switch-case 到单一方法调用）
- ✅ 向后兼容（保留原有特定方法如 `SaveB3DMFileAsync`）

#### 3. 策略模式 (Strategy Pattern)

**切片策略接口**:
```csharp
public interface ISlicingStrategy
{
    Task<List<Slice>> GenerateSlicesAsync(...);
    int EstimateSliceCount(int level, SlicingConfig config);
}
```

---

## 核心功能模块

### 1. 切片策略 (Slicing Strategies)

#### Grid (网格切片)
- **适用场景**: 规则网格布局的建筑群、城市场景
- **特点**: 均匀划分空间，可预测性强
- **性能**: 快速生成，内存占用低
- **配置参数**: `tileSize` (单个瓦片尺寸)

#### Octree (八叉树切片)
- **适用场景**: 不规则模型，细节密度变化大的场景
- **特点**: 自适应空间剖分，平衡细节和性能
- **性能**: 适中的生成速度和内存占用
- **配置参数**: `maxDepth` (最大深度), `minTrianglesPerNode` (节点最小三角形数)

#### KdTree (KD树切片)
- **适用场景**: 高维空间查询优化，长条形或扁平模型
- **特点**: 基于方差的二分剖分，优化特定方向
- **性能**: 查询效率高，适合大范围场景
- **配置参数**: `maxDepth`, `splitThreshold` (分割阈值)

#### Adaptive (自适应切片)
- **适用场景**: 复杂场景，需要根据几何密度动态调整
- **特点**: 智能分析模型复杂度，自动调整切片策略
- **性能**: 最优化的切片结果，但生成时间较长
- **配置参数**: `densityThreshold` (密度阈值), `adaptiveLevel` (自适应级别)

#### Recursive (递归剖分)
- **适用场景**: 类似Obj2Tiles的递归处理，精细控制
- **特点**: 从粗到细递归剖分，支持动态深度决策
- **性能**: 高质量输出，适合专业场景
- **配置参数**: `maxDepth`, `minTrianglesPerSlice`, `maxTrianglesPerSlice`

### 2. 瓦片生成器 (Tile Generators)

#### B3DM (Batched 3D Model)
- **用途**: 批量建筑模型，城市场景
- **特点**: 高效的批处理，支持Feature Table和Batch Table
- **文件结构**: Header + Feature Table + Batch Table + GLB

**生成示例**:
```csharp
var generator = new B3dmGenerator(logger, gltfGenerator);
var b3dmData = generator.GenerateTile(triangles, bounds, materials);
await generator.SaveTileAsync(triangles, bounds, "output.b3dm", materials);
```

#### I3DM (Instanced 3D Model)
- **用途**: 大量重复对象（树木、路灯、标志牌）
- **特点**: GPU实例化渲染，极高性能
- **文件结构**: Header + Feature Table + Batch Table + GLB

**生成示例**:
```csharp
var generator = new I3dmGenerator(logger, b3dmGenerator);
var i3dmData = generator.GenerateI3DM(triangles, bounds, instanceCount, positions);
```

#### GLTF/GLB (Standard glTF 2.0)
- **用途**: 独立3D模型导出，跨平台交换
- **特点**: 标准格式，广泛支持，完整PBR材质
- **支持格式**: GLB (二进制) 和 GLTF (JSON + BIN)

**生成示例**:
```csharp
var generator = new GltfGenerator(logger, textureAtlasGenerator);
var glbData = generator.GenerateGLB(triangles, bounds, materials);
await generator.SaveGLBFileAsync(triangles, bounds, "output.glb", materials);
```

#### PNTS (Point Cloud)
- **用途**: 点云数据，大规模扫描数据
- **特点**: 高效的点云存储，支持法线和颜色
- **采样策略**: VerticesOnly, UniformSampling, AdaptiveSampling

**生成示例**:
```csharp
var generator = new PntsGenerator(logger);
var pntsData = generator.GeneratePNTS(triangles, bounds,
    SamplingStrategy.UniformSampling, samplingDensity: 10);
```

#### CMPT (Composite)
- **用途**: 混合数据类型，复杂场景优化
- **特点**: 组合多种格式的瓦片为一个文件
- **文件结构**: Header + 多个子瓦片

**生成示例**:
```csharp
var generator = new CmptGenerator(logger, b3dmGen, pntsGen);
var tiles = new[] {
    new TileData { Format = "b3dm", Data = b3dmData },
    new TileData { Format = "pnts", Data = pntsData }
};
var cmptData = generator.GenerateCMPT(tiles);
```

### 3. 材质和纹理系统

#### Material 材质类
```csharp
public class Material
{
    public string Name { get; set; }
    public Color3D? AmbientColor { get; set; }      // Ka
    public Color3D? DiffuseColor { get; set; }      // Kd
    public Color3D? SpecularColor { get; set; }     // Ks
    public Color3D? EmissiveColor { get; set; }     // Ke
    public double SpecularExponent { get; set; }    // Ns
    public double Opacity { get; set; }             // d
    public double IndexOfRefraction { get; set; }   // Ni
    public TextureInfo? DiffuseTexture { get; set; }
    public TextureInfo? NormalTexture { get; set; }
    public TextureInfo? MetallicRoughnessTexture { get; set; }
}
```

#### MTL文件解析
```csharp
var parser = new MtlParser(logger);
var materials = await parser.ParseMtlFileAsync("model.mtl");
```

**支持的MTL指令**:
- `newmtl` - 材质定义
- `Ka` - 环境光颜色
- `Kd` - 漫反射颜色
- `Ks` - 镜面反射颜色
- `Ke` - 自发光颜色
- `Ns` - 镜面指数
- `d` - 不透明度
- `map_Kd` - 漫反射纹理
- `map_Bump` / `bump` - 法线贴图

#### 纹理图集生成
```csharp
var atlasGenerator = new TextureAtlasGenerator(logger);
var (atlasImage, uvMappings) = await atlasGenerator.GenerateAtlasAsync(
    textures,
    maxAtlasSize: 2048,
    padding: 2
);
```

**算法**: MaxRects矩形装箱算法
**优化**: 自动尺寸调整、边界填充、UV坐标重映射

#### GLB内嵌纹理导出
```csharp
// 自动检测和导出GLB中的内嵌纹理
var loader = new GltfModelLoader(logger);
var (triangles, materials) = await loader.LoadModelAsync("model.glb");
// 纹理自动导出到 <modelPath>/textures/ 目录
```

### 4. LOD网格简化

#### QEM (Quadric Error Metrics) 算法
```csharp
var decimationService = new MeshDecimationService(logger);

// 单级简化
var simplified = decimationService.SimplifyMesh(
    triangles,
    targetRatio: 0.5,  // 保留50%的三角形
    preserveBoundaries: true
);

// 多级LOD生成
var lods = decimationService.GenerateLODs(
    triangles,
    lodLevels: new[] { 1.0, 0.5, 0.25, 0.1 }  // 100%, 50%, 25%, 10%
);
```

**特点**:
- 保持模型外观质量
- 边界保护
- 法向量权重
- 拓扑一致性

---

## API使用指南

### 1. 基础切片任务

#### 创建切片任务 (默认B3DM格式)

**请求**:
```http
POST /api/slicing/tasks
Content-Type: application/json

{
  "name": "建筑模型切片",
  "sourceModelPath": "C:/models/building.obj",
  "outputPath": "C:/output/tiles",
  "modelType": "OBJ",
  "slicingConfig": {
    "strategy": "Grid",
    "maxLevel": 5,
    "tileSize": 100.0,
    "tileFormat": "B3DM"
  }
}
```

**响应**:
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "建筑模型切片",
  "status": "Queued",
  "progress": 0,
  "createdAt": "2025-11-09T10:00:00Z"
}
```

### 2. 多格式瓦片生成

#### GLTF格式输出
```json
{
  "name": "GLTF格式切片",
  "sourceModelPath": "C:/models/building.obj",
  "outputPath": "C:/output/gltf-tiles",
  "modelType": "OBJ",
  "slicingConfig": {
    "strategy": "Octree",
    "maxLevel": 4,
    "tileFormat": "GLTF",
    "outputFormat": "glb"
  }
}
```

#### 点云格式输出
```json
{
  "name": "点云切片",
  "sourceModelPath": "C:/models/scan.obj",
  "outputPath": "C:/output/pointcloud",
  "modelType": "OBJ",
  "slicingConfig": {
    "strategy": "Adaptive",
    "maxLevel": 3,
    "tileFormat": "PNTS",
    "samplingDensity": 10
  }
}
```

### 3. LOD网格简化

```json
{
  "name": "带LOD的切片",
  "sourceModelPath": "C:/models/complex.obj",
  "outputPath": "C:/output/lod-tiles",
  "modelType": "OBJ",
  "slicingConfig": {
    "strategy": "Recursive",
    "maxLevel": 6,
    "tileFormat": "B3DM",
    "enableLOD": true,
    "lodLevels": [1.0, 0.75, 0.5, 0.25, 0.15, 0.1],
    "preserveBoundaries": true
  }
}
```

### 4. 查询任务状态

```http
GET /api/slicing/tasks/{taskId}
```

**响应**:
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "建筑模型切片",
  "status": "Processing",
  "progress": 45,
  "currentLevel": 3,
  "totalLevels": 5,
  "processedSlices": 120,
  "estimatedSlices": 267,
  "elapsedTime": 180,
  "estimatedRemainingTime": 220
}
```

### 5. Obj2Tiles快速转换

```http
POST /api/slicing/obj2tiles
Content-Type: application/json

{
  "inputPath": "C:/models/building.obj",
  "outputPath": "C:/output/tiles",
  "maxLevel": 5,
  "enableLOD": true,
  "tileFormat": "B3DM"
}
```

---

## 技术实现细节

### 1. 切片处理流程

```
1. 任务创建
   ├─ 验证输入文件
   ├─ 解析配置参数
   └─ 创建任务记录

2. 模型加载
   ├─ ObjModelLoader / GltfModelLoader
   ├─ 解析顶点、面片、材质
   ├─ 加载纹理图片
   └─ 计算包围盒

3. 材质处理
   ├─ MtlParser解析MTL文件
   ├─ TextureAtlasGenerator生成纹理图集
   └─ 材质与三角形关联

4. 切片策略执行
   ├─ ISlicingStrategyFactory.CreateStrategy()
   ├─ 递归/迭代空间剖分
   └─ 生成切片元数据

5. LOD生成（可选）
   ├─ MeshDecimationService.GenerateLODs()
   ├─ QEM网格简化
   └─ 多级别模型生成

6. 瓦片生成
   ├─ ITileGeneratorFactory.CreateGenerator()
   ├─ 生成B3DM/I3DM/GLTF/PNTS/CMPT
   └─ TileGenerator.SaveTileAsync()

7. Tileset.json生成
   ├─ TilesetGenerator.GenerateTileset()
   ├─ 构建四叉树/八叉树结构
   └─ 计算几何误差和包围盒

8. 持久化
   ├─ 保存瓦片文件
   ├─ 更新数据库记录
   └─ 上传到MinIO（可选）
```

### 2. 工厂模式实现

#### SlicingStrategyFactory
```csharp
public class SlicingStrategyFactory : ISlicingStrategyFactory
{
    private readonly ILogger<SlicingStrategyFactory> _logger;
    private readonly ITileGeneratorFactory _tileGeneratorFactory;
    private readonly IModelLoader _modelLoader;
    private readonly IMinioStorageService _minioService;
    private readonly MeshDecimationService? _meshDecimationService;

    public ISlicingStrategy CreateStrategy(SlicingStrategy strategy)
    {
        return strategy switch
        {
            SlicingStrategy.Grid => new GridSlicingStrategy(...),
            SlicingStrategy.Octree => new OctreeSlicingStrategy(...),
            SlicingStrategy.KdTree => new KdTreeSlicingStrategy(...),
            SlicingStrategy.Adaptive => new AdaptiveSlicingStrategy(...),
            SlicingStrategy.Recursive => new RecursiveSubdivisionStrategy(...),
            _ => throw new NotSupportedException($"不支持的切片策略: {strategy}")
        };
    }
}
```

#### TileGeneratorFactory
```csharp
public class TileGeneratorFactory : ITileGeneratorFactory
{
    public object CreateGenerator(TileFormat format)
    {
        return format switch
        {
            TileFormat.B3DM => _serviceProvider.GetRequiredService<B3dmGenerator>(),
            TileFormat.I3DM => _serviceProvider.GetRequiredService<I3dmGenerator>(),
            TileFormat.GLTF => _serviceProvider.GetRequiredService<GltfGenerator>(),
            TileFormat.PNTS => _serviceProvider.GetRequiredService<PntsGenerator>(),
            TileFormat.CMPT => _serviceProvider.GetRequiredService<CmptGenerator>(),
            _ => throw new NotSupportedException($"不支持的瓦片格式: {format}")
        };
    }
}
```

### 3. 依赖注入配置

**Program.cs**:
```csharp
// 切片策略工厂
builder.Services.AddScoped<ISlicingStrategyFactory, SlicingStrategyFactory>();

// 瓦片生成器工厂
builder.Services.AddScoped<ITileGeneratorFactory, TileGeneratorFactory>();

// 瓦片生成器
builder.Services.AddScoped<B3dmGenerator>();
builder.Services.AddScoped<GltfGenerator>();
builder.Services.AddScoped<I3dmGenerator>();
builder.Services.AddScoped<PntsGenerator>();
builder.Services.AddScoped<CmptGenerator>();
builder.Services.AddScoped<TilesetGenerator>();

// 模型加载器
builder.Services.AddScoped<IModelLoader, ObjModelLoader>();
builder.Services.AddScoped<GltfModelLoader>();

// 支持服务
builder.Services.AddScoped<MtlParser>();
builder.Services.AddScoped<MeshDecimationService>();
builder.Services.AddScoped<TextureAtlasGenerator>();
```

### 4. 性能优化

#### 内存管理
- 使用 `ArrayPool<T>` 减少内存分配
- 流式处理大文件，避免一次性加载
- 及时释放不需要的资源

#### 并行处理
```csharp
// 并行生成多个切片
await Parallel.ForEachAsync(sliceGroups,
    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
    async (group, ct) => {
        foreach (var slice in group)
        {
            await GenerateSliceAsync(slice, ct);
        }
    });
```

#### 缓存策略
- Redis缓存热点数据
- MinIO存储大文件
- PostgreSQL索引优化空间查询

---

## 最佳实践

### 1. 切片策略选择

| 场景类型 | 推荐策略 | 原因 |
|---------|---------|------|
| 规则建筑群 | Grid | 均匀划分，简单高效 |
| 复杂地形 | Octree | 自适应细节，平衡性能 |
| 大范围城市 | KdTree | 优化查询效率 |
| 不规则模型 | Adaptive | 智能分析，最优化 |
| 专业场景 | Recursive | 精细控制，高质量 |

### 2. LOD级别配置

```
Level 0: 100% (完整模型)
Level 1: 75%  (远距离)
Level 2: 50%  (中距离)
Level 3: 25%  (远距离)
Level 4: 10%  (极远距离)
Level 5: 5%   (超远距离，可选)
```

### 3. 瓦片格式选择

| 格式 | 适用场景 | 优点 | 缺点 |
|-----|---------|------|------|
| B3DM | 建筑、静态模型 | 高效批处理 | 单一模型类型 |
| I3DM | 重复对象（树木） | GPU实例化，性能极高 | 需要相同模型 |
| GLTF | 通用模型导出 | 标准格式，广泛支持 | 文件较大 |
| PNTS | 点云、扫描数据 | 高效存储点云 | 缺少几何细节 |
| CMPT | 混合场景 | 灵活组合 | 复杂度高 |

### 4. 性能调优建议

#### 模型准备
- 使用 glTF 2.0 格式可获得最佳性能
- 提前优化模型，移除不必要的顶点
- 合并相邻的相同材质面片
- 纹理分辨率适中（推荐 1024-2048）

#### 切片配置
- `maxLevel`: 根据模型复杂度调整（推荐 4-6）
- `tileSize`: 根据场景范围设置（推荐 50-200 单位）
- `enableLOD`: 大模型必须启用
- `preserveBoundaries`: 重要边缘启用

#### 服务器配置
- CPU: 多核处理器，建议 8核以上
- 内存: 16GB以上，大模型需要 32GB+
- 存储: SSD，确保高I/O性能
- 数据库: PostgreSQL + PostGIS扩展

### 5. 错误处理

#### 常见错误及解决方案

**1. 内存不足**
```json
{
  "error": "OutOfMemoryException",
  "solution": "减少maxLevel或启用流式处理"
}
```

**2. 文件格式不支持**
```json
{
  "error": "UnsupportedFormatException",
  "solution": "检查文件扩展名，支持: .obj, .gltf, .glb"
}
```

**3. 纹理文件缺失**
```json
{
  "error": "TextureNotFoundException",
  "solution": "确保纹理文件与模型文件在同一目录"
}
```

**4. 切片生成失败**
```json
{
  "error": "SlicingException",
  "solution": "检查模型完整性，尝试其他切片策略"
}
```

### 6. 监控和日志

#### 关键指标
- 任务处理时间
- 内存使用峰值
- 生成的切片数量
- 瓦片文件总大小
- 错误率

#### 日志级别
```csharp
// 生产环境
builder.Services.AddLogging(config => {
    config.SetMinimumLevel(LogLevel.Information);
    config.AddFile("logs/app-{Date}.log");
});

// 调试环境
builder.Services.AddLogging(config => {
    config.SetMinimumLevel(LogLevel.Debug);
    config.AddConsole();
});
```

---

## 更新历史

### v2.0.0 (2025-11-09)
- ✅ 实现工厂模式重构（切片策略和瓦片生成器）
- ✅ 添加 GLB 内嵌纹理自动导出功能
- ✅ 完善材质和纹理系统
- ✅ 优化 API 文档和使用示例
- ✅ 改进错误处理和日志记录

### v1.5.0 (2025-11-08)
- ✅ 集成 Obj2Tiles 功能
- ✅ 实现 5 种切片策略
- ✅ 支持 5 种瓦片格式
- ✅ 添加 QEM 网格简化
- ✅ 实现纹理图集生成

### v1.0.0 (2025-11-07)
- ✅ 基础切片系统
- ✅ B3DM 格式支持
- ✅ Grid 和 Octree 策略

---

## 许可证

MIT License

---

## 联系方式

- **项目仓库**: [GitHub](https://github.com/your-repo/RealScene3D)
- **问题反馈**: [Issues](https://github.com/your-repo/RealScene3D/issues)
- **文档**: [Wiki](https://github.com/your-repo/RealScene3D/wiki)

---

**最后更新**: 2025-11-09
**文档版本**: 2.0
**系统状态**: ✅ 生产就绪
