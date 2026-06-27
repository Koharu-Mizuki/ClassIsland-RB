using System;
using System.Collections.Generic;
using System.Linq;
using ClassIsland.F1Timing.Models;

namespace ClassIsland.F1Timing.Services;

/// <summary>
/// F1 赛程服务。内置 2025 赛历（UTC 时间），提供下一场赛程查询。
/// 赛历数据来源：F1 官方赛历，手动维护。
/// </summary>
public sealed class F1CalendarService
{
    private static readonly List<F1CalendarEntry> Calendar2025 = new()
    {
        new F1CalendarEntry { Circuit = "Melbourne", GrandPrix = "澳大利亚大奖赛", Country = "澳大利亚",
            FP1 = new DateTime(2025, 3, 14, 1, 30, 0), Qualifying = new DateTime(2025, 3, 15, 5, 0, 0), Race = new DateTime(2025, 3, 16, 4, 0, 0) },
        new F1CalendarEntry { Circuit = "Shanghai", GrandPrix = "中国大奖赛", Country = "中国",
            FP1 = new DateTime(2025, 3, 21, 3, 30, 0), Qualifying = new DateTime(2025, 3, 22, 7, 0, 0), Sprint = new DateTime(2025, 3, 22, 3, 0, 0), Race = new DateTime(2025, 3, 23, 7, 0, 0) },
        new F1CalendarEntry { Circuit = "Suzuka", GrandPrix = "日本大奖赛", Country = "日本",
            FP1 = new DateTime(2025, 4, 4, 2, 30, 0), Qualifying = new DateTime(2025, 4, 5, 6, 0, 0), Race = new DateTime(2025, 4, 6, 5, 0, 0) },
        new F1CalendarEntry { Circuit = "Sakhir", GrandPrix = "巴林大奖赛", Country = "巴林",
            FP1 = new DateTime(2025, 4, 11, 11, 30, 0), Qualifying = new DateTime(2025, 4, 12, 15, 0, 0), Race = new DateTime(2025, 4, 13, 15, 0, 0) },
        new F1CalendarEntry { Circuit = "Jeddah", GrandPrix = "沙特阿拉伯大奖赛", Country = "沙特阿拉伯",
            FP1 = new DateTime(2025, 4, 18, 13, 30, 0), Qualifying = new DateTime(2025, 4, 19, 17, 0, 0), Race = new DateTime(2025, 4, 20, 17, 0, 0) },
        new F1CalendarEntry { Circuit = "Miami", GrandPrix = "迈阿密大奖赛", Country = "美国",
            FP1 = new DateTime(2025, 5, 2, 16, 30, 0), Qualifying = new DateTime(2025, 5, 3, 20, 0, 0), Sprint = new DateTime(2025, 5, 3, 16, 0, 0), Race = new DateTime(2025, 5, 4, 20, 0, 0) },
        new F1CalendarEntry { Circuit = "Imola", GrandPrix = "艾米利亚-罗马涅大奖赛", Country = "意大利",
            FP1 = new DateTime(2025, 5, 16, 11, 30, 0), Qualifying = new DateTime(2025, 5, 17, 15, 0, 0), Race = new DateTime(2025, 5, 18, 13, 0, 0) },
        new F1CalendarEntry { Circuit = "Monaco", GrandPrix = "摩纳哥大奖赛", Country = "摩纳哥",
            FP1 = new DateTime(2025, 5, 23, 11, 30, 0), Qualifying = new DateTime(2025, 5, 24, 15, 0, 0), Race = new DateTime(2025, 5, 25, 13, 0, 0) },
        new F1CalendarEntry { Circuit = "Catalunya", GrandPrix = "西班牙大奖赛", Country = "西班牙",
            FP1 = new DateTime(2025, 5, 30, 11, 30, 0), Qualifying = new DateTime(2025, 5, 31, 15, 0, 0), Race = new DateTime(2025, 6, 1, 13, 0, 0) },
        new F1CalendarEntry { Circuit = "Montreal", GrandPrix = "加拿大大奖赛", Country = "加拿大",
            FP1 = new DateTime(2025, 6, 13, 17, 30, 0), Qualifying = new DateTime(2025, 6, 14, 20, 0, 0), Race = new DateTime(2025, 6, 15, 18, 0, 0) },
        new F1CalendarEntry { Circuit = "Spielberg", GrandPrix = "奥地利大奖赛", Country = "奥地利",
            FP1 = new DateTime(2025, 6, 27, 11, 30, 0), Qualifying = new DateTime(2025, 6, 28, 15, 0, 0), Sprint = new DateTime(2025, 6, 28, 11, 0, 0), Race = new DateTime(2025, 6, 29, 13, 0, 0) },
        new F1CalendarEntry { Circuit = "Silverstone", GrandPrix = "英国大奖赛", Country = "英国",
            FP1 = new DateTime(2025, 7, 4, 11, 30, 0), Qualifying = new DateTime(2025, 7, 5, 15, 0, 0), Race = new DateTime(2025, 7, 6, 14, 0, 0) },
        new F1CalendarEntry { Circuit = "Spa-Francorchamps", GrandPrix = "比利时大奖赛", Country = "比利时",
            FP1 = new DateTime(2025, 7, 25, 11, 30, 0), Qualifying = new DateTime(2025, 7, 26, 15, 0, 0), Sprint = new DateTime(2025, 7, 26, 11, 0, 0), Race = new DateTime(2025, 7, 27, 13, 0, 0) },
        new F1CalendarEntry { Circuit = "Hungaroring", GrandPrix = "匈牙利大奖赛", Country = "匈牙利",
            FP1 = new DateTime(2025, 8, 1, 11, 30, 0), Qualifying = new DateTime(2025, 8, 2, 15, 0, 0), Race = new DateTime(2025, 8, 3, 13, 0, 0) },
        new F1CalendarEntry { Circuit = "Zandvoort", GrandPrix = "荷兰大奖赛", Country = "荷兰",
            FP1 = new DateTime(2025, 8, 29, 10, 30, 0), Qualifying = new DateTime(2025, 8, 30, 14, 0, 0), Race = new DateTime(2025, 8, 31, 13, 0, 0) },
        new F1CalendarEntry { Circuit = "Monza", GrandPrix = "意大利大奖赛", Country = "意大利",
            FP1 = new DateTime(2025, 9, 5, 11, 30, 0), Qualifying = new DateTime(2025, 9, 6, 15, 0, 0), Race = new DateTime(2025, 9, 7, 13, 0, 0) },
        new F1CalendarEntry { Circuit = "Baku", GrandPrix = "阿塞拜疆大奖赛", Country = "阿塞拜疆",
            FP1 = new DateTime(2025, 9, 19, 8, 30, 0), Qualifying = new DateTime(2025, 9, 20, 12, 0, 0), Race = new DateTime(2025, 9, 21, 11, 0, 0) },
        new F1CalendarEntry { Circuit = "Singapore", GrandPrix = "新加坡大奖赛", Country = "新加坡",
            FP1 = new DateTime(2025, 10, 3, 9, 30, 0), Qualifying = new DateTime(2025, 10, 4, 13, 0, 0), Race = new DateTime(2025, 10, 5, 12, 0, 0) },
        new F1CalendarEntry { Circuit = "Austin", GrandPrix = "美国大奖赛", Country = "美国",
            FP1 = new DateTime(2025, 10, 17, 17, 30, 0), Qualifying = new DateTime(2025, 10, 18, 21, 0, 0), Sprint = new DateTime(2025, 10, 18, 17, 0, 0), Race = new DateTime(2025, 10, 19, 19, 0, 0) },
        new F1CalendarEntry { Circuit = "Mexico City", GrandPrix = "墨西哥大奖赛", Country = "墨西哥",
            FP1 = new DateTime(2025, 10, 24, 18, 30, 0), Qualifying = new DateTime(2025, 10, 25, 22, 0, 0), Race = new DateTime(2025, 10, 26, 21, 0, 0) },
        new F1CalendarEntry { Circuit = "Interlagos", GrandPrix = "巴西大奖赛", Country = "巴西",
            FP1 = new DateTime(2025, 11, 7, 14, 30, 0), Qualifying = new DateTime(2025, 11, 8, 18, 0, 0), Sprint = new DateTime(2025, 11, 8, 14, 0, 0), Race = new DateTime(2025, 11, 9, 17, 0, 0) },
        new F1CalendarEntry { Circuit = "Las Vegas", GrandPrix = "拉斯维加斯大奖赛", Country = "美国",
            FP1 = new DateTime(2025, 11, 21, 3, 30, 0), Qualifying = new DateTime(2025, 11, 22, 7, 0, 0), Race = new DateTime(2025, 11, 23, 5, 0, 0) },
        new F1CalendarEntry { Circuit = "Lusail", GrandPrix = "卡塔尔大奖赛", Country = "卡塔尔",
            FP1 = new DateTime(2025, 11, 28, 12, 30, 0), Qualifying = new DateTime(2025, 11, 29, 16, 0, 0), Sprint = new DateTime(2025, 11, 29, 12, 0, 0), Race = new DateTime(2025, 11, 30, 16, 0, 0) },
        new F1CalendarEntry { Circuit = "Yas Marina", GrandPrix = "阿布扎比大奖赛", Country = "阿联酋",
            FP1 = new DateTime(2025, 12, 5, 9, 30, 0), Qualifying = new DateTime(2025, 12, 6, 13, 0, 0), Race = new DateTime(2025, 12, 7, 13, 0, 0) },
    };

