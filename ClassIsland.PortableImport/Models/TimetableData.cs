using System.Collections.Generic;

namespace ClassIsland.PortableImport.Models;

public class TimetableImportData
{
    public ImportTimeLayout TimeLayout { get; set; } = new();
    public List<ImportClassPlan> ClassPlans { get; set; } = [];
}

public class ImportTimeLayout
{
    public string Name { get; set; } = "导入的时间表";
    public List<ImportPeriod> Periods { get; set; } = [];
}

public class ImportPeriod
{
    public string Start { get; set; } = "";
    public string End { get; set; } = "";
    // "class" 或 "break"
    public string Type { get; set; } = "class";
}

public class ImportClassPlan
{
    public string Name { get; set; } = "导入的课表";
    public List<string> Classes { get; set; } = [];
    public int? WeekDay { get; set; }  // null=不指定, .NET DayOfWeek (0=Sun,1=Mon...)
    public string DayName { get; set; } = "";
}
