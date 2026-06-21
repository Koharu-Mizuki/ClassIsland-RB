using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ClassIsland.F1Timing.Models;

namespace ClassIsland.F1Timing.Converters;

/// <summary>轮胎配方 → 单字母标识（S / M / H / I / W）。</summary>
public sealed class TyreToLetterConverter : IValueConverter
{
    public static readonly TyreToLetterConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is TyreCompound c
            ? c switch
            {
                TyreCompound.Soft => "S",
                TyreCompound.Medium => "M",
                TyreCompound.Hard => "H",
                TyreCompound.Intermediate => "I",
                TyreCompound.Wet => "W",
                _ => "•"
            }
            : "•";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>轮胎配方 → 代表色。</summary>
public sealed class TyreToBrushConverter : IValueConverter
{
    public static readonly TyreToBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = value is TyreCompound c
            ? c switch
            {
                TyreCompound.Soft => "#DA291C",
                TyreCompound.Medium => "#FFD12E",
                TyreCompound.Hard => "#EBEBEB",
                TyreCompound.Intermediate => "#43B02A",
                TyreCompound.Wet => "#0067AD",
                _ => "#888888"
            }
            : "#888888";
        return new SolidColorBrush(Color.Parse(hex));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>十六进制颜色字符串（如 #3671C6） → 画刷。</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public static readonly HexToBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = value as string;
        if (string.IsNullOrWhiteSpace(hex)) return new SolidColorBrush(Colors.Gray);
        try
        {
            return new SolidColorBrush(Color.Parse(hex));
        }
        catch
        {
            return new SolidColorBrush(Colors.Gray);
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>赛道旗帜 → 状态色。</summary>
public sealed class FlagToBrushConverter : IValueConverter
{
    public static readonly FlagToBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = value is TrackFlag f
            ? f switch
            {
                TrackFlag.Green => "#2ECC40",
                TrackFlag.Yellow => "#FFD12E",
                TrackFlag.DoubleYellow => "#FFB000",
                TrackFlag.Red => "#E60000",
                TrackFlag.SafetyCar => "#FF8C00",
                TrackFlag.VirtualSafetyCar => "#FFB000",
                TrackFlag.Chequered => "#BBBBBB",
                _ => "#555555"
            }
            : "#555555";
        return new SolidColorBrush(Color.Parse(hex));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>赛道旗帜 → 简短中文文案。</summary>
public sealed class FlagToTextConverter : IValueConverter
{
    public static readonly FlagToTextConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is TrackFlag f
            ? f switch
            {
                TrackFlag.Green => "绿旗",
                TrackFlag.Yellow => "黄旗",
                TrackFlag.DoubleYellow => "双黄旗",
                TrackFlag.Red => "红旗",
                TrackFlag.SafetyCar => "安全车",
                TrackFlag.VirtualSafetyCar => "虚拟安全车",
                TrackFlag.Chequered => "方格旗",
                _ => "—"
            }
            : "—";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>分段计时状态 → 颜色（紫=全场最快 / 绿=个人最佳 / 黄=完成 / 暗=无数据）。</summary>
public sealed class SectorStateToBrushConverter : IValueConverter
{
    public static readonly SectorStateToBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = value is SectorState s
            ? s switch
            {
                SectorState.Purple => "#B14BE0",
                SectorState.Green => "#2ECC40",
                SectorState.Yellow => "#FFD12E",
                _ => "#2E2E38"
            }
            : "#2E2E38";
        return new SolidColorBrush(Color.Parse(hex));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
