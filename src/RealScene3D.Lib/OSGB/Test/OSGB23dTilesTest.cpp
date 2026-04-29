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

void test_local_filesystem()
{
    std::cout << "\n========================================" << std::endl;
    std::cout << "测试1: 本地文件系统存储" << std::endl;
    std::cout << "========================================" << std::endl;

    std::string strInputPath = "E:/Data/3D/Production_3";
    std::string strOutputDir = "E:/Data/3D/output_osgb_batch";

    std::cout << "输入目录: " << strInputPath << std::endl;
    std::cout << "输出目录: " << strOutputDir << std::endl;

    std::filesystem::create_directories(strOutputDir);

    OSGB23dTiles reader;

    auto start = std::chrono::high_resolution_clock::now();

    bool success = reader.ToB3DMBatch(
        strInputPath,
        strOutputDir,
        0.0, 0.0, -1,
        false, false, false
    );

    auto end = std::chrono::high_resolution_clock::now();
    auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(end - start);

    if (success)
    {
        std::cout << "[SUCCESS] 本地文件系统存储成功" << std::endl;
        std::cout << "  耗时: " << duration.count() << " ms" << std::endl;
    }
    else
    {
        std::cerr << "[FAILED] 本地文件系统存储失败" << std::endl;
    }
}

#ifdef ENABLE_MINIO
void test_minio_storage()
{
    std::cout << "\n========================================" << std::endl;
    std::cout << "测试2: MinIO 对象存储" << std::endl;
    std::cout << "========================================" << std::endl;

    std::string strInputPath = "E:/Data/3D/Production_3";
    std::string strMinioPath = "slices/Production_3_test";
    std::string strMinioEndpoint = "localhost:9000";
    std::string strAccessKey = "minioadmin";
    std::string strSecretKey = "minioadmin";
    bool bUseSSL = false;

    std::cout << "输入目录: " << strInputPath << std::endl;
    std::cout << "MinIO路径: " << strMinioPath << std::endl;
    std::cout << "MinIO端点: " << strMinioEndpoint << std::endl;

    // 验证路径格式
    size_t slash_pos = strMinioPath.find('/');
    if (slash_pos == std::string::npos)
    {
        std::cerr << "[ERROR] MinIO路径格式错误，应为: bucket/path" << std::endl;
        return;
    }

    std::string bucket = strMinioPath.substr(0, slash_pos);
    std::string prefix = strMinioPath.substr(slash_pos + 1);

    std::cout << "  解析结果: bucket='" << bucket << "', prefix='" << prefix << "'" << std::endl;

    OSGB23dTiles reader;

    auto start = std::chrono::high_resolution_clock::now();

    bool success = reader.ToB3DMBatchToMinIO(
        strInputPath,
        strMinioPath,
        strMinioEndpoint,
        strAccessKey,
        strSecretKey,
        bUseSSL,
        0.0, 0.0, -1,
        false, false, false
    );

    auto end = std::chrono::high_resolution_clock::now();
    auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(end - start);

    if (success)
    {
        std::cout << "[SUCCESS] MinIO存储成功" << std::endl;
        std::cout << "  耗时: " << duration.count() << " ms" << std::endl;
        std::cout << "  对象前缀: " << prefix << std::endl;
    }
    else
    {
        std::cerr << "[FAILED] MinIO存储失败" << std::endl;
    }
}
#endif

int main(int argc, char* argv[])
{
#ifdef _WIN32
    SetConsoleOutputCP(CP_UTF8);
#endif

    std::cout << "========================================" << std::endl;
    std::cout << "OSGB23dTiles C++ 测试程序" << std::endl;
    std::cout << "========================================" << std::endl;

    std::cout << "请输入要测试的功能编号:" << std::endl;
    std::cout << " 1: 本地文件系统存储" << std::endl; 
    std::cout << " 2: MinIO 对象存储" << std::endl;
    std::cout << " 其他: 退出程序" << std::endl;

    int nChoice = 0;
    std::cin >> nChoice;

    if (nChoice != 1 && nChoice != 2)
    {
        std::cout << "退出程序" << std::endl;
        return 0;
    }

    if (nChoice == 1)
    {
        test_local_filesystem();
    }
    else if (nChoice == 2)
    {
#ifdef ENABLE_MINIO
        test_minio_storage();
#else
        std::cout << "\n[INFO] ENABLE_MINIO 未定义，跳过 MinIO 测试" << std::endl;
        std::cout << "  编译时添加 -DENABLE_MINIO 启用 MinIO 测试" << std::endl;
#endif      
    }

    std::cout << "\n========================================" << std::endl;
    std::cout << "所有测试完成" << std::endl;
    std::cout << "========================================" << std::endl;

    return 0;
}
