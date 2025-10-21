# 3D Real Scene 完整开发指南

本文档整合了编译、部署、存储架构等所有详细说明。基于实际代码库更新，确保与项目实现保持一致。

**最后更新**: 2025-10-15
**版本**: v1.0
**技术栈**: ASP.NET Core 8.0 + Vue 3 + PostgreSQL/PostGIS + MongoDB + Redis + MinIO

---

## 目录

- [项目架构](#项目架构)
- [编译和运行](#编译和运行)
- [异构存储架构](#异构存储架构)
- [3D切片功能](#3d切片功能)
  - [切片功能概述](#切片功能概述)
  - [切片策略详解](#切片策略详解)
  - [切片配置参数](#切片配置参数)
  - [渲染优化算法](#渲染优化算法)
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
│              API层 (ASP.NET Core 8.0 WebAPI)                │
│  Controllers: Scenes, Slicing, Workflows, Monitoring        │
└────────────────────────────┬────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────┐
│                应用层 (Application Layer)                    │
│  Services: SceneService, SlicingAppService,                 │
│           WorkflowService, MonitoringService                │
└────────────────────────────┬────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────┐
│                领域层 (Domain Layer)                         │
│  Entities: Scene3D, SlicingTask, Slice, User, Workflow      │
│  Domain Services: SlicingService, SpatialAnalysisService    │
└────────────────────────────┬────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────┐
│             基础设施层 (Infrastructure Layer)                │
│  - PostgreSqlDbContext (主数据库)                            │
│  - MongoDbContext (文档存储)                                 │
│  - RedisCacheService (缓存服务)                              │
│  - MinioStorageService (对象存储)                            │
│  - Repository<T> (仓储实现)                                  │
│  - UnitOfWork (工作单元)                                     │
└─────────────────────────────────────────────────────────────┘
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
│   │   └── Workflow.cs                 # 工作流实体
│   ├── Interfaces/                     # 接口定义
│   │   ├── IRepository.cs              # 仓储接口
│   │   └── IUnitOfWork.cs              # 工作单元接口
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
│   │   ├── SlicingService.cs           # 切片应用服务
│   │   ├── WorkflowService.cs          # 工作流服务
│   │   └── MonitoringAppService.cs     # 监控服务
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

---

## 编译和运行

### 前提条件

- **.NET 8.0 SDK** 或更高版本
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
- HTTP API: `http://localhost:5177`
- HTTPS API: `https://localhost:7149`
- Swagger文档: `http://localhost:5177/swagger`
- 根路径: `http://localhost:5177/` (自动重定向到Swagger)

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
curl http://localhost:5177/swagger

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
=== 开始初始化异构融合存储系统 ===
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

## 异构存储架构

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

## 3D切片功能

### 切片功能概述

3D切片功能是RealScene3D系统的核心特性之一，用于将大型3D模型分割成多个小的、可管理的切片，以提高渲染性能和数据传输效率。该功能采用了先进的计算机图形学算法，支持多种切片策略和渲染优化技术。

#### 核心特性

- **多策略切片**：支持网格、八叉树、KD树、自适应四种切片策略
- **LOD支持**：多层次细节（Level of Detail）技术，根据视距动态切换切片精度
- **渲染优化**：内置视锥剔除和预测加载算法，大幅提升渲染性能
- **多格式输出**：支持B3DM、GLTF、JSON等多种3D Tiles标准格式
- **增量更新**：支持模型的增量切片更新，无需完全重新处理
- **并行处理**：采用多线程并行处理，提高切片生成速度

#### 适用场景

- **大规模城市模型**：倾斜摄影模型、BIM模型的切片处理
- **复杂工业场景**：工厂、设备等复杂3D模型的渲染优化
- **虚拟现实应用**：VR/AR场景的高性能渲染
- **WebGL应用**：浏览器端3D场景的流式加载和渲染

#### 技术优势

1. **智能剖分算法**：根据几何密度和视点重要性自动调整切片大小
2. **高效空间索引**：采用层次化索引结构，支持快速空间查询
3. **内存优化**：采用流式处理和内存映射技术，处理超大规模模型
4. **网络传输优化**：支持压缩和增量传输，减少带宽占用

### 切片策略详解

系统提供了四种不同的切片策略，每种策略适用于不同的场景和数据特征：

#### 1. 网格切片策略 (Grid)

**适用场景**：规则地形、均匀分布的建筑群、矩形区域的城市模型

**算法原理**：
- 采用均匀网格剖分算法
- 在三维空间中进行规则的网格划分
- 计算简单，内存占用规律

**优点**：
- 处理速度快，适合实时切片
- 切片大小均匀，易于管理
- 空间查询效率高

**缺点**：
- 对不规则模型适应性差
- 可能产生大量空切片

```csharp
// 网格切片配置示例
var config = new SlicingConfig
{
    Strategy = SlicingStrategy.Grid,
    TileSize = 100.0,        // 切片大小100米
    MaxLevel = 8,           // 最大8级LOD
    OutputFormat = "b3dm"
};
```

#### 2. 八叉树切片策略 (Octree)

**适用场景**：不规则分布的城市模型、复杂建筑群、异构场景

**算法原理**：
- 采用递归八叉树剖分算法
- 基于空间密度和几何复杂度进行自适应剖分
- 平衡细节表现和性能开销

**优点**：
- 自适应精度，细节丰富区域切片更细
- 有效减少总切片数量
- 适合复杂不规则模型

**缺点**：
- 剖分算法较为复杂
- 切片大小差异较大

```csharp
// 八叉树切片配置示例
var config = new SlicingConfig
{
    Strategy = SlicingStrategy.Octree,
    TileSize = 50.0,         // 基础切片大小50米
    MaxLevel = 12,          // 支持更深层次剖分
    GeometricErrorThreshold = 1.0,
    OutputFormat = "gltf"
};
```

#### 3. KD树切片策略 (KdTree)

**适用场景**：高维空间数据、复杂几何分布、优化空间查询的场景

**算法原理**：
- 采用基于方差的二分剖分算法
- 交替在X、Y、Z轴上进行剖分
- 适用于高维空间查询优化

**优点**：
- 剖分轴交替选择，避免轴偏向
- 适合高维空间查询
- 自适应数据分布

**缺点**：
- 构建时间相对较长
- 内存消耗较高

```csharp
// KD树切片配置示例
var config = new SlicingConfig
{
    Strategy = SlicingStrategy.KdTree,
    TileSize = 75.0,
    MaxLevel = 10,
    DensityAnalysisSampleRate = 0.7,
    OutputFormat = "b3dm"
};
```

#### 4. 自适应切片策略 (Adaptive)

**适用场景**：几何密度差异大的复杂场景、需要精细控制切片大小的场景

**算法原理**：
- 基于几何密度分析的智能剖分
- 自动调整切片大小和LOD级别
- 结合多种算法的优点

**优点**：
- 最高程度的适应性
- 最优的细节表现
- 智能资源分配

**缺点**：
- 计算复杂度最高
- 需要较长的预处理时间

```csharp
// 自适应切片配置示例
var config = new SlicingConfig
{
    Strategy = SlicingStrategy.Adaptive,
    TileSize = 60.0,
    MaxLevel = 15,
    DensityAnalysisSampleRate = 0.8,
    ViewportOptimizationThreshold = 10.0,
    OutputFormat = "gltf"
};
```

### 切片配置参数

切片配置通过 `SlicingConfig` 类进行控制，以下是主要参数详解：

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `TileSize` | double | 100.0 | 基础切片大小（米），影响剖分粒度 |
| `MaxLevel` | int | 10 | LOD最大级别，每级切片数为2^level |
| `OutputFormat` | string | "b3dm" | 输出格式：b3dm、gltf、json |
| `CompressOutput` | bool | true | 是否压缩输出文件 |
| `GeometricErrorThreshold` | double | 1.0 | LOD切换的几何误差阈值 |
| `TextureQuality` | double | 0.8 | 纹理质量（0-1之间） |
| `Strategy` | SlicingStrategy | Octree | 切片策略选择 |
| `EnableIncrementalUpdates` | bool | false | 是否启用增量更新 |
| `ViewportOptimizationThreshold` | double | 10.0 | 视点优化距离阈值 |
| `DensityAnalysisSampleRate` | double | 0.5 | 密度分析采样率 |
| `CompressionLevel` | int | 6 | 压缩级别（0-9） |
| `ParallelProcessingCount` | int | 4 | 并行处理线程数 |

### 渲染优化算法

#### 视锥剔除算法 (Frustum Culling)

视锥剔除是3D渲染的核心优化技术，用于剔除不可见的切片，减少渲染负载。

**算法原理**：
1. 计算相机视锥体的6个裁剪面
2. 判断切片包围盒与视锥体的相交关系
3. 只渲染可见或部分可见的切片

**API使用示例**：
```bash
POST /api/slicing/tasks/{taskId}/frustum-culling
Content-Type: application/json

{
  "cameraPosition": {"x": 0, "y": 0, "z": 100},
  "cameraDirection": {"x": 0, "y": 0, "z": -1},
  "fieldOfView": 1.047,    // 60度视野
  "nearPlane": 1.0,
  "farPlane": 10000.0
}
```

**算法优势**：
- 显著减少渲染物体数量
- 提高帧率和响应速度
- 降低GPU和内存压力

#### 预测加载算法 (Predictive Loading)

预测加载基于用户视点移动趋势，提前加载将要可见的切片。

**算法原理**：
1. 分析用户视点移动向量
2. 预测未来2-3秒的视口位置
3. 提前加载预测可见的切片

**API使用示例**：
```bash
POST /api/slicing/tasks/{taskId}/predict-loading
Content-Type: application/json

{
  "cameraPosition": {"x": 0, "y": 0, "z": 100},
  "cameraDirection": {"x": 0, "y": 0, "z": -1},
  "fieldOfView": 1.047,
  "nearPlane": 1.0,
  "farPlane": 10000.0
}

# 请求体中的移动向量
{
  "x": 10, "y": 5, "z": 0
}
```

**算法优势**：
- 消除加载等待时间
- 提供流畅的用户体验
- 减少网络请求峰值

### API接口使用指南

#### 创建切片任务

```bash
POST /api/slicing/tasks
Content-Type: application/json

{
  "name": "城市模型切片任务",
  "sourceModelPath": "models/city.ifc",
  "modelType": "ifc",
  "slicingConfig": {
    "tileSize": 100.0,
    "maxLevel": 10,
    "outputFormat": "b3dm",
    "strategy": 1,
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
  "currentStage": "处理中",
  "processedTiles": 1250,
  "totalTiles": 2800,
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

#### 获取切片策略

```bash
GET /api/slicing/strategies
```

**响应示例**：
```json
[
  {
    "id": 0,
    "name": "Grid",
    "description": "规则网格切片算法，适用于规则地形和均匀分布的数据"
  },
  {
    "id": 1,
    "name": "Octree",
    "description": "八叉树切片算法，适用于不规则模型，自适应精度"
  }
]
```

### 切片文件格式

系统支持多种3D Tiles标准格式：

#### 1. B3DM格式

**用途**：二进制glTF格式，Cesium默认支持
**优点**：
- 二进制格式，文件小巧
- 支持纹理和材质
- 压缩效率高

#### 2. GLTF格式

**用途**：JSON格式的3D模型标准
**优点**：
- 文本格式，可读性好
- 跨平台兼容性强
- 支持复杂材质和动画

#### 3. JSON格式

**用途**：轻量级自定义格式
**优点**：
- 格式简单，处理速度快
- 易于扩展和定制
- 调试友好

### 切片处理流程

1. **任务创建**：用户提交切片任务，指定模型路径和配置
2. **模型分析**：系统分析源模型的几何特征和复杂度
3. **策略选择**：根据模型特征选择最优的切片策略
4. **空间剖分**：采用选择的策略进行空间剖分
5. **切片生成**：为每个剖分单元生成切片文件
6. **索引构建**：建立空间索引和LOD层次结构
7. **格式转换**：转换为指定的输出格式
8. **压缩存储**：压缩并存储到对象存储系统

### 切片任务实体结构

```csharp
// 来自 Domain/Entities/Slicing.cs
public class SlicingTask
{
    public Guid Id { get; set; }                      // 任务唯一标识符
    public string Name { get; set; }                  // 任务名称
    public string SourceModelPath { get; set; }       // 源模型路径（MinIO）
    public string ModelType { get; set; }             // 模型类型
    public string SlicingConfig { get; set; }         // 切片配置JSON
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
    public string FilePath { get; set; }              // 文件路径
    public string BoundingBox { get; set; }           // 包围盒JSON
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
│              WorkflowService (应用服务)              │
│  - CreateWorkflowAsync()                            │
│  - StartWorkflowAsync()                             │
│  - GetWorkflowInstanceAsync()                       │
└──────────────────────┬──────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────┐
│              WorkflowEngine (引擎核心)               │
│  - ExecuteWorkflowAsync()                           │
│  - ExecuteNodeAsync()                               │
│  - 节点路由和状态转换                                 │
└──────────────────────┬──────────────────────────────┘
                       │
        ┌──────────────┴────────────────┐
        │                               │
┌───────▼────────────┐        ┌─────────▼────────────┐
│ DelayNodeExecutor  │        │ConditionNodeExecutor │
│  - 延迟执行         │        │  - 条件判断           │
└────────────────────┘        └──────────────────────┘
```

### 内置节点执行器

#### 1. DelayNodeExecutor - 延迟节点

延迟节点用于在工作流中引入等待时间，适用于需要定时执行或暂停的场景。

**使用场景**：
- 定时任务
- 批处理等待
- 限流控制

**配置示例**：
```json
{
  "nodeType": "Delay",
  "config": {
    "delaySeconds": 60
  }
}
```

#### 2. ConditionNodeExecutor - 条件节点

条件节点根据条件表达式决定工作流的分支走向。

**使用场景**：
- 条件分支
- 审批流程
- 异常处理

**配置示例**：
```json
{
  "nodeType": "Condition",
  "config": {
    "condition": "status == 'approved'",
    "trueNextNode": "node-approve",
    "falseNextNode": "node-reject"
  }
}
```

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
        proxy_pass http://localhost:5177;
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
        proxy_pass http://localhost:5177;
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
2. 切片配置参数不合理
3. 系统资源不足

**解决步骤**：
```bash
# 1. 检查源模型文件
mc stat myminio/models-3d/your-model.ifc

# 2. 查看应用日志
tail -f logs/log-.txt | grep -i slicing

# 3. 调整切片配置
{
  "tileSize": 200.0,        # 增大切片大小
  "maxLevel": 8,           # 减少LOD级别
  "parallelProcessingCount": 2  # 减少并行度
}
```

#### 错误: 内存不足 (OutOfMemoryException)
**现象**：切片处理过程中出现内存溢出
**可能原因**：
1. 模型过大，并行处理数量过多
2. 切片大小设置过小导致切片数量爆炸
3. 系统可用内存不足

**解决步骤**：
```csharp
// 减小并行处理数量
var config = new SlicingConfig
{
    ParallelProcessingCount = 2,  // 从4减少到2
    TileSize = 150.0,            // 增大切片大小
    MaxLevel = 8                 // 减少LOD级别
};
```

#### 错误: 视锥剔除效果不佳
**现象**：渲染性能提升不明显，仍有较多切片被渲染
**可能原因**：
1. 视锥剔除参数配置不当
2. 切片包围盒计算不准确
3. 相机参数更新不及时

**解决步骤**：
```csharp
// 调整视锥剔除参数
var viewport = new ViewportInfo
{
    FarPlane = 5000.0,           // 减少远裁剪面
    FieldOfView = Math.PI / 4,   // 缩小视野角度
    NearPlane = 10.0             // 增大近裁剪面
};
```

#### 错误: 切片文件损坏或无法加载
**现象**：前端无法正常显示切片内容
**可能原因**：
1. 切片文件生成过程中断
2. 压缩参数设置错误
3. 存储过程中文件损坏

**解决步骤**：
```bash
# 1. 检查切片文件完整性
mc stat myminio/slices/task-output/tileset.json

# 2. 验证切片索引
GET /api/slicing/tasks/{taskId}/incremental-index

# 3. 重新生成损坏的切片
POST /api/slicing/tasks/{taskId}/regenerate-slices
```

#### 错误: 增量更新失败
**现象**：增量更新索引生成失败或更新不准确
**可能原因**：
1. 原模型变化检测算法不准确
2. 切片哈希计算冲突
3. 增量更新索引损坏

**解决步骤**：
```bash
# 1. 重新计算切片哈希
POST /api/slicing/tasks/{taskId}/recalculate-hashes

# 2. 重建增量更新索引
POST /api/slicing/tasks/{taskId}/rebuild-incremental-index

# 3. 执行完整重新切片（最后手段）
POST /api/slicing/tasks/{taskId}/full-reslice
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

**Q: 如何选择合适的切片策略?**
A: 根据模型特征选择：网格策略适合规则模型，八叉树适合不规则模型，KD树适合复杂查询，自适应策略适合最高质量要求。

**Q: 切片处理速度很慢怎么办?**
A: 1. 调整并行处理数量；2. 增大切片大小减少切片总数；3. 降低LOD级别；4. 使用更高性能的硬件。

**Q: 切片文件太大影响传输怎么办?**
A: 1. 启用压缩输出；2. 调整纹理质量参数；3. 使用更高压缩级别；4. 优化几何误差阈值。

**Q: 视锥剔除不起作用怎么办?**
A: 1. 检查视口参数是否正确更新；2. 调整远裁剪面距离；3. 验证包围盒计算准确性；4. 检查相机矩阵是否正确。

**Q: 增量更新不准确怎么办?**
A: 1. 重新计算切片哈希值；2. 重建增量更新索引；3. 检查模型变化检测算法；4. 必要时执行完整重新切片。

---

## 总结

本指南涵盖了从开发到生产的完整流程。如有其他问题，请查阅：

- [README.md](./README.md) - 项目概览
- [Swagger API](http://localhost:5177/swagger) - API文档

**^_^** 🚀
