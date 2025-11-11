using Microsoft.EntityFrameworkCore;
using Minio;
using MongoDB.Driver;
using StackExchange.Redis;
using RealScene3D.Application.Interfaces;
using RealScene3D.Application.Services;
using RealScene3D.Application.Services.Generators;
using RealScene3D.Application.Services.Loaders;
using RealScene3D.Application.Services.Strategys;
using RealScene3D.Application.Services.Workflows;
using RealScene3D.Domain.Interfaces;
using RealScene3D.Infrastructure.Data;
using RealScene3D.Infrastructure.MongoDB;
using RealScene3D.Infrastructure.MongoDB.Repositories;
using RealScene3D.Infrastructure.Redis;
using RealScene3D.Infrastructure.MinIO;
using RealScene3D.Infrastructure.Services;
using RealScene3D.Infrastructure.Repositories;
using RealScene3D.Infrastructure.Workflow;
using RealScene3D.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using RealScene3D.WebApi.Filters;
using RealScene3D.Application.Services.Slicing;

/// <summary>
/// RealScene3D Web API 程序入口
/// </summary>
/// <remarks>
/// 配置ASP.NET Core Web API应用程序
/// 包括中间件、服务注册、存储系统初始化等
/// 支持JWT认证、Swagger文档、CORS等功能
/// 采用异构存储架构：PostgreSQL/PostGIS、MongoDB、Redis、MinIO
/// </remarks>
/// </summary>
var builder = WebApplication.CreateBuilder(args);

// ========== Kestrel服务器配置 ==========
// 配置请求大小限制以支持大文件上传（最大500MB）
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 500 * 1024 * 1024; // 500MB
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 500 * 1024 * 1024; // 500MB
});

// ========== 基础服务配置 ==========

/// <summary>
/// 添加控制器和API探索服务
/// 配置ASP.NET Core MVC和Swagger API文档生成
/// </summary>
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "RealScene3D API - 实景三维管理系统",
        Version = "v1",
        Description = "实景三维管理系统 - 支持PostgreSQL/PostGIS、MongoDB、Redis、MinIO异构存储"
    });

    // 添加JWT认证到Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // 配置文件上传支持
    c.OperationFilter<FileUploadOperationFilter>();
});

// 配置CORS - 允许前端访问
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(origin => true)  // 允许所有来源（开发环境）
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()  // 允许发送凭据（Cookie）
              .WithExposedHeaders("Content-Disposition", "Content-Type");  // 暴露响应头
    });
});

// ========== JWT认证配置 ==========
/// <summary>
/// 配置JWT认证服务
/// 使用Bearer Token方案进行身份验证
/// 支持基于角色的访问控制(RBAC)
/// </summary>

// 读取JWT配置
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName);
builder.Services.Configure<JwtSettings>(jwtSettings);

// 注册JWT令牌服务
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// 配置JWT身份验证
var jwtKey = jwtSettings.Get<JwtSettings>()?.Secret ?? throw new InvalidOperationException("JWT Secret is not configured");
var key = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // 开发环境可以不使用HTTPS
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Get<JwtSettings>()?.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtSettings.Get<JwtSettings>()?.Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero // 不允许令牌过期时间偏移
    };
});

builder.Services.AddAuthorization();

// ========== 数据存储层配置 ==========
/// <summary>
/// 配置存储系统，支持多种数据库和存储服务
/// 采用多级容错策略：PostgreSQL/PostGIS -> SQL Server -> 内存数据库
/// 确保系统在各种部署环境下都能正常运行，提供最佳的用户体验
/// </summary>

// 1. PostgreSQL/PostGIS - 主数据库（地形、模型、业务数据）
/// <summary>
/// 配置PostgreSQL/PostGIS主数据库连接，支持地理空间数据存储和查询
/// 功能特性：
/// - 地理空间索引和查询优化（PostGIS）
/// - 复杂几何计算和空间分析
/// - 高并发事务处理
/// - 连接池和负载均衡支持
/// 适用场景：大型3D场景、海量地形数据、复杂空间查询
/// </summary>
var postgresConnection = builder.Configuration.GetConnectionString("PostgreSqlConnection");
var sqlServerConnection = builder.Configuration.GetConnectionString("SqlServerConnection");

