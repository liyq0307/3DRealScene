@echo off
setlocal enabledelayedexpansion

REM Check auto mode
set AUTO_MODE=0
if /i "%1"=="/auto" set AUTO_MODE=1

echo ========================================
echo  RealScene3D.Lib.OSGB - vcpkg Setup
echo ========================================
echo.

set SCRIPT_DIR=%~dp0
set VCPKG_DIR=%SCRIPT_DIR%..\3dParty\vcpkg
set VCPKG_INSTALLED_DIR=%SCRIPT_DIR%..\3dParty\vcpkg_installed

echo [1/4] Checking vcpkg...
if exist "%VCPKG_DIR%\vcpkg.exe" (
    echo       vcpkg is already installed: %VCPKG_DIR%
    goto :install_deps
)

echo [2/4] Cloning vcpkg repository...
if not exist "%SCRIPT_DIR%..\3dParty" mkdir "%SCRIPT_DIR%..\3dParty"
git clone https://github.com/microsoft/vcpkg.git "%VCPKG_DIR%"
if errorlevel 1 (
    echo       ERROR: Failed to clone vcpkg repository!
    echo       Please check git installation and network connection.
    echo       You may need to configure proxy if behind firewall.
    exit /b 1
)

echo [3/4] Bootstrapping vcpkg...
call "%VCPKG_DIR%\bootstrap-vcpkg.bat"
if errorlevel 1 (
    echo       ERROR: vcpkg bootstrap failed!
    exit /b 1
)

:install_deps
echo [4/4] Configuration completed
echo.
echo ========================================
echo  vcpkg path:      %VCPKG_DIR%
echo  Install path:    %VCPKG_INSTALLED_DIR%
echo ========================================
echo.

if %AUTO_MODE%==0 (
    echo Next steps:
    echo   1. mkdir build ^&^& cd build
    echo   2. cmake .. -G "Visual Studio 17 2022" -A x64
    echo   3. cmake --build . --config Release
    echo.
    echo Note: vcpkg will auto-download dependencies during CMake configure.
    echo.
    pause
) else (
    echo CMake will continue configuration...
)

exit /b 0
