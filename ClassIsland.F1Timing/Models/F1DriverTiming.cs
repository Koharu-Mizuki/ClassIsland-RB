namespace ClassIsland.F1Timing.Models;

/// <summary>
/// Timing Tower 中单个车手的一行计时数据。
/// 车手基础信息（号码 / 缩写 / 车队 / 颜色）在一场会话内基本不变，使用普通属性；
/// 位次与各项计时数据会随轮询变化，使用 <see cref="ObservableModelBase.SetField"/> 以便 UI 原地刷新。
/// </summary>
public class F1DriverTiming : ObservableModelBase
{
    /// <summary>车手号，作为跨轮询匹配同一行的唯一键。</summary>
    public int DriverNumber { get; set; }

    /// <summary>三字母缩写，如 VER、HAM。</summary>
    public string Tla { get; set; } = "";

    /// <summary>车手全名。</summary>
    public string FullName { get; set; } = "";

    /// <summary>车队名。</summary>
    public string TeamName { get; set; } = "";

    /// <summary>车队代表色，形如 <c>#3671C6</c>。</summary>
    public string TeamColour { get; set; } = "#888888";

    private int _position;
    /// <summary>当前位次（1 起）。</summary>
    public int Position
    {
        get => _position;
        set => SetField(ref _position, value);
    }

    private string _gapToLeader = "";
    /// <summary>与领先者的差距，如 <c>+12.345</c>、<c>1 LAP</c>，领先者为空。</summary>
    public string GapToLeader
    {
        get => _gapToLeader;
        set => SetField(ref _gapToLeader, value);
    }

    private string _interval = "";
    /// <summary>与前车的间隔。</summary>
    public string Interval
    {
        get => _interval;
        set => SetField(ref _interval, value);
    }

    private string _lastLapTime = "";
    /// <summary>上一圈圈速，如 <c>1:23.456</c>。</summary>
    public string LastLapTime
    {
        get => _lastLapTime;
        set => SetField(ref _lastLapTime, value);
    }

    private string _bestLapTime = "";
    /// <summary>本场个人最快圈。</summary>
    public string BestLapTime
    {
        get => _bestLapTime;
        set => SetField(ref _bestLapTime, value);
    }

    private bool _isOverallFastest;
    /// <summary>是否持有全场最快圈（紫色高亮）。</summary>
    public bool IsOverallFastest
    {
        get => _isOverallFastest;
        set => SetField(ref _isOverallFastest, value);
    }

    private bool _isPersonalBest;
    /// <summary>上一圈是否刷新了个人最快（绿色高亮）。</summary>
    public bool IsPersonalBest
    {
        get => _isPersonalBest;
        set => SetField(ref _isPersonalBest, value);
    }

    private TyreCompound _tyre = TyreCompound.Unknown;
    /// <summary>当前轮胎配方。</summary>
    public TyreCompound Tyre
    {
        get => _tyre;
        set => SetField(ref _tyre, value);
    }

    private int _stintLaps;
    /// <summary>当前这套轮胎已使用的圈数（本 stint）。</summary>
    public int StintLaps
    {
        get => _stintLaps;
        set => SetField(ref _stintLaps, value);
    }

    private int _tyreAge;
    /// <summary>当前轮胎总圈龄（含上场旧胎），用于显示磨损。</summary>
    public int TyreAge
    {
        get => _tyreAge;
        set => SetField(ref _tyreAge, value);
    }

    private int _pitStops;
    /// <summary>已进站次数。</summary>
    public int PitStops
    {
        get => _pitStops;
        set => SetField(ref _pitStops, value);
    }

    private bool _inPit;
    /// <summary>当前是否在维修区 / 进站中。</summary>
    public bool InPit
    {
        get => _inPit;
        set => SetField(ref _inPit, value);
    }

    private bool _isRetired;
    /// <summary>是否已退赛 / 退出会话。</summary>
    public bool IsRetired
    {
        get => _isRetired;
        set => SetField(ref _isRetired, value);
    }

    private bool _drs;
    /// <summary>当前是否开启 DRS。</summary>
    public bool Drs
    {
        get => _drs;
        set => SetField(ref _drs, value);
    }

    private string _sector1 = "";
    public string Sector1
    {
        get => _sector1;
        set => SetField(ref _sector1, value);
    }

    private string _sector2 = "";
    public string Sector2
    {
        get => _sector2;
        set => SetField(ref _sector2, value);
    }

    private string _sector3 = "";
    public string Sector3
    {
        get => _sector3;
        set => SetField(ref _sector3, value);
    }

    private SectorState _sector1State;
    public SectorState Sector1State
    {
        get => _sector1State;
        set => SetField(ref _sector1State, value);
    }

    private SectorState _sector2State;
    public SectorState Sector2State
    {
        get => _sector2State;
        set => SetField(ref _sector2State, value);
    }

    private SectorState _sector3State;
    public SectorState Sector3State
    {
        get => _sector3State;
        set => SetField(ref _sector3State, value);
    }

    /// <summary>从数据源产出的行拷贝动态字段（基础信息不变），用于 UI 集合的原地刷新。</summary>
    public void UpdateDynamicFrom(F1DriverTiming s)
    {
        Position = s.Position;
        GapToLeader = s.GapToLeader;
        Interval = s.Interval;
        LastLapTime = s.LastLapTime;
        BestLapTime = s.BestLapTime;
        IsOverallFastest = s.IsOverallFastest;
        IsPersonalBest = s.IsPersonalBest;
        Tyre = s.Tyre;
        StintLaps = s.StintLaps;
        TyreAge = s.TyreAge;
        PitStops = s.PitStops;
        InPit = s.InPit;
        IsRetired = s.IsRetired;
        Drs = s.Drs;
        Sector1 = s.Sector1;
        Sector2 = s.Sector2;
        Sector3 = s.Sector3;
        Sector1State = s.Sector1State;
        Sector2State = s.Sector2State;
        Sector3State = s.Sector3State;
    }
}
