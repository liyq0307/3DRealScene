# 3D Real Scene - 企业级3D场景管理系统

基于 ASP.NET Core 9.0、Vue 3 和 Three.js 的企业级3D真实场景管理系统，采用DDD分层架构和异构融合存储。

## ⭐ 核心特性

### 后端架构
- 🏗️ **DDD分层设计** - Domain/Application/Infrastructure/API清晰分离
- 🗄️ **异构存储** - PostgreSQL/PostGIS + MongoDB + Redis + MinIO
- 🌍 **GIS支持** - NetTopologySuite空间数据处理
- 🚀 **高性能** - 多级缓存、分布式存储、Redis会话管理
- 📦 **容器化** - Docker Compose一键部署

### 前端技术
- 🎮 **WebGL渲染** - Three.js + Cesium 3D场景可视化
- ✂️ **智能切片** - 四叉树空间分割 + QEM网格简化
- 🎯 **LOD自适应** - 多层次细节自动切换
- 📦 **纹理优化** - 智能纹理重打包，减少文件体积
- 🎨 **现代化UI** - 毛玻璃效果、渐变色、流畅动画

### 核心功能
- ✅ 用户认证与JWT Token自动刷新
- ✅ 3D场景管理与空间分析（PostGIS）
- ✅ 3D模型切片系统（四叉树+LOD+纹理重打包）
- ✅ 工作流引擎（可视化设计器）
- ✅ 监控告警系统（系统指标+业务指标）
- ✅ 8种3D格式支持（GLTF/GLB/OBJ/FBX/OSGB/IFC/STL/PLY）

## 🛠️ 技术栈

### 后端 (.NET 9.0)
```
ASP.NET Core 9.0
├── Entity Framework Core 9.0
├── PostgreSQL/PostGIS (空间数据)
├── MongoDB 3.0 (文档存储)
├── Redis 7 (缓存)
├── MinIO 6.0 (对象存储)
├── NetTopologySuite 2.5 (GIS)
├── SharpGLTF 1.0.5 (GLTF处理)
└── ImageSharp 3.1.11 (图像处理)
```

### 前端 (Vue 3)
```
Vue 3 + TypeScript
├── Three.js + Cesium (3D渲染)
├── Vite (构建工具)
├── Pinia (状态管理)
└── Axios (HTTP客户端)
```

## 🚀 快速开始

### 前提条件
- .NET 9.0 SDK
- Node.js 18+
- Docker & Docker Compose

### 一键启动

```bash
# 1. 启动存储服务
docker-compose -f docker-compose.storage.yml up -d

# 2. 启动后端（新终端）
cd src/RealScene3D.WebApi
dotnet run

# 3. 启动前端（新终端）
cd src/RealScene3D.Web
npm install && npm run dev
```

**访问地址：**
- 前端：http://localhost:5173
- 后端API：http://localhost:5000
- Swagger：http://localhost:5000/swagger
- MinIO Console：http://localhost:9001

## 📐 系统架构

```
┌─────────────────┐      ┌──────────────────────┐
│   前端展示层     │ ←──→ │    Web API / MVC     │
│  (Vue + WebGL)  │      │  (ASP.NET Core 9.0)  │
│                 │      │  - JWT认证           │
│ - 3D渲染引擎    │      │  - 切片任务管理       │
│ - LOD自适应     │      │  - 实时进度监控       │
│ - 视锥剔除      │      │  - 工作流引擎         │
└─────────────────┘      └──────────┬───────────┘
                                    │
                ┌───────────────────┴──────────────────┐
                │         应用服务层 (C# .NET)          │
                │  - 用户管理 / 权限控制                │
                │  - 场景管理 / 空间分析                │
                │  - 切片生成流水线（四叉树分割）        │
                │  - 网格简化服务（QEM算法）             │
                │  - 工作流引擎 / 监控服务              │
                └───────────────────┬──────────────────┘
                                    │
                ┌───────────────────┴──────────────────┐
                │          数据服务层 (C# + GIS)        │
                │  - EF Core仓储模式                    │
                │  - NetTopologySuite空间处理           │
                │  - 切片数据服务                       │
                │  - 纹理处理器                         │
                └───────────────────┬──────────────────┘
                                    │
        ┌───────────────────────────┴───────────────────────────┐
        │                  异构存储层                            │
        │  PostgreSQL/PostGIS │ MongoDB │ Redis │ MinIO         │
        │  (业务+空间数据)     │ (元数据) │ (缓存) │ (对象存储)    │
        └───────────────────────────────────────────────────────┘
```

