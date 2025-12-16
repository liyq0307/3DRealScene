# 3D Real Scene 完整开发指南

本文档整合了编译、部署、存储架构等所有详细说明。基于实际代码库更新，确保与项目实现保持一致。

**最后更新**: 2025-12-16
**版本**: v1.0
**技术栈**: ASP.NET Core 9.0 + Vue 3 + PostgreSQL/PostGIS + MongoDB + Redis + MinIO

---

## 目录

- [项目架构](#项目架构)
- [编译和运行](#编译和运行)
- [存储架构](#存储架构)
- [3D切片功能](#3d切片功能)
  - [切片功能概述](#切片功能概述)
  - [四叉树空间分割](#四叉树空间分割)
  - [切片配置参数](#切片配置参数)
  - [瓦片生成流水线](#瓦片生成流水线)
  - [API接口使用指南](#api接口使用指南)
  - [切片文件格式](#切片文件格式)
  - [切片处理流程](#切片处理流程)
- [工作流引擎](#工作流引擎)
- [监控系统](#监控系统)
- [部署指南](#部署指南)
- [故障排除](#故障排除)
- [性能优化](#性能优化)

---

## 项目架构

### 系统架构概览

```
┌─────────────────────────────────────────────────────────────┐
│                     前端层 (Vue 3 + Three.js)                │
│  - 3D场景可视化  - 切片管理  - 工作流设计  - 监控仪表板         │
└────────────────────────────┬────────────────────────────────┘
                             │ HTTP/REST API
┌────────────────────────────▼────────────────────────────────┐
│              API层 (ASP.NET Core 9.0 WebAPI)                │
│  Controllers: Scenes, Slicing, Workflows, Monitoring        │
└────────────────────────────┬────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────┐
│                应用层 (Application Layer)                    │
│  Services: SceneService, SlicingAppService,                 │
│           WorkflowService, MonitoringService                │
│                                                             │
│  ┌─── 切片系统架构  ────────────────────────────────┐       │
│  │  SlicingAppService (主入口)                     │       │
│  │         ↓                                       │       │
│  │  SlicingProcessor (任务调度)                    │       │
│  │         ↓                                       │       │
│  │  TileGenerationPipeline (四阶段流水线)          │       │
│  │    ├─ Stage 0: 加载模型                         │       │
│  │    ├─ Stage 1: 网格简化 (MeshDecimationService) │       │
│  │    ├─ Stage 2: 空间分割 (SpatialSplitterService)│       │
│  │    ├─ Stage 3: 切片生成 (GltfGenerator等)       │       │
│  │    └─ Stage 4: 索引生成 (TilesetGenerator)      │       │
│  │                                                  │       │
│  │  增量更新: IncrementalUpdateService             │       │
│  └──────────────────────────────────────────────────┘       │
└────────────────────────────┬────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────┐
│                领域层 (Domain Layer)                         │
│  Entities: Scene3D, SlicingTask, Slice, SlicingConfig,      │
│            User, Workflow, Geometry                         │
│  Enums: SlicingTaskStatus, TextureStrategy, StorageLocation │
│  Domain Services: SpatialAnalysisService                    │
└────────────────────────────┬────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────┐
│             基础设施层 (Infrastructure Layer)                │
│  - PostgreSqlDbContext (主数据库 - 切片任务、进度)           │
│  - MongoDbContext (文档存储 - 元数据)                        │
│  - RedisCacheService (缓存服务 - 热点数据)                   │
│  - MinioStorageService (对象存储 - 切片文件、模型)           │
│  - Repository<T> (仓储实现)                                  │
│  - UnitOfWork (工作单元)                                     │
└─────────────────────────────────────────────────────────────┘
```

### 切片系统数据流

```
用户上传模型 (.obj/.gltf)
    ↓
MinIO 对象存储
    ↓
SlicingAppService 创建任务
    ↓
SlicingProcessor 启动处理
    ↓
TileGenerationPipeline 流水线处理
    │
    ├─→ Stage 0: 加载模型数据
    │   └─ 解析几何、材质、纹理
    │
    ├─→ Stage 1: 网格简化 (可选)
    │   └─ QEM算法生成多级LOD
    │
    ├─→ Stage 2: 四叉树空间分割
    │   └─ SAT算法精确相交测试
    │
    ├─→ Stage 3: 切片生成
    │   ├─ 纹理重打包 (TextureAtlasGenerator)
    │   ├─ GLTF生成 (GltfGenerator)
    │   └─ B3DM封装
    │
    └─→ Stage 4: 索引生成
        └─ tileset.json (TilesetGenerator)
    ↓
切片文件存储 (MinIO/本地)
    ↓
前端加载渲染 (Cesium/Three.js)
```

### 项目结构

```
src/
├── RealScene3D.Domain/                 # 领域层
│   ├── Entities/                       # 实体类
│   │   ├── BaseEntity.cs               # 基础实体
│   │   ├── User.cs                     # 用户实体
│   │   ├── Scene3D.cs                  # 场景实体
│   │   ├── SceneObject.cs              # 场景对象实体
│   │   ├── Slicing.cs                  # 切片任务和切片实体
│   │   ├── SlicingConfig.cs            # 切片配置实体
│   │   ├── Geometry.cs                 # 几何实体
│   │   └── Workflow.cs                 # 工作流实体
│   ├── Interfaces/                     # 接口定义
│   │   ├── IRepository.cs              # 仓储接口
│   │   └── IUnitOfWork.cs              # 工作单元接口
│   ├── Enums/                          # 枚举定义
│   │   └── SlicingEnums.cs             # 切片枚举（任务状态、纹理策略等）
│   └── Services/                       # 领域服务
│       ├── SlicingService.cs           # 切片领域服务
│       └── SpatialAnalysisService.cs   # 空间分析服务
│
├── RealScene3D.Infrastructure/         # 基础设施层
│   ├── Data/                           # 数据上下文
│   │   ├── ApplicationDbContext.cs     # 通用数据库上下文
│   │   └── PostgreSqlDbContext.cs      # PostgreSQL上下文
│   ├── MongoDB/                        # MongoDB集成
│   │   └── MongoDbContext.cs           # MongoDB上下文
│   ├── Redis/                          # Redis集成
│   │   └── RedisCacheService.cs        # Redis缓存服务
│   ├── MinIO/                          # MinIO集成
│   │   └── MinioStorageService.cs      # MinIO存储服务
│   ├── Repositories/                   # 仓储实现
│   │   ├── Repository.cs               # 通用仓储
│   │   └── UnitOfWork.cs               # 工作单元
│   └── Workflow/                       # 工作流引擎
│       ├── WorkflowEngine.cs           # 工作流引擎
│       ├── DelayNodeExecutor.cs        # 延迟节点执行器
│       └── ConditionNodeExecutor.cs    # 条件节点执行器
│
├── RealScene3D.Application/            # 应用层
│   ├── Services/                       # 应用服务
│   │   ├── UserService.cs              # 用户服务
│   │   ├── SceneService.cs             # 场景服务
│   │   ├── SceneObjectService.cs       # 场景对象服务
│   │   ├── WorkflowService.cs          # 工作流服务
│   │   ├── MonitoringAppService.cs     # 监控服务
│   │   ├── Obj2TilesService.cs         # OBJ转3D Tiles服务
│   │   │
│   │   ├── Slicing/                    # 切片服务模块
│   │   │   ├── SlicingAppService.cs            # 切片应用服务（主入口）
│   │   │   ├── SlicingProcessor.cs             # 切片处理器
│   │   │   ├── SlicingDataService.cs           # 切片数据服务
│   │   │   ├── TileGenerationPipeline.cs       # 瓦片生成流水线（四阶段处理）
│   │   │   ├── IncrementalUpdateService.cs     # 增量更新服务
│   │   │   └── TaskProgressHistory.cs          # 任务进度历史
│   │   │
│   │   ├── Generators/                 # 生成器模块
│   │   │   ├── GltfGenerator.cs                # GLTF生成器
│   │   │   ├── TilesetGenerator.cs             # Tileset索引生成器
│   │   │   ├── TileGeneratorFactory.cs         # Tile生成器工厂
│   │   │   └── TextureAtlasGenerator.cs        # 纹理图集生成器
│   │   │
│   │   ├── Loaders/                    # 加载器模块
│   │   │   └── MtlParser.cs                    # MTL材质解析器
│   │   │
│   │   ├── MeshDecimationService.cs            # 网格简化服务（LOD生成）
│   │   ├── SpatialSplitterService.cs           # 空间分割服务（四叉树算法）
│   │   └── SlicingUtilities.cs                 # 切片工具类
│   │
│   ├── Interfaces/                     # 服务接口
│   │   ├── IUserService.cs
│   │   ├── ISceneService.cs
│   │   ├── ISlicingAppService.cs
│   │   ├── ISlicingProcessor.cs
│   │   ├── IWorkflowService.cs
│   │   └── IMonitoringService.cs
│   └── DTOs/                           # 数据传输对象
│       ├── UserDtos.cs
│       ├── SceneDtos.cs
│       └── SlicingDtos.cs
│
├── RealScene3D.WebApi/                 # API层
│   ├── Controllers/                    # 控制器
│   │   ├── UsersController.cs          # 用户控制器
│   │   ├── ScenesController.cs         # 场景控制器
│   │   ├── SceneObjectsController.cs   # 场景对象控制器
│   │   ├── SlicingController.cs        # 切片控制器
│   │   ├── WorkflowsController.cs      # 工作流控制器
│   │   └── MonitoringController.cs     # 监控控制器
│   ├── Program.cs                      # 应用入口
│   └── appsettings.json                # 配置文件
│
└── RealScene3D.Web/                    # 前端
    ├── src/
    │   ├── components/                 # 组件库
    │   ├── views/                      # 页面视图
    │   ├── stores/                     # 状态管理
    │   ├── services/                   # API服务
    │   └── router/                     # 路由配置
    └── package.json
```

## 编译和运行

### 前提条件

- **.NET 9.0 SDK** 或更高版本
- **Node.js 18+** 和 npm
- **Docker & Docker Compose** (用于运行存储服务)
- **Git** (用于克隆代码库)

### 快速启动步骤

#### 1. 启动存储服务（Docker）

```bash
# 启动所有存储服务（PostgreSQL、MongoDB、Redis、MinIO）
docker-compose -f docker-compose.storage.yml up -d

# 等待服务完全启动（约30秒）
sleep 30

# 检查服务状态
docker-compose -f docker-compose.storage.yml ps
```

**存储服务地址**:
- PostgreSQL: `localhost:5432` (用户: postgres, 密码: postgres)
- MongoDB: `localhost:27017` (用户: admin, 密码: admin123)
- Redis: `localhost:6379` (密码: redis123)
- MinIO API: `localhost:9000` (访问密钥: minioadmin)
- MinIO Console: `http://localhost:9001` (用户: minioadmin, 密码: minioadmin123)

#### 2. 初始化MinIO存储桶（首次启动）

```bash
# 配置MinIO客户端别名
docker exec realscene3d-minio mc alias set myminio http://localhost:9000 minioadmin minioadmin

# 创建所有必需的存储桶
docker exec realscene3d-minio mc mb myminio/tilt-photography
docker exec realscene3d-minio mc mb myminio/bim-models
docker exec realscene3d-minio mc mb myminio/models-3d
docker exec realscene3d-minio mc mb myminio/videos
docker exec realscene3d-minio mc mb myminio/textures
docker exec realscene3d-minio mc mb myminio/thumbnails

# 验证存储桶创建
docker exec realscene3d-minio mc ls myminio
```

#### 3. 配置应用程序

编辑 `src/RealScene3D.WebApi/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "PostgreSqlConnection": "Host=localhost;Port=5432;Database=RealScene3D;Username=postgres;Password=postgres",
    "MongoDbConnection": "mongodb://admin:admin123@localhost:27017",
    "RedisConnection": "localhost:6379,password=redis123"
  },
  "MongoDB": {
    "DatabaseName": "RealScene3D"
  },
  "MinIO": {
    "Endpoint": "localhost:9000",
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin",
    "UseSSL": false
  }
}
```

#### 4. 编译和运行后端

```bash
# 编译整个解决方案
dotnet build RealScene3D.sln

# 创建和应用数据库迁移（首次启动）
cd src/RealScene3D.Infrastructure
dotnet ef migrations add InitialCreate_PostgreSQL \
    --context PostgreSqlDbContext \
    --startup-project ../RealScene3D.WebApi \
    --output-dir Migrations/PostgreSQL

dotnet ef database update \
    --context PostgreSqlDbContext \
    --startup-project ../RealScene3D.WebApi

# 运行API服务
cd ../RealScene3D.WebApi
dotnet run
```

**方法2: 使用热重载 (开发推荐)**
```bash
cd src/RealScene3D.WebApi
dotnet watch run
```

**后端访问地址:**
- HTTP API: `http://localhost:5000`
- HTTPS API: `https://localhost:7149`
- Swagger文档: `http://localhost:5000/swagger`
- 根路径: `http://localhost:5000/` (自动重定向到Swagger)

#### 5. 运行前端

```bash
cd src/RealScene3D.Web

# 安装依赖（首次启动）
npm install

# 启动开发服务器
npm run dev

# 或者使用生产构建
npm run build
```

**前端访问地址:**
- 开发服务器: `http://localhost:5173`

### 验证安装

#### 检查后端服务

```bash
# 测试API健康状态
curl http://localhost:5000/swagger

# 测试PostgreSQL连接
docker exec realscene3d-postgres pg_isready -U postgres

# 测试MongoDB连接
docker exec realscene3d-mongodb mongosh --eval "db.adminCommand('ping')" -u admin -p admin123

# 测试Redis连接
docker exec realscene3d-redis redis-cli -a redis123 ping

# 测试MinIO连接
curl http://localhost:9000/minio/health/live
```

#### 检查前端服务

访问 `http://localhost:5173`，应该能看到应用主页。

### 系统初始化日志

当后端启动时，你会在控制台看到类似以下的初始化日志：

```
=== 开始初始化存储系统 ===
系统架构：前端Vue.js + 后端ASP.NET Core WebAPI + 异构存储层
支持存储：PostgreSQL/PostGIS、MongoDB、Redis、MinIO

正在执行PostgreSQL数据库迁移...
✓ PostgreSQL/PostGIS database initialized successfully

正在初始化MinIO存储桶...
✓ MinIO storage buckets initialized successfully
存储桶列表：倾斜摄影、BIM模型、视频、3D模型、纹理、缩略图

✓ Redis cache connection established successfully
✓ MongoDB connection established successfully

=== 异构融合存储系统启动完成 ===
PostgreSQL/PostGIS: 地形、模型、业务数据
MongoDB: 非结构化数据（视频元数据、倾斜摄影、BIM）
Redis: 会话缓存、热点数据
MinIO: 文件对象存储
```

### 常见启动问题

#### 问题1: 端口已被占用

```bash
# 查看占用端口的进程
# Windows
netstat -ano | findstr :5177
# Linux/Mac
lsof -i :5177

# 修改端口配置（如果需要）
# 编辑 src/RealScene3D.WebApi/Properties/launchSettings.json
```

#### 问题2: Docker服务未启动

```bash
# 检查Docker服务状态
docker ps

# 如果服务未启动，重启Docker Desktop或Docker守护进程
```

#### 问题3: 数据库连接失败

```bash
# 查看PostgreSQL日志
docker logs realscene3d-postgres

# 重启PostgreSQL容器
docker restart realscene3d-postgres
```

---

## 存储架构

### 存储系统设计理念

RealScene3D采用**异构融合存储架构**，根据数据特性选择最优的存储方案：

- **PostgreSQL/PostGIS**: 结构化数据 + 空间数据（ACID保证）
- **MongoDB**: 非结构化文档数据（灵活Schema）
- **Redis**: 高速缓存（微秒级响应）
- **MinIO**: 对象存储（大文件、媒体资源）

### 存储策略详情

| 存储系统 | 数据类型 | 特点 | 应用场景 |
|---------|---------|------|---------|
| **PostgreSQL/PostGIS** | 用户、场景、业务数据、地理空间数据 | 强一致性、空间索引、复杂查询 | 用户管理、场景CRUD、GIS分析 |
| **MongoDB** | 视频元数据、倾斜摄影元数据、BIM元数据 | 灵活Schema、水平扩展、文档存储 | 非结构化元数据管理 |
| **Redis** | 会话、热点数据、计数器、临时数据 | 高性能、内存存储、多数据结构 | 缓存、会话管理、实时计数 |
| **MinIO** | 3D模型、视频文件、纹理贴图、大文件 | S3兼容、分布式、高可用 | 文件存储、CDN源站、备份 |

### 存储桶设计策略

MinIO存储桶按照业务类型进行分类管理：

```csharp
// MinIO存储桶定义（来自实际代码）
public static class MinioBuckets
{
    public const string TILT_PHOTOGRAPHY = "tilt-photography";  // 倾斜摄影数据
    public const string BIM_MODELS = "bim-models";              // BIM模型文件
    public const string MODELS_3D = "models-3d";                // 通用3D模型
    public const string VIDEOS = "videos";                      // 视频文件
    public const string TEXTURES = "textures";                  // 纹理贴图
    public const string THUMBNAILS = "thumbnails";              // 缩略图
}
```

### 数据流架构

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   前端应用       │───▶│    API层        │───▶│  缓存层(Redis)  │
│  - 3D渲染       │    │  - 请求验证     │    │  - 会话缓存     │
│  - 文件上传     │    │  - 业务逻辑     │    │  - 热点数据     │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         ▼                       ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│  MinIO存储       │    │ PostgreSQL      │    │   MongoDB       │
│  - 大文件存储   │    │  - 结构化数据   │    │  - 文档数据     │
│  - 媒体资源     │    │  - 空间数据     │    │  - 元数据       │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

---

#### 1. PostgreSQL/PostGIS - 主数据库
**用途**: 结构化数据 + GIS空间数据

**创建数据库:**
```bash
# 使用Docker
docker exec -it realscene3d-postgres psql -U postgres

# 创建数据库
CREATE DATABASE RealScene3D;

# 启用PostGIS扩展
\c RealScene3D
CREATE EXTENSION postgis;
```

**数据库迁移:**
```bash
cd src/RealScene3D.Infrastructure

# 创建迁移
dotnet ef migrations add InitialCreate_PostgreSQL \
    --context PostgreSqlDbContext \
    --startup-project ../RealScene3D.WebApi \
    --output-dir Migrations/PostgreSQL

# 应用迁移
dotnet ef database update \
    --context PostgreSqlDbContext \
    --startup-project ../RealScene3D.WebApi
```

**空间查询示例:**
```sql
-- 查找半径1km内的场景
SELECT id, name FROM scenes
WHERE ST_DWithin(
    center_point,
    ST_SetSRID(ST_MakePoint(116.40, 39.90, 0), 4326)::geography,
    1000
);

-- 计算场景边界面积
SELECT id, name, ST_Area(boundary::geography) / 1000000 as area_km2
FROM scenes;
```

#### 2. MongoDB - 文档存储
**用途**: 非结构化元数据

**连接MongoDB:**
```bash
docker exec -it realscene3d-mongodb mongosh -u admin -p admin123
```

**数据操作:**
```javascript
use RealScene3D

// 插入视频元数据
db.video_metadata.insertOne({
    sceneId: UUID("..."),
    fileName: "demo.mp4",
    fileSize: 52428800,
    duration: 120.0,
    resolution: { width: 1920, height: 1080 },
    codec: "h264",
    bitrate: 5000,
    frameRate: 30.0,
    uploadedAt: new Date()
});

// 查询视频
db.video_metadata.find({ sceneId: UUID("...") }).pretty();

// 创建索引
db.video_metadata.createIndex({ sceneId: 1 });
db.video_metadata.createIndex({ uploadedAt: -1 });
```

#### 3. Redis - 缓存
**用途**: 高速缓存，会话管理

**连接Redis:**
```bash
docker exec -it realscene3d-redis redis-cli -a redis123
```

**缓存操作:**
```bash
# 设置缓存
SET session:abc123 "user_data" EX 3600

# 获取缓存
GET session:abc123

# 递增计数器
INCR counter:scene:uuid:views

# Hash操作
HSET user:uuid name "John"
HSET user:uuid email "john@example.com"
HGETALL user:uuid

# 查看所有键
KEYS *
```

**C# 代码示例:**
```csharp
// 设置缓存
await _redis.SetAsync("scene:uuid", sceneData, TimeSpan.FromMinutes(10));

// 获取缓存
var data = await _redis.GetAsync<SceneDto>("scene:uuid");

// 递增计数
await _redis.IncrementAsync("counter:scene:uuid:views");
```

#### 4. MinIO - 对象存储
**用途**: 大文件存储

**访问 MinIO Console:**
- URL: `http://localhost:9001`
- 用户: `minioadmin`
- 密码: `minioadmin123`

**使用mc命令行:**
```bash
# 配置
mc alias set myminio http://localhost:9000 minioadmin minioadmin123

# 列出存储桶
mc ls myminio

# 上传文件
mc cp /path/to/model.glb myminio/models-3d/

# 下载文件
mc cp myminio/models-3d/model.glb ./

# 生成分享链接（7天有效）
mc share download --expire 7d myminio/models-3d/model.glb
```

**C# 代码示例:**
```csharp
// 上传文件
using var stream = File.OpenRead("model.glb");
var path = await _minioService.UploadFileAsync(
    MinioBuckets.MODELS_3D,
    "furniture/chair.glb",
    stream,
    FileTypes.GLB
);

// 生成下载URL
var url = await _minioService.GetPresignedUrlAsync(
    MinioBuckets.MODELS_3D,
    "furniture/chair.glb",
    expiryInSeconds: 3600
);
```

### 存储桶设计

| 存储桶 | 用途 | 示例文件 |
|-------|------|---------|
| `tilt-photography` | 倾斜摄影 | tileset.json, *.osgb |
| `bim-models` | BIM模型 | *.ifc, *.rvt |
| `models-3d` | 3D模型 | *.glb, *.gltf, *.obj |
| `videos` | 视频 | *.mp4, *.avi |
| `textures` | 纹理 | *.jpg, *.png |
| `thumbnails` | 缩略图 | *.webp |

---

## 几何库和材质系统

### 几何库概述

RealScene3D包含完整的几何处理库，支持3D模型的加载、处理和渲染优化：

#### 核心几何类

1. **向量类**：
   - `Vector2`, `Vector2d`, `Vector2i` - 2D向量（浮点、双精度、整数）
   - `Vector3`, `Vector3d`, `Vector3i` - 3D向量（浮点、双精度、整数）
   - `Vector4`, `Vector4d`, `Vector4i` - 4D向量（浮点、双精度、整数）

2. **几何体类**：
   - `Box2`, `Box3` - 2D/3D包围盒
   - `Rectangle` - 矩形
   - `Edge`, `Face` - 边和面

3. **网格系统**：
   - `IMesh` - 网格接口，定义网格基本操作
   - `Mesh`, `MeshT` - 网格实现类
   - `Vertex2`, `Vertex3` - 2D/3D顶点

#### 网格分割算法

系统使用IMesh.Split方法进行网格分割，支持两种分割策略：

```csharp
// 使用IMesh.Split方法进行网格分割
int Split(IVertexUtils utils, double q, out IMesh left, out IMesh right);

// 获取顶点重心
Vertex3 GetVertexBaricenter();
```

**分割点策略**：
- `AbsoluteCenter`：使用网格边界框的中心点作为分割点
- `VertexBaricenter`：使用网格顶点的重心作为分割点

### 材质系统

系统包含完整的材质管理系统，支持复杂的材质和纹理处理：

#### 核心材质类

1. **Material** - 基础材质类
   - 支持环境光、漫反射、镜面反射颜色
   - 支持透明度、折射率、光泽度等参数
   - 支持纹理映射和UV坐标

2. **MaterialEx** - 扩展材质类
   - 支持更复杂的材质属性
   - 支持多层材质叠加
   - 支持自定义着色器参数

3. **IlluminationModel** - 光照模型
   - 支持多种光照计算模型
   - 支持环境光、点光源、方向光、聚光灯

#### 纹理缓存系统

`TexturesCache` 类提供纹理缓存管理：
- 自动加载和缓存纹理文件
- 支持纹理压缩和格式转换
- 内存优化，避免重复加载

#### RGB颜色系统

`RGB` 类提供颜色处理功能：
- RGB颜色表示和转换
- 颜色混合和插值
- 颜色空间转换（RGB、HSV、HSL）

### 网格简化算法

系统使用FastQuadricMesh算法进行网格简化：

```csharp
// 创建简化算法实例
var algorithm = MeshDecimation.CreateAlgorithm(Algorithm.FastQuadricMesh);

// 执行网格简化
var simplifiedMesh = algorithm.DecimateMesh(originalMesh, targetTriangleCount);
```

**算法特点**：
- 基于二次误差度量的网格简化
- 保持几何特征和纹理坐标
- 支持多级LOD生成
- 高性能，适合大规模模型处理

---

## 3D切片功能

### 切片功能概述

3D切片功能是RealScene3D系统的核心特性之一，用于将大型3D模型分割成多个小的、可管理的切片，以提高渲染性能和数据传输效率。该功能采用了先进的计算机图形学算法，支持四叉树空间分割和LOD网格简化技术。

#### 核心特性

- **四叉树空间分割**：采用递归四叉树算法进行精确的空间剖分
- **LOD网格简化**：使用QEM（二次误差度量）算法生成多级细节，根据视距动态切换模型精度
- **智能纹理处理**：自动纹理重打包，每个切片只包含实际使用的纹理区域，大幅减少文件体积
- **多格式输出**：支持B3DM、GLTF等3D Tiles标准格式
- **增量更新**：支持模型的增量切片更新，无需完全重新处理
- **精确相交测试**：基于分离轴定理（SAT）的三角面-AABB相交测试，确保空间分割的准确性
- **并行处理**：支持多线程并行切片生成，提高处理速度

#### 适用场景

- **大规模城市模型**：倾斜摄影模型、BIM模型的切片处理
- **复杂工业场景**：工厂、设备等复杂3D模型的渲染优化
- **虚拟现实应用**：VR/AR场景的高性能渲染
- **WebGL应用**：浏览器端3D场景的流式加载和渲染

#### 技术优势

1. **四叉树空间分割**：根据模型几何分布自动调整切片大小，平衡细节和性能
2. **高效空间索引**：采用层次化索引结构，支持快速空间查询
3. **内存优化**：采用流式处理和增量生成，处理超大规模模型
4. **网络传输优化**：支持纹理压缩和智能重打包，减少带宽占用

### 四叉树空间分割

系统采用递归四叉树算法进行空间剖分，这是一种高效的二维空间分割技术，特别适合地理空间数据和大规模3D场景的处理。

#### 算法原理

四叉树分割是一种递归的空间剖分算法，每次递归将当前空间单元沿X和Y轴同时分割，产生4个子节点：

- **XL-YL**：左下象限（X低，Y低）
- **XL-YH**：左上象限（X低，Y高）
- **XH-YL**：右下象限（X高，Y低）
- **XH-YH**：右上象限（X高，Y高）

**分割深度控制**：
- `Divisions = 1`：产生 2×2 = 4 个空间单元
- `Divisions = 2`：产生 4×4 = 16 个空间单元
- `Divisions = 3`：产生 8×8 = 64 个空间单元
- `Divisions = n`：产生 2^n × 2^n 个空间单元

#### 相交测试

使用基于分离轴定理（SAT）的精确三角面-AABB相交测试，确保空间分割的准确性：

1. **测试3个AABB法线**（X、Y、Z轴）：检测三角形是否在包围盒外侧
2. **测试三角形法线**：检测包围盒是否在三角形平面外侧
3. **测试9个边叉乘轴**：检测三角形边与包围盒边的交叉情况

通过这些测试，确保只有真正相交的三角形才会被分配到该空间单元，避免重复和遗漏。

#### 优势特点

**优点**：
- 自适应几何分布，细节丰富区域自动获得更多切片
- 空间索引结构简单高效，查询性能优秀
- 适合地理空间数据和大规模城市模型
- 平衡了切片数量和渲染性能

**适用场景**：
- 地形数据和倾斜摄影模型
- 城市建筑群和大规模场景
- 地理信息系统（GIS）应用
- 需要快速空间查询的场景

#### 配置示例

```csharp
// 基础配置 - 适合中小型模型（4个空间单元）
var config = new SlicingConfig
{
    TileSize = 100.0,
    Divisions = 1,              // 产生 2×2 = 4 个空间单元
    LodLevels = 3,
    EnableMeshDecimation = true,
    GenerateTileset = true,
    OutputFormat = "b3dm"
};

// 标准配置 - 适合中等规模模型（16个空间单元）
var config = new SlicingConfig
{
    TileSize = 100.0,
    Divisions = 2,              // 产生 4×4 = 16 个空间单元
    LodLevels = 3,
    EnableMeshDecimation = true,
    TextureStrategy = TextureStrategy.Repack,
    OutputFormat = "b3dm"
};

// 高精度配置 - 适合大规模城市模型（64个空间单元）
var config = new SlicingConfig
{
    TileSize = 50.0,
    Divisions = 3,              // 产生 8×8 = 64 个空间单元
    LodLevels = 4,
    EnableMeshDecimation = true,
    TextureStrategy = TextureStrategy.RepackCompressed,
    EnableIncrementalUpdates = true,
    OutputFormat = "b3dm"
};
```

### 切片配置参数

切片配置通过 `SlicingConfig` 类进行控制，以下是主要参数详解：

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `TileSize` | double | 100.0 | 基础切片大小（米），影响剖分粒度 |
| `Divisions` | int | 2 | 空间分割递归深度，产生 2^n × 2^n 个空间单元 |
| `LodLevels` | int | 3 | LOD级别数量，控制网格简化层次 |
| `EnableMeshDecimation` | bool | true | 是否启用网格简化（LOD生成） |
| `GenerateTileset` | bool | true | 是否生成 tileset.json 索引文件 |
| `OutputFormat` | string | "b3dm" | 输出格式：b3dm、gltf |
| `TextureStrategy` | enum | Repack | 纹理处理策略：KeepOriginal、Compress、Repack、RepackCompressed |
| `EnableIncrementalUpdates` | bool | false | 是否启用增量更新支持 |
| `StorageLocation` | enum | MinIO | 存储位置：MinIO 或 LocalFileSystem |
| `GeometricErrorThreshold` | double | 0.001 | LOD切换的几何误差阈值 |
| `SplitPointStrategy` | enum | AbsoluteCenter | 分割点策略：AbsoluteCenter（绝对中心）、VertexBaricenter（顶点重心） |

### 瓦片生成流水线

`TileGenerationPipeline` 是切片系统的核心处理流程，集成了网格简化、空间分割和切片生成的完整流程。

#### Stage 0: 加载模型数据

**功能**：
- 加载源3D模型文件（OBJ、GLTF等格式）
- 解析几何数据（顶点、法线、UV坐标）
- 加载材质和纹理信息
- 计算模型整体包围盒

**输出**：
- 完整的网格数据结构
- 材质和纹理映射关系
- 模型空间边界信息

#### Stage 1: Decimation（网格简化）

**功能**：
- 使用FastQuadricMesh算法（快速四元网格简化）对模型进行网格简化
- 生成多级LOD（Level of Detail）
- 每个LOD级别保持视觉质量的同时减少三角形数量

**LOD级别示例**：
- **LOD0**：原始模型（100%三角形）
- **LOD1**：简化50%（50%三角形）
- **LOD2**：简化75%（25%三角形）
- **LOD3**：简化87.5%（12.5%三角形）

**参数控制**：
- `LodLevels`：控制生成的LOD级别数量
- `EnableMeshDecimation`：是否启用网格简化

#### Stage 2 & 3: 空间分割与切片生成

**功能**：
- 使用IMesh.Split方法进行递归空间分割
- 根据SplitPointStrategy选择分割点（绝对中心或顶点重心）
- 对每个分割后的网格生成切片文件
- 应用纹理处理策略（KeepOriginal、Compress、Repack、RepackCompressed）

**空间分割过程**：
1. 计算模型的整体包围盒
2. 根据 `Divisions` 参数递归分割空间
3. 使用IMesh.Split方法进行网格分割
4. 应用纹理处理策略
5. 输出B3DM或GLTF格式文件

**参数控制**：
- `Divisions`：控制空间分割深度
- `TextureStrategy`：纹理处理策略
- `OutputFormat`：输出文件格式
- `SplitPointStrategy`：分割点策略

#### Stage 4: 生成 tileset.json

**功能**：
- 生成3D Tiles标准的索引文件
- 包含切片层次结构和包围盒信息
- 定义LOD切换的几何误差阈值
- 支持Cesium等渲染引擎直接加载

**tileset.json结构**：
```json
{
  "asset": {
    "version": "1.0"
  },
  "geometricError": 1000.0,
  "root": {
    "boundingVolume": {
      "box": [...]
    },
    "geometricError": 500.0,
    "refine": "ADD",
    "content": {
      "uri": "0_0_0.b3dm"
    },
    "children": [...]
  }
}
```

#### 流程图

```
输入模型 (OBJ/GLTF)
    ↓
Stage 0: 加载模型
    ↓
Stage 1: 网格简化 → 生成 LOD0, LOD1, LOD2, ...
    ↓
Stage 2: 四叉树分割 → 空间单元划分（2^n × 2^n）
    ↓
Stage 3: 切片生成 → 生成 B3DM/GLTF 文件
    ↓
Stage 4: 生成索引 → tileset.json
    ↓
输出切片集合
```

### API接口使用指南

#### 创建切片任务

```bash
POST /api/slicing/tasks
Content-Type: application/json

{
  "name": "城市模型切片任务",
  "sourceModelPath": "models/city.obj",
  "modelType": "obj",
  "slicingConfig": {
    "tileSize": 100.0,
    "divisions": 2,
    "lodLevels": 3,
    "enableMeshDecimation": true,
    "generateTileset": true,
    "outputFormat": "b3dm",
    "textureStrategy": "Repack",
    "compressOutput": true
  }
}
```

**响应示例**：
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "name": "城市模型切片任务",
  "status": "Created",
  "progress": 0,
  "createdAt": "2024-01-15T10:30:00Z"
}
```

#### 获取切片进度

```bash
GET /api/slicing/tasks/{taskId}/progress
```

**响应示例**：
```json
{
  "taskId": "123e4567-e89b-12d3-a456-426614174000",
  "progress": 45,
  "currentStage": "Stage 2: Quadtree Splitting",
  "processedTiles": 12,
  "totalTiles": 16,
  "estimatedTimeRemaining": 120
}
```

#### 获取切片数据

```bash
# 获取单个切片
GET /api/slicing/tasks/{taskId}/slices/{level}/{x}/{y}/{z}

# 获取层级元数据
GET /api/slicing/tasks/{taskId}/slices/{level}/metadata

# 批量获取切片
POST /api/slicing/tasks/{taskId}/slices/{level}/batch
Content-Type: application/json

{
  "coordinates": [
    {"x": 0, "y": 0, "z": 0},
    {"x": 1, "y": 0, "z": 0},
    {"x": 0, "y": 1, "z": 0}
  ]
}
```

#### 下载切片文件

```bash
GET /api/slicing/tasks/{taskId}/slices/{level}/{x}/{y}/{z}/download
```

#### 获取增量更新索引

```bash
GET /api/slicing/tasks/{taskId}/incremental-index
```

**响应示例**：
```json
{
  "version": 2,
  "changedTiles": [
    {"level": 0, "x": 1, "y": 1, "z": 0, "hash": "abc123..."},
    {"level": 1, "x": 2, "y": 3, "z": 0, "hash": "def456..."}
  ],
  "deletedTiles": [
    {"level": 0, "x": 0, "y": 2, "z": 0}
  ]
}
```

### 切片文件格式

系统支持两种主要的3D Tiles标准格式：

#### 1. B3DM格式（推荐）

**用途**：二进制glTF格式，Cesium默认支持

**优点**：
- 二进制格式，文件小巧高效
- 支持纹理和复杂材质
- 压缩效率高，传输速度快
- 广泛支持（Cesium、CesiumJS等）

**文件结构**：
```
B3DM文件 = Header + Feature Table + Batch Table + GLB (Binary glTF)
```

#### 2. GLTF格式

**用途**：JSON格式的3D模型标准

**优点**：
- 文本格式，可读性好，便于调试
- 跨平台兼容性强
- 支持复杂材质和动画
- 易于扩展和定制

**使用场景**：
- 需要调试和检查模型内容
- 需要手动编辑模型数据
- 需要非常精确的纹理和材质控制

### 切片处理流程

以下是完整的切片处理流程，从任务创建到切片生成：

#### 1. 任务创建
- 用户提交切片任务，指定模型路径和配置参数
- 系统验证模型文件存在性和格式
- 创建切片任务记录，状态设置为 `Created`

#### 2. 任务排队
- 任务加入处理队列
- 根据优先级和资源可用性调度执行
- 状态更新为 `Queued`

#### 3. 模型加载与分析
- 从存储系统（MinIO或本地文件系统）加载源模型
- 解析几何数据：顶点、法线、UV坐标、材质
- 计算模型包围盒和统计信息
- 状态更新为 `Processing` - Stage 0

#### 4. 网格简化（可选）
- 如果 `EnableMeshDecimation = true`，执行网格简化
- 使用QEM算法生成多级LOD
- 每个LOD级别保持视觉质量同时减少三角形数量
- Stage 1 进度更新

#### 5. 空间剖分
- 根据 `Divisions` 参数进行四叉树递归分割
- 对每个空间单元使用SAT算法测试三角形相交
- 筛选出相交的三角形，构建子网格
- Stage 2 进度更新

#### 6. 切片生成
- 对每个非空空间单元生成切片文件
- 根据 `TextureStrategy` 处理纹理：
  - **Repack**: 重打包纹理，只包含使用的区域
  - **KeepOriginal**: 保留原始纹理
  - **RepackCompressed**: 重打包并压缩
- 输出B3DM或GLTF格式文件
- 存储到指定位置（MinIO或本地）
- Stage 3 进度更新

#### 7. 索引生成
- 生成 `tileset.json` 索引文件
- 定义切片层次结构和包围盒
- 设置LOD切换的几何误差阈值
- Stage 4 进度更新

#### 8. 任务完成
- 状态更新为 `Completed`
- 记录完成时间和输出路径
- 生成任务报告（切片数量、文件大小等）

#### 流程时间估算

| 模型规模 | 三角形数 | Divisions | LOD Levels | 预计时间 |
|---------|---------|-----------|-----------|---------|
| 小型 | < 1万 | 1 | 2 | 10-30秒 |
| 中型 | 1-10万 | 2 | 3 | 1-5分钟 |
| 大型 | 10-50万 | 2 | 3 | 5-20分钟 |
| 超大型 | > 50万 | 3 | 4 | 20-60分钟 |

**注意**：实际时间取决于硬件性能、纹理大小、材质复杂度等因素。

### 切片任务实体结构

```csharp
public class SlicingTask
{
    public Guid Id { get; set; }                      // 任务唯一标识符
    public string Name { get; set; } = string.Empty;  // 任务名称
    public string SourceModelPath { get; set; } = string.Empty; // 源模型路径（MinIO）
    public Guid? SceneObjectId { get; set; }          // 关联的场景对象ID
    public SceneObject? SceneObject { get; set; }     // 关联的场景对象
    public string ModelType { get; set; } = string.Empty; // 模型类型
    public string SlicingConfig { get; set; } = string.Empty; // 切片配置JSON
    public SlicingTaskStatus Status { get; set; }     // 任务状态
    public int Progress { get; set; }                 // 进度 0-100
    public string? OutputPath { get; set; }           // 输出路径
    public string? ErrorMessage { get; set; }         // 错误信息
    public Guid CreatedBy { get; set; }               // 创建者
    public DateTime CreatedAt { get; set; }           // 创建时间
    public DateTime? StartedAt { get; set; }          // 开始时间
    public DateTime? CompletedAt { get; set; }        // 完成时间
}

// 切片任务状态枚举
public enum SlicingTaskStatus
{
    Created = 0,      // 已创建
    Queued = 1,       // 队列中
    Processing = 2,   // 处理中
    Completed = 3,    // 已完成
    Failed = 4,       // 失败
    Cancelled = 5     // 已取消
}

// 切片实体
public class Slice
{
    public Guid Id { get; set; }                      // 切片ID
    public Guid SlicingTaskId { get; set; }           // 所属任务ID
    public int Level { get; set; }                    // LOD级别
    public int X { get; set; }                        // X坐标
    public int Y { get; set; }                        // Y坐标
    public int Z { get; set; }                        // Z坐标
    public string FilePath { get; set; } = string.Empty; // 文件路径
    public string BoundingBox { get; set; } = string.Empty; // 包围盒JSON
    public long FileSize { get; set; }                // 文件大小（字节）
    public DateTime CreatedAt { get; set; }           // 创建时间
}
```

---

## 工作流引擎

### 工作流系统概述

RealScene3D内置了强大的工作流引擎，用于编排和执行复杂的业务流程。工作流引擎支持：

- **可视化设计**：通过拖拽方式设计工作流
- **节点扩展**：支持自定义节点类型
- **异步执行**：支持长时间运行的任务
- **状态管理**：完整的生命周期管理
- **历史追踪**：记录所有执行历史

### 工作流架构

```
┌─────────────────────────────────────────────────────┐
│              WorkflowEngine (引擎核心)               │
│  - StartWorkflowAsync()                             │
│  - SuspendWorkflowAsync()                           │
│  - ResumeWorkflowAsync()                            │
│  - CancelWorkflowAsync()                            │
│  - GetWorkflowInstanceAsync()                       │
└──────────────────────┬──────────────────────────────┘
                       │
        ┌──────────────┴────────────────┐
        │                               │
┌───────▼────────────┐        ┌─────────▼────────────┐
│ IWorkflowNodeExecutor[] │        │ 自定义节点执行器   │
│  - 节点执行器数组       │        │  - 支持扩展        │
└────────────────────┘        └──────────────────────┘
```

### 节点执行器系统

工作流引擎支持通过 `IWorkflowNodeExecutor` 接口扩展节点类型。系统通过依赖注入注册节点执行器：

```csharp
// 在Program.cs中注册节点执行器
builder.Services.AddScoped<IWorkflowNodeExecutor, CustomNodeExecutor>();
```

**节点执行器接口**：
```csharp
public interface IWorkflowNodeExecutor
{
    Task<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowNode node, 
        WorkflowInstance instance, 
        CancellationToken cancellationToken);
}
```

**支持自定义节点类型**：
- 业务逻辑节点
- 数据处理节点
- 外部服务调用节点
- 条件判断节点

### 工作流API接口

#### 创建工作流定义

```http
POST /api/workflows
Content-Type: application/json

{
  "name": "3D模型处理流程",
  "description": "自动化3D模型导入、切片、发布流程",
  "nodes": [
    {
      "id": "start",
      "type": "Start",
      "name": "开始",
      "nextNodes": ["validate"]
    },
    {
      "id": "validate",
      "type": "Condition",
      "name": "验证模型",
      "config": {
        "condition": "modelSize < 1000000000"
      },
      "nextNodes": ["slice", "error"]
    },
    {
      "id": "slice",
      "type": "Custom",
      "name": "执行切片",
      "config": {
        "strategy": "Octree",
        "maxLevel": 10
      },
      "nextNodes": ["publish"]
    },
    {
      "id": "publish",
      "type": "Custom",
      "name": "发布模型",
      "nextNodes": ["end"]
    },
    {
      "id": "error",
      "type": "Custom",
      "name": "错误处理",
      "nextNodes": ["end"]
    },
    {
      "id": "end",
      "type": "End",
      "name": "结束"
    }
  ]
}
```

#### 启动工作流实例

```http
POST /api/workflows/{workflowId}/instances
Content-Type: application/json

{
  "input": {
    "modelId": "uuid-here",
    "userId": "user-uuid"
  }
}
```

#### 获取工作流实例状态

```http
GET /api/workflows/instances/{instanceId}

响应:
{
  "id": "instance-uuid",
  "workflowId": "workflow-uuid",
  "status": "Running",
  "currentNode": "slice",
  "progress": 45,
  "startedAt": "2025-10-15T10:00:00Z",
  "input": {...},
  "output": null
}
```

#### 工作流控制操作

```http
# 暂停工作流实例
POST /api/workflows/instances/{instanceId}/suspend

# 恢复工作流实例
POST /api/workflows/instances/{instanceId}/resume

# 取消工作流实例
POST /api/workflows/instances/{instanceId}/cancel

# 获取执行历史
GET /api/workflows/instances/{instanceId}/history
```

### 自定义节点开发

要添加自定义节点执行器，按以下步骤操作：

#### 1. 实现节点执行器接口

```csharp
using RealScene3D.Domain.Interfaces;

public class CustomNodeExecutor : IWorkflowNodeExecutor
{
    public string NodeType => "Custom";

    public async Task<object?> ExecuteAsync(
        WorkflowNode node,
        Dictionary<string, object> context)
    {
        // 实现自定义逻辑
        var config = node.Config;

        // 执行业务逻辑
        // ...

        return result;
    }
}
```

#### 2. 注册节点执行器

在 `Program.cs` 中注册：

```csharp
// 注册自定义节点执行器
builder.Services.AddScoped<CustomNodeExecutor>();
builder.Services.AddScoped<IWorkflowNodeExecutor, CustomNodeExecutor>(
    sp => sp.GetRequiredService<CustomNodeExecutor>()
);
```

---

## 监控系统

### 监控系统概述

RealScene3D提供全面的监控和告警系统，支持系统指标和业务指标的实时监控。

### 监控架构

```
┌────────────────────────────────────────────────────┐
│            MonitoringController (API层)            │
│  - 记录指标  - 查询指标  - 告警管理                  │
└────────────────────┬───────────────────────────────┘
                     │
┌────────────────────▼───────────────────────────────┐
│         MonitoringAppService (应用服务)             │
│  - RecordSystemMetricAsync()                       │
│  - RecordBusinessMetricAsync()                     │
│  - CheckAlertRulesAsync()                          │
└────────────────────┬───────────────────────────────┘
                     │
        ┌────────────┴────────────┐
        │                         │
┌───────▼──────┐         ┌────────▼──────┐
│  PostgreSQL  │         │    Redis      │
│ - 历史指标   │          │ - 实时指标    │
│ - 告警规则   │          │ - 热点数据    │
└──────────────┘         └───────────────┘
```

### 监控指标类型

#### 1. 系统指标

**基础设施指标**：
- CPU使用率
- 内存使用率
- 磁盘I/O
- 网络流量

**数据库指标**：
- 连接数
- 查询性能
- 慢查询统计
- 锁等待时间

**缓存指标**：
- Redis内存使用
- 缓存命中率
- Key过期统计
- 连接数

**存储指标**：
- MinIO存储用量
- 上传/下载速率
- 请求成功率

#### 2. 业务指标

**用户行为指标**：
- 活跃用户数
- 访问量统计
- 操作频率
- 用户留存

**场景指标**：
- 场景创建数
- 场景加载时间
- 渲染性能
- 模型大小分布

**切片指标**：
- 切片任务数
- 切片生成速度
- 切片缓存命中率
- 切片传输性能

**工作流指标**：
- 执行成功率
- 平均执行时间
- 失败原因统计
- 并发执行数

### 监控API使用

#### 记录系统指标

```http
POST /api/monitoring/metrics/system
Content-Type: application/json

{
  "name": "cpu_usage",
  "value": 65.5,
  "unit": "percent",
  "tags": {
    "host": "server-01",
    "region": "us-east-1"
  }
}
```

#### 记录业务指标

```http
POST /api/monitoring/metrics/business
Content-Type: application/json

{
  "name": "scene_load_time",
  "value": 1250,
  "unit": "milliseconds",
  "tags": {
    "sceneId": "uuid-here",
    "userId": "user-uuid"
  }
}
```

#### 查询指标历史

```http
GET /api/monitoring/metrics/system/cpu_usage?
    startTime=2025-10-15T00:00:00Z&
    endTime=2025-10-15T23:59:59Z&
    aggregation=avg&
    interval=5m

响应:
{
  "name": "cpu_usage",
  "data": [
    {"timestamp": "2025-10-15T00:00:00Z", "value": 45.2},
    {"timestamp": "2025-10-15T00:05:00Z", "value": 48.1},
    ...
  ]
}
```

#### 获取系统指标快照

```http
GET /api/monitoring/metrics/system/snapshot

响应:
{
  "timestamp": "2025-10-15T12:30:00Z",
  "metrics": {
    "cpu_usage": 65.5,
    "memory_usage": 72.3,
    "disk_io_read": 125.6,
    "disk_io_write": 89.2,
    "network_rx": 1024,
    "network_tx": 512
  }
}
```

### 告警规则配置

#### 创建告警规则

```http
POST /api/monitoring/alert-rules
Content-Type: application/json

{
  "name": "CPU使用率过高",
  "description": "当CPU使用率超过80%持续5分钟时触发告警",
  "metricName": "cpu_usage",
  "condition": {
    "operator": "GreaterThan",
    "threshold": 80,
    "duration": 300
  },
  "severity": "Warning",
  "notificationChannels": ["email", "webhook"],
  "enabled": true
}
```

#### 告警严重级别

```csharp
public enum AlertSeverity
{
    Info = 0,       // 信息
    Warning = 1,    // 警告
    Critical = 2,   // 严重
    Error = 3       // 错误
}
```

#### 查询活跃告警

```http
GET /api/monitoring/alerts/active

响应:
[
  {
    "id": "alert-uuid",
    "ruleName": "CPU使用率过高",
    "severity": "Warning",
    "message": "CPU usage is 85.5% for 5 minutes",
    "triggeredAt": "2025-10-15T12:25:00Z",
    "acknowledged": false
  }
]
```

#### 确认和解决告警

```http
# 确认告警
POST /api/monitoring/alerts/{alertId}/acknowledge
Content-Type: application/json
{
  "acknowledgedBy": "admin-user",
  "comment": "正在处理"
}

# 解决告警
POST /api/monitoring/alerts/{alertId}/resolve
Content-Type: application/json
{
  "resolvedBy": "admin-user",
  "resolution": "已扩容服务器资源"
}
```

---

## 部署指南

### Docker 部署（推荐）

#### 启动存储服务

```bash
# 启动所有服务
docker-compose -f docker-compose.storage.yml up -d

# 查看日志
docker-compose -f docker-compose.storage.yml logs -f

# 查看状态
docker-compose -f docker-compose.storage.yml ps
```

#### 服务健康检查

```bash
# PostgreSQL
docker exec realscene3d-postgres pg_isready -U postgres

# MongoDB
docker exec realscene3d-mongodb mongosh --eval "db.adminCommand('ping')"

# Redis
docker exec realscene3d-redis redis-cli -a redis123 ping

# MinIO
curl http://localhost:9000/minio/health/live
```

### 生产环境部署

#### 后端发布

```bash
cd src/RealScene3D.WebApi

# 发布为自包含应用
dotnet publish -c Release -r linux-x64 --self-contained -o ./publish

# 或发布为依赖框架应用
dotnet publish -c Release -o ./publish
```

#### 使用 IIS 部署

1. 安装 .NET Core Hosting Bundle
2. 创建新站点，指向发布目录
3. 配置应用程序池为"无托管代码"
4. 配置 `web.config`

#### 使用 Nginx 反向代理

```nginx
server {
    listen 80;
    server_name api.yourdomain.com;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

#### 前端部署

```bash
cd src/RealScene3D.Web

# 构建生产版本
npm run build

# 部署 dist/ 目录到静态服务器
# - Nginx
# - Apache
# - CDN (Cloudflare, AWS CloudFront)
```

**Nginx 配置:**
```nginx
server {
    listen 80;
    server_name yourdomain.com;
    root /var/www/realscene3d;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }

    location /api {
        proxy_pass http://localhost:5000;
    }
}
```

---

## 故障排除

### 编译错误

#### 错误: NetTopologySuite 未找到
**解决:**
```bash
cd src/RealScene3D.Infrastructure
dotnet add package NetTopologySuite --version 2.5.0
dotnet add package Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite --version 8.0.10
```

#### 错误: Coordinate 构造函数参数不匹配
**解决:** 使用 `CoordinateZ` 而不是 `Coordinate`
```csharp
// 错误
new Coordinate(x, y, z)

// 正确
new CoordinateZ(x, y, z)
```

### 数据库连接失败

#### PostgreSQL 连接失败
```bash
# 检查服务状态
docker ps | grep postgres

# 查看日志
docker logs realscene3d-postgres

# 测试连接
psql -h localhost -U postgres -d RealScene3D
```

#### MongoDB 连接失败
```bash
# 检查认证
mongosh "mongodb://admin:admin123@localhost:27017"

# 查看日志
docker logs realscene3d-mongodb
```

### Redis 连接失败
```bash
# 测试连接
redis-cli -h localhost -p 6379 -a redis123 ping

# 清空缓存（慎用）
redis-cli -a redis123 FLUSHALL
```

### MinIO 上传失败
```bash
# 检查存储桶
mc ls myminio

# 创建缺失的存储桶
mc mb myminio/models-3d

# 设置公共访问（可选）
mc policy set download myminio/models-3d
```

### 切片相关故障

#### 错误: 切片任务长时间卡在处理中
**现象**：切片进度停滞不前，长时间无响应

**可能原因**：
1. 模型文件损坏或格式不支持
2. 切片配置参数不合理（Divisions过大）
3. 系统资源不足（内存/CPU）
4. 纹理文件过大或损坏

**解决步骤**：
```bash
# 1. 检查源模型文件
mc stat myminio/models-3d/your-model.obj

# 2. 查看应用日志
tail -f logs/log-.txt | grep -i slicing

# 3. 调整切片配置（降低复杂度）
{
  "tileSize": 200.0,           # 增大切片大小
  "divisions": 1,              # 减少分割深度（产生4个单元）
  "lodLevels": 2,              # 减少LOD级别
  "enableMeshDecimation": false  # 禁用网格简化加快速度
}

# 4. 检查系统资源
# Windows
tasklist | findstr dotnet

# 5. 重启任务
POST /api/slicing/tasks/{taskId}/cancel
# 创建新任务
```

#### 错误: 内存不足 (OutOfMemoryException)
**现象**：切片处理过程中出现内存溢出

**可能原因**：
1. 模型过大，Divisions设置过高导致切片数量爆炸
2. 纹理文件过大，重打包占用大量内存
3. LOD级别过多，同时处理多个简化级别
4. 系统可用内存不足

**解决步骤**：
```csharp
// 方案1：减小切片复杂度
var config = new SlicingConfig
{
    Divisions = 1,                  // 从2或3减少到1（4个单元 vs 16或64个）
    LodLevels = 2,                  // 从3或4减少到2
    TileSize = 150.0,               // 增大切片大小
    EnableMeshDecimation = false,   // 禁用网格简化（减少内存峰值）
    TextureStrategy = TextureStrategy.KeepOriginal  // 不重打包纹理
};

// 方案2：分批处理（手动拆分模型）
// 将大模型拆分为多个小模型，分别切片后合并

// 方案3：增加系统内存或使用更高配置的机器
```

**监控内存使用**：
```bash
# Windows PowerShell
Get-Process -Name dotnet | Select-Object WorkingSet, VirtualMemorySize

# 如果内存使用超过可用内存的80%，考虑降低复杂度
```

#### 错误: 切片文件损坏或无法加载
**现象**：前端无法正常显示切片内容，或加载时报错

**可能原因**：
1. 切片文件生成过程中断
2. 纹理重打包过程出错
3. 存储过程中文件损坏
4. tileset.json格式错误

**解决步骤**：
```bash
# 1. 检查切片文件完整性
mc stat myminio/slices/task-output/tileset.json
mc stat myminio/slices/task-output/0_0_0.b3dm

# 2. 验证切片文件格式
# 下载文件并用Cesium Viewer或其他工具测试

# 3. 查看生成日志
GET /api/slicing/tasks/{taskId}
# 检查errorMessage字段

# 4. 重新生成切片
POST /api/slicing/tasks/{taskId}/cancel
# 创建新任务，使用更保守的配置

# 5. 验证 tileset.json
# 确保JSON格式正确，包围盒坐标有效
```

#### 错误: 增量更新失败
**现象**：增量更新索引生成失败或更新不准确

**可能原因**：
1. 原模型变化检测算法不准确
2. 切片哈希计算冲突
3. 增量更新索引损坏或版本不兼容

**解决步骤**：
```bash
# 1. 检查增量更新索引
GET /api/slicing/tasks/{taskId}/incremental-index

# 2. 重新计算切片哈希
POST /api/slicing/tasks/{taskId}/recalculate-hashes

# 3. 重建增量更新索引
POST /api/slicing/tasks/{taskId}/rebuild-incremental-index

# 4. 如果以上方法都失败，执行完整重新切片
# 禁用增量更新，重新生成所有切片
{
  "enableIncrementalUpdates": false
}
```

#### 错误: 纹理重打包失败
**现象**：切片生成时纹理处理失败

**可能原因**：
1. 纹理文件格式不支持
2. 纹理文件损坏
3. UV坐标超出[0,1]范围
4. 内存不足无法加载大纹理

**解决步骤**：
```csharp
// 方案1：使用 KeepOriginal 策略，不重打包
var config = new SlicingConfig
{
    TextureStrategy = TextureStrategy.KeepOriginal
};

// 方案2：检查并修复纹理文件
// 使用图像处理工具转换为标准格式（PNG、JPEG）

// 方案3：减小纹理尺寸
// 在建模软件中将纹理缩小到合理大小（例如 2048x2048）
```

#### 错误: 空间分割结果不正确
**现象**：某些三角形被分配到错误的切片，或缺失三角形

**可能原因**：
1. 模型包围盒计算错误
2. SAT相交测试边界情况处理不当
3. 浮点精度问题

**解决步骤**：
```bash
# 1. 查看日志，检查包围盒计算
tail -f logs/log-.txt | grep -i "bounding box"

# 2. 尝试增大 TileSize，减少边界情况
{
  "tileSize": 150.0  # 从100增加到150
}

# 3. 减少 Divisions，减少空间单元数量
{
  "divisions": 1  # 从2减少到1
}

# 4. 联系开发团队报告问题，提供模型文件
```

---

## 性能优化

### 数据库优化

#### PostgreSQL 优化
```sql
-- 1. 创建索引
CREATE INDEX idx_scenes_owner ON scenes(owner_id) WHERE is_deleted = FALSE;
CREATE INDEX idx_scenes_name ON scenes(name) WHERE is_deleted = FALSE;

-- 2. VACUUM 清理
VACUUM ANALYZE scenes;

-- 3. 查看慢查询
SELECT query, calls, total_time, mean_time
FROM pg_stat_statements
ORDER BY mean_time DESC
LIMIT 10;

-- 4. 查看索引使用情况
SELECT
    schemaname,
    tablename,
    indexname,
    idx_scan,
    idx_tup_read,
    idx_tup_fetch
FROM pg_stat_user_indexes
ORDER BY idx_scan DESC;
```

**连接池配置:**
```csharp
"PostgreSqlConnection": "Host=localhost;Database=RealScene3D;Username=postgres;Password=postgres;Maximum Pool Size=100;Minimum Pool Size=10"
```

#### MongoDB 优化
```javascript
// 1. 创建复合索引
db.video_metadata.createIndex({ sceneId: 1, uploadedAt: -1 });

// 2. 查看索引
db.video_metadata.getIndexes();

// 3. 分析查询性能
db.video_metadata.find({ sceneId: UUID("...") }).explain("executionStats");

// 4. 投影查询（只返回需要的字段）
db.video_metadata.find(
    { sceneId: UUID("...") },
    { fileName: 1, duration: 1, _id: 0 }
);
```

### Redis 优化

```bash
# 1. 查看内存使用
redis-cli -a redis123 INFO memory

# 2. 设置最大内存和淘汰策略
redis-cli -a redis123 CONFIG SET maxmemory 2gb
redis-cli -a redis123 CONFIG SET maxmemory-policy allkeys-lru

# 3. 持久化配置
# RDB快照
redis-cli -a redis123 CONFIG SET save "900 1 300 10 60 10000"

# AOF持久化
redis-cli -a redis123 CONFIG SET appendonly yes
```

**C# 批量操作:**
```csharp
var batch = _redis.CreateBatch();
foreach (var item in items)
{
    batch.SetAsync($"key:{item.Id}", item);
}
await batch.ExecuteAsync();
```

### MinIO 优化

```bash
# 1. 启用版本控制
mc version enable myminio/critical-data

# 2. 设置生命周期（自动删除临时文件）
mc ilm add --expiry-days 7 myminio/temp

# 3. 配置CDN
# 将MinIO作为源站，CloudFlare/AWS CloudFront作为CDN

# 4. 监控性能
mc admin prometheus generate myminio
```

### 应用层优化

#### 启用响应压缩
```csharp
// Program.cs
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<GzipCompressionProvider>();
    options.Providers.Add<BrotliCompressionProvider>();
});
```

#### 启用输出缓存
```csharp
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(builder =>
    {
        builder.Expire(TimeSpan.FromMinutes(10));
    });
});
```

---

## 监控和日志

### 应用日志

使用 Serilog:

```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.File
dotnet add package Serilog.Sinks.Console
```

```csharp
// Program.cs
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();
```

### 健康检查

```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(postgresConnection)
    .AddMongoDb(mongoConnectionString)
    .AddRedis(redisConnection);

app.MapHealthChecks("/health");
```

### Prometheus 监控

```bash
# MinIO metrics
curl http://localhost:9000/minio/prometheus/metrics

# Redis metrics (使用redis_exporter)
docker run -d -p 9121:9121 oliver006/redis_exporter \
    --redis.addr=redis://localhost:6379 \
    --redis.password=redis123
```

---

## 备份和恢复

### PostgreSQL 备份

```bash
# 全量备份
docker exec realscene3d-postgres pg_dump -U postgres RealScene3D > backup_$(date +%Y%m%d).sql

# 恢复
docker exec -i realscene3d-postgres psql -U postgres RealScene3D < backup.sql
```

### MongoDB 备份

```bash
# 备份
docker exec realscene3d-mongodb mongodump \
    --uri="mongodb://admin:admin123@localhost:27017/RealScene3D" \
    --out=/backup/

# 恢复
docker exec realscene3d-mongodb mongorestore \
    --uri="mongodb://admin:admin123@localhost:27017" \
    /backup/
```

### Redis 备份

```bash
# RDB快照
docker exec realscene3d-redis redis-cli -a redis123 SAVE

# 复制RDB文件
docker cp realscene3d-redis:/data/dump.rdb ./backup/
```

### MinIO 备份

```bash
# 同步到备份位置
mc mirror myminio/bim-models /backup/minio/bim-models/

# 使用mc cp复制
mc cp --recursive myminio/videos /backup/minio/videos/
```

---

## 安全性最佳实践

### 1. 密码和密钥管理
```bash
# 使用环境变量
export PG_PASSWORD="strong_password_here"
export REDIS_PASSWORD="strong_password_here"

# 使用 .NET Secret Manager
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:PostgreSqlConnection" "..."
```

### 2. HTTPS 配置
```csharp
// 强制HTTPS
app.UseHttpsRedirection();
app.UseHsts();
```

### 3. CORS 限制
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins("https://yourdomain.com")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
```

### 4. API 限流
```bash
dotnet add package AspNetCoreRateLimit
```

```csharp
builder.Services.AddMemoryCache();
builder.Services.AddInMemoryRateLimiting();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.GeneralRules = new List<RateLimitRule>
    {
        new RateLimitRule
        {
            Endpoint = "*",
            Limit = 1000,
            Period = "1h"
        }
    };
});
```

---

## 常见问题 (FAQ)

**Q: 如何切换数据库从SQL Server到PostgreSQL?**
A: 修改 `appsettings.json` 中的连接字符串，确保 `PostgreSqlConnection` 有值即可。系统会自动优先使用PostgreSQL。

**Q: MinIO 存储桶如何设置访问权限?**
A: 使用mc命令: `mc policy set public myminio/thumbnails`

**Q: 如何清空Redis缓存?**
A: 使用命令: `redis-cli -a redis123 FLUSHALL`（谨慎操作！）

**Q: 数据库迁移失败怎么办?**
A: 删除Migrations文件夹，重新创建迁移。如果数据库已存在，先备份后删除。

**Q: 如何选择合适的切片配置?**
A: 根据模型规模选择：
- 小模型（<1万三角形）：Divisions=1, LodLevels=2
- 中等模型（1-10万）：Divisions=2, LodLevels=3
- 大模型（10-50万）：Divisions=2, LodLevels=3-4
- 超大模型（>50万）：Divisions=3, LodLevels=4-5

纹理策略推荐：
- 需要最小文件体积：TextureStrategy.RepackCompressed
- 平衡质量和大小：TextureStrategy.Repack
- 快速处理：TextureStrategy.KeepOriginal

**Q: 切片处理速度很慢怎么办?**
A: 1. 减小 Divisions（空间分割深度）；2. 增大 TileSize 减少切片总数；3. 降低 LodLevels；4. 禁用 EnableMeshDecimation（牺牲LOD换速度）；5. 使用 KeepOriginal 纹理策略；6. 使用更高性能的硬件。

**Q: 切片文件太大影响传输怎么办?**
A: 1. 使用 TextureStrategy.RepackCompressed 启用纹理压缩；2. 调整 TextureQuality 参数（降低到0.7-0.8）；3. 提高 CompressionLevel 到 7-9；4. 使用 B3DM 格式而非 GLTF；5. 考虑使用 CDN 加速传输。

**Q: Divisions参数如何选择？**
A: Divisions 控制空间分割深度，产生 2^n × 2^n 个空间单元：
- Divisions=1: 4个单元，适合小型模型
- Divisions=2: 16个单元，适合中型模型（推荐）
- Divisions=3: 64个单元，适合大型城市模型
- Divisions=4: 256个单元，仅用于超大规模场景（慎用）

**注意**：Divisions越大，内存占用越高，处理时间越长。建议从小值开始测试。

**Q: LOD级别如何配置？**
A: LodLevels 控制网格简化的层次数量：
- LodLevels=2: 快速处理，适合预览
- LodLevels=3: 标准配置，平衡质量和性能（推荐）
- LodLevels=4-5: 高质量，适合远距离和近距离都需要清晰的场景
- LodLevels=1: 不简化，保留原始模型（不推荐）

每增加一个LOD级别，处理时间增加约30-50%。

**Q: 增量更新不准确怎么办?**
A: 1. 重新计算切片哈希值；2. 重建增量更新索引；3. 检查模型变化检测算法；4. 必要时禁用增量更新，执行完整重新切片。

---

## 总结

本指南涵盖了从开发到生产的完整流程，特别详细介绍了3D切片系统的核心功能。

### 主要特性总结

#### 3D切片系统
- **四叉树空间分割**：高效的二维空间剖分算法
- **LOD网格简化**：QEM算法生成多级细节
- **智能纹理处理**：三种纹理策略（Repack、KeepOriginal、RepackCompressed）
- **瓦片生成流水线**：TileGenerationPipeline 四阶段处理流程
- **增量更新支持**：避免完全重新处理模型

#### 异构存储架构
- **PostgreSQL/PostGIS**：结构化数据和GIS空间数据
- **MongoDB**：非结构化元数据（视频、倾斜摄影、BIM）
- **Redis**：高速缓存和会话管理
- **MinIO**：对象存储（3D模型、纹理、视频）

#### 高级功能
- **工作流引擎**：可视化流程设计和自动化执行
- **监控系统**：系统指标和业务指标全方位监控
- **告警机制**：智能告警规则和多渠道通知

### 最佳实践

1. **切片配置选择**：根据模型规模选择合适的 Divisions 和 LodLevels
2. **纹理策略**：平衡文件大小和处理速度，推荐使用 Repack
3. **性能优化**：监控内存使用，必要时降低配置复杂度
4. **故障排除**：遇到问题先检查日志，尝试降低配置复杂度
5. **增量更新**：仅在需要频繁更新模型时启用

### 相关文档

- [README.md](./README.md) - 项目概览和快速入门
- [Swagger API](http://localhost:5000/swagger) - API在线文档
- [3D Tiles 规范](https://github.com/CesiumGS/3d-tiles) - 3D Tiles标准文档
- [GLTF 规范](https://github.com/KhronosGroup/glTF) - GLTF格式文档

### 技术支持

如有其他问题，请查阅：
- GitHub Issues: 提交问题和建议
- 开发文档：查看详细的开发指南
- API文档：参考Swagger在线文档

**祝使用愉快！** 🚀
