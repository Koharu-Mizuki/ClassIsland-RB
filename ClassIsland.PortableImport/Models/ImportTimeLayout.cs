using System.Collections.Generic;

namespace ClassIsland.PortableImport.Models;

/// <summary>
/// 解析出的时间表。
/// </summary>
public class ImportTimeLayout
{
    public string Name { get; set; } = "导入的时间表";

    public List<ImportPeriod> Periods { get; set; } = new();
}
