// ============================================================================
// OsgbReader C++ 测试程序
// 用于 OSGB 到 GLB 和 3D Tiles 转换功能
// ============================================================================

#include "Native/OSGB23dTiles.h"
#include <iostream>
#include <fstream>
#include <chrono>
#include <filesystem>

#ifdef _WIN32
#include <direct.h>
#include <windows.h>
#else
#include <sys/stat.h>
#endif

int main(int argc, char* argv[])
{
#ifdef _WIN32
    // 设置控制台代码页为 UTF-8
    SetConsoleOutputCP(CP_UTF8);
#endif

    std::cout << "========================================" << std::endl;
    std::cout << "OSGB23dTiles C++ 测试程序" << std::endl;
    std::cout << "========================================" << std::endl;

    // 设置输入输出路径
    std::string strInputPath = "E:/Data/3D/g_tsg_osgb";
    std::string strOutputDir = "E:/Data/3D/output_osgb_batch";

    std::cout << "输入目录: " << strInputPath.c_str() << std::endl;
    std::cout << "输出目录: " << strOutputDir.c_str() << std::endl;
    std::cout << "========================================" << std::endl;

    // 创建输出目录
    std::filesystem::create_directories(strOutputDir);

    // 初始化 OSGB23dTiles 实例
    std::cout << "[INFO] 初始化 OsgbReader 实例..." << std::endl;
    OSGB23dTiles reader;

    // 开始转换: OSGB 文件转换为 3D Tiles
    std::cout << "OSGB 文件转换为 3D Tiles" << std::endl;
    std::cout << "----------------------------------------" << std::endl;

    double bboxData[6] = {0};
    int bboxLen = 0;

    auto start = std::chrono::high_resolution_clock::now();

    std::string strTilesetJson = reader.To3dTileBatch(
        strInputPath,
        strOutputDir,
        bboxData,
        &bboxLen,
        0.0,        // center_x (将从 metadata.xml 自动读取)
        0.0,        // center_y (将从 metadata.xml 自动读取)
        100,        // max_lvl (足够大以包含所有LOD层级)
        false,      // enable_texture_compress
        false,      // enable_meshopt
        false       // enable_draco
    );

    auto end = std::chrono::high_resolution_clock::now();
    auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(end - start);

    if (!strTilesetJson.empty())
    {
        std::cout << "[INFO] OSGB 转换为 3D Tiles 成功!" << std::endl;
        std::cout << "  耗时: " << duration.count() << " ms" << std::endl;

        // 获取 tileset JSON
        std::cout << "  Root Tileset JSON 大小: " << strTilesetJson.size() << " 字节" << std::endl;

        // 显示 bbox
        if (bboxLen > 0)
        {
            std::cout << "  Merged BBox: [" << bboxData[0] << ", " << bboxData[1] << ", " << bboxData[2] << "] - ["
                      << bboxData[3] << ", " << bboxData[4] << ", " << bboxData[5] << "]" << std::endl;
        }
    }
    else
    {
        std::cerr << "[ERROR] OSGB 转换为 3D Tiles 失败!" << std::endl;
    }

    std::cout << "\n========================================" << std::endl;
    std::cout << "程序结束!" << std::endl;
    std::cout << "========================================" << std::endl;

    return 0;
}
