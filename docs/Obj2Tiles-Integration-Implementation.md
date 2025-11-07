# Obj2Tiles 集成实施文档

## 📋 项目概述

基于 [Obj2Tiles](https://github.com/OpenDroneMap/Obj2Tiles) 的架构和算法,重新实现 RealScene3D 的 3D Tiles 切片生成功能,以支持真正的 LOD(多层次细节)网格简化和优化的分层 tileset 结构。

## 🎯 核心目标

1. **实现基于 QEM 的网格简化**: 使用 Quadric Error Metric 算法生成真正的多分辨率 LOD
2. **改进 tileset.json 层次结构**: 支持父子节点关系和几何误差计算
3. **增强空间剖分策略**: 实现递归 N×N 剖分,类似 Obj2Tiles
4. **优化纹理处理**: (可选)矩形装箱算法整合纹理

## ✅ 已完成的工作

### 1. 网格简化服务 (MeshDecimationService.cs)

**位置**: `src/RealScene3D.Application/Services/MeshDecimationService.cs`

**核心功能**:
- **Quadric Error Metric (QEM) 算法**: 基于误差矩阵的边折叠算法
- **对称矩阵优化**: 4×4 对称矩阵使用 10 个值存储
- **边界保护**: 可选的边界顶点保护,保持模型轮廓
- **多 LOD 生成**: 使用 Obj2Tiles 的质量公式 `quality[i] = 1 - ((i + 1) / lods)`
- **统计和日志**: 详细的简化统计和进度监控

**关键类**:
```csharp
// 简化选项配置
public class DecimationOptions
{
    public double Quality { get; set; } = 1.0;  // 0.0-1.0
    public bool PreserveBoundary { get; set; } = true;
    public bool PreserveUV { get; set; } = false;
    public int MaxIterations { get; set; } = 100;
    public double Aggressiveness { get; set; } = 7.0;
}

// 简化结果
public class DecimatedMesh
{
    public List<Triangle> Triangles { get; set; }
    public int OriginalTriangleCount { get; set; }
    public int SimplifiedTriangleCount { get; set; }
    public double ReductionRatio { get; set; }
    public double QualityFactor { get; set; }
}
```

**核心方法**:
- `SimplifyMesh()`: 单次网格简化
- `GenerateLODs()`: 生成多级LOD
- `BuildMeshStructure()`: 构建顶点-三角形拓扑
- `ComputeVertexQuadrics()`: 计算二次误差矩阵
- `SimplifyMeshIterative()`: 迭代边折叠

### 2. 几何基础类扩展 (Geometry.cs)

**位置**: `src/RealScene3D.Domain/Entities/Geometry.cs`

**新增**:
- `Triangle` 类: 三角形几何单元 (待添加)
  - `ComputeNormal()`: 法向量计算
  - `ComputeArea()`: 面积计算
  - `ComputeCenter()`: 质心计算

### 3. 模型加载器接口 (IModelLoader.cs)

**位置**: `src/RealScene3D.Application/Interfaces/IModelLoader.cs`

**功能**:
- 统一的3D模型加载接口
- 支持多格式检测和加载
- 提取三角形网格和包围盒

## 🚧 进行中的工作

### 4. 网格简化集成到切片管道

**任务**: 将 MeshDecimationService 集成到现有的切片流程中

**实施步骤**:

#### 步骤 A: 创建模型加载器实现
```csharp
// OBJ 格式加载器
public class ObjModelLoader : IModelLoader
{
    // 解析 OBJ 文件,提取顶点和面数据
    // 计算模型包围盒
    // 返回三角形列表
}

// GLB/GLTF 格式加载器
public class GltfModelLoader : IModelLoader
{
    // 使用 SharpGLTF 或类似库加载
    // 提取几何数据
}
```

#### 步骤 B: 修改切片策略接口
```csharp
public interface ISlicingStrategy
{
    // 新增: 支持传入原始网格数据
    Task<List<Slice>> GenerateSlicesWithLODAsync(
        SlicingTask task,
        List<Triangle> sourceTriangles,  // 新增
        int level,
        SlicingConfig config,
        BoundingBox3D modelBounds,
        CancellationToken cancellationToken);
}
```

#### 步骤 C: 更新 GridSlicingStrategy
在 `GridSlicingStrategy.cs` 中:
1. 注入 `MeshDecimationService`
2. 在生成每个切片时:
   - 根据 LOD level 计算目标简化质量
   - 调用 `SimplifyMesh()` 简化网格
   - 使用简化后的网格生成 B3DM/GLB 文件

```csharp
private Slice? CreateSliceWithLOD(
    SlicingTask task,
    List<Triangle> triangles,  // 完整网格
    int level,
    SlicingConfig config,
    int x, int y, int z,
    BoundingBox3D modelBounds)
{
    // 1. 计算该切片的包围盒
    var sliceBounds = GenerateGridBoundingBox(...);

    // 2. 提取该切片范围内的三角形
    var sliceTriangles = ExtractTrianglesInBounds(triangles, sliceBounds);

    // 3. 根据 LOD 级别简化网格
    var quality = CalculateLODQuality(level, config.MaxLevel);
    var decimatedMesh = _meshDecimationService.SimplifyMesh(
        sliceTriangles,
        new DecimationOptions { Quality = quality });

    // 4. 生成 B3DM 文件
    GenerateB3DMFile(decimatedMesh.Triangles, slicePath);

    // 5. 返回切片元数据
    return new Slice { ... };
}
```

#### 步骤 D: B3DM 生成器增强
创建 `B3dmGenerator.cs`:
```csharp
public class B3dmGenerator
{
    // 从三角形列表生成 GLB 二进制数据
    byte[] GenerateGLB(List<Triangle> triangles);

    // 封装 GLB 为 B3DM 格式
    byte[] GenerateB3DM(byte[] glbData, BoundingBox3D bounds);
}
```

## 📝 待实施的工作

### 5. 改进的 tileset.json 层次结构生成

**目标**: 实现类似 Obj2Tiles 的分层 tileset 结构

**核心功能**:
- 父子节点关系 (`children` 属性)
- 几何误差计算 (`geometricError`)
- 内容引用 (`content.uri`)
- 包围体积 (`boundingVolume.box`)

**实施要点**:
```json
{
  "asset": { "version": "1.0" },
  "geometricError": 1000,
  "root": {
    "boundingVolume": { ... },
    "geometricError": 500,
    "refine": "REPLACE",
    "content": { "uri": "0/0_0_0.b3dm" },
    "children": [
      {
        "boundingVolume": { ... },
        "geometricError": 250,
        "content": { "uri": "1/0_0_0.b3dm" },
        "children": [ ... ]
      }
    ]
  }
}
```

**几何误差计算** (参考 Obj2Tiles):
```csharp
double CalculateGeometricError(int level, int maxLevel, BoundingBox3D bounds)
{
    // 基于包围盒对角线长度和 LOD 级别
    var diagonal = CalculateDiagonalLength(bounds);
    var errorFactor = Math.Pow(2, maxLevel - level);
    return diagonal * errorFactor * 0.1;  // 调整系数
}
```

### 6. 增强的空间剖分策略

**目标**: 实现递归 N×N 剖分,支持四叉树/八叉树结构

**新策略**: `RecursiveSubdivisionStrategy`
- 从粗粒度父节点开始
- 递归剖分为 4 个(2D) 或 8 个(3D) 子节点
- 动态决定剖分深度(基于几何密度)

### 7. 集成测试和验证

**测试场景**:
1. 小规模模型 (< 10MB OBJ): 验证基本功能
2. 中等规模模型 (10-100MB): 测试性能和LOD质量
3. 大规模模型 (> 100MB): 压力测试和内存优化

**验证指标**:
- LOD 生成质量 (视觉效果)
- 简化率 (三角形数量减少比例)
- 生成速度 (切片/秒)
- 内存占用
- tileset.json 正确性 (Cesium 加载测试)

## 🏗️ 架构图

```
┌─────────────────────────────────────────────────────────┐
│                   SlicingController                      │
│                  (接收切片请求)                          │
└────────────────────┬────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────┐
│                    SlicingService                        │
│          (切片任务管理和流程编排)                        │
└────┬────────────────┬────────────────┬──────────────────┘
     │                │                │
     ▼                ▼                ▼
┌─────────┐    ┌──────────────┐  ┌──────────────────┐
│ Model   │    │  Slicing     │  │  Mesh           │
│ Loader  │───>│  Strategy    │<─│  Decimation      │
│         │    │  (Grid/      │  │  Service         │
│ (OBJ/   │    │   Octree)    │  │  (QEM 简化)      │
│  GLTF)  │    └──────┬───────┘  └──────────────────┘
└─────────┘           │
                      ▼
          ┌───────────────────────┐
          │   B3DM Generator       │
          │   (生成 3D Tiles)      │
          └───────────┬───────────┘
                      │
                      ▼
          ┌───────────────────────┐
          │  Tileset.json         │
          │  Generator            │
          │  (分层结构)           │
          └───────────────────────┘
```

## 📚 参考资料

### Obj2Tiles 核心实现
- **Mesh Decimation**: [fqms.py](https://github.com/OpenDroneMap/Obj2Tiles/blob/master/obj2tiles/fqms.py) - Fast Quadric Mesh Simplification
- **Splitting**: [splitter.py](https://github.com/OpenDroneMap/Obj2Tiles/blob/master/obj2tiles/splitter.py) - 递归空间剖分
- **Tileset Generation**: [converter.py](https://github.com/OpenDroneMap/Obj2Tiles/blob/master/obj2tiles/converter.py) - 3D Tiles 生成

### 相关论文和规范
- Garland & Heckbert (1997): "Surface Simplification Using Quadric Error Metrics"
- Cesium 3D Tiles Specification: https://github.com/CesiumGS/3d-tiles
- glTF 2.0 Specification: https://github.com/KhronosGroup/glTF

## 🔄 下一步行动

1. ✅ **完成 Triangle 类**: 添加到 Geometry.cs
2. 🚧 **实现 ObjModelLoader**: 基础 OBJ 文件加载
3. 🔜 **集成 MeshDecimationService**: 修改 GridSlicingStrategy
4. 🔜 **实现 B3dmGenerator**: GLB/B3DM 二进制生成
5. 🔜 **增强 tileset.json**: 分层结构和几何误差
6. 🔜 **测试和优化**: 端到端测试流程

## 💡 实施建议

### 性能优化
1. **并行处理**: 每个切片独立简化,可并行处理
2. **内存管理**: 使用对象池减少 GC 压力
3. **增量生成**: 支持断点续传和增量更新
4. **缓存机制**: LOD 结果缓存,避免重复计算

### 代码质量
1. **单元测试**: 针对 QEM 算法的正确性测试
2. **集成测试**: 端到端的切片生成测试
3. **性能测试**: 大规模模型的性能基准测试
4. **文档完善**: API 文档和使用示例

### 扩展性考虑
1. **插件化**: 支持自定义简化算法
2. **多格式支持**: 扩展更多 3D 格式加载器
3. **云原生**: 支持分布式切片生成
4. **流式处理**: 大模型的流式加载和处理

---

**最后更新**: 2025-01-23
**负责人**: Claude
**状态**: 进行中 (40% 完成)
