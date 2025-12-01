using RealScene3D.Application.Services.MeshDecimator.Algorithms;

namespace RealScene3D.Application.Services.MeshDecimator;

/// <summary>
/// 网格简化算法。
/// </summary>
public enum Algorithm
{
    /// <summary>
    /// 默认算法。
    /// </summary>
    Default,
    /// <summary>
    /// 快速四元网格简化算法。
    /// </summary>
    FastQuadricMesh
}

/// <summary>
/// 网格简化 API。
/// </summary>
public static class MeshDecimation
{
    /// <summary>
    /// 创建特定的简化算法。
    /// </summary>
    /// <param name="algorithm">期望的算法。</param>
    /// <returns>简化算法。</returns>
    public static DecimationAlgorithm CreateAlgorithm(Algorithm algorithm)
    {
        DecimationAlgorithm alg;

        switch (algorithm)
        {
            case Algorithm.Default:
            case Algorithm.FastQuadricMesh:
                alg = new FastQuadricMeshSimplification();
                break;
            default:
                throw new ArgumentException("The specified algorithm is not supported.", "algorithm");
        }

        return alg;
    }

    /// <summary>
    /// 简化网格。
    /// </summary>
    /// <param name="mesh">要简化的网格。</param>
    /// <param name="targetTriangleCount">目标三角形数量。</param>
    /// <returns>简化的网格。</returns>
    public static SimpleMesh DecimateMesh(SimpleMesh mesh, int targetTriangleCount)
    {
        return DecimateMesh(Algorithm.Default, mesh, targetTriangleCount);
    }

    /// <summary>
    /// 简化网格。
    /// </summary>
    /// <param name="algorithm">期望的算法。</param>
    /// <param name="mesh">要简化的网格。</param>
    /// <param name="targetTriangleCount">目标三角形数量。</param>
    /// <returns>简化的网格。</returns>
    public static SimpleMesh DecimateMesh(Algorithm algorithm, SimpleMesh mesh, int targetTriangleCount)
    {
        if (mesh == null)
            throw new ArgumentNullException("mesh");

        var decimationAlgorithm = CreateAlgorithm(algorithm);
        return DecimateMesh(decimationAlgorithm, mesh, targetTriangleCount);
    }

    /// <summary>
    /// 简化网格。
    /// </summary>
    /// <param name="algorithm">简化算法。</param>
    /// <param name="mesh">要简化的网格。</param>
    /// <param name="targetTriangleCount">目标三角形数量。</param>
    /// <returns>简化的网格。</returns>
    public static SimpleMesh DecimateMesh(DecimationAlgorithm algorithm, SimpleMesh mesh, int targetTriangleCount)
    {
        if (algorithm == null)
            throw new ArgumentNullException("algorithm");
        else if (mesh == null)
            throw new ArgumentNullException("mesh");

        int currentTriangleCount = mesh.TriangleCount;
        if (targetTriangleCount > currentTriangleCount)
            targetTriangleCount = currentTriangleCount;
        else if (targetTriangleCount < 0)
            targetTriangleCount = 0;

        algorithm.Initialize(mesh);
        algorithm.DecimateMesh(targetTriangleCount);
        return algorithm.ToMesh();
    }

    /// <summary>
    /// 无损简化网格。
    /// </summary>
    /// <param name="mesh">要简化的网格。</param>
    /// <returns>简化的网格。</returns>
    public static SimpleMesh DecimateMeshLossless(SimpleMesh mesh)
    {
        return DecimateMeshLossless(Algorithm.Default, mesh);
    }

    /// <summary>
    /// 无损简化网格。
    /// </summary>
    /// <param name="algorithm">期望的算法。</param>
    /// <param name="mesh">要简化的网格。</param>
    /// <returns>简化的网格。</returns>
    public static SimpleMesh DecimateMeshLossless(Algorithm algorithm, SimpleMesh mesh)
    {
        if (mesh == null)
            throw new ArgumentNullException("mesh");

        var decimationAlgorithm = CreateAlgorithm(algorithm);
        return DecimateMeshLossless(decimationAlgorithm, mesh);
    }

    /// <summary>
    /// 无损简化网格。
    /// </summary>
    /// <param name="algorithm">简化算法。</param>
    /// <param name="mesh">要简化的网格。</param>
    /// <returns>简化的网格。</returns>
    public static SimpleMesh DecimateMeshLossless(DecimationAlgorithm algorithm, SimpleMesh mesh)
    {
        if (algorithm == null)
            throw new ArgumentNullException("algorithm");
        else if (mesh == null)
            throw new ArgumentNullException("mesh");

        int currentTriangleCount = mesh.TriangleCount;
        algorithm.Initialize(mesh);
        algorithm.DecimateMeshLossless();
        return algorithm.ToMesh();
    }
}