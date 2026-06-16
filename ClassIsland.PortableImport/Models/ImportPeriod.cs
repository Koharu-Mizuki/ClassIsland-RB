namespace ClassIsland.PortableImport.Models;

/// <summary>
/// 解析出的一个时间段。
/// </summary>
public class ImportPeriod
{
    /// <summary>开始时间，格式 <c>HH:mm</c>。</summary>
    public string Start { get; set; } = "";

    /// <summary>结束时间，格式 <c>HH:mm</c>。</summary>
    public string End { get; set; } = "";

    /// <summary>时间段类型：<c>class</c> 上课，<c>break</c> 课间。</summary>
    public string Type { get; set; } = "class";
}
