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

# 输出目录配置（支持多配置生成器）
# 对于单配置生成器（Makefile, Ninja），使用CMAKE_BUILD_TYPE
# 对于多配置生成器（Visual Studio, Xcode），使用每个配置的特定目录
if(CMAKE_CONFIGURATION_TYPES)
    # 多配置生成器（Visual Studio, Xcode）
    set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/../../../bin)
    set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/../../../bin)
    set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/../../../bin)

    # 为每个配置类型设置输出目录
    foreach(CONFIG_TYPE ${CMAKE_CONFIGURATION_TYPES})
        string(TOUPPER ${CONFIG_TYPE} CONFIG_TYPE_UPPER)
        set(CMAKE_RUNTIME_OUTPUT_DIRECTORY_${CONFIG_TYPE_UPPER} ${CMAKE_SOURCE_DIR}/../../../bin/${CONFIG_TYPE})
        set(CMAKE_LIBRARY_OUTPUT_DIRECTORY_${CONFIG_TYPE_UPPER} ${CMAKE_SOURCE_DIR}/../../../bin/${CONFIG_TYPE})
        set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY_${CONFIG_TYPE_UPPER} ${CMAKE_SOURCE_DIR}/../../../bin/${CONFIG_TYPE})
    endforeach()
else()
    # 单配置生成器（Makefile, Ninja）
    set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/../../../bin/${CMAKE_BUILD_TYPE})
    set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/../../../bin/${CMAKE_BUILD_TYPE})
    set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/../../../bin/${CMAKE_BUILD_TYPE})
endif()

# 预处理宏定义
add_definitions(
    -DOSGB_EXPORTS
    -DNOMINMAX
    -DWIN32_LEAN_AND_MEAN
    -DSPDLOG_FMT_EXTERNAL
    -DSPDLOG_COMPILED_LIB
)

# 源文件
set(NATIVE_SOURCES
    Native/OSGB23dTiles.cpp
    Native/MeshProcessor.cpp
    Native/GeoTransform.cpp
    Native/OSGBTools.cpp
    Native/Tileset.cpp
)

# 头文件
set(NATIVE_HEADERS
    Native/OSGB23dTiles.h
    Native/MeshProcessor.h
    Native/GeoTransform.h
    Native/OSGBTools.h
    Native/OSGBFix.h
    Native/Tileset.h
)

# 创建动态链接库
add_library(${PROJECT_NAME} SHARED ${NATIVE_SOURCES} ${NATIVE_HEADERS})

# 链接 fmt 和 spdlog 库
target_link_libraries(${PROJECT_NAME} PRIVATE fmt::fmt spdlog::spdlog)

# Windows 动态库需要导出符号以生成.lib导入库
# 这样SWIG包装库才能正确链接主库
set_target_properties(${PROJECT_NAME} PROPERTIES
    WINDOWS_EXPORT_ALL_SYMBOLS ON
)

# 包含目录
target_include_directories(${PROJECT_NAME}
    PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/Native
        ${CMAKE_CURRENT_SOURCE_DIR}/../3dParty/include
)

# Windows特定配置
if(WIN32)
    # 暂时禁用预编译头以避免条件编译宏冲突
    target_precompile_headers(${PROJECT_NAME} PRIVATE Native/OSGBFix.h)

    # 添加/bigobj选项以支持大型对象文件，启用OpenMP并行支持
    target_compile_options(${PROJECT_NAME} PRIVATE /bigobj /utf-8 /openmp)
endif()

