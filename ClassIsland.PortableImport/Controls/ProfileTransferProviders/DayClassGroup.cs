using System.Collections.ObjectModel;

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
}
