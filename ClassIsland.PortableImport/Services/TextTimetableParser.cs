using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ClassIsland.PortableImport.Models;

namespace ClassIsland.PortableImport.Services;

/// <summary>
/// 把用户手动输入的课表文字解析成结构化的 <see cref="TimetableImportData"/>。
/// </summary>
public static class TextTimetableParser
{
    private record DaySection(string DayName, int? WeekDay, List<string> Lines);

    private static readonly Regex TimeRangeRegex =
        new(@"(\d{1,2}:\d{2})\s*[-–—~～至到]\s*(\d{1,2}:\d{2})", RegexOptions.Compiled);

    private static readonly Regex BreakKeyword =
        new(@"^(课间|休息|午休|午饭|午餐|大课间|break|间隔|自习\s*\(.*\))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex WeekDayRegex =
        new(@"星期([一二三四五六日天])|周([一二三四五六日天])|\b(monday|tuesday|wednesday|thursday|friday|saturday|sunday|mon|tue|wed|thu|fri|sat|sun)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Dictionary<char, int> ChineseDayMap = new()
    {
        ['一'] = 1, ['二'] = 2, ['三'] = 3, ['四'] = 4,
        ['五'] = 5, ['六'] = 6, ['日'] = 0, ['天'] = 0
    };

    private static readonly Dictionary<string, int> EnglishDayMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["monday"] = 1, ["mon"] = 1,
        ["tuesday"] = 2, ["tue"] = 2,
        ["wednesday"] = 3, ["wed"] = 3,
        ["thursday"] = 4, ["thu"] = 4,
        ["friday"] = 5, ["fri"] = 5,
        ["saturday"] = 6, ["sat"] = 6,
        ["sunday"] = 0, ["sun"] = 0
    };

    /// <summary>
    /// 解析整段课表文字。
    /// </summary>
    /// <exception cref="InvalidOperationException">未能识别到任何时间段时抛出。</exception>
    public static TimetableImportData Parse(string text)
    {
        var lines = text.Split('\n').Select(l => l.Trim()).ToList();
        var sections = SplitBySections(lines);

        // 汇总所有出现过的时间段，仅用于预览展示（按 起-止-类型 去重，最后按开始时间排序）；
        // 每一天实际使用的时间表以下面 ImportClassPlan.Periods 为准，不与其他天混用。
        var seen = new HashSet<string>();
        var periods = new List<ImportPeriod>();
        var classPlans = new List<ImportClassPlan>();
        foreach (var section in sections)
        {
            var (sectionPeriods, classes) = ParseLines(section.Lines);
            sectionPeriods.Sort((a, b) => string.Compare(a.Start, b.Start, StringComparison.Ordinal));

            foreach (var period in sectionPeriods)
            {
                if (seen.Add($"{period.Start}-{period.End}-{period.Type}"))
                {
                    periods.Add(period);
                }
            }

            if (classes.Count != 0 || section.WeekDay.HasValue)
            {
                classPlans.Add(new ImportClassPlan
                {
                    Name = section.WeekDay.HasValue ? section.DayName + "课表" : "手动输入的课表",
                    Classes = classes,
                    WeekDay = section.WeekDay,
                    DayName = section.DayName,
                    Periods = sectionPeriods
                });
            }
        }

        if (periods.Count == 0)
        {
            throw new InvalidOperationException("未能识别到任何时间段。请确保每行包含形如「08:00-08:45 语文」的格式。");
        }

        periods.Sort((a, b) => string.Compare(a.Start, b.Start, StringComparison.Ordinal));

        if (classPlans.Count == 0)
        {
            classPlans.Add(new ImportClassPlan { Name = "手动输入的课表" });
        }

        return new TimetableImportData
        {
            TimeLayout = new ImportTimeLayout
            {
                Name = "手动输入的时间表",
                Periods = periods
            },
            ClassPlans = classPlans
        };
    }

    /// <summary>
    /// 按星期标题把文本分段；若全文没有任何星期标题，则整体作为一段。
    /// </summary>
    private static List<DaySection> SplitBySections(List<string> lines)
    {
        if (!lines.Any(l => !string.IsNullOrEmpty(l) && TryParseWeekDay(l, out _)))
        {
            return
            [
                new DaySection("", null, lines.Where(l => !string.IsNullOrEmpty(l)).ToList())
            ];
        }

        var result = new List<DaySection>();
        var dayName = "";
        int? weekDay = null;
        var buffer = new List<string>();
        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            if (TryParseWeekDay(line, out var parsedWeekDay))
            {
                if (weekDay.HasValue || buffer.Count > 0)
                {
                    result.Add(new DaySection(dayName, weekDay, buffer));
                }
                dayName = line;
                weekDay = parsedWeekDay;
                buffer = new List<string>();
            }
            else
            {
                buffer.Add(line);
            }
        }

        if (weekDay.HasValue || buffer.Count > 0)
        {
            result.Add(new DaySection(dayName, weekDay, buffer));
        }

        return result;
    }

    /// <summary>
    /// 解析一段文本中的时间段与课程名。
    /// </summary>
    private static (List<ImportPeriod> periods, List<string> classes) ParseLines(List<string> lines)
    {
        var periods = new List<ImportPeriod>();
        var classes = new List<string>();
        foreach (var line in lines)
        {
            var match = TimeRangeRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var start = NormalizeTime(match.Groups[1].Value);
            var end = NormalizeTime(match.Groups[2].Value);
            var rest = TimeRangeRegex.Replace(line, "").Trim().TrimEnd('-', '–', '—').Trim();
            var isBreak = string.IsNullOrEmpty(rest) || BreakKeyword.IsMatch(rest);

            periods.Add(new ImportPeriod
            {
                Start = start,
                End = end,
                Type = isBreak ? "break" : "class"
            });

            if (!isBreak)
            {
                classes.Add(rest);
            }
        }

        return (periods, classes);
    }

    private static bool TryParseWeekDay(string line, out int weekDay)
    {
        weekDay = -1;
        if (TimeRangeRegex.IsMatch(line))
        {
            return false;
        }

        var match = WeekDayRegex.Match(line);
        if (!match.Success)
        {
            return false;
        }

        if (match.Groups[1].Success)
        {
            return ChineseDayMap.TryGetValue(match.Groups[1].Value[0], out weekDay);
        }
        if (match.Groups[2].Success)
        {
            return ChineseDayMap.TryGetValue(match.Groups[2].Value[0], out weekDay);
        }
        if (match.Groups[3].Success)
        {
            return EnglishDayMap.TryGetValue(match.Groups[3].Value, out weekDay);
        }
        return false;
    }

    private static string NormalizeTime(string t)
    {
        var parts = t.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out var hour))
        {
            return $"{hour:D2}:{parts[1]}";
        }
        return t;
    }
}
