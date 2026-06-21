namespace ClassIsland.F1Timing.Models.Settings;

/// <summary>
/// F1 计时组件的持久化设置。
/// </summary>
public class F1TimingComponentSettings : ObservableModelBase
{
    private string _dataSource = "openf1";
    /// <summary>数据源标识：openf1 / livetiming。</summary>
    public string DataSource
    {
        get => _dataSource;
        set => SetField(ref _dataSource, value);
    }

    private int _pollIntervalSeconds = 5;
    /// <summary>轮询间隔（秒），限制在 3~60。</summary>
    public int PollIntervalSeconds
    {
        get => _pollIntervalSeconds;
        set => SetField(ref _pollIntervalSeconds, value < 3 ? 3 : value > 60 ? 60 : value);
    }

    private bool _towerTopmost = true;
    /// <summary>Timing Tower 窗口是否置顶。</summary>
    public bool TowerTopmost
    {
        get => _towerTopmost;
        set => SetField(ref _towerTopmost, value);
    }

    private string _highlightDriver = "";
    /// <summary>高亮关注的车手三字母缩写（留空不高亮）。</summary>
    public string HighlightDriver
    {
        get => _highlightDriver;
        set => SetField(ref _highlightDriver, value);
    }

    private bool _showInterval = true;
    public bool ShowInterval
    {
        get => _showInterval;
        set => SetField(ref _showInterval, value);
    }

    private bool _showLastLap = true;
    public bool ShowLastLap
    {
        get => _showLastLap;
        set => SetField(ref _showLastLap, value);
    }

    private bool _showBestLap = true;
    public bool ShowBestLap
    {
        get => _showBestLap;
        set => SetField(ref _showBestLap, value);
    }

    private bool _showTyre = true;
    public bool ShowTyre
    {
        get => _showTyre;
        set => SetField(ref _showTyre, value);
    }

    private bool _onlyWhenLive;
    /// <summary>仅在赛事进行时显示顶栏排位条；关闭则常驻显示。</summary>
    public bool OnlyWhenLive
    {
        get => _onlyWhenLive;
        set => SetField(ref _onlyWhenLive, value);
    }
}
