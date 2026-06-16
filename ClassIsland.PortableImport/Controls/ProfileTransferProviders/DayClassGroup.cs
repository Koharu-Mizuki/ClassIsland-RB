using System.Collections.Generic;
using System.Collections.ObjectModel;
using ClassIsland.PortableImport.Models;

namespace ClassIsland.PortableImport.Controls.ProfileTransferProviders;

/// <summary>
/// 解析结果中某一天的课程分组（用于预览与编辑）。
/// </summary>
public class DayClassGroup
{
    public string DayName { get; set; } = "";

    public int? WeekDay { get; set; }

    public bool HasDayName => !string.IsNullOrEmpty(DayName);

    public ObservableCollection<EditableClassItem> Classes { get; } = new();

    /// <summary>这一天自己的时间段，导入时用来生成/匹配专属时间表。</summary>
    public List<ImportPeriod> Periods { get; set; } = new();
}
