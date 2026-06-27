namespace ClassIsland.F1Timing.Models;

/// <summary>
/// 车手积分榜条目。
/// </summary>
public sealed class F1StandingEntry
{
    /// <summary>排名。</summary>
    public int Position { get; set; }

    /// <summary>车手三字母缩写。</summary>
    public string Tla { get; set; } = "";

    /// <summary>车手全名。</summary>
    public string FullName { get; set; } = "";

    /// <summary>车队名。</summary>
    public string TeamName { get; set; } = "";

    /// <summary>车队代表色（hex）。</summary>
    public string TeamColour { get; set; } = "#888888";

    /// <summary>总积分。</summary>
    public int Points { get; set; }

    /// <summary>胜场数。</summary>
    public int Wins { get; set; }

    /// <summary>国籍。</summary>
    public string Nationality { get; set; } = "";
}

/// <summary>
/// 车队积分榜条目。
/// </summary>
public sealed class F1ConstructorStandingEntry
{
    public int Position { get; set; }
    public string TeamName { get; set; } = "";
    public string TeamColour { get; set; } = "#888888";
    public int Points { get; set; }
    public int Wins { get; set; }
}
