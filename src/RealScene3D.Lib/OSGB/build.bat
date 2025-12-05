@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ========================================
echo RealScene3D.Lib.OSGB 自动化构建脚本
echo ========================================
echo.

REM ============================================================
REM 参数解析
REM 用法: build.bat [OSG_ROOT] [Configuration]
REM 示例: build.bat "C:\Program Files\OpenSceneGraph" Release
REM       build.bat "C:\Program Files\OpenSceneGraph"
REM       build.bat
REM ============================================================

REM 检查第一个参数是否为配置类型（Debug/Release）
set FirstParamIsConfig=0
if /i "%~1"=="Debug" set FirstParamIsConfig=1
if /i "%~1"=="Release" set FirstParamIsConfig=1

REM 根据参数位置设置 OSG_ROOT 和 Configuration
if "%FirstParamIsConfig%"=="1" (
    REM 第一个参数是配置，使用环境变量的 OSG_ROOT
    set Configuration=%~1
) else if not "%~1"=="" (
    REM 第一个参数是 OSG_ROOT
    set "OSG_ROOT=%~1"
    if not "%~2"=="" (
        set Configuration=%~2
    ) else (
        set Configuration=Release
    )
) else (
    REM 没有参数，使用默认值
    set Configuration=Release
)

REM 检查 OSG_ROOT（优先使用参数，其次使用环境变量）
if not defined OSG_ROOT (
    echo [错误] 未设置 OSG_ROOT
    echo.
    echo 请通过以下方式之一指定 OSG_ROOT:
    echo   1. 作为参数: build.bat "C:\Program Files\OpenSceneGraph"
    echo   2. 环境变量: set OSG_ROOT=C:\Program Files\OpenSceneGraph
    echo.
    exit /b 1
)

echo [信息] OSG_ROOT = %OSG_ROOT%
echo [信息] 部署配置 = %Configuration%
echo.

REM ============================================================
REM 第一步：环境检查
REM ============================================================

echo ========================================
echo [步骤 1/4] 环境检查
echo ========================================
echo.

REM 检查 OSG 目录是否存在
if not exist "%OSG_ROOT%\include" (
    echo [错误] OSG 包含目录不存在: %OSG_ROOT%\include
    exit /b 1
)

if not exist "%OSG_ROOT%\lib" (
    echo [错误] OSG 库目录不存在: %OSG_ROOT%\lib
    exit /b 1
)

echo [✓] OpenSceneGraph 目录检查通过
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
REM 第二步：编译
REM ============================================================

echo ========================================
echo [步骤 2/4] 编译项目
echo ========================================
echo.

REM 编译配置
echo 正在编译 %Configuration% 配置...
"%MSBUILD_PATH%" RealScene3D.Lib.OSGB.vcxproj /p:Configuration=%Configuration% /p:Platform=x64 /v:minimal
if %errorlevel% neq 0 (
    echo [错误] %Configuration% 编译失败
    exit /b 1
)
echo [✓] %Configuration% 编译成功
echo.

echo 编译输出: ..\..\..\bin\%Configuration%\RealScene3D.Lib.OSGB.dll
echo.

REM ============================================================
REM 第三步：部署
REM ============================================================

echo ========================================
echo [步骤 3/4] 部署 DLL
echo ========================================
echo.

REM 设置路径
set TargetPath=..\..\..\bin\%Configuration%\net8.0
set SourceDll=..\..\..\bin\%Configuration%\RealScene3D.Lib.OSGB.dll
set OsgBinPath=%OSG_ROOT%\bin

echo [信息] 部署配置: %Configuration%
echo [信息] 目标路径: %TargetPath%
echo.

REM 检查源文件
if not exist "%SourceDll%" (
    echo [错误] 找不到编译输出: %SourceDll%
    exit /b 1
)

REM 创建目标目录
if not exist "%TargetPath%" (
    mkdir "%TargetPath%"
    echo [✓] 创建目标目录
)

REM 复制 RealScene3D.Lib.OSGB.dll
echo [1/3] 复制 RealScene3D.Lib.OSGB.dll...
copy /Y "%SourceDll%" "%TargetPath%" >nul
if %errorlevel% neq 0 (
    echo [错误] 复制失败
    exit /b 1
)
echo [✓] 完成
echo.

