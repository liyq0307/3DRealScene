@echo off
chcp 65001 >nul
REM 重置数据库脚本
REM 此脚本将删除现有数据库并重新应用所有迁移

echo ========================================
echo 正在重置 RealScene3D 数据库...
echo ========================================
echo.

cd /d "%~dp0..\src\RealScene3D.Infrastructure"

echo 步骤 1: 正在删除现有数据库...
dotnet ef database drop --force --startup-project "../RealScene3D.WebApi/RealScene3D.WebApi.csproj" --context ApplicationDbContext

if %ERRORLEVEL% NEQ 0 (
    echo 警告: 删除数据库时出现错误，可能数据库不存在
)

echo.
echo 步骤 2: 正在应用所有迁移...
dotnet ef database update --startup-project "../RealScene3D.WebApi/RealScene3D.WebApi.csproj" --context ApplicationDbContext

if %ERRORLEVEL% NEQ 0 (
    echo 错误: 应用迁移失败！
    pause
    exit /b 1
)

echo.
echo ========================================
echo 数据库重置完成！
echo ========================================
pause
