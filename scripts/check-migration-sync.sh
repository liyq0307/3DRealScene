#!/bin/bash
# 检查实体-迁移同步状态脚本
# 此脚本检查实体模型是否与数据库迁移同步
# 如果发现差异，可以自动创建并应用迁移

echo "========================================"
echo "正在检查实体-迁移同步状态..."
echo "========================================"
echo ""

# 导航到 Infrastructure 项目目录
cd "$(dirname "$0")/../src/RealScene3D.Infrastructure" || exit 1

echo "步骤 1: 正在构建项目..."
dotnet build > /dev/null 2>&1

if [ $? -ne 0 ]; then
    echo "错误: 构建失败！请先修复构建错误。"
    exit 1
fi

echo "步骤 2: 正在检查待处理的模型更改..."
echo ""

# 尝试添加一个临时迁移来检查是否有更改
dotnet ef migrations add TempCheckMigration --context ApplicationDbContext --no-build > /dev/null 2>&1

if [ $? -ne 0 ]; then
    echo "错误: 检查迁移时出错！"
    exit 1
fi

# 查找生成的临时迁移文件
migration_file=$(find Migrations -name "*TempCheckMigration.cs" -type f | head -n 1)

if [ -z "$migration_file" ]; then
    echo "错误: 未找到生成的迁移文件！"
    exit 1
fi

# 检查迁移文件的 Up 方法是否为空（说明没有变更）
# 读取文件内容并检查 Up 方法是否只包含空白
if grep -Pzo 'protected override void Up\(MigrationBuilder migrationBuilder\)\s*\{\s*\}' "$migration_file" > /dev/null 2>&1; then
    # Up 方法为空，说明没有变更
    dotnet ef migrations remove --context ApplicationDbContext --force > /dev/null 2>&1
    echo "[完成] 所有实体模型已与迁移同步！"
    echo "无需任何操作。"
    echo ""
    exit 0
fi

# 如果执行到这里，说明检测到更改
echo "[警告] 实体模型与数据库不同步！"
echo ""
echo "检测到实体模型中的更改尚未反映在迁移中。"
echo ""

# 删除临时迁移
dotnet ef migrations remove --context ApplicationDbContext --force > /dev/null 2>&1

echo "您想要:"
echo "1. 创建新迁移以同步更改"
echo "2. 退出而不做更改"
echo ""
read -p "请输入您的选择 (1 或 2): " choice

if [ "$choice" = "1" ]; then
    echo ""
    read -p "请输入迁移名称（例如：SyncEntityChanges）: " migration_name

    if [ -z "$migration_name" ]; then
        migration_name="SyncEntityChanges"
    fi

    echo ""
    echo "正在创建迁移: $migration_name..."
    dotnet ef migrations add "$migration_name" --context ApplicationDbContext

    if [ $? -ne 0 ]; then
        echo "错误: 创建迁移失败！"
        exit 1
    fi

    echo ""
    echo "迁移创建成功！"
    echo ""
    read -p "是否要立即将此迁移应用到数据库？(y/n): " apply

    if [ "$apply" = "y" ] || [ "$apply" = "Y" ]; then
        echo ""
        echo "正在应用迁移到数据库..."
        dotnet ef database update --context ApplicationDbContext

        if [ $? -ne 0 ]; then
            echo "错误: 应用迁移失败！"
            exit 1
        fi

        echo ""
        echo "========================================"
        echo "迁移应用成功！"
        echo "========================================"
    else
        echo ""
        echo "迁移已创建但未应用。"
        echo "稍后运行 dotnet ef database update 来应用它。"
    fi
else
    echo ""
    echo "退出而不做更改。"
fi

echo ""
