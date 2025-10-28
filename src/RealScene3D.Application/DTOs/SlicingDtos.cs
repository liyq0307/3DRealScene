using RealScene3D.Domain.Entities;
using RealScene3D.Domain.Interfaces;

namespace RealScene3D.Application.DTOs;

/// <summary>
/// 3D模型切片数据传输对象集合
/// 包含切片任务、切片进度、切片数据等相关DTO
/// 用于处理3D模型切片业务的数据传输
/// </summary>
public class SlicingDtos
{
    /// <summary>
    /// 创建切片任务请求DTO
    /// 用于接收客户端创建3D模型切片任务的请求数据
    /// </summary>
    public class CreateSlicingTaskRequest
    {
        /// <summary>
        /// 切片任务名称，必填项
        /// 用于标识和区分不同的切片任务
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// </summary>
        public string SourceModelPath { get; set; } = string.Empty;

        /// <summary>
        /// 关联的场景对象ID，可选项
        /// </summary>
        public Guid? SceneObjectId { get; set;}


        /// <summary>
        /// 模型类型，必填项
        /// 用于指定切片算法和处理方式，如：BIM、倾斜摄影、激光点云等
        /// </summary>
        public string ModelType { get; set; } = string.Empty;

            /// <summary>
            /// 切片配置参数，必填项
            /// 包含切片粒度、输出格式、坐标系等配置信息
            /// </summary>
            public SlicingConfig SlicingConfig { get; set; } = new();
        }
        
        /// <summary>
        /// 切片配置参数DTO
        /// 用于定义切片任务的详细配置，如切片粒度、输出格式、坐标系等
        /// </summary>
        public class SlicingConfig
        {
            /// <summary>
            /// 切片粒度，例如："High", "Medium", "Low"
            /// </summary>
            public string Granularity { get; set; } = "Medium";
        
            /// <summary>
            /// 输出格式，例如："3D Tiles", "Cesium3DTiles", "GLTF"
            /// </summary>
            public string OutputFormat { get; set; } = "3D Tiles";
        
            /// <summary>
            /// 坐标系，例如："EPSG:4326", "EPSG:3857"
            /// </summary>
            public string CoordinateSystem { get; set; } = "EPSG:4326";
        
            /// <summary>
            /// 其他自定义配置，JSON字符串
            /// </summary>
            public string CustomSettings { get; set; } = "{}";
        /// <summary>
        /// 切片输出目录路径，可选项
        /// 指定切片结果文件的存储目录路径，如果未提供则自动生成
        /// </summary>
        public string? OutputPath { get; set; }
    }

    /// <summary>
    /// 切片任务响应DTO
    /// 用于向前端返回切片任务的完整信息和状态
    /// </summary>
    public class SlicingTaskDto
    {
        /// <summary>
        /// 切片任务唯一标识符
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 切片任务名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 源3D模型文件路径
        /// </summary>
        public string SourceModelPath { get; set; } = string.Empty;

        /// </summary>
        public string ModelType { get; set; } = string.Empty;

        /// <summary>
        /// 关联的场景对象ID，可选项
        /// </summary>
        public Guid? SceneObjectId { get; set;}

        /// <summary>
        /// 切片配置参数
        /// </summary>
        public SlicingConfig SlicingConfig { get; set; } = new();

        /// <summary>
        /// 切片任务当前状态
        /// 如：Pending、Processing、Completed、Failed等
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// 切片进度百分比，0-100
        /// </summary>
        public int Progress { get; set; }

        /// <summary>
        /// 切片输出目录路径，可选项
        /// 任务完成后切片文件的存储位置
        /// </summary>
        public string? OutputPath { get; set; }

        /// <summary>
        /// 错误信息，可选项
        /// 当任务失败时显示的错误详情
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 任务创建者用户ID
        /// </summary>
        public Guid CreatedBy { get; set; }

        /// <summary>
        /// 任务创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 任务开始处理时间，可选项
        /// </summary>
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// 任务完成时间，可选项
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// 总切片数量
        /// </summary>
        public int TotalSlices { get; set; }
    }

    /// <summary>
    /// 切片进度响应DTO
    /// 用于实时返回切片任务的进度信息
    /// </summary>
    public class SlicingProgressDto
    {
        /// <summary>
        /// 关联的切片任务ID
        /// </summary>
        public Guid TaskId { get; set; }

        /// <summary>
        /// 当前进度百分比，0-100
        /// </summary>
        public int Progress { get; set; }

        /// <summary>
        /// 当前处理阶段描述
        /// 如：模型加载、空间索引构建、切片生成等
        /// </summary>
        public string CurrentStage { get; set; } = string.Empty;

