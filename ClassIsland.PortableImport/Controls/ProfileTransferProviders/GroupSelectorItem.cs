using System;

namespace ClassIsland.PortableImport.Controls.ProfileTransferProviders;

/// <summary>
/// 课表群下拉选择项。<see cref="Id"/> 为 <c>null</c> 表示「新建课表群」。
/// </summary>
public class GroupSelectorItem
{
    public Guid? Id { get; set; }

    public string DisplayName { get; set; } = "";

    public bool IsNew => !Id.HasValue;
}