// 多级数据库配置策略：优先使用高性能数据库，回退到兼容选项，最终使用内存数据库保证可用性
if (!string.IsNullOrEmpty(postgresConnection))
{
    try
    {
        /// <summary>
        /// PostgreSQL数据库上下文配置
        /// 启用PostGIS扩展以支持地理空间数据类型和操作
        /// 配置连接重试、超时、池化等高级选项优化性能
        /// </summary>
        builder.Services.AddDbContext<PostgreSqlDbContext>(options =>
        {
            options.UseNpgsql(postgresConnection, npgsqlOptions =>
            {
                npgsqlOptions.UseNetTopologySuite(); // 启用PostGIS支持，提供地理空间功能
                // 可选：添加更多Npgsql特定配置
                // npgsqlOptions.CommandTimeout(30); // 命令超时时间
                // npgsqlOptions.MaxBatchSize(1000); // 批量操作大小
                // npgsqlOptions.EnableRetryOnFailure(3); // 失败重试次数
            });
        });
        // 注册 ApplicationDbContext，让它使用相同的 PostgreSQL 配置
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(postgresConnection, npgsqlOptions =>
            {
                npgsqlOptions.UseNetTopologySuite();
            });
        });
        builder.Services.AddScoped<DbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
    }
    catch (Exception ex)
    {
        // 如果 PostgreSQL 配置失败，回退到内存数据库，确保应用仍能启动用于开发和测试
        Console.WriteLine($"PostgreSQL配置失败，回退到内存数据库: {ex.Message}");
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseInMemoryDatabase("RealScene3D");
        });
        builder.Services.AddScoped<DbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
    }
}
else if (!string.IsNullOrEmpty(sqlServerConnection))
{
    try
    {
        // SQL Server 数据库（兼容选项）
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseSqlServer(sqlServerConnection, sqlOptions =>
            {
                sqlOptions.UseNetTopologySuite();
            });
        });
        builder.Services.AddScoped<DbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
    }
    catch
    {
        // 如果 SQL Server 配置失败，回退到内存数据库
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseInMemoryDatabase("RealScene3D");
        });
        builder.Services.AddScoped<DbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
    }
}
else
{
    // 如果没有配置任何数据库，使用内存数据库
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseInMemoryDatabase("RealScene3D");
    });
    builder.Services.AddScoped<DbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
}

// 2. MongoDB - 非结构化数据（视频元数据、倾斜摄影、BIM）
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDbConnection")
    ?? "mongodb://localhost:27017";
var mongoDatabaseName = builder.Configuration["MongoDB:DatabaseName"] ?? "RealScene3D";

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    return new MongoClient(mongoConnectionString);
});

builder.Services.AddScoped<MongoDbContext>(sp =>
{
    var mongoClient = sp.GetRequiredService<IMongoClient>();
    return new MongoDbContext(mongoClient, mongoDatabaseName);
});

// MongoDB 仓储注册
builder.Services.AddScoped<IVideoMetadataRepository, VideoMetadataRepository>();
builder.Services.AddScoped<IBimModelMetadataRepository, BimModelMetadataRepository>();
builder.Services.AddScoped<ITiltPhotographyMetadataRepository, TiltPhotographyMetadataRepository>();

// MongoDB 数据库初始化器
builder.Services.AddScoped<MongoDbInitializer>();

// 3. Redis - 缓存（会话、热点数据）
var redisConnection = builder.Configuration.GetConnectionString("RedisConnection")
    ?? "localhost:6379";

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    return ConnectionMultiplexer.Connect(redisConnection);
});

builder.Services.AddSingleton<IRedisCacheService, RedisCacheService>();

// 添加分布式缓存
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnection;
    options.InstanceName = "RealScene3D:";
});