        /// <summary>
        /// 已处理的瓦片数量
        /// </summary>
        public long ProcessedTiles { get; set; }

        /// <summary>
        /// 总瓦片数量
        /// </summary>
        public long TotalTiles { get; set; }

        /// <summary>
        /// 预计剩余时间（毫秒）
        /// </summary>
        public long EstimatedTimeRemaining { get; set; }

        /// <summary>
        /// 当前任务状态
        /// </summary>
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// 切片响应DTO
    /// 用于返回单个切片数据的详细信息
    /// </summary>
    public class SliceDto
    {
        /// <summary>
        /// 切片唯一标识符
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 所属切片任务ID
        /// </summary>
        public Guid SlicingTaskId { get; set; }

        /// <summary>
        /// 切片层级，表示细节程度，数值越大细节越丰富
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// 切片X坐标（瓦片坐标系）
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// 切片Y坐标（瓦片坐标系）
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// 切片Z坐标（瓦片坐标系）
        /// </summary>
        public int Z { get; set; }

        /// <summary>
        /// 切片文件物理存储路径
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// 切片包围盒信息，定义空间范围
        /// </summary>
        public string BoundingBox { get; set; } = string.Empty;

        /// <summary>
        /// 切片文件大小（字节）
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// 切片创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// 切片元数据DTO（不包含文件数据）
    /// 用于传输切片的基本信息，不包含实际的切片文件内容
    /// </summary>
    public class SliceMetadataDto
    {
        /// <summary>
        /// 切片X坐标
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// 切片Y坐标
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// 切片Z坐标
        /// </summary>
        public int Z { get; set; }

        /// <summary>
        /// 切片包围盒信息
        /// </summary>
        public string BoundingBox { get; set; } = string.Empty;

        /// <summary>
        /// 切片文件大小（字节）
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// 切片文件内容类型，如：image/png、application/octet-stream等
        /// </summary>
        public string ContentType { get; set; } = string.Empty;
    }

    /// <summary>
    /// 切片批次请求DTO
    /// 用于批量请求特定层级的多个切片数据
    /// </summary>
    public class SliceBatchRequest
    {
        /// <summary>
        /// 切片任务ID，必填项
        /// </summary>
        public Guid TaskId { get; set; }

        /// <summary>
        /// 请求的切片层级，必填项
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// 请求的切片坐标集合
        /// 每个元组包含X、Y、Z坐标，用于精确定位切片
        /// </summary>
        public IEnumerable<(int x, int y, int z)> Coordinates { get; set; } = new List<(int x, int y, int z)>();
    }

    /// <summary>
    /// 切片批次响应DTO
    /// 用于批量返回切片数据的响应信息
    /// </summary>
    public class SliceBatchResponse
    {
        /// <summary>
        /// 切片任务ID
        /// </summary>
        public Guid TaskId { get; set; }

        /// <summary>
        /// 切片层级
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// 切片数据集合
        /// </summary>
        public IEnumerable<SliceDto> Slices { get; set; } = new List<SliceDto>();

        /// <summary>
        /// 总切片数量
        /// </summary>
        public int TotalCount { get; set; }
    }

    /// <summary>
    /// 增量更新索引DTO
    /// 用于返回切片任务的增量更新索引信息
    /// </summary>
    public class IncrementalUpdateIndexDto
    {
        /// <summary>
        /// 切片任务ID
        /// </summary>
        public Guid TaskId { get; set; }

        /// <summary>
        /// 索引版本号，格式：yyyyMMddHHmmss
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// 切片数量
        /// </summary>
        public int SliceCount { get; set; }

        /// <summary>
        /// 切片信息集合
        /// </summary>
        public IEnumerable<IncrementalSliceInfo> Slices { get; set; } = new List<IncrementalSliceInfo>();

        /// <summary>
        /// 切片策略名称
        /// </summary>
        public string Strategy { get; set; } = string.Empty;

        /// <summary>
        /// 瓦片大小
        /// </summary>
        public double TileSize { get; set; }
    }

    /// <summary>
    /// 增量切片信息DTO
    /// 用于增量更新索引中的单个切片信息
    /// </summary>
    public class IncrementalSliceInfo
    {
        /// <summary>
        /// 切片层级
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// 切片X坐标
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// 切片Y坐标
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// 切片Z坐标
        /// </summary>
        public int Z { get; set; }

        /// <summary>
        /// 切片文件路径
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// 切片哈希值，用于增量更新比对
        /// </summary>
        public string Hash { get; set; } = string.Empty;

        /// <summary>
        /// 包围盒信息
        /// </summary>
        public string BoundingBox { get; set; } = string.Empty;
    }
}