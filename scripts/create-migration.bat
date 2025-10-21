@echo off
chcp 65001 >nul
REM 创建数据迁移脚本
REM 此脚本帮助您快速创建数据库迁移，并可选择是否立即应用

setlocal enabledelayedexpansion

echo ========================================
echo 创建数据库迁移
echo ========================================
echo.

REM 检查是否通过参数提供了迁移名称
if "%~1"=="" (
    set /p migration_name="请输入迁移名称（直接按回车使用默认名称）: "
) else (
    set migration_name=%~1
)

REM 如果未提供迁移名称，使用默认名称（基于时间戳）
if "!migration_name!"=="" (
    REM 使用 PowerShell 生成时间戳，格式：Migration_YYYYMMDD_HHMMSS
    for /f "usebackq delims=" %%i in (`powershell -Command "Get-Date -Format 'yyyyMMdd_HHmmss'"`) do set timestamp=%%i
    set "migration_name=Migration_!timestamp!"
    echo 使用默认迁移名称: !migration_name!
)

echo.
echo 迁移名称: !migration_name!
echo.

cd /d "%~dp0..\src\RealScene3D.Infrastructure"

echo 步骤 1: 正在构建项目...
dotnet build > nul 2>&1

if %ERRORLEVEL% NEQ 0 (
    echo 错误: 构建失败！请先修复构建错误。
    echo.
    echo 运行构建并显示错误信息:
    dotnet build
    pause
    exit /b 1
)

echo [完成] 构建成功
echo.

echo 步骤 2: 正在创建迁移 '!migration_name!'...
echo.

dotnet ef migrations add "!migration_name!" --context ApplicationDbContext --startup-project "../RealScene3D.WebApi/RealScene3D.WebApi.csproj"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo 错误: 创建迁移失败！
    pause
    exit /b 1
)

echo.
echo ========================================
echo 迁移创建成功！
echo ========================================
echo.

REM 询问用户是否应用迁移
echo 您是否要立即将此迁移应用到数据库？
echo.
echo 选项:
echo 1. 是 - 应用迁移到数据库
echo 2. 否 - 不应用直接退出
echo 3. 先查看迁移 SQL 脚本
echo.
set /p apply_choice="请输入您的选择 (1/2/3): "

if "!apply_choice!"=="1" (
    goto :apply_migration
)

if "!apply_choice!"=="3" (
    echo.
    echo 正在生成迁移的 SQL 脚本...
    echo.

    REM 创建 migrations 目录（如果不存在）
    if not exist "..\..\migrations" mkdir "..\..\migrations"

    dotnet ef migrations script --context ApplicationDbContext --startup-project "../RealScene3D.WebApi/RealScene3D.WebApi.csproj" --output "../../migrations/!migration_name!.sql"

    if %ERRORLEVEL% EQU 0 (
        echo SQL 脚本已保存到: migrations/!migration_name!.sql
        echo.

        REM 尝试用默认编辑器打开 SQL 文件
        if exist "..\..\migrations\!migration_name!.sql" (
            echo 正在打开 SQL 文件...
            start "" "..\..\migrations\!migration_name!.sql"
        )

        echo.
        set /p apply_after_view="查看后是否要应用迁移？(Y/N): "

        if /i "!apply_after_view!"=="Y" (
            goto :apply_migration
        )
    )
)

echo.
echo 迁移已创建但未应用。
echo 稍后应用迁移请运行:
echo   dotnet ef database update --context ApplicationDbContext
echo.
pause
exit /b 0

:apply_migration
echo.
echo 步骤 3: 正在应用迁移到数据库...
echo.

dotnet ef database update --context ApplicationDbContext --startup-project "../RealScene3D.WebApi/RealScene3D.WebApi.csproj"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo 错误: 应用迁移失败！
    echo.
    echo 您可能需要:
    echo 1. 检查 appsettings.json 中的数据库连接
    echo 2. 确保 PostgreSQL 服务正在运行
    echo 3. 验证数据库凭据
    pause
    exit /b 1
)

echo.
echo ========================================
echo 迁移应用成功！
echo ========================================
echo.

REM 显示当前迁移状态
echo 当前数据库状态:
dotnet ef migrations list --context ApplicationDbContext --startup-project "../RealScene3D.WebApi/RealScene3D.WebApi.csproj" | findstr /C:"!migration_name!"

echo.
pause
exit /b 0
