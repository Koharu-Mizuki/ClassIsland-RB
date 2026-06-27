using System;

namespace ClassIsland.F1Timing.Models;

/// <summary>
/// Race Control / 车队无线电 等赛况消息的类别，决定显示图标与配色。
/// </summary>
public enum RaceControlCategory
{
    /// <summary>一般信息。</summary>
    Info,
    /// <summary>旗帜（绿/黄/红/方格）。</summary>
    Flag,
    /// <summary>安全车 / 虚拟安全车。</summary>
    SafetyCar,
    /// <summary>DRS 启用 / 禁用。</summary>
    Drs,
    /// <summary>事故 / 调查 / 处罚。</summary>
    Incident,
    /// <summary>进站 / 维修区。</summary>
    PitLane,
    /// <summary>车队无线电。</summary>
    Radio,
    /// <summary>天气。</summary>
    Weather
}

/// <summary>
/// 一条赛况消息（Race Control 指令、旗帜、无线电等）。作为只读载体，
/// 由数据源产出、服务层插入快照的 <see cref="F1SessionSnapshot.RaceControl"/> 集合。
/// </summary>
public sealed class F1RaceControlMessage
{
    /// <summary>产生时间（本地）。</summary>
    public DateTime Time { get; set; } = DateTime.Now;

    /// <summary>展示用时间/圈数标签，如 <c>L23</c> 或 <c>14:32</c>。</summary>
    public string TimeText { get; set; } = "";

    /// <summary>消息类别。</summary>
    public RaceControlCategory Category { get; set; } = RaceControlCategory.Info;

    /// <summary>消息正文。</summary>
    public string Message { get; set; } = "";

    /// <summary>相关车手三字母缩写（无线电/事故时用，可空）。</summary>
    public string DriverTla { get; set; } = "";

    /// <summary>强调色（十六进制，可空；为空时由类别决定）。</summary>
    public string Accent { get; set; } = "";
}
