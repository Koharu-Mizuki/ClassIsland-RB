namespace ClassIsland.F1Timing.Models;

/// <summary>
/// 赛道天气快照。后台服务维护单一实例并原地更新，Tower / 组件共享绑定。
/// </summary>
public class F1Weather : ObservableModelBase
{
    private double _airTemp;
    /// <summary>气温（℃）。</summary>
    public double AirTemp
    {
        get => _airTemp;
        set => SetField(ref _airTemp, value);
    }

    private double _trackTemp;
    /// <summary>路温（℃）。</summary>
    public double TrackTemp
    {
        get => _trackTemp;
        set => SetField(ref _trackTemp, value);
    }

    private double _humidity;
    /// <summary>相对湿度（%）。</summary>
    public double Humidity
    {
        get => _humidity;
        set => SetField(ref _humidity, value);
    }

    private double _windSpeed;
    /// <summary>风速（m/s）。</summary>
    public double WindSpeed
    {
        get => _windSpeed;
        set => SetField(ref _windSpeed, value);
    }

    private double _rainfallProbability;
    /// <summary>降雨概率（0~1）。</summary>
    public double RainfallProbability
    {
        get => _rainfallProbability;
        set
        {
            if (SetField(ref _rainfallProbability, value))
                OnPropertyChanged(nameof(RainfallPercentText));
        }
    }

    private bool _isRaining;
    /// <summary>当前是否正在下雨。</summary>
    public bool IsRaining
    {
        get => _isRaining;
        set => SetField(ref _isRaining, value);
    }

    private string _summary = "";
    /// <summary>天气概述文案，如「晴」「小雨」。</summary>
    public string Summary
    {
        get => _summary;
        set => SetField(ref _summary, value);
    }

    /// <summary>降雨概率百分比文本，如 <c>35%</c>。</summary>
    public string RainfallPercentText => $"{RainfallProbability * 100:0}%";

    /// <summary>从另一份天气数据原地拷贝。</summary>
    public void CopyFrom(F1Weather s)
    {
        AirTemp = s.AirTemp;
        TrackTemp = s.TrackTemp;
        Humidity = s.Humidity;
        WindSpeed = s.WindSpeed;
        RainfallProbability = s.RainfallProbability;
        IsRaining = s.IsRaining;
        Summary = s.Summary;
    }
}
