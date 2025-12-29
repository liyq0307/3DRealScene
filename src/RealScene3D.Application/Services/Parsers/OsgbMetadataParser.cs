using Microsoft.Extensions.Logging;
using System.Xml.Linq;

namespace RealScene3D.Application.Services.Parsers;

/// <summary>
/// OSGB 倾斜摄影 metadata.xml 解析器
///
/// 解析 Smart3D 生成的 metadata.xml 文件，获取：
/// - SRS（坐标参考系统）信息
/// - 数据边界范围
/// - 投影坐标或 ENU 坐标原点
/// </summary>
public class OsgbMetadataParser
{
    private readonly ILogger<OsgbMetadataParser> _logger;

    public OsgbMetadataParser(ILogger<OsgbMetadataParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 元数据信息
    /// </summary>
    public class MetadataInfo
    {
        /// <summary>SRS类型（EPSG代码或WKT）</summary>
        public string? SrsType { get; set; }

        /// <summary>EPSG代码</summary>
        public int? EpsgCode { get; set; }

        /// <summary>WKT字符串</summary>
        public string? WktString { get; set; }

        /// <summary>是否为 ENU 坐标系</summary>
        public bool IsENU { get; set; }

        /// <summary>SRS原点坐标 (投影坐标或ENU偏移)</summary>
        public (double X, double Y, double Z) SrsOrigin { get; set; }

        /// <summary>地理坐标原点（经纬度）</summary>
        public (double Longitude, double Latitude, double Height)? GeoOrigin { get; set; }

        /// <summary>数据范围（米）</summary>
        public BoundingBox? Bounds { get; set; }
    }

    /// <summary>
    /// 包围盒
    /// </summary>
    public class BoundingBox
    {
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MinZ { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
        public double MaxZ { get; set; }
    }

    /// <summary>
    /// 解析 metadata.xml 文件
    /// </summary>
    /// <param name="metadataPath">metadata.xml 文件路径</param>
    public async Task<MetadataInfo> ParseAsync(string metadataPath)
    {
        _logger.LogInformation("开始解析 metadata.xml: {Path}", metadataPath);

        var metadata = new MetadataInfo();

        try
        {
            XDocument doc = await Task.Run(() => XDocument.Load(metadataPath));
            XElement? root = doc.Root;

            if (root == null)
            {
                throw new InvalidOperationException("metadata.xml 根节点为空");
            }

            // 解析 SRS 信息
            ParseSRS(root, metadata);

            // 解析边界范围
            ParseBounds(root, metadata);

            _logger.LogInformation(
                "metadata.xml 解析完成: SRS类型={SrsType}, EPSG={EpsgCode}, ENU={IsENU}, " +
                "原点=({X}, {Y}, {Z})",
                metadata.SrsType, metadata.EpsgCode, metadata.IsENU,
                metadata.SrsOrigin.X, metadata.SrsOrigin.Y, metadata.SrsOrigin.Z);

            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析 metadata.xml 失败: {Path}", metadataPath);
            throw;
        }
    }

    /// <summary>
    /// 解析坐标参考系统（SRS）
    /// </summary>
    private void ParseSRS(XElement root, MetadataInfo metadata)
    {
        // 尝试查找 SRS 节点
        XElement? srsElement = root.Descendants("SRS").FirstOrDefault();
        if (srsElement == null)
        {
            _logger.LogWarning("未找到 SRS 节点，使用默认坐标系");
            return;
        }

        // 解析 SRS 原点
        if (TryParseVector3(srsElement, "SRSOrigin", out var srsOrigin))
        {
            metadata.SrsOrigin = srsOrigin;
        }

        // 检查是否为 ENU 坐标系
        XElement? enuElement = srsElement.Element("ENU");
        if (enuElement != null)
        {
            metadata.IsENU = true;
            metadata.SrsType = "ENU";

            // 解析地理坐标原点
            if (TryParseVector3(enuElement, "Center", out var geoCenter))
            {
                metadata.GeoOrigin = (geoCenter.X, geoCenter.Y, geoCenter.Z);
                _logger.LogInformation(
                    "检测到 ENU 坐标系: 中心=({Lon}, {Lat}, {Height})",
                    geoCenter.X, geoCenter.Y, geoCenter.Z);
            }
        }
        else
        {
            // EPSG 或 WKT
            metadata.IsENU = false;

            // 尝试 EPSG
            string? epsgStr = srsElement.Element("EPSG")?.Value;
            if (!string.IsNullOrEmpty(epsgStr) && int.TryParse(epsgStr, out int epsgCode))
            {
                metadata.SrsType = "EPSG";
                metadata.EpsgCode = epsgCode;
                _logger.LogInformation("检测到 EPSG 坐标系: {EpsgCode}", epsgCode);
            }
            else
            {
                // 尝试 WKT
                string? wkt = srsElement.Element("WKT")?.Value;
                if (!string.IsNullOrEmpty(wkt))
                {
                    metadata.SrsType = "WKT";
                    metadata.WktString = wkt;
                    _logger.LogInformation("检测到 WKT 坐标系: {Length} 字符", wkt.Length);
                }
            }
        }
    }

    /// <summary>
    /// 解析边界范围
    /// </summary>
    private void ParseBounds(XElement root, MetadataInfo metadata)
    {
        XElement? boundsElement = root.Descendants("BoundingBox")
            .FirstOrDefault() ?? root.Descendants("Bounds").FirstOrDefault();

        if (boundsElement == null)
        {
            _logger.LogWarning("未找到 BoundingBox 节点");
            return;
        }

        try
        {
            var bounds = new BoundingBox();

            // 尝试多种可能的节点名称
            if (TryParseDouble(boundsElement, "MinX", out double minX)) bounds.MinX = minX;
            if (TryParseDouble(boundsElement, "MinY", out double minY)) bounds.MinY = minY;
            if (TryParseDouble(boundsElement, "MinZ", out double minZ)) bounds.MinZ = minZ;
            if (TryParseDouble(boundsElement, "MaxX", out double maxX)) bounds.MaxX = maxX;
            if (TryParseDouble(boundsElement, "MaxY", out double maxY)) bounds.MaxY = maxY;
            if (TryParseDouble(boundsElement, "MaxZ", out double maxZ)) bounds.MaxZ = maxZ;

            metadata.Bounds = bounds;

            _logger.LogInformation(
                "边界范围: X=[{MinX}, {MaxX}], Y=[{MinY}, {MaxY}], Z=[{MinZ}, {MaxZ}]",
                bounds.MinX, bounds.MaxX, bounds.MinY, bounds.MaxY, bounds.MinZ, bounds.MaxZ);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析边界范围失败");
        }
    }

    /// <summary>
    /// 尝试解析三维向量
    /// </summary>
    private bool TryParseVector3(XElement parent, string elementName, out (double X, double Y, double Z) vector)
    {
        vector = (0, 0, 0);
        XElement? element = parent.Element(elementName);
        if (element == null) return false;

        try
        {
            double x = double.Parse(element.Element("x")?.Value ?? element.Element("X")?.Value ?? "0");
            double y = double.Parse(element.Element("y")?.Value ?? element.Element("Y")?.Value ?? "0");
            double z = double.Parse(element.Element("z")?.Value ?? element.Element("Z")?.Value ?? "0");
            vector = (x, y, z);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 尝试解析浮点数
    /// </summary>
    private bool TryParseDouble(XElement parent, string elementName, out double value)
    {
        value = 0;
        string? str = parent.Element(elementName)?.Value;
        return !string.IsNullOrEmpty(str) && double.TryParse(str, out value);
    }
}