# Linux/macOS 特定配置
if(UNIX)
    # 启用 OpenMP 并行支持
    find_package(OpenMP)
    if(OpenMP_CXX_FOUND)
        target_link_libraries(${PROJECT_NAME} PRIVATE OpenMP::OpenMP_CXX)
        message(STATUS "OpenMP enabled for parallel processing")
    else()
        message(WARNING "OpenMP not found - parallel processing will be disabled")
    endif()
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

        # 根据构建类型设置不同的OSG路径
        # 支持多配置生成器(Visual Studio)和单配置生成器(Makefile/Ninja)
        if(CMAKE_CONFIGURATION_TYPES)
            # 多配置生成器 - 添加debug和release子目录到搜索路径
            # CMake会根据配置类型自动选择正确的库
            set(OSG_DIR "${OSG_PREBUILT_DIR}" CACHE PATH "OSG installation directory")
            list(APPEND CMAKE_PREFIX_PATH
                "${OSG_PREBUILT_DIR}/debug"
                "${OSG_PREBUILT_DIR}/release"
                "${OSG_PREBUILT_DIR}"
            )
            message(STATUS "多配置模式 - 将根据构建类型自动选择Debug/Release库")
        else()
            # 单配置生成器 - 根据CMAKE_BUILD_TYPE选择路径
            if(CMAKE_BUILD_TYPE STREQUAL "Debug")
                set(OSG_DIR "${OSG_PREBUILT_DIR}/debug" CACHE PATH "OSG installation directory")
                set(CMAKE_PREFIX_PATH "${OSG_PREBUILT_DIR}/debug" ${CMAKE_PREFIX_PATH})
                message(STATUS "使用Debug版本OSG: ${OSG_PREBUILT_DIR}/debug")
            else()
                set(OSG_DIR "${OSG_PREBUILT_DIR}/release" CACHE PATH "OSG installation directory")
                set(CMAKE_PREFIX_PATH "${OSG_PREBUILT_DIR}/release" ${CMAKE_PREFIX_PATH})
                message(STATUS "使用Release版本OSG: ${OSG_PREBUILT_DIR}/release")
            endif()
        endif()
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
        # 输出目录
        set(OUTPUT_DIR "${CMAKE_SOURCE_DIR}/../../../bin")

        if(CMAKE_CONFIGURATION_TYPES)
            # 多配置生成器 - 为每个配置类型分别设置复制命令
            # 检测目录结构
            if(EXISTS "${OSG_PREBUILT_DIR}/debug/bin" OR EXISTS "${OSG_PREBUILT_DIR}/release/bin")
                # 结构1: osg/debug/bin, osg/release/bin
                set(OSG_DEBUG_BIN "${OSG_PREBUILT_DIR}/debug/bin")
                set(OSG_RELEASE_BIN "${OSG_PREBUILT_DIR}/release/bin")
                message(STATUS "OSG DLL复制: ${OSG_PREBUILT_DIR}/[debug|release]/bin")
            elseif(EXISTS "${OSG_PREBUILT_DIR}/bin/Debug" OR EXISTS "${OSG_PREBUILT_DIR}/bin/Release")
                # 结构2: osg/bin/Debug, osg/bin/Release
                set(OSG_DEBUG_BIN "${OSG_PREBUILT_DIR}/bin/Debug")
                set(OSG_RELEASE_BIN "${OSG_PREBUILT_DIR}/bin/Release")
                message(STATUS "OSG DLL复制: ${OSG_PREBUILT_DIR}/bin/[Debug|Release]")
            else()
                # 结构3: osg/bin (不区分配置)
                set(OSG_DEBUG_BIN "${OSG_PREBUILT_DIR}/bin")
                set(OSG_RELEASE_BIN "${OSG_PREBUILT_DIR}/bin")
                message(STATUS "OSG DLL复制: ${OSG_PREBUILT_DIR}/bin")
            endif()

            # Debug配置 - 仅在Debug构建时复制DLL和插件
            if(EXISTS "${OSG_DEBUG_BIN}")
                file(GLOB_RECURSE OSG_DEBUG_DLLS
                    "${OSG_DEBUG_BIN}/*.dll"
                    "${OSG_DEBUG_BIN}/*.pdb"
                )
                foreach(DLL_FILE ${OSG_DEBUG_DLLS})
                    file(RELATIVE_PATH REL_PATH "${OSG_DEBUG_BIN}" "${DLL_FILE}")
                    get_filename_component(REL_DIR "${REL_PATH}" DIRECTORY)
                    add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
                        COMMAND $<$<CONFIG:Debug>:${CMAKE_COMMAND}> $<$<CONFIG:Debug>:-E> $<$<CONFIG:Debug>:make_directory> $<$<CONFIG:Debug>:"${OUTPUT_DIR}/Debug/${REL_DIR}">
                        COMMAND $<$<CONFIG:Debug>:${CMAKE_COMMAND}> $<$<CONFIG:Debug>:-E> $<$<CONFIG:Debug>:copy_if_different>
                            $<$<CONFIG:Debug>:"${DLL_FILE}">
                            $<$<CONFIG:Debug>:"${OUTPUT_DIR}/Debug/${REL_PATH}">
                        COMMENT "Copying Debug: ${REL_PATH}"
                    )
                endforeach()
            endif()

            # Release配置 - 仅在Release构建时复制DLL和插件
            if(EXISTS "${OSG_RELEASE_BIN}")
                file(GLOB_RECURSE OSG_RELEASE_DLLS
                    "${OSG_RELEASE_BIN}/*.dll"
                    "${OSG_RELEASE_BIN}/*.pdb"
                )
                foreach(DLL_FILE ${OSG_RELEASE_DLLS})
                    file(RELATIVE_PATH REL_PATH "${OSG_RELEASE_BIN}" "${DLL_FILE}")
                    get_filename_component(REL_DIR "${REL_PATH}" DIRECTORY)
                    add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
                        COMMAND $<$<CONFIG:Release>:${CMAKE_COMMAND}> $<$<CONFIG:Release>:-E> $<$<CONFIG:Release>:make_directory> $<$<CONFIG:Release>:"${OUTPUT_DIR}/Release/${REL_DIR}">
                        COMMAND $<$<CONFIG:Release>:${CMAKE_COMMAND}> $<$<CONFIG:Release>:-E> $<$<CONFIG:Release>:copy_if_different>
                            $<$<CONFIG:Release>:"${DLL_FILE}">
                            $<$<CONFIG:Release>:"${OUTPUT_DIR}/Release/${REL_PATH}">
                        COMMENT "Copying Release: ${REL_PATH}"
                    )
                endforeach()
            endif()
        else()
            # 单配置生成器 - OSG_DIR已经指向正确的配置目录
            set(OSG_BIN_DIR "${OSG_DIR}/bin")
            if(EXISTS "${OSG_BIN_DIR}")
                message(STATUS "OSG DLL目录: ${OSG_BIN_DIR}")
                set(OUTPUT_DIR_CONFIG "${OUTPUT_DIR}/${CMAKE_BUILD_TYPE}")

                # 递归复制所有OSG DLL文件（包括插件目录中的）
                file(GLOB_RECURSE OSG_DLLS
                    "${OSG_BIN_DIR}/*.dll"
                    "${OSG_BIN_DIR}/*.pdb"
                )
                foreach(DLL_FILE ${OSG_DLLS})
                    file(RELATIVE_PATH REL_PATH "${OSG_BIN_DIR}" "${DLL_FILE}")
                    get_filename_component(REL_DIR "${REL_PATH}" DIRECTORY)
                    add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
                        COMMAND ${CMAKE_COMMAND} -E make_directory "${OUTPUT_DIR_CONFIG}/${REL_DIR}"
                        COMMAND ${CMAKE_COMMAND} -E copy_if_different
                            "${DLL_FILE}"
                            "${OUTPUT_DIR_CONFIG}/${REL_PATH}"
                        COMMENT "Copying ${CMAKE_BUILD_TYPE}: ${REL_PATH}"
                    )
                endforeach()
            else()
                message(WARNING "OSG bin目录不存在: ${OSG_BIN_DIR}")
            endif()
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

    # 拷贝 proj.db 数据文件到输出目录
    if(WIN32)
        # 获取 PROJ 安装路径并查找 proj.db
        get_target_property(PROJ_LOCATION PROJ::proj LOCATION)
        if(PROJ_LOCATION)
            get_filename_component(PROJ_BIN_DIR "${PROJ_LOCATION}" DIRECTORY)
            get_filename_component(PROJ_ROOT_DIR "${PROJ_BIN_DIR}" DIRECTORY)

            # vcpkg 安装：proj.db 在 x64-windows/share/proj（共享数据目录，不区分 debug/release）
            # 预编译版本：proj.db 在 <root>/share/proj
            # 尝试多个可能的路径
            set(PROJ_DATA_DIR "")
            set(POSSIBLE_PATHS
                "${PROJ_ROOT_DIR}/../share/proj"        # vcpkg: x64-windows/share/proj
                "${PROJ_ROOT_DIR}/share/proj"            # 预编译版本
                "${PROJ_ROOT_DIR}/../../share/proj"      # 其他可能的结构
            )

            foreach(POSSIBLE_PATH ${POSSIBLE_PATHS})
                get_filename_component(ABS_PATH "${POSSIBLE_PATH}" ABSOLUTE)
                if(EXISTS "${ABS_PATH}/proj.db")
                    set(PROJ_DATA_DIR "${ABS_PATH}")
                    break()
                endif()
            endforeach()

            if(PROJ_DATA_DIR)
                message(STATUS "Found proj.db: ${PROJ_DATA_DIR}/proj.db")

                set(OUTPUT_DIR "${CMAKE_SOURCE_DIR}/../../../bin")

                if(CMAKE_CONFIGURATION_TYPES)
                    # 多配置生成器 - 为 Debug 和 Release 分别复制
                    foreach(CONFIG_TYPE Debug Release)
                        add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
                            COMMAND $<$<CONFIG:${CONFIG_TYPE}>:${CMAKE_COMMAND}>
                                $<$<CONFIG:${CONFIG_TYPE}>:-E>
                                $<$<CONFIG:${CONFIG_TYPE}>:copy_if_different>
                                $<$<CONFIG:${CONFIG_TYPE}>:"${PROJ_DATA_DIR}/proj.db">
                                $<$<CONFIG:${CONFIG_TYPE}>:"${OUTPUT_DIR}/${CONFIG_TYPE}/proj.db">
                            COMMENT "Copying proj.db for ${CONFIG_TYPE}"
                        )
                    endforeach()
                else()
                    # 单配置生成器
                    add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
                        COMMAND ${CMAKE_COMMAND} -E copy_if_different
                            "${PROJ_DATA_DIR}/proj.db"
                            "${OUTPUT_DIR}/${CMAKE_BUILD_TYPE}/proj.db"
                        COMMENT "Copying proj.db for ${CMAKE_BUILD_TYPE}"
                    )
                endif()
            else()
                message(WARNING "proj.db not found. Searched paths:")
                foreach(POSSIBLE_PATH ${POSSIBLE_PATHS})
                    get_filename_component(ABS_PATH "${POSSIBLE_PATH}" ABSOLUTE)
                    message(WARNING "  - ${ABS_PATH}/proj.db")
                endforeach()
            endif()
        endif()
    endif()