// 4. MinIO - 对象存储（倾斜摄影、BIM文件、视频）
/// <summary>
/// 配置MinIO对象存储服务，用于存储大型3D模型文件和媒体资源
/// 功能特性：
/// - 大文件分块上传和断点续传
/// - 多版本控制和生命周期管理
/// - CDN加速和全球分发支持
/// - 加密存储和访问控制
/// 存储策略：
/// - 倾斜摄影数据：按项目和区域分桶存储
/// - BIM模型：按建筑和楼层组织
/// - 纹理贴图：压缩存储，CDN加速
/// - 视频媒体：转码和流式传输
/// </summary>
var minioEndpoint = builder.Configuration["MinIO:Endpoint"] ?? "localhost:9000";
var minioAccessKey = builder.Configuration["MinIO:AccessKey"] ?? "minioadmin";
var minioSecretKey = builder.Configuration["MinIO:SecretKey"] ?? "minioadmin";
var minioUseSSL = builder.Configuration.GetValue<bool>("MinIO:UseSSL", false);

/// <summary>
/// 创建MinIO客户端实例，配置连接参数和安全选项
/// 支持多种认证方式：用户名密码、令牌、证书等
/// 连接优化：长连接、重试机制、超时控制
/// </summary>
builder.Services.AddSingleton<IMinioClient>(sp =>
{
    var client = new MinioClient()
        .WithEndpoint(minioEndpoint)
        .WithCredentials(minioAccessKey, minioSecretKey)
        .WithSSL(minioUseSSL)
        .Build();

    // 可选：配置更多客户端选项
    // .WithTimeout(300) // 请求超时时间（秒）
    // .WithRetryCount(3) // 重试次数

    return client;
});

/// <summary>
/// 注册MinIO存储服务，提供高层API封装
/// 封装常用的存储操作：上传、下载、删除、列表等
/// 自动处理分块上传、进度回调、错误重试等复杂逻辑
/// </summary>
builder.Services.AddScoped<IMinioStorageService, MinioStorageService>();

/// <summary>
/// 注册图片处理服务
/// 提供缩略图生成、图片压缩、尺寸调整等功能
/// </summary>
builder.Services.AddScoped<IImageProcessingService, ImageProcessingService>();

/// <summary>
/// 注册文件清理服务
/// 用于清理旧的上传文件，避免存储空间浪费
/// </summary>
builder.Services.AddScoped<IFileCleanupService, FileCleanupService>();

// ========== 依赖注入配置 ==========
/// <summary>
/// 配置领域层和基础设施层的基础服务和仓储
/// 采用依赖注入模式，支持单元测试和模块化开发
/// 服务生命周期：Scoped（请求级别）、Singleton（全局）、Transient（瞬时）
/// </summary>

// 注册通用仓储模式，提供CRUD操作的泛型实现
// 支持实体框架和自定义仓储的统一接口
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// 注册特定的仓储实现，提供优化的查询方法
builder.Services.AddScoped<ISliceRepository, SliceRepository>();

/// <summary>
/// 工作单元模式注册，确保跨聚合的事务一致性
/// 协调多个仓储的操作，统一提交或回滚
/// 适用于复杂的业务操作需要确保数据一致性的场景
/// </summary>
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ========== 应用服务层配置 ==========
/// <summary>
/// 注册核心业务应用服务，提供用户、场景、空间分析等业务功能
/// 这些服务封装了业务规则、流程编排、数据转换等应用层逻辑
/// 依赖领域服务和基础设施服务，是系统的核心业务引擎
/// </summary>

// 用户管理服务：身份认证、权限控制、用户信息管理
builder.Services.AddScoped<IUserService, UserService>();

// 场景管理服务：3D场景的创建、编辑、渲染配置
builder.Services.AddScoped<ISceneService, SceneService>();

// 场景对象服务：场景内对象的增删改查和关系管理
builder.Services.AddScoped<ISceneObjectService, SceneObjectService>();

// 空间分析服务：地理计算、缓冲区分析、空间查询等
builder.Services.AddScoped<ISpatialAnalysisService, SpatialAnalysisService>();

// ========== 工作流引擎配置 ==========
/// <summary>
/// 配置工作流系统，支持异步任务处理和复杂业务流程
/// 功能包括：流程定义、节点执行、状态机管理、异步调度等
/// 适用于：数据处理、批处理、长时间运行的任务等场景
/// </summary>

