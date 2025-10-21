using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace RealScene3D.Infrastructure.Data;

/// <summary>
/// ApplicationDbContext 设计时工厂
/// 用于 EF Core 工具在设计时（运行迁移命令时）创建 DbContext 实例
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // 获取 Infrastructure 项目的根目录
        var infrastructureDirectory = Directory.GetCurrentDirectory();

        // 计算 WebApi 项目的路径（向上一级到 src，再进入 RealScene3D.WebApi）
        var webApiDirectory = Path.Combine(infrastructureDirectory, "..", "RealScene3D.WebApi");
        var fullWebApiPath = Path.GetFullPath(webApiDirectory);

        // 构建配置 - appsettings.json 可选，优先使用 Development
        var configuration = new ConfigurationBuilder()
            .SetBasePath(fullWebApiPath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        // 获取连接字符串 - 尝试多个可能的键名
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                             ?? configuration.GetConnectionString("PostgreSqlConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "无法从配置文件中找到数据库连接字符串。" +
                $"请检查 {fullWebApiPath} 目录下的 appsettings.json 或 appsettings.Development.json 文件中的 'DefaultConnection' 或 'PostgreSqlConnection'。");
        }

        // 构建 DbContext 选项
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString, o => o.UseNetTopologySuite());

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
