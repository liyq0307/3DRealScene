using RealScene3D.Managed;

namespace RealScene3D.Examples;

/// <summary>
/// OSGB 原生纹理提取示例
/// </summary>
public class OsgbTextureExtractionExample
{
    public static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("用法: OsgbTextureExtractionExample <osgb文件路径> <输出目录>");
            Console.WriteLine("示例: OsgbTextureExtractionExample E:\\Data\\3D\\model.osgb E:\\Output");
            return;
        }

        var inputPath = args[0];
        var outputDirectory = args[1];

        Console.WriteLine("========================================");
        Console.WriteLine("OSGB 原生纹理提取示例");
        Console.WriteLine("========================================");
        Console.WriteLine();

        try
        {
            ExtractTextures(inputPath, outputDirectory);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[错误] {ex.Message}");
            Console.ResetColor();
            Environment.Exit(1);
        }
    }

    static void ExtractTextures(string osgbPath, string outputDir)
    {
        // 检查输入文件
        if (!File.Exists(osgbPath))
        {
            throw new FileNotFoundException($"OSGB 文件不存在: {osgbPath}");
        }

        Console.WriteLine($"[信息] 输入文件: {osgbPath}");
        Console.WriteLine($"[信息] 文件大小: {new FileInfo(osgbPath).Length / 1024.0 / 1024.0:F2} MB");
        Console.WriteLine();

        // 创建输出目录
        Directory.CreateDirectory(outputDir);
        Console.WriteLine($"[信息] 输出目录: {outputDir}");
        Console.WriteLine();

        // 创建 OSGB 读取器
        Console.WriteLine("[1/3] 创建 OSGB 读取器...");
        using var reader = new OsgbReaderWrapper();
        Console.WriteLine("[✓] 完成");
        Console.WriteLine();

        // 加载 OSGB 文件
        Console.WriteLine("[2/3] 加载 OSGB 文件...");
        if (!reader.LoadFile(osgbPath))
        {
            var error = reader.GetLastError();
            throw new InvalidOperationException($"加载失败: {error}");
        }
        Console.WriteLine("[✓] 完成");
        Console.WriteLine();

        // 提取纹理
        Console.WriteLine("[3/3] 提取纹理...");
        var textures = reader.ExtractTextures();
        Console.WriteLine($"[✓] 找到 {textures.Count} 个纹理");
        Console.WriteLine();

        if (textures.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[警告] 未找到任何纹理");
            Console.ResetColor();
            return;
        }

        // 保存每个纹理
        Console.WriteLine("开始保存纹理:");
        for (int i = 0; i < textures.Count; i++)
        {
            var texture = textures[i];
            var outputPath = Path.Combine(outputDir, $"texture_{i}.jpg");

            Console.Write($"  [{i + 1}/{textures.Count}] {Path.GetFileName(outputPath)} ");
            Console.Write($"({texture.Width}x{texture.Height}, ");
            Console.Write($"{texture.ImageData.Length / 1024.0:F1} KB) ... ");

            if (reader.SaveTexture(texture, outputPath))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗");
                Console.ResetColor();
            }
        }

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"提取完成！共 {textures.Count} 个纹理");
        Console.ResetColor();
        Console.WriteLine("========================================");
    }
}
