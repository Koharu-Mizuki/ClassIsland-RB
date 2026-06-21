namespace ClassIsland.F1Timing.Models;

/// <summary>
/// 会话类型。
/// </summary>
public enum SessionType
{
    Unknown,
    Practice,
    Qualifying,
    SprintQualifying,
    Sprint,
    Race
}

/// <summary>
/// 赛道旗帜 / 安全车状态。
/// </summary>
public enum TrackFlag
{
    None,
    Green,
    Yellow,
    DoubleYellow,
    Red,
    SafetyCar,
    VirtualSafetyCar,
    Chequered
}

/// <summary>
/// 轮胎配方。
/// </summary>
public enum TyreCompound
{
    Unknown,
    Soft,
    Medium,
    Hard,
    Intermediate,
    Wet
}

/// <summary>
/// 分段计时状态，决定显示颜色（无 / 黄=完成但非最佳 / 绿=个人最佳 / 紫=全场最快）。
/// </summary>
public enum SectorState
{
    None,
    Yellow,
    Green,
    Purple
}
