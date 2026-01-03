cmake_minimum_required(VERSION 3.22)

# ============================================================================
# vcpkg本地集成配置 - 自动下载依赖到3dParty目录
# ============================================================================

# 设置vcpkg安装目录为本地3dParty
set(VCPKG_INSTALLED_DIR "${CMAKE_SOURCE_DIR}/../3dParty/vcpkg_installed" CACHE PATH "vcpkg安装目录")

# 启用vcpkg二进制缓存（加速重复构建）
set(VCPKG_BINARY_SOURCES "clear;files,${CMAKE_SOURCE_DIR}/../3dParty/vcpkg_cache,readwrite" CACHE STRING "vcpkg binary cache")

# ============================================================================
# vcpkg 可选依赖构建控制
# ============================================================================

# 允许使用外部OSG安装（跳过vcpkg）
option(USE_EXTERNAL_OSG "Use externally installed OSG instead of vcpkg" OFF)

# 可选库的vcpkg构建开关（默认不构建，按需启用）
option(BUILD_PROJ_WITH_VCPKG "Build PROJ library with vcpkg" OFF)
option(BUILD_BASISU_WITH_VCPKG "Build Basis Universal with vcpkg" OFF)
option(BUILD_MESHOPT_WITH_VCPKG "Build meshoptimizer with vcpkg" OFF)
option(BUILD_DRACO_WITH_VCPKG "Build Draco with vcpkg" OFF)

# 功能启用开关（控制是否查找和使用库，默认启用）
option(ENABLE_TEXTURE_COMPRESSION "Enable KTX2 texture compression" ON)
option(ENABLE_MESH_OPTIMIZATION "Enable mesh optimization" ON)
option(ENABLE_DRACO_COMPRESSION "Enable Draco mesh compression" ON)

# 根据选项配置vcpkg manifest features（必须在vcpkg工具链加载前设置）
set(VCPKG_MANIFEST_FEATURES "")

# OSG控制
if(NOT USE_EXTERNAL_OSG)
    list(APPEND VCPKG_MANIFEST_FEATURES "core-osg")
endif()

# 可选库控制
if(BUILD_PROJ_WITH_VCPKG)
    list(APPEND VCPKG_MANIFEST_FEATURES "coordinate-transform")
endif()
if(BUILD_BASISU_WITH_VCPKG)
    list(APPEND VCPKG_MANIFEST_FEATURES "texture-compression")
endif()
if(BUILD_MESHOPT_WITH_VCPKG)
    list(APPEND VCPKG_MANIFEST_FEATURES "mesh-optimization")
endif()
if(BUILD_DRACO_WITH_VCPKG)
    list(APPEND VCPKG_MANIFEST_FEATURES "draco-compression")
endif()

# 将features设置为CACHE变量（vcpkg需要，必须无条件设置）
set(VCPKG_MANIFEST_FEATURES "${VCPKG_MANIFEST_FEATURES}" CACHE STRING "vcpkg manifest features" FORCE)
if(VCPKG_MANIFEST_FEATURES)
    message(STATUS "启用vcpkg特性: ${VCPKG_MANIFEST_FEATURES}")
endif()

