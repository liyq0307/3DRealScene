// ============================================================================
// OsgbReader C++ ���Գ���
// ���� OSGB �� GLB �� 3D Tiles ��ת������
// ============================================================================

#include "Native/OsgbReader.h"
#include <iostream>
#include <fstream>
#include <chrono>
#include <filesystem>

#ifdef _WIN32
#include <direct.h>
#else
#include <sys/stat.h>
#endif

int main(int argc, char* argv[])
{
    std::cout << "========================================" << std::endl;
    std::cout << "OsgbReader C++ ���Գ���" << std::endl;
    std::cout << "========================================" << std::endl;

    // ��������������
    std::string strInputPath = "E:/Data/3D/g_tsg_osgb";
    std::string strOutputDir = "E:/Data/3D/output_osgb_batch";

    std::cout << "����Ŀ¼: " << strInputPath.c_str() << std::endl;
    std::cout << "���Ŀ¼: " << strOutputDir.c_str() << std::endl;
    std::cout << "========================================" << std::endl;

    // �������Ŀ¼
    std::filesystem::create_directories(strOutputDir);

    // ���� OsgbReader ʵ��
    std::cout << "[INFO] ���� OsgbReader ʵ��..." << std::endl;
    OsgbReader reader;

    // ����������: OSGB ����ת��Ϊ 3D Tiles
    std::cout << "OSGB ����ת��Ϊ 3D Tiles" << std::endl;
    std::cout << "----------------------------------------" << std::endl;

    double bboxData[6] = {0};
    int bboxLen = 0;

    auto start = std::chrono::high_resolution_clock::now();

    void* tilesetResult = reader.Osgb23dTileBatch(
        strInputPath.c_str(),
        strOutputDir.c_str(),
        bboxData,
        &bboxLen,
        120.34445,  // center_x (经度)
        36.09953,   // center_y (纬度)
        100,        // max_lvl (足够大以包含所有LOD层级)
        false,      // enable_texture_compress
        false,      // enable_meshopt
        false       // enable_draco
    );

    auto end = std::chrono::high_resolution_clock::now();
    auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(end - start);

    if (tilesetResult != nullptr)
    {
        std::cout << "[INFO] OSGB ����ת�� 3D Tiles �ɹ�!" << std::endl;
        std::cout << "  ��ʱ: " << duration.count() << " ms" << std::endl;

        // ��ȡ tileset JSON
        std::string tilesetJson((char*)tilesetResult, bboxLen);
        std::cout << "  Root Tileset JSON ��С: " << tilesetJson.size() << " �ֽ�" << std::endl;

        // ��ʾ bbox
        if (bboxLen > 0)
        {
            std::cout << "  Merged BBox: [" << bboxData[0] << ", " << bboxData[1] << ", " << bboxData[2] << "] - ["
                      << bboxData[3] << ", " << bboxData[4] << ", " << bboxData[5] << "]" << std::endl;
        }

        // ��ʾ tileset JSON ǰ 500 ���ַ�
        if (tilesetJson.size() > 0)
        {
            std::cout << "\n  Root Tileset JSON (ǰ 500 �ַ�):\n";
            std::cout << "  " << tilesetJson.substr(0, std::min(size_t(500), tilesetJson.size())) << "...\n";
        }

        // �ͷ��ڴ�
        free(tilesetResult);
    }
    else
    {
        std::cerr << "[ERROR] OSGB ����ת�� 3D Tiles ʧ��!" << std::endl;
    }

    std::cout << "\n========================================" << std::endl;
    std::cout << "�������!" << std::endl;
    std::cout << "========================================" << std::endl;

    return 0;
}
