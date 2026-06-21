using System;
using System.Collections.ObjectModel;

namespace ClassIsland.F1Timing.Models;

/// <summary>
/// 一场会话的整体快照。后台服务维护单一长期实例并在每次轮询时原地更新，
/// 顶栏组件与 Timing Tower 窗口共享绑定到同一实例。
/// </summary>
public class F1SessionSnapshot : ObservableModelBase
{
    /// <summary>计时行集合（按位次排序），内容随轮询原地增删改。</summary>
    public ObservableCollection<F1DriverTiming> Drivers { get; } = new();

    public F1SessionSnapshot()
    {
        Drivers.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasDrivers));
    }

    private bool _hasDrivers;
    /// <summary>当前是否有车手数据（驱动顶栏 Viewport 可见性）。</summary>
    public bool HasDrivers
    {
        get => _hasDrivers;
        set => SetField(ref _hasDrivers, value);
    }

    private bool _isLive;
    /// <summary>当前是否有进行中的会话数据。</summary>
    public bool IsLive
    {
        get => _isLive;
        set => SetField(ref _isLive, value);
    }

    private bool _hasData;
    /// <summary>当前是否已有车手计时数据（用于空态显示）。</summary>
    public bool HasData
    {
        get => _hasData;
        set => SetField(ref _hasData, value);
    }

    private string _statusMessage = "尚未连接";
    /// <summary>无数据 / 错误时展示的状态文案。</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    private string _sessionName = "";
    /// <summary>会话名称，如 Race、Qualifying。</summary>
    public string SessionName
    {
        get => _sessionName;
        set => SetField(ref _sessionName, value);
    }

    private SessionType _sessionType = SessionType.Unknown;
    public SessionType SessionType
    {
        get => _sessionType;
        set => SetField(ref _sessionType, value);
    }

    private string _circuitName = "";
    /// <summary>赛道 / 大奖赛名称。</summary>
    public string CircuitName
    {
        get => _circuitName;
        set => SetField(ref _circuitName, value);
    }

    private string _countryName = "";
    public string CountryName
    {
        get => _countryName;
        set => SetField(ref _countryName, value);
    }

    private int _currentLap;
    /// <summary>当前圈数。</summary>
    public int CurrentLap
    {
        get => _currentLap;
        set
        {
            if (SetField(ref _currentLap, value))
                OnPropertyChanged(nameof(LapsText));
        }
    }

    private int _totalLaps;
    /// <summary>总圈数（0 表示未知 / 计时赛）。</summary>
    public int TotalLaps
    {
        get => _totalLaps;
        set
        {
            if (SetField(ref _totalLaps, value))
                OnPropertyChanged(nameof(LapsText));
        }
    }

    /// <summary>圈数文本，如 <c>L 23 / 57</c>；无圈数信息时为空。</summary>
    public string LapsText => TotalLaps > 0 ? $"L {CurrentLap} / {TotalLaps}"
        : CurrentLap > 0 ? $"L {CurrentLap}" : "";

    private TrackFlag _flag = TrackFlag.None;
    /// <summary>当前旗帜 / 安全车状态。</summary>
    public TrackFlag Flag
    {
        get => _flag;
        set => SetField(ref _flag, value);
    }

    private string _leaderTla = "";
    /// <summary>领先者三字母缩写（供顶栏精简显示）。</summary>
    public string LeaderTla
    {
        get => _leaderTla;
        set => SetField(ref _leaderTla, value);
    }

    private DateTime _lastUpdated;
    /// <summary>最近一次成功更新的本地时间。</summary>
    public DateTime LastUpdated
    {
        get => _lastUpdated;
        set => SetField(ref _lastUpdated, value);
    }
}
