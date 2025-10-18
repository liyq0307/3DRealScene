#!/bin/bash
# 重置数据库脚本
# 此脚本会删除现有数据库并重新应用所有迁移

echo "========================================"
echo "正在重置RealScene3D数据库..."
echo "========================================"
echo

cd "$(dirname "$0")/../src/RealScene3D.Infrastructure"

echo "第1步: 删除现有数据库..."
dotnet ef database drop --force --startup-project "../RealScene3D.WebApi/RealScene3D.WebApi.csproj" --context ApplicationDbContext

if [ $? -ne 0 ]; then
    echo "警告: 删除数据库时出现错误，可能数据库不存在"
fi

echo
echo "第2步: 应用所有迁移..."
dotnet ef database update --startup-project "../RealScene3D.WebApi/RealScene3D.WebApi.csproj" --context ApplicationDbContext

if [ $? -ne 0 ]; then
    echo "错误: 应用迁移失败！"
    exit 1
fi

echo
echo "========================================"
echo "数据库重置完成！"
echo "========================================"
