@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ========================================
echo RealScene3D.Lib.OSGB 编译脚本
echo ========================================
echo.

REM ============================================================
REM 参数解析
REM 用法: build.bat [Configuration] [Action]
REM 示例: build.bat Release
REM       build.bat Debug
REM       build.bat Clean
REM       build.bat Release Clean
REM ============================================================

set Configuration=Release
set Action=Build

REM 解析参数
if /i "%~1"=="Clean" (
    set Action=Clean
) else if /i "%~1"=="Debug" (
    set Configuration=Debug
    if /i "%~2"=="Clean" set Action=Clean
) else if /i "%~1"=="Release" (
    set Configuration=Release
    if /i "%~2"=="Clean" set Action=Clean
) else if not "%~1"=="" (
    echo [错误] 无效的参数: %~1
    echo.
    echo 用法: build.bat [Debug^|Release] [Clean]
    echo.
    exit /b 1
)

echo [信息] 配置: %Configuration%
echo [信息] 操作: %Action%
echo.

REM ============================================================
REM 环境检查
REM ============================================================

echo ========================================
echo [步骤 1/2] 环境检查
echo ========================================
echo.

REM 检查并查找 MSBuild
echo 正在查找 MSBuild...
set MSBUILD_PATH=

REM 方式 1：检查 PATH 中是否有 msbuild
where msbuild >nul 2>nul
if %errorlevel% equ 0 (
    set MSBUILD_PATH=msbuild
    goto :msbuild_found
)

REM 方式 2：使用 vswhere 查找 Visual Studio 安装路径
set VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe
if exist "%VSWHERE%" (
    echo 使用 vswhere 查找 Visual Studio...
    for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do (
        set MSBUILD_PATH=%%i
        goto :msbuild_found
    )
)

REM 方式 3：手动搜索常见的 Visual Studio 安装路径
echo 在常见路径中搜索 MSBuild...
for %%V in (2026 2025 2024 2022) do (
    for %%E in (Enterprise Professional Community Preview) do (
        set "TEST_PATH=%ProgramFiles%\Microsoft Visual Studio\%%V\%%E\MSBuild\Current\Bin\MSBuild.exe"
        if exist "!TEST_PATH!" (
            set "MSBUILD_PATH=!TEST_PATH!"
            goto :msbuild_found
        )
    )
)

REM 未找到 MSBuild
echo [错误] 未找到 MSBuild
echo.
echo 请确保已安装 Visual Studio 2022 或更高版本
echo 并且选择了"使用 C++ 的桌面开发"工作负载
echo.
echo 如果已安装 Visual Studio，请尝试：
echo 1. 打开 "Developer Command Prompt for VS"
echo 2. 在该命令提示符中运行此脚本
echo.
exit /b 1

:msbuild_found
echo [✓] 找到 MSBuild: %MSBUILD_PATH%

REM 获取 MSBuild 版本信息
for /f "tokens=*" %%i in ('"%MSBUILD_PATH%" -version ^| findstr /C:"Microsoft"') do set MSBUILD_VERSION=%%i
if defined MSBUILD_VERSION (
    echo [✓] MSBuild 版本: %MSBUILD_VERSION%
)
echo.

REM ============================================================
REM 执行操作
REM ============================================================

echo ========================================
echo [步骤 2/2] %Action% 项目
echo ========================================
echo.

if /i "%Action%"=="Clean" (
    REM 清理项目
    echo 正在清理 %Configuration% 配置...
    "%MSBUILD_PATH%" RealScene3D.Lib.OSGB.vcxproj /t:Clean /p:Configuration=%Configuration% /p:Platform=x64 /v:minimal
    if %errorlevel% neq 0 (
        echo [错误] 清理失败
        exit /b 1
    )
    echo [✓] 清理成功
    echo.

    REM 清理输出目录
    set OutputDir=..\..\..\bin\%Configuration%
    if exist "!OutputDir!\RealScene3D.Lib.OSGB.dll" (
        echo 正在清理输出文件...
        del /Q "!OutputDir!\RealScene3D.Lib.OSGB.dll" 2>nul
        del /Q "!OutputDir!\RealScene3D.Lib.OSGB.pdb" 2>nul
        if exist "!OutputDir!\net9.0" (
            rmdir /S /Q "!OutputDir!\net9.0" 2>nul
        )
        echo [✓] 输出文件已清理
    )
    echo.
) else (
    REM 编译项目
    echo 正在编译 %Configuration% 配置...
    echo 注意: 编译完成后将自动部署 DLL 和 OSG 依赖到 bin\%Configuration%\net9.0\
    echo.
    "%MSBUILD_PATH%" RealScene3D.Lib.OSGB.vcxproj /p:Configuration=%Configuration% /p:Platform=x64 /v:minimal
    if %errorlevel% neq 0 (
        echo [错误] %Configuration% 编译失败
        exit /b 1
    )
    echo.
    echo [✓] %Configuration% 编译成功
    echo.

    REM 检查输出
    set OutputDll=..\..\..\bin\%Configuration%\RealScene3D.Lib.OSGB.dll
    set DeployDir=..\..\..\bin\%Configuration%\net9.0

    if exist "!OutputDll!" (
        echo [✓] 编译输出: !OutputDll!
    ) else (
        echo [x] 未找到编译输出
    )

    if exist "!DeployDir!\RealScene3D.Lib.OSGB.dll" (
        echo [✓] 自动部署: !DeployDir!\RealScene3D.Lib.OSGB.dll
        echo [✓] 部署目录: !DeployDir!\
    ) else (
        echo [警告] 自动部署可能未成功，请检查项目配置
    )
    echo.
)

REM ============================================================
REM 完成
REM ============================================================

echo ========================================
if /i "%Action%"=="Clean" (
    echo ✓ 清理完成！
) else (
    echo ✓ 编译完成！
)
echo ========================================
echo.

if /i "%Action%"=="Build" (
    echo 已完成以下操作:
    echo   1. 编译 %Configuration% 配置
    echo   2. 自动部署到 bin\%Configuration%\net9.0\
    echo.
    echo 您现在可以运行应用程序：
    echo   cd ..\..\..
    echo   dotnet run --project src\RealScene3D.WebApi\RealScene3D.WebApi.csproj
    echo.
) else (
    echo 已清理 %Configuration% 配置的编译输出
    echo.
)

pause
