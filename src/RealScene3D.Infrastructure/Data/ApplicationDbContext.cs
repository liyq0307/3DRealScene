using Microsoft.EntityFrameworkCore;
using RealScene3D.Domain.Entities;
using WorkflowEntity = RealScene3D.Domain.Entities.Workflow;
using WorkflowInstanceEntity = RealScene3D.Domain.Entities.WorkflowInstance;
using WorkflowExecutionHistoryEntity = RealScene3D.Domain.Entities.WorkflowExecutionHistory;
using SlicingTaskEntity = RealScene3D.Domain.Entities.SlicingTask;
using SliceEntity = RealScene3D.Domain.Entities.Slice;
using SystemMetricEntity = RealScene3D.Domain.Entities.SystemMetric;
using BusinessMetricEntity = RealScene3D.Domain.Entities.BusinessMetric;
using AlertRuleEntity = RealScene3D.Domain.Entities.AlertRule;
using AlertEventEntity = RealScene3D.Domain.Entities.AlertEvent;
using DashboardEntity = RealScene3D.Domain.Entities.Dashboard;

namespace RealScene3D.Infrastructure.Data;

/// <summary>
/// 应用程序数据库上下文类
/// 继承自Entity Framework Core的DbContext，提供数据访问的核心功能
/// 配置实体关系、索引、约束和全局查询过滤器
/// 支持多种数据库类型：PostgreSQL、SQL Server等
/// </summary>
public class ApplicationDbContext : DbContext
{
    /// <summary>
    /// 构造函数 - 通过依赖注入接收数据库配置选项
    /// </summary>
    /// <param name="options">数据库上下文配置选项，包含连接字符串、日志等配置</param>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<UserLog> UserLogs { get; set; }
    public DbSet<Scene3D> Scenes { get; set; }
    public DbSet<SceneObject> SceneObjects { get; set; }
    public DbSet<WorkflowEntity> Workflows { get; set; }
    public DbSet<WorkflowInstanceEntity> WorkflowInstances { get; set; }
    public DbSet<WorkflowExecutionHistoryEntity> WorkflowExecutionHistories { get; set; }
    public DbSet<SlicingTaskEntity> SlicingTasks { get; set; }
    public DbSet<SliceEntity> Slices { get; set; }
    public DbSet<SystemMetricEntity> SystemMetrics { get; set; }
    public DbSet<BusinessMetricEntity> BusinessMetrics { get; set; }
    public DbSet<AlertRuleEntity> AlertRules { get; set; }
    public DbSet<AlertEventEntity> AlertEvents { get; set; }
    public DbSet<DashboardEntity> Dashboards { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 配置用户实体
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PasswordHash).IsRequired();

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // 配置用户日志实体
        modelBuilder.Entity<UserLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.User)
                  .WithMany(u => u.UserLogs)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // 配置场景实体（包含GIS数据）
        modelBuilder.Entity<Scene3D>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);

            // 配置GIS几何字段
            entity.Property(e => e.Boundary)
                  .HasColumnType("geography");

            entity.Property(e => e.CenterPoint)
                  .HasColumnType("geography");

            entity.HasOne(e => e.Owner)
                  .WithMany()
                  .HasForeignKey(e => e.OwnerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // 配置场景对象实体
        modelBuilder.Entity<SceneObject>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);

            // 配置GIS点字段
            entity.Property(e => e.Position)
                  .HasColumnType("geography");

            entity.HasOne(e => e.Scene)
                  .WithMany(s => s.SceneObjects)
                  .HasForeignKey(e => e.SceneId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // 配置工作流实体
        modelBuilder.Entity<WorkflowEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Version).HasMaxLength(20);
            entity.Property(e => e.Definition).IsRequired();

            entity.HasOne<User>()
                  .WithMany()
                  .HasForeignKey(e => e.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict);

            // 移除全局查询过滤器，避免与必需关系冲突
            // entity.HasQueryFilter(e => e.IsEnabled);
        });

        // 配置工作流实例实体
        modelBuilder.Entity<WorkflowInstanceEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200);

            // 使用导航属性配置关系，避免影子属性
            entity.HasOne(e => e.Workflow)
                  .WithMany()
                  .HasForeignKey(e => e.WorkflowId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<User>()
                  .WithMany()
                  .HasForeignKey(e => e.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 配置工作流执行历史实体
        modelBuilder.Entity<WorkflowExecutionHistoryEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NodeId).HasMaxLength(100);
            entity.Property(e => e.NodeType).HasMaxLength(50);

            // 使用导航属性配置关系，避免影子属性
            entity.HasOne(e => e.WorkflowInstance)
                  .WithMany()
                  .HasForeignKey(e => e.WorkflowInstanceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