# 查找或下载vcpkg（只要有任何feature需要，就加载vcpkg）
if(NOT DEFINED CMAKE_TOOLCHAIN_FILE AND VCPKG_MANIFEST_FEATURES)
    set(VCPKG_ROOT "${CMAKE_SOURCE_DIR}/../3dParty/vcpkg")

    # 检查本地vcpkg是否存在
    if(NOT EXISTS "${VCPKG_ROOT}/scripts/buildsystems/vcpkg.cmake")
        message(STATUS "========================================")
        message(STATUS "  vcpkg未找到，自动安装中...")
        message(STATUS "========================================")

        # 自动执行安装脚本
        if(WIN32)
            set(SETUP_SCRIPT "${CMAKE_SOURCE_DIR}/setup-vcpkg.bat")
            if(EXISTS "${SETUP_SCRIPT}")
                message(STATUS "执行安装脚本: ${SETUP_SCRIPT}")
                execute_process(
                    COMMAND cmd /c "${SETUP_SCRIPT}" /auto
                    WORKING_DIRECTORY "${CMAKE_SOURCE_DIR}"
                    RESULT_VARIABLE SETUP_RESULT
                    OUTPUT_VARIABLE SETUP_OUTPUT
                    ERROR_VARIABLE SETUP_ERROR
                )

                if(NOT SETUP_RESULT EQUAL 0)
                    message(STATUS "脚本输出: ${SETUP_OUTPUT}")
                    message(STATUS "脚本错误: ${SETUP_ERROR}")
                    message(FATAL_ERROR "vcpkg安装失败！请手动运行: ${SETUP_SCRIPT}")
                endif()

                # 再次检查vcpkg是否安装成功
                if(NOT EXISTS "${VCPKG_ROOT}/scripts/buildsystems/vcpkg.cmake")
                    message(FATAL_ERROR "vcpkg安装脚本执行完成，但vcpkg仍未找到。请检查网络连接或手动安装。")
                endif()

                message(STATUS "vcpkg安装成功！")
            else()
                message(FATAL_ERROR "安装脚本未找到: ${SETUP_SCRIPT}\n请手动克隆vcpkg: git clone https://github.com/microsoft/vcpkg.git \"${VCPKG_ROOT}\"")
            endif()
        else()
            # Linux/macOS手动安装提示
            message(STATUS "请手动执行以下命令安装vcpkg:")
            message(STATUS "  git clone https://github.com/microsoft/vcpkg.git \"${VCPKG_ROOT}\"")
            message(STATUS "  ${VCPKG_ROOT}/bootstrap-vcpkg.sh")
            message(FATAL_ERROR "请先安装vcpkg后重新运行CMake")
        endif()
    else()
        message(STATUS "发现vcpkg: ${VCPKG_ROOT}")
    endif()

    # 设置vcpkg工具链文件
    set(CMAKE_TOOLCHAIN_FILE "${VCPKG_ROOT}/scripts/buildsystems/vcpkg.cmake"
        CACHE STRING "Vcpkg toolchain file")
    message(STATUS "使用本地vcpkg: ${VCPKG_ROOT}")
    message(STATUS "依赖将安装到: ${VCPKG_INSTALLED_DIR}")
    message(STATUS "二进制缓存: ${CMAKE_SOURCE_DIR}/../3dParty/vcpkg_cache")
else()
    if(NOT VCPKG_MANIFEST_FEATURES)
        message(STATUS "未启用任何vcpkg特性，跳过vcpkg")
    endif()
endif()

project(RealScene3D.Lib.OSGB VERSION 1.0.0 LANGUAGES CXX)

# C++标准设置
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# 输出目录
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/../../../bin/${CMAKE_BUILD_TYPE})
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/../../../bin/${CMAKE_BUILD_TYPE})
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/../../../bin/${CMAKE_BUILD_TYPE})

# 预处理宏定义
add_definitions(
    -DOSGB_EXPORTS
    -DNOMINMAX
    -DWIN32_LEAN_AND_MEAN
)

# 源文件
set(NATIVE_SOURCES
    Native/OsgbReaderCApi.cpp
    Native/Osgb23dTile.cpp
    Native/MeshProcessor.cpp
    Native/GeoTransform.cpp
    Native/CoordinateApi.cpp
)

# 头文件
set(NATIVE_HEADERS
    Native/OsgbReaderCApi.h
    Native/MeshProcessor.h
    Native/LodPipeline.h
    Native/GeoTransform.h
    Native/CoordinateApi.h
    Native/Extern.h
    Native/OsgFix.h
)

# 创建动态链接库
add_library(${PROJECT_NAME} SHARED ${NATIVE_SOURCES} ${NATIVE_HEADERS})

