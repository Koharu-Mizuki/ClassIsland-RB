using System.Collections.Generic;

namespace ClassIsland.PortableImport.Models;

/// <summary>
/// 文字解析的总结果：一个时间表 + 若干天课表。
/// </summary>
public class TimetableImportData
{
    public ImportTimeLayout TimeLayout { get; set; } = new();

    public List<ImportClassPlan> ClassPlans { get; set; } = new();
}