REM 复制 OpenSceneGraph DLL
echo [2/3] 复制 OpenSceneGraph DLL...

REM 定义 OSG DLL 列表
set OsgDlls=osg.dll osgDB.dll osgUtil.dll OpenThreads.dll

REM 如果是 Debug 配置，使用 Debug DLL
if /i "%Configuration%"=="Debug" (
    set OsgDlls=osgd.dll osgDBd.dll osgUtild.dll OpenThreadsd.dll
)

set CopiedCount=0

for %%f in (%OsgDlls%) do (
    if exist "%OsgBinPath%\%%f" (
        copy /Y "%OsgBinPath%\%%f" "%TargetPath%" >nul 2>&1
        if !errorlevel! equ 0 (
            echo   - %%f
            set /a CopiedCount+=1
        )
    ) else (
        echo   [警告] 未找到: %%f
    )
)

echo [✓] 复制了 %CopiedCount% 个 OSG DLL
echo.

REM 复制 OSG 插件目录
echo [3/3] 复制 OSG 插件目录...
set PluginCopied=0
for /d %%d in ("%OsgBinPath%\osgPlugins-*") do (
    set PluginDir=%%~nxd
    set TargetPluginPath=%TargetPath%\!PluginDir!

    if not exist "!TargetPluginPath!" (
        xcopy "%%d" "!TargetPluginPath!" /E /I /Q >nul
        echo [✓] 复制插件: !PluginDir!
        set PluginCopied=1
    ) else (
        echo [跳过] 插件目录已存在: !PluginDir!
        set PluginCopied=1
    )
    goto :plugin_done
)
:plugin_done

if "%PluginCopied%"=="0" (
    echo [警告] 未找到 OSG 插件目录
)
echo.

REM ============================================================
REM 第四步：验证部署
REM ============================================================

echo ========================================
echo [步骤 4/4] 验证部署
echo ========================================
echo.

REM 检查 RealScene3D.Lib.OSGB.dll
set VerifyFailed=0
if exist "%TargetPath%\RealScene3D.Lib.OSGB.dll" (
    echo [✓] RealScene3D.Lib.OSGB.dll
) else (
    echo [x] RealScene3D.Lib.OSGB.dll 未找到
    set VerifyFailed=1
)

REM 检查 OSG DLL（根据配置）
set CheckDlls=osg.dll osgDB.dll osgUtil.dll OpenThreads.dll
if /i "%Configuration%"=="Debug" (
    set CheckDlls=osgd.dll osgDBd.dll osgUtild.dll OpenThreadsd.dll
)

for %%f in (%CheckDlls%) do (
    if exist "%TargetPath%\%%f" (
        echo [✓] %%f
    ) else (
        echo [x] %%f 未找到
        set VerifyFailed=1
    )
)

REM 检查插件目录
set PluginFound=0
for /d %%d in ("%TargetPath%\osgPlugins-*") do (
    echo [✓] %%~nxd
    set PluginFound=1
)

if %PluginFound%==0 (
    echo [X] osgPlugins 目录未找到
    set VerifyFailed=1
)

echo.

REM ============================================================
REM 构建结果
REM ============================================================

echo ========================================
if "%VerifyFailed%"=="1" (
    echo x 构建失败！
    echo ========================================
    echo.
    echo 部分文件未成功部署，请检查上述错误信息。
    echo.
    pause
    exit /b 1
) else (
    echo ✓ 构建成功！
    echo ========================================
    echo.
    echo 已完成以下操作:
    echo   1. 编译 %Configuration% 配置
    echo   2. 部署 %Configuration% DLL 到应用程序目录
    echo   3. 复制 OSG 依赖
    echo   4. 验证部署完整性
    echo.
    echo 输出目录: %TargetPath%
    echo.
    echo 您现在可以运行应用程序：
    echo   cd ..\..\..
    echo   dotnet run --project src\RealScene3D.WebApi\RealScene3D.WebApi.csproj
    echo.
)

pause
