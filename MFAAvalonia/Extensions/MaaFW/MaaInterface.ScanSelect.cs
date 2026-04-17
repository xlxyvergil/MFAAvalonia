using Newtonsoft.Json;

namespace MFAAvalonia.Extensions.MaaFW;

/// <summary>
/// scan_select 功能的属性扩展（通过 partial class 实现）
/// </summary>
public partial class MaaInterface
{
    public partial class MaaInterfaceOption
    {
        /// <summary>扫描目录路径（用于 scan_select 类型）</summary>
        [JsonProperty("scan_dir")]
        public string? ScanDir { get; set; }

        /// <summary>扫描文件过滤器，支持 glob 模式（用于 scan_select 类型）</summary>
        [JsonProperty("scan_filter")]
        public string? ScanFilter { get; set; }

        /// <summary>是否为 scan_select 扫描选择类型</summary>
        [JsonIgnore]
        public bool IsScanSelect => Type?.ToLower() == "scan_select";
    }
}