else()
    message(STATUS "PROJ not found - coordinate transformations disabled")
endif()

# ============================================================================
# 测试可执行文件
# ============================================================================

# 添加测试可执行文件
add_executable(OSGB23dTilesTest
    Test/OSGB23dTilesTest.cpp
)

# 添加包含目录
target_include_directories(OSGB23dTilesTest PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_CURRENT_SOURCE_DIR}/../3dParty/include
    ${OSG_INCLUDE_DIR}
)

# 链接主库
target_link_libraries(OSGB23dTilesTest PRIVATE ${PROJECT_NAME})

# Windows特定配置
if(WIN32)
    # 添加编译选项：UTF-8编码
    target_compile_options(OSGB23dTilesTest PRIVATE /utf-8)
endif()

# 设置输出目录
if(CMAKE_CONFIGURATION_TYPES)
    # 多配置生成器
    foreach(CONFIG_TYPE ${CMAKE_CONFIGURATION_TYPES})
        string(TOUPPER ${CONFIG_TYPE} CONFIG_TYPE_UPPER)
        set_target_properties(OSGB23dTilesTest PROPERTIES
            RUNTIME_OUTPUT_DIRECTORY_${CONFIG_TYPE_UPPER} ${CMAKE_SOURCE_DIR}/../../../bin/${CONFIG_TYPE}
        )
    endforeach()
