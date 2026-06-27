namespace ClassIsland.F1Timing.Models;

/// <summary>
/// F1 赛历条目，描述一场大奖赛的各会话时间。
/// </summary>
public sealed class F1CalendarEntry
{
    /// <summary>赛道短名。</summary>
    public string Circuit { get; set; } = "";

    /// <summary>大奖赛名称。</summary>
    public string GrandPrix { get; set; } = "";

    /// <summary>国家。</summary>
    public string Country { get; set; } = "";

    /// <summary>第一次练习赛（UTC）。</summary>
    public DateTime? FP1 { get; set; }

    /// <summary>排位赛（UTC）。</summary>
    public DateTime? Qualifying { get; set; }

    /// <summary>冲刺赛（UTC，可空）。</summary>
    public DateTime? Sprint { get; set; }

    /// <summary>正赛（UTC）。</summary>
    public DateTime? Race { get; set; }

    /// <summary>是否为冲刺周末。</summary>
    public bool IsSprintWeekend => Sprint.HasValue;
}