## 📦 项目结构

```
3DRealScene/
├── src/
│   ├── RealScene3D.Domain/          # 领域层
│   │   ├── Entities/                # 11个实体类
│   │   ├── Geometry/                # 几何库（6,593行代码）
│   │   ├── Materials/               # 材质系统
│   │   └── Interfaces/              # 仓储接口
│   ├── RealScene3D.Application/     # 应用层
│   │   ├── Services/                # 39个业务服务
│   │   │   ├── Slicing/             # 切片服务
│   │   │   ├── Generators/          # 瓦片生成器
│   │   │   ├── Loaders/             # 8种模型加载器
│   │   │   ├── MeshDecimator/       # QEM网格简化
│   │   │   └── Workflows/           # 工作流服务
│   │   └── DTOs/                    # 数据传输对象
│   ├── RealScene3D.Infrastructure/  # 基础设施层
│   │   ├── Data/                    # PostgreSQL DbContext
│   │   ├── MongoDB/                 # MongoDB集成
│   │   ├── Redis/                   # Redis缓存
│   │   ├── MinIO/                   # 对象存储
│   │   ├── Authentication/          # JWT认证
│   │   └── Workflow/                # 工作流引擎
│   ├── RealScene3D.WebApi/          # API层（11个控制器）
│   └── RealScene3D.Web/             # 前端（Vue 3 + TS）
│       ├── components/              # 20个Vue组件
│       ├── views/                   # 15个页面
│       └── services/                # API服务
├── docker-compose.storage.yml
└── README.md
```

## 🎯 核心功能详解

### 1. 3D切片系统

**四叉树空间分割**
- 递归四叉树算法精确剖分
- 每次递归产生4个子节点（XL-YL, XL-YR, XR-YL, XR-YR）
- 基于SAT（分离轴定理）的三角面-AABB精确相交测试

**LOD网格简化**
- QEM（二次误差度量）算法生成多级细节
- 根据视距动态切换模型精度
- 保持视觉质量同时减少三角形数量

**纹理重打包**
- 为每个切片生成专属纹理图集
- 自动计算新的UV坐标
- 可减少50-90%文件体积

**配置示例**
```json
{
  "TileSize": 100.0,
  "Divisions": 2,              // 产生 4^2 = 16个空间单元
  "LodLevels": 3,              // 生成3级LOD
  "EnableMeshDecimation": true,
  "OutputFormat": "b3dm",
  "TextureStrategy": "Repack"
}
```

### 2. 异构存储策略

| 数据库 | 用途 | 数据类型 |
|-------|------|---------|
| **PostgreSQL/PostGIS** | 主数据库 | 用户、场景、空间数据 |
| **MongoDB** | 文档存储 | 视频元数据、倾斜摄影、BIM |
| **Redis** | 内存缓存 | 会话、热点数据、计数器 |
| **MinIO** | 对象存储 | 3D模型、视频、纹理 |

### 3. 前端组件库（20个）

**基础UI组件**：Badge、Button、Card、Modal、Pagination、SearchFilter、Input、Select、LoadingSpinner、MessageToast、ErrorDisplay、BarChart、LineChart

**3D组件**：CesiumViewer、ThreeViewer、ModelViewer、ModelBrowser、SceneViewer、SlicePreview、FileUpload

## 📡 API端点

### 核心API
```
# 认证
POST   /api/auth/login
POST   /api/auth/register

# 场景管理
GET    /api/scenes
POST   /api/scenes
PUT    /api/scenes/{id}
DELETE /api/scenes/{id}

# 切片管理
POST   /api/slicing/tasks
GET    /api/slicing/tasks/{id}/progress
GET    /api/slicing/tasks/{taskId}/slices/{level}/{x}/{y}/{z}

# 工作流
POST   /api/workflows
POST   /api/workflows/{id}/instances
GET    /api/workflows/instances

# 监控
POST   /api/monitoring/metrics/system
GET    /api/monitoring/alerts/active
```

