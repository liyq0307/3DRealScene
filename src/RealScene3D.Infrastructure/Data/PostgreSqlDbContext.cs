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
/// PostgreSQL/PostGIS 数据库上下文
/// </summary>
public class PostgreSqlDbContext : DbContext
{
    public PostgreSqlDbContext(DbContextOptions<PostgreSqlDbContext> options)
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

        // 配置场景实体（PostGIS 几何数据）
        modelBuilder.Entity<Scene3D>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);

            // PostGIS 几何字段 - 使用 SRID 4326 (WGS 84)
            entity.Property(e => e.Boundary)
                  .HasColumnType("geometry(Polygon, 4326)");

            entity.Property(e => e.CenterPoint)
                  .HasColumnType("geometry(PointZ, 4326)");

            entity.HasOne(e => e.Owner)
                  .WithMany()
                  .HasForeignKey(e => e.OwnerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(e => !e.IsDeleted);

            // PostGIS 空间索引
            entity.HasIndex(e => e.Boundary)
                  .HasMethod("gist");

            entity.HasIndex(e => e.CenterPoint)
                  .HasMethod("gist");
        });

        // 配置场景对象实体
        modelBuilder.Entity<SceneObject>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);

            // PostGIS 3D 点
            entity.Property(e => e.Position)
                  .HasColumnType("geometry(PointZ, 4326)");

            entity.HasOne(e => e.Scene)
                  .WithMany(s => s.SceneObjects)
                  .HasForeignKey(e => e.SceneId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasQueryFilter(e => !e.IsDeleted);

            // 空间索引
            entity.HasIndex(e => e.Position)
                  .HasMethod("gist");
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
        });

        // 配置工作流实例实体
        modelBuilder.Entity<WorkflowInstanceEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200);

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

            entity.HasOne(e => e.WorkflowInstance)
                  .WithMany()
                  .HasForeignKey(e => e.WorkflowInstanceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