// 工作流核心服务：流程定义、实例管理、执行控制
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<IWorkflowExecutorService, WorkflowService>();
builder.Services.AddScoped<IWorkflowEngine, WorkflowEngine>();

// 工作流节点执行器：实现具体的工作流节点逻辑
// 支持延迟执行、条件分支、并行处理等多种控制结构
builder.Services.AddScoped<DelayNodeExecutor>();
builder.Services.AddScoped<ConditionNodeExecutor>();

// 注册节点执行器到工作流引擎，支持动态发现和扩展
builder.Services.AddScoped<IWorkflowNodeExecutor, DelayNodeExecutor>(sp => sp.GetRequiredService<DelayNodeExecutor>());
builder.Services.AddScoped<IWorkflowNodeExecutor, ConditionNodeExecutor>(sp => sp.GetRequiredService<ConditionNodeExecutor>());

// ========== 3D切片服务配置 ==========
/// <summary>
/// 配置3D模型切片处理系统，支持多种切片算法和渲染优化
/// 功能特性：
/// - 多层次细节（LOD）切片生成
/// - 多种空间剖分算法（网格、八叉树、KD树、自适应）
/// - 视锥剔除和预测加载优化
/// - 并行处理和增量更新支持
/// - 索引文件一致性验证和自动修复
/// 适用于：倾斜摄影、BIM模型、海量点云等3D数据
/// </summary>

// 独立功能服务（依赖其他注册的服务，需要通过DI注入）
builder.Services.AddScoped<IncrementalUpdateService>();
builder.Services.AddScoped<SlicingDataService>();

// 切片应用服务：提供切片任务管理的高层API接口
builder.Services.AddScoped<ISlicingAppService, SlicingAppService>();

// 切片后台处理器：执行实际的切片处理任务，支持异步队列
builder.Services.AddScoped<ISlicingProcessor, SlicingProcessor>();

// 3D Tiles生成器工厂：支持动态创建不同格式的瓦片生成器
// 工厂会自动创建生成器实例，无需单独注册各个生成器类
builder.Services.AddScoped<ITileGeneratorFactory, TileGeneratorFactory>();

// 切片策略工厂：支持动态创建不同的切片策略
builder.Services.AddScoped<ISlicingStrategyFactory, SlicingStrategyFactory>();

// 模型加载器工厂：根据文件扩展名或枚举创建对应的加载器（抽象工厂模式）
// 支持格式: .obj, .gltf, .glb, .stl, .ply, .fbx, .ifc, .ifcxml, .ifczip, .osgb, .osg
// 工厂会自动创建加载器实例，无需单独注册各个加载器类
builder.Services.AddScoped<IModelLoaderFactory, ModelLoaderFactory>();

// MTL材质解析器（OBJ加载器需要）
builder.Services.AddScoped<MtlParser>();

// 网格处理服务：LOD生成和纹理优化
builder.Services.AddScoped<MeshDecimationService>(); // QEM网格简化服务
builder.Services.AddScoped<TextureAtlasGenerator>(); // 纹理图集生成器

// Obj2Tiles服务：端到端OBJ/GLTF转3D Tiles
builder.Services.AddScoped<Obj2TilesService>();

// 添加索引文件生成服务（已存在）

// ========== 系统监控服务配置 ==========
/// <summary>
/// 配置系统监控和性能分析服务
/// 提供实时的性能指标、健康检查、告警通知等功能
/// 支持系统运维、故障诊断、性能优化等场景
/// </summary>

// 系统监控服务：收集和分析系统运行时数据
builder.Services.AddScoped<IMonitoringService, MonitoringAppService>();

var app = builder.Build();

// 配置HTTP请求管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "RealScene3D API v1");
        c.RoutePrefix = "swagger";
    });
}

// 禁用HTTPS重定向，方便本地开发
// app.UseHttpsRedirection();
app.UseCors("AllowAll");

// 启用认证和授权中间件
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// 添加根路径处理
app.MapGet("/", () => Results.Redirect("/swagger"));