# 包含目录
target_include_directories(${PROJECT_NAME}
    PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/Native
        ${CMAKE_CURRENT_SOURCE_DIR}/../3dParty/include
)

# Windows特定配置
if(WIN32)
    # 暂时禁用预编译头以避免条件编译宏冲突
    # target_precompile_headers(${PROJECT_NAME} PRIVATE Native/OsgFix.h)

    # 添加/bigobj选项以支持大型对象文件
    target_compile_options(${PROJECT_NAME} PRIVATE /bigobj)
endif()

# ============================================================================
# 依赖库配置
# ============================================================================

# 查找OpenSceneGraph（必需）
# 优先使用外部预编译版本
if(USE_EXTERNAL_OSG)
    # 检查预编译OSG目录
    set(OSG_PREBUILT_DIR "${CMAKE_SOURCE_DIR}/../3dParty/osg")
    if(EXISTS "${OSG_PREBUILT_DIR}")
        message(STATUS "使用预编译OSG: ${OSG_PREBUILT_DIR}")
        set(OSG_DIR "${OSG_PREBUILT_DIR}" CACHE PATH "OSG installation directory")
        set(CMAKE_PREFIX_PATH "${OSG_PREBUILT_DIR}" ${CMAKE_PREFIX_PATH})
    else()
        message(WARNING "预编译OSG目录不存在: ${OSG_PREBUILT_DIR}")
        message(STATUS "请运行自行下载预编译版本")
    endif()
endif()

find_package(OpenSceneGraph REQUIRED COMPONENTS osg osgDB osgUtil OpenThreads)
if(OpenSceneGraph_FOUND)
    message(STATUS "Found OpenSceneGraph: ${OSG_INCLUDE_DIR}")
    target_include_directories(${PROJECT_NAME} PRIVATE ${OSG_INCLUDE_DIR})
    target_link_libraries(${PROJECT_NAME} PRIVATE
        ${OSG_LIBRARY}
        ${OSGDB_LIBRARY}
        ${OSGUTIL_LIBRARY}
        ${OPENTHREADS_LIBRARY}
    )

    # Linux/macOS需要链接静态插件
    if(UNIX)
        target_link_libraries(${PROJECT_NAME} PRIVATE osgdb_osg)
    endif()

    # Windows复制DLL到输出目录
    if(WIN32 AND USE_EXTERNAL_OSG)
        set(OSG_BIN_DIR "${OSG_DIR}/bin")
        if(EXISTS "${OSG_BIN_DIR}")
            message(STATUS "OSG DLL目录: ${OSG_BIN_DIR}")
            # 使用file(GLOB)获取DLL文件列表后逐个复制
            file(GLOB OSG_DLLS "${OSG_BIN_DIR}/osg*.dll" "${OSG_BIN_DIR}/ot*.dll")
            foreach(DLL_FILE ${OSG_DLLS})
                add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
                    COMMAND ${CMAKE_COMMAND} -E copy_if_different
                        "${DLL_FILE}"
                        "${CMAKE_RUNTIME_OUTPUT_DIRECTORY}"
                    COMMENT "Copying ${DLL_FILE}"
                )
            endforeach()
        endif()
    endif()
else()
    message(WARNING "OpenSceneGraph not found. Please install OSG or set OSG_DIR")
endif()

# ============================================================================
# 可选依赖（用于高级功能）
# ============================================================================

# Basis Universal (KTX2纹理压缩)
if(ENABLE_TEXTURE_COMPRESSION)
    find_package(basisu CONFIG)
    if(basisu_FOUND)
        message(STATUS "Found Basis Universal - KTX2 compression enabled")
        target_link_libraries(${PROJECT_NAME} PRIVATE basisu::basisu_encoder)
        target_compile_definitions(${PROJECT_NAME} PRIVATE ENABLE_KTX2)
    else()
        message(WARNING "basisu not found - texture compression disabled")
    endif()