完整API文档：http://localhost:5000/swagger

## 🔧 开发指南

### 数据库迁移

```bash
# 创建迁移
cd src/RealScene3D.Infrastructure
dotnet ef migrations add MigrationName \
    --context PostgreSqlDbContext \
    --startup-project ../RealScene3D.WebApi

# 应用迁移
dotnet ef database update \
    --context PostgreSqlDbContext \
    --startup-project ../RealScene3D.WebApi
```

### 环境变量配置

```json
{
  "ConnectionStrings": {
    "PostgreSqlConnection": "Host=localhost;Port=5432;Database=RealScene3D;Username=postgres;Password=postgres",
    "MongoDbConnection": "mongodb://localhost:27017",
    "RedisConnection": "localhost:6379"
  },
  "MinIO": {
    "Endpoint": "localhost:9000",
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin123"
  },
  "JwtSettings": {
    "SecretKey": "your-secret-key-here",
    "Issuer": "RealScene3D.WebApi",
    "Audience": "RealScene3D.Client",
    "ExpirationMinutes": 60
  }
}
```

## 🐳 Docker部署

### 启动存储服务
```bash
docker-compose -f docker-compose.storage.yml up -d
```

### 服务健康检查
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

## ⚡ 性能优化

### 后端优化
- ✅ EF Core查询优化和索引
- ✅ Redis分布式缓存
- ✅ 响应压缩（Gzip/Brotli）
- ✅ 异步编程（async/await）

### 前端优化
- ✅ API请求缓存（减少70-90%请求）
- ✅ 路由级别代码分割
- ✅ LOD自适应渲染
- ✅ 视锥剔除算法

### 3D渲染优化
- ✅ 网格简化算法（QEM）
- ✅ 纹理缓存管理
- ✅ 批量渲染
- ✅ GPU加速

## 🔍 故障排除

### 常见问题

**Q: 数据库连接失败**
```bash
# 检查Docker容器状态
docker ps | grep postgres

# 查看日志
docker logs realscene3d-postgres
```

**Q: 切片任务卡住**
- 检查源模型文件是否完整
- 降低切片复杂度（减少Divisions和LodLevels）
- 检查系统内存是否充足

**Q: MinIO上传失败**
```bash
# 创建缺失的存储桶
mc mb myminio/models-3d
```

## 📊 项目统计

| 指标 | 数值 |
|-----|------|
| 代码行数 | ~75,000行 |
| 后端代码 | C# 42,000行 |
| 前端代码 | TypeScript 33,000行 |
| 组件数量 | 35+ |
| API接口 | 50+ |
| 数据库支持 | 4种 |
| 3D格式支持 | 8种 |
| 几何库代码 | 6,593行 |

## 🎖️ 技术亮点

1. **完整的DDD分层架构** - Domain/Application/Infrastructure/API清晰分离
2. **异构存储集成** - PostgreSQL/MongoDB/Redis/MinIO四合一
3. **智能3D切片系统** - 四叉树+QEM算法+纹理重打包
4. **完整的几何处理引擎** - 6,593行代码实现向量、矩阵、网格处理
5. **8种3D格式支持** - GLTF/GLB/OBJ/FBX/OSGB/IFC/STL/PLY
6. **工作流引擎** - 可视化设计器+自定义节点执行器
7. **监控告警系统** - 系统指标+业务指标全面监控
8. **高性能优化** - API缓存+3D渲染优化+分布式缓存

## 📝 许可证

MIT License

## 🔗 相关链接

- [Swagger API文档](http://localhost:5000/swagger)
- [Vue 3 文档](https://vuejs.org/)
- [Three.js 文档](https://threejs.org/)
- [ASP.NET Core 文档](https://docs.microsoft.com/aspnet/core)

---

**Made with ❤️ using ASP.NET Core 9.0 and Vue 3**
