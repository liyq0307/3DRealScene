#!/bin/bash
# 创建数据迁移脚本
# 此脚本帮助您快速创建数据库迁移，并可选择是否立即应用

echo "========================================"
echo "创建数据库迁移"
echo "========================================"
echo ""

# 检查是否通过参数提供了迁移名称
if [ -z "$1" ]; then
    read -p "请输入迁移名称（直接按回车使用默认名称）: " migration_name
else
    migration_name="$1"
fi

# 如果未提供迁移名称，使用默认名称（基于时间戳）
if [ -z "$migration_name" ]; then
    # 生成默认迁移名称：Migration_YYYYMMDD_HHMMSS
    migration_name="Migration_$(date +%Y%m%d_%H%M%S)"
    echo "使用默认迁移名称: $migration_name"
fi

echo ""
echo "迁移名称: $migration_name"
echo ""

# 导航到 Infrastructure 项目目录
cd "$(dirname "$0")/../src/RealScene3D.Infrastructure" || exit 1

echo "步骤 1: 正在构建项目..."
dotnet build > /dev/null 2>&1

if [ $? -ne 0 ]; then
    echo "错误: 构建失败！请先修复构建错误。"
    echo ""
    echo "运行构建并显示错误信息:"
    dotnet build
    exit 1
fi

echo "[完成] 构建成功"
echo ""

echo "步骤 2: 正在创建迁移 '$migration_name'..."
echo ""

dotnet ef migrations add "$migration_name" --context ApplicationDbContext --startup-project "../RealScene3D.WebApi/RealScene3D.WebApi.csproj"

if [ $? -ne 0 ]; then
    echo ""
    echo "错误: 创建迁移失败！"
    exit 1
fi

echo ""
echo "========================================"
echo "迁移创建成功！"
echo "========================================"
echo ""

# 应用迁移的函数
apply_migration() {
    echo ""
    echo "步骤 3: 正在应用迁移到数据库..."
    echo ""

    dotnet ef database update --context ApplicationDbContext --startup-project "../RealScene3D.WebApi/RealScene3D.WebApi.csproj"

    if [ $? -ne 0 ]; then
        echo ""
        echo "错误: 应用迁移失败！"
        echo ""
        echo "您可能需要:"
        echo "1. 检查 appsettings.json 中的数据库连接"
        echo "2. 确保 PostgreSQL 服务正在运行"
        echo "3. 验证数据库凭据"
        exit 1
    fi

    echo ""
    echo "========================================"
    echo "迁移应用成功！"
    echo "========================================"
    echo ""

    # 显示当前迁移状态
    echo "当前数据库状态:"
    dotnet ef migrations list --context ApplicationDbContext --startup-project "../RealScene3D.WebApi/RealScene3D.WebApi.csproj" | grep "$migration_name"

    echo ""
}

# 询问用户是否应用迁移
echo "您是否要立即将此迁移应用到数据库？"
echo ""
echo "选项:"
echo "1. 是 - 应用迁移到数据库"
echo "2. 否 - 不应用直接退出"
echo "3. 先查看迁移 SQL 脚本"
echo ""
read -p "请输入您的选择 (1/2/3): " apply_choice

if [ "$apply_choice" = "1" ]; then
    apply_migration
    exit 0
fi

if [ "$apply_choice" = "3" ]; then
    echo ""
    echo "正在生成迁移的 SQL 脚本..."
    echo ""

    # 如果不存在则创建 migrations 目录
    mkdir -p "../../migrations"

    dotnet ef migrations script --context ApplicationDbContext --startup-project "../RealScene3D.WebApi/RealScene3D.WebApi.csproj" --output "../../migrations/${migration_name}.sql"

    if [ $? -eq 0 ]; then
        echo "SQL 脚本已保存到: migrations/${migration_name}.sql"
        echo ""

        # 尝试显示 SQL 文件
        if [ -f "../../migrations/${migration_name}.sql" ]; then
            echo "SQL 脚本预览 (前 50 行):"
            echo "------------------------------------"
            head -50 "../../migrations/${migration_name}.sql"
            echo "------------------------------------"
            echo ""
        fi

        read -p "查看后是否要应用迁移？(y/n): " apply_after_view

        if [ "$apply_after_view" = "y" ] || [ "$apply_after_view" = "Y" ]; then
            apply_migration
            exit 0
        fi
    fi
fi

echo ""
echo "迁移已创建但未应用。"
echo "稍后应用迁移请运行:"
echo "  dotnet ef database update --context ApplicationDbContext"
echo ""
