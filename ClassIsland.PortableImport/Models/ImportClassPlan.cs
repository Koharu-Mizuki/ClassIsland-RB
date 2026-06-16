using System.Collections.Generic;

namespace ClassIsland.PortableImport.Models;

/// <summary>
/// 解析出的某一天的课表。
/// </summary>
public class ImportClassPlan
{
    public string Name { get; set; } = "导入的课表";

    public List<string> Classes { get; set; } = new();

    /// <summary>星期序号，0=周日 … 6=周六；为 <c>null</c> 表示未识别到星期。</summary>
    public int? WeekDay { get; set; }

    public string DayName { get; set; } = "";

    /// <summary>这一天自己的时间段（用于生成专属时间表，不与其他天混用）。</summary>
    public List<ImportPeriod> Periods { get; set; } = new();
}