else()
    # 单配置生成器
    set_target_properties(OSGB23dTilesTest PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/../../../bin/${CMAKE_BUILD_TYPE}
    )
endif()

# ============================================================================
# 安装配置
# ============================================================================

install(TARGETS ${PROJECT_NAME}
    RUNTIME DESTINATION bin
    LIBRARY DESTINATION lib
    ARCHIVE DESTINATION lib
)

# 安装公共头文件（用于 C++ API）
install(FILES Native/OSGB23dTiles.h DESTINATION include)

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
message(STATUS "    USE_EXTERNAL_OSG:         ${USE_EXTERNAL_OSG}")
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
message(STATUS "    SWIG:                ${SWIG_FOUND}")
message(STATUS "========================================")
message(STATUS "")

# ============================================================================
# SWIG 绑定 - C# 包装代码生成（可选）
# ============================================================================

# 查找 SWIG（可选，不阻塞核心库构建）
find_package(SWIG 4.0)

if(SWIG_FOUND)
    message(STATUS "Found SWIG: ${SWIG_EXECUTABLE}")
    message(STATUS "SWIG Version: ${SWIG_VERSION}")

    include(${SWIG_USE_FILE})

    # 设置 SWIG 输出目录
    set(SWIG_OUTPUT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/Interop/Swig")
    file(MAKE_DIRECTORY ${SWIG_OUTPUT_DIR})

    # SWIG C# 模块配置
    set_property(SOURCE Interop/OSGB23dTiles.i PROPERTY CPLUSPLUS ON)
    set_property(SOURCE Interop/OSGB23dTiles.i PROPERTY SWIG_MODULE_NAME OSGB23dTilesCS)

    # SWIG 编译选项
    set(SWIG_FLAGS_LIST
        "-namespace" "RealScene3D.Lib.OSGB.Interop"
        "-doxygen"  # 支持 Doxygen 注释
        "-c++"      # C++ 模式
    )
    set_property(SOURCE Interop/OSGB23dTiles.i PROPERTY SWIG_FLAGS ${SWIG_FLAGS_LIST})

    # 添加 SWIG 包含目录
    set_property(SOURCE Interop/OSGB23dTiles.i PROPERTY USE_SWIG_DEPENDENCIES TRUE)
    set(CMAKE_SWIG_FLAGS "-I${CMAKE_CURRENT_SOURCE_DIR}/Native")

    # 创建 SWIG 包装库
    swig_add_library(OSGB23dTilesCS
        TYPE SHARED
        LANGUAGE csharp
        SOURCES Interop/OSGB23dTiles.i
        OUTPUT_DIR ${SWIG_OUTPUT_DIR}
    )

    # 设置 SWIG 生成的 C# 文件输出目录
    set_target_properties(OSGB23dTilesCS PROPERTIES
        SWIG_COMPILE_OPTIONS "${SWIG_FLAGS_LIST}"
        SWIG_SUPPORT_FILES_DIRECTORY "${SWIG_OUTPUT_DIR}"
        SWIG_USE_TARGET_INCLUDE_DIRECTORIES TRUE
    )

    # 设置 C# 特定属性
    set_property(TARGET OSGB23dTilesCS PROPERTY SWIG_COMPILE_DEFINITIONS OSGB_EXPORTS)

    # C# DLL 导入配置（P/Invoke 使用）
    if(CMAKE_CONFIGURATION_TYPES)
        # 多配置生成器（Visual Studio）
        set_target_properties(OSGB23dTilesCS PROPERTIES
            SWIG_COMPILE_OPTIONS "-outdir;${SWIG_OUTPUT_DIR};-dllimport;RealScene3D.Lib.OSGB"
        )
    else()
        # 单配置生成器
        set_target_properties(OSGB23dTilesCS PROPERTIES
            SWIG_COMPILE_OPTIONS "-outdir;${SWIG_OUTPUT_DIR};-dllimport;RealScene3D.Lib.OSGB"
        )
    endif()

    # 链接依赖（链接到主库）
    target_link_libraries(OSGB23dTilesCS PRIVATE ${PROJECT_NAME})

    # 链接主库的所有可选依赖库，确保包含目录正确传递
    # 这样CMake会自动添加这些库的包含目录到OSGB23dTilesCS的编译参数

    # PROJ (地理坐标系转换)
    if(PROJ_FOUND)
        target_link_libraries(OSGB23dTilesCS PRIVATE PROJ::proj)
        target_compile_definitions(OSGB23dTilesCS PRIVATE ENABLE_PROJ)
    endif()

    # Basis Universal (KTX2纹理压缩)
    if(basisu_FOUND)
        target_link_libraries(OSGB23dTilesCS PRIVATE basisu::basisu_encoder)
        target_compile_definitions(OSGB23dTilesCS PRIVATE ENABLE_KTX2)
    endif()

    # meshoptimizer (网格优化)
    if(meshoptimizer_FOUND)
        target_link_libraries(OSGB23dTilesCS PRIVATE meshoptimizer::meshoptimizer)
        target_compile_definitions(OSGB23dTilesCS PRIVATE OPTIMIZER)
    endif()

    # Draco (网格压缩)
    if(draco_FOUND)
        target_link_libraries(OSGB23dTilesCS PRIVATE draco::draco)
        target_compile_definitions(OSGB23dTilesCS PRIVATE ENABLE_DRACO)
    endif()

    # 包含主库的头文件目录
    # 添加 CMAKE_CURRENT_SOURCE_DIR 让 SWIG 生成的代码能找到 Native/OSGB23dTiles.h
    # 添加 3dParty/include 让代码能找到 glm/glm.hpp 等非vcpkg管理的第三方库
    target_include_directories(OSGB23dTilesCS PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}
        ${CMAKE_CURRENT_SOURCE_DIR}/Native
        ${CMAKE_CURRENT_SOURCE_DIR}/../3dParty/include
        ${OSG_INCLUDE_DIR}
    )

    # 设置输出目录（与主库保持一致）
    if(CMAKE_CONFIGURATION_TYPES)
        # 多配置生成器
        foreach(CONFIG_TYPE ${CMAKE_CONFIGURATION_TYPES})
            string(TOUPPER ${CONFIG_TYPE} CONFIG_TYPE_UPPER)
            set_target_properties(OSGB23dTilesCS PROPERTIES
                LIBRARY_OUTPUT_DIRECTORY_${CONFIG_TYPE_UPPER} ${CMAKE_SOURCE_DIR}/../../../bin/${CONFIG_TYPE}
                RUNTIME_OUTPUT_DIRECTORY_${CONFIG_TYPE_UPPER} ${CMAKE_SOURCE_DIR}/../../../bin/${CONFIG_TYPE}
                ARCHIVE_OUTPUT_DIRECTORY_${CONFIG_TYPE_UPPER} ${CMAKE_SOURCE_DIR}/../../../bin/${CONFIG_TYPE}
            )
        endforeach()
    else()
        # 单配置生成器
        set_target_properties(OSGB23dTilesCS PROPERTIES
            LIBRARY_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/../../../bin/${CMAKE_BUILD_TYPE}
            RUNTIME_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/../../../bin/${CMAKE_BUILD_TYPE}
            ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_SOURCE_DIR}/../../../bin/${CMAKE_BUILD_TYPE}
        )
    endif()

    # Windows 特定配置
    if(WIN32)
        set_target_properties(OSGB23dTilesCS PROPERTIES
            PREFIX ""
            SUFFIX ".dll"
            OUTPUT_NAME "OSGB23dTilesCS"
        )

        # 添加编译选项
        target_compile_options(OSGB23dTilesCS PRIVATE /bigobj /utf-8)
    endif()

    # 安装规则
    install(TARGETS OSGB23dTilesCS
        RUNTIME DESTINATION bin
        LIBRARY DESTINATION lib
        ARCHIVE DESTINATION lib
    )

    # 安装生成的 C# 文件
    install(DIRECTORY ${SWIG_OUTPUT_DIR}/
        DESTINATION include/OSGB/Swig
        FILES_MATCHING PATTERN "*.cs"
    )

    message(STATUS "========================================")
    message(STATUS "  SWIG C# Bindings Configuration")
    message(STATUS "========================================")
    message(STATUS "  Output Directory:      ${SWIG_OUTPUT_DIR}")
    message(STATUS "  Namespace:             RealScene3D.Lib.OSGB.Interop")
    message(STATUS "  DLL Import Name:       RealScene3D.Lib.OSGB")
    message(STATUS "  SWIG Flags:            ${SWIG_FLAGS_LIST}")
    message(STATUS "========================================")
else()
    message(WARNING "========================================")
    message(WARNING "  SWIG not found - C# bindings will NOT be generated")
    message(WARNING "  Install SWIG from: https://www.swig.org/download.html")
    message(WARNING "  Or use Chocolatey: choco install swig")
    message(WARNING "  Core library will still be built.")
    message(WARNING "========================================")
endif()
