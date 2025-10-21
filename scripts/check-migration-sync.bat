@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion
REM 检查实体-迁移同步状态脚本
REM 此脚本检查实体模型是否与数据库迁移同步
REM 如果发现差异，可以自动创建并应用迁移

echo ========================================
echo 正在检查实体-迁移同步状态...
echo ========================================
echo.

cd /d "%~dp0..\src\RealScene3D.Infrastructure"

echo 步骤 1: 正在构建项目...
dotnet build > nul 2>&1

if %ERRORLEVEL% NEQ 0 (
    echo 错误: 构建失败！请先修复构建错误。
    pause
    exit /b 1
)

echo 步骤 2: 正在检查待处理的模型更改...
echo.

REM 尝试添加一个临时迁移来检查是否有更改
dotnet ef migrations add TempCheckMigration --context ApplicationDbContext --no-build > nul 2>&1

if %ERRORLEVEL% NEQ 0 (
    echo 错误: 检查迁移时出错！
    pause
    exit /b 1
)

REM 查找生成的临时迁移文件
for /f "delims=" %%f in ('dir /b /o-d "Migrations\*TempCheckMigration.cs" 2^>nul') do (
    set "migration_file=Migrations\%%f"
    goto :found_file
)

echo 错误: 未找到生成的迁移文件！
pause
exit /b 1

:found_file
REM 检查迁移文件的 Up 方法是否为空（说明没有变更）
findstr /C:"protected override void Up(MigrationBuilder migrationBuilder)" "!migration_file!" > nul
if %ERRORLEVEL% EQU 0 (
    REM 读取 Up 方法后的几行，检查是否只有空白和大括号
    powershell -Command "$content = Get-Content '!migration_file!' -Raw; if ($content -match 'protected override void Up\(MigrationBuilder migrationBuilder\)\s*\{\s*\}') { exit 0 } else { exit 1 }"

    if !ERRORLEVEL! EQU 0 (
        REM Up 方法为空，说明没有变更
        dotnet ef migrations remove --context ApplicationDbContext --force > nul 2>&1
        echo [完成] 所有实体模型已与迁移同步！
        echo 无需任何操作。
        echo.
        pause
        exit /b 0
    )
)

REM 如果执行到这里，说明检测到更改
echo [警告] 实体模型与数据库不同步！
echo.
echo 检测到实体模型中的更改尚未反映在迁移中。
echo.

REM 删除临时迁移
dotnet ef migrations remove --context ApplicationDbContext --force > nul 2>&1

echo 您想要:
echo 1. 创建新迁移以同步更改
echo 2. 退出而不做更改
echo.
set /p choice="请输入您的选择 (1 或 2): "

if "%choice%"=="1" (
    echo.
    set /p migration_name="请输入迁移名称（例如：SyncEntityChanges）: "

    if "!migration_name!"=="" (
        set migration_name=SyncEntityChanges
    )

    echo.
    echo 正在创建迁移: !migration_name!...
    dotnet ef migrations add !migration_name! --context ApplicationDbContext

    if %ERRORLEVEL% NEQ 0 (
        echo 错误: 创建迁移失败！
        pause
        exit /b 1
    )

    echo.
    echo 迁移创建成功！
    echo.
    echo 是否要立即将此迁移应用到数据库？(Y/N)
    set /p apply="您的选择: "

    if /i "!apply!"=="Y" (
        echo.
        echo 正在应用迁移到数据库...
        dotnet ef database update --context ApplicationDbContext

        if %ERRORLEVEL% NEQ 0 (
            echo 错误: 应用迁移失败！
            pause
            exit /b 1
        )

        echo.
        echo ========================================
        echo 迁移应用成功！
        echo ========================================
    ) else (
        echo.
        echo 迁移已创建但未应用。
        echo 稍后运行 dotnet ef database update 来应用它。
    )
) else (
    echo.
    echo 退出而不做更改。
)

echo.
pause