endif()

# meshoptimizer (网格优化)
if(ENABLE_MESH_OPTIMIZATION)
    find_package(meshoptimizer CONFIG)
    if(meshoptimizer_FOUND)
        message(STATUS "Found meshoptimizer - mesh optimization enabled")
        target_link_libraries(${PROJECT_NAME} PRIVATE meshoptimizer::meshoptimizer)
        target_compile_definitions(${PROJECT_NAME} PRIVATE OPTIMIZER)
    else()
        message(WARNING "meshoptimizer not found - mesh optimization disabled")
    endif()
endif()

# Draco (网格压缩)
if(ENABLE_DRACO_COMPRESSION)
    find_package(draco CONFIG)
    if(draco_FOUND)
        message(STATUS "Found Draco - mesh compression enabled")
        target_link_libraries(${PROJECT_NAME} PRIVATE draco::draco)
        target_compile_definitions(${PROJECT_NAME} PRIVATE ENABLE_DRACO)
    else()
        message(WARNING "draco not found - mesh compression disabled")
    endif()
endif()

# PROJ (地理坐标系转换 - 可选，轻量级)
find_package(PROJ CONFIG)
if(PROJ_FOUND)
    message(STATUS "Found PROJ - coordinate transformations enabled")
    target_link_libraries(${PROJECT_NAME} PRIVATE PROJ::proj)
    target_compile_definitions(${PROJECT_NAME} PRIVATE ENABLE_PROJ)
else()
    message(STATUS "PROJ not found - coordinate transformations disabled")
endif()

# ============================================================================
# 安装配置
# ============================================================================

install(TARGETS ${PROJECT_NAME}
    RUNTIME DESTINATION bin
    LIBRARY DESTINATION lib
    ARCHIVE DESTINATION lib
)

install(FILES Native/OsgbReaderCApi.h DESTINATION include)

# ============================================================================
# 编译信息输出
# ============================================================================

message(STATUS "")
message(STATUS "========================================")
message(STATUS "  ${PROJECT_NAME} Configuration")
message(STATUS "========================================")
message(STATUS "  C++ Standard:          C++${CMAKE_CXX_STANDARD}")
message(STATUS "  Build Type:            ${CMAKE_BUILD_TYPE}")
message(STATUS "  Output Directory:      ${CMAKE_RUNTIME_OUTPUT_DIRECTORY}")
message(STATUS "")
message(STATUS "  Required Dependencies:")
message(STATUS "    OpenSceneGraph:      ${OpenSceneGraph_FOUND}")
message(STATUS "")
message(STATUS "  vcpkg 构建选项:")
message(STATUS "    USE_EXTERNAL_OSG:    ${USE_EXTERNAL_OSG}")
message(STATUS "    BUILD_PROJ_WITH_VCPKG:    ${BUILD_PROJ_WITH_VCPKG}")
message(STATUS "    BUILD_BASISU_WITH_VCPKG:  ${BUILD_BASISU_WITH_VCPKG}")
message(STATUS "    BUILD_MESHOPT_WITH_VCPKG: ${BUILD_MESHOPT_WITH_VCPKG}")
message(STATUS "    BUILD_DRACO_WITH_VCPKG:   ${BUILD_DRACO_WITH_VCPKG}")
message(STATUS "")
message(STATUS "  Optional Features:")
message(STATUS "    Texture Compression: ${ENABLE_TEXTURE_COMPRESSION}")
message(STATUS "    Mesh Optimization:   ${ENABLE_MESH_OPTIMIZATION}")
message(STATUS "    Draco Compression:   ${ENABLE_DRACO_COMPRESSION}")
message(STATUS "    Eigen3:              ${Eigen3_FOUND}")
message(STATUS "    PROJ:                ${PROJ_FOUND}")
message(STATUS "========================================")
message(STATUS "")
