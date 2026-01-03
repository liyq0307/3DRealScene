#ifndef LOD_PIPELINE_H
#define LOD_PIPELINE_H

#include <vector>
#include <cstddef>
#include "MeshProcessor.h"

/**
 * @brief 单个LOD级别的配置
 *
 * 每个LOD级别对应一个不同精度的GLB输出文件
 */
struct LODLevelSettings {
    float target_ratio = 1.0f;              // 目标简化比例：1.0 = 完整精度；0.5 = 保留50%三角面
    float target_error = 0.01f;             // 简化误差预算（匹配 SimplificationParams）
    bool enable_simplification = false;     // 是否对此LOD级别启用网格简化
    bool enable_draco = false;              // 是否对此LOD级别应用Draco压缩

    SimplificationParams simplify;          // 基础简化参数（ratio/error会被上面的值覆盖）
    DracoCompressionParams draco;           // 基础Draco压缩参数
};

/**
 * @brief LOD流水线配置
 *
 * 管理整个LOD层级生成过程
 */
struct LODPipelineSettings {
    bool enable_lod = false;                // 主开关：false时只生成LOD0
    std::vector<LODLevelSettings> levels;   // LOD级别列表（从粗到细或从细到粗，取决于使用场景）
};

/**
 * @brief 从简化比例数组构建LOD级别配置
 *
 * 使用提供的比例数组和模板参数自动生成多个LOD级别配置。
 *
 * @param ratios 简化比例数组，按顺序应用到各级别（如 {1.0f, 0.7f, 0.5f, 0.3f}）
 * @param base_error 所有级别使用的基础简化误差
 * @param simplify_template 网格简化模板参数
 * @param draco_template Draco压缩模板参数
 * @param draco_for_lod0 是否对LOD0应用Draco压缩（默认false，LOD0通常保持未压缩以便快速加载）
 * @return 配置好的LOD级别数组
 *
 * @example
 * // 创建4级LOD，精度递减
 * SimplificationParams simplify = {.enable_simplification = true, .target_error = 0.01f};
 * DracoCompressionParams draco = {.enable_compression = true};
 * auto levels = build_lod_levels({1.0f, 0.7f, 0.5f, 0.3f}, 0.01f, simplify, draco, false);
 * // levels[0]: 100%精度，无Draco
 * // levels[1]: 70%精度，有Draco
 * // levels[2]: 50%精度，有Draco
 * // levels[3]: 30%精度，有Draco
 */
std::vector<LODLevelSettings> build_lod_levels(
    const std::vector<float>& ratios,
    float base_error,
    const SimplificationParams& simplify_template,
    const DracoCompressionParams& draco_template,
    bool draco_for_lod0 = false)
{
    std::vector<LODLevelSettings> levels;
    levels.reserve(ratios.size());

    for (size_t i = 0; i < ratios.size(); ++i) {
        LODLevelSettings lvl;
        lvl.target_ratio = ratios[i];
        lvl.target_error = base_error;

        // 使用用户的 enable_simplification 设置而不是强制为 true
        // 这允许用户控制是否对特定级别启用简化
        lvl.enable_simplification = simplify_template.enable_simplification;
        lvl.simplify = simplify_template;
        lvl.simplify.target_ratio = ratios[i];
        lvl.simplify.target_error = base_error;

        // 应用Draco压缩设置
        lvl.enable_draco = draco_template.enable_compression;

        // 特殊处理：LOD0 通常保持未压缩以便快速加载
        if (i == 0 && !draco_for_lod0) {
            lvl.enable_draco = false;
        }
        lvl.draco = draco_template;

        levels.push_back(lvl);
    }

    return levels;
}

#endif // LOD_PIPELINE_H