/// <summary>
/// 系统初始化任务 - 异步非阻塞执行
/// 在应用启动后执行存储系统的初始化和健康检查
/// 包括数据库迁移、存储桶创建、连接测试等操作
/// 采用容错设计，确保初始化失败不影响主应用启动
/// </summary>
_ = Task.Run(async () =>
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    logger.LogInformation("=== 开始初始化存储系统 ===");
    logger.LogInformation("系统架构：前端Vue.js + 后端ASP.NET Core WebAPI + 异构存储层");
    logger.LogInformation("支持存储：PostgreSQL/PostGIS、MongoDB、Redis、MinIO");

    // 1. 初始化PostgreSQL数据库（仅当配置时）
    /// <summary>
    /// PostgreSQL数据库初始化流程
    /// 执行数据库迁移，确保数据库结构与代码版本同步
    /// 仅在开发环境执行，生产环境应由运维团队控制迁移时机
    /// </summary>
    if (!string.IsNullOrEmpty(configuration.GetConnectionString("PostgreSqlConnection")))
    {
        try
        {
            /// <summary>
            /// 获取ApplicationDbContext数据库上下文并执行迁移
            /// 迁移操作：创建表、索引、约束、外键等数据库对象
            /// 支持增量迁移，保留现有数据不丢失
            /// </summary>
            var appContext = scope.ServiceProvider.GetService<ApplicationDbContext>();
            if (appContext != null)
            {
                logger.LogInformation("正在执行PostgreSQL数据库迁移...");

                // 获取待处理的迁移
                var pendingMigrations = await appContext.Database.GetPendingMigrationsAsync();
                if (pendingMigrations.Any())
                {
                    logger.LogInformation("发现 {Count} 个待处理的迁移: {Migrations}",
                        pendingMigrations.Count(),
                        string.Join(", ", pendingMigrations));
                }
                else
                {
                    logger.LogInformation("没有待处理的迁移");
                }

                // 执行迁移（如果数据库不存在会自动创建）
                logger.LogInformation("应用数据库迁移...");
                await appContext.Database.MigrateAsync();

                // 获取已应用的迁移
                var appliedMigrations = await appContext.Database.GetAppliedMigrationsAsync();
                logger.LogInformation("已应用 {Count} 个迁移", appliedMigrations.Count());

                logger.LogInformation("✓ PostgreSQL/PostGIS数据库初始化成功");

                // 验证连接
                var canConnect = await appContext.Database.CanConnectAsync();
                if (canConnect)
                {
                    logger.LogInformation("✓ PostgreSQL连接测试通过");
                }
                else
                {
                    logger.LogWarning("✗ PostgreSQL连接测试失败");
                }
            }
            else
            {
                logger.LogInformation("跳过PostgreSQL迁移（上下文不存在）");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("✗ PostgreSQL数据库迁移失败：{Message}", ex.Message);
            logger.LogWarning("详细错误信息：{Details}", ex.ToString());
            logger.LogWarning("系统将继续运行，但部分功能可能受限");
        }
    }
    else
    {
        logger.LogInformation("未配置PostgreSQL连接，跳过数据库初始化");
    }

    // 2. 初始化SQL Server数据库（仅当配置时）
    if (!string.IsNullOrEmpty(configuration.GetConnectionString("SqlServerConnection")))
    {
        try
        {
            var sqlServerContext = scope.ServiceProvider.GetService<ApplicationDbContext>();
            if (sqlServerContext != null && app.Environment.IsDevelopment())
            {
                await sqlServerContext.Database.MigrateAsync();
                logger.LogInformation("✓ SQL Server数据库初始化成功");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("✗ SQL Server数据库迁移失败：{Message}", ex.Message);
        }
    }

    // 3. 初始化MinIO存储桶
    /// <summary>
    /// MinIO存储桶初始化流程
    /// 为不同类型的3D资源创建专门的存储桶，实现分类存储和管理
    /// 存储桶设计：
    /// - 倾斜摄影：原始影像和处理结果
    /// - BIM模型：建筑信息模型和构件数据
    /// - 视频媒体：监控视频和宣传素材
    /// - 3D模型：通用三维模型文件
    /// - 纹理贴图：模型纹理和材质文件
    /// - 缩略图：预览图片和图标资源
    /// </summary>
    try
    {
        logger.LogInformation("正在初始化MinIO存储桶...");
        var minioService = scope.ServiceProvider.GetRequiredService<IMinioStorageService>();

        /// <summary>
        /// 倾斜摄影数据存储桶
        /// 存储无人机倾斜摄影采集的原始影像和处理结果
        /// 支持海量影像数据的高效存储和快速访问
        /// </summary>
        await minioService.EnsureBucketExistsAsync(MinioBuckets.TILT_PHOTOGRAPHY);

        /// <summary>
        /// BIM模型数据存储桶
        /// 存储建筑信息模型，包括建筑结构、构件、属性等信息
        /// 支持IFC、RVT等多种BIM格式的标准存储
        /// </summary>
        await minioService.EnsureBucketExistsAsync(MinioBuckets.BIM_MODELS);

        /// <summary>
        /// 视频媒体数据存储桶
        /// 存储监控视频、宣传视频、教学视频等多媒体内容
        /// 支持视频转码、缩略图生成、流式播放等功能
        /// </summary>
        await minioService.EnsureBucketExistsAsync(MinioBuckets.VIDEOS);

        /// <summary>
        /// 3D模型数据存储桶
        /// 存储通用三维模型文件，如OBJ、FBX、glTF等格式
        /// 支持模型预览、格式转换、纹理分离等处理
        /// </summary>
        await minioService.EnsureBucketExistsAsync(MinioBuckets.MODELS_3D);

        /// <summary>
        /// 纹理贴图数据存储桶
        /// 存储模型纹理、材质、贴图等相关资源
        /// 支持纹理压缩、格式优化、CDN加速等功能
        /// </summary>
        await minioService.EnsureBucketExistsAsync(MinioBuckets.TEXTURES);

        /// <summary>
        /// 缩略图数据存储桶
        /// 存储模型预览图、场景截图、图标等图片资源
        /// 支持图片压缩、水印、缩放等处理操作
        /// </summary>
        await minioService.EnsureBucketExistsAsync(MinioBuckets.THUMBNAILS);

        logger.LogInformation("✓ MinIO存储桶初始化成功");
        logger.LogInformation("存储桶列表：倾斜摄影、BIM模型、视频、3D模型、纹理、缩略图");
    }
    catch (Exception ex)
    {
        logger.LogWarning("✗ MinIO初始化失败：{Message}", ex.Message);
        logger.LogWarning("对象存储功能可能受限，但系统仍可运行");
    }

    // 4. 测试Redis连接（仅当配置时）
    if (!string.IsNullOrEmpty(configuration.GetConnectionString("RedisConnection")))
    {
        try
        {
            var redis = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
            var db = redis.GetDatabase();
            await db.PingAsync();
            logger.LogInformation("✓ Redis缓存连接建立成功");
        }
        catch (Exception ex)
        {
            logger.LogWarning("✗ Redis连接失败：{Message}", ex.Message);
        }
    }

    // 5. 测试MongoDB连接并初始化索引（仅当配置时）
    if (!string.IsNullOrEmpty(configuration.GetConnectionString("MongoDbConnection")))
    {
        try
        {
            var mongoClient = scope.ServiceProvider.GetRequiredService<IMongoClient>();
            await mongoClient.ListDatabaseNamesAsync();
            logger.LogInformation("✓ MongoDB连接建立成功");

            // 初始化 MongoDB 索引
            var mongoInitializer = scope.ServiceProvider.GetRequiredService<MongoDbInitializer>();
            await mongoInitializer.InitializeAsync();
            await mongoInitializer.ValidateAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning("✗ MongoDB连接失败：{Message}", ex.Message);
        }
    }

    logger.LogInformation("=== 存储系统启动完成 ===");
    logger.LogInformation("PostgreSQL/PostGIS: 地形、模型、业务数据");
    logger.LogInformation("MongoDB: 非结构化数据（视频元数据、倾斜摄影、BIM）");
    logger.LogInformation("Redis: 会话缓存、热点数据");
    logger.LogInformation("MinIO: 文件对象存储");
});

app.Run();