    /// <summary>
    /// 把 2025 模板日期投影到「下一次发生」：先按当前年份套用月/日/时，
    /// 若已过去则顺延到次年。让内置赛历在跨年后仍能给出近似倒计时。
    /// ⚠️ 仅为近似值——真实年度赛历日期会有出入，应定期更新 <see cref="Calendar2025"/>。
    /// </summary>
    private static DateTime ShiftToUpcoming(DateTime template, DateTime now)
    {
        var candidate = new DateTime(now.Year, template.Month, template.Day,
            template.Hour, template.Minute, template.Second, DateTimeKind.Unspecified);
        if (candidate < now.AddHours(-3))
            candidate = candidate.AddYears(1);
        return candidate;
    }

    private static IEnumerable<(DateTime Time, string Label)> EnumerateSessions(F1CalendarEntry e)
    {
        if (e.FP1.HasValue) yield return (e.FP1.Value, "FP1");
        if (e.Sprint.HasValue) yield return (e.Sprint.Value, "冲刺赛");
        if (e.Qualifying.HasValue) yield return (e.Qualifying.Value, "排位赛");
        if (e.Race.HasValue) yield return (e.Race.Value, "正赛");
    }

    /// <summary>获取下一场大奖赛（基于投影后的正赛时间，跨年自动顺延）。</summary>
    public F1CalendarEntry? GetNextRace()
    {
        var now = DateTime.UtcNow;
        return Calendar2025
            .Where(e => e.Race.HasValue)
            .OrderBy(e => ShiftToUpcoming(e.Race!.Value, now))
            .FirstOrDefault();
    }

    /// <summary>获取下一场赛程的倒计时信息（全局最近的一个未来会话，跨年自动顺延）。</summary>
    public (string Name, TimeSpan TimeToNext, string SessionLabel) GetNextSessionCountdown()
    {
        var now = DateTime.UtcNow;

        (string Name, DateTime Time, string Label)? best = null;
        foreach (var e in Calendar2025)
        {
            foreach (var (time, label) in EnumerateSessions(e))
            {
                var t = ShiftToUpcoming(time, now);
                if (best == null || t < best.Value.Time)
                    best = (e.GrandPrix, t, label);
            }
        }

        if (best == null)
            return ("", TimeSpan.Zero, "");

        return (best.Value.Name, best.Value.Time - now, best.Value.Label);
    }

    /// <summary>格式化倒计时文本。</summary>
    public static string FormatCountdown(TimeSpan ts)
    {
        if (ts.TotalSeconds <= 0) return "进行中";
        if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays}天 {ts.Hours:D2}:{ts.Minutes:D2}";
        return $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }
}
