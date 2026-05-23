using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ClassIsland.PortableImport.Models;

namespace ClassIsland.PortableImport.Services;

public static class TextTimetableParser
{
    private static readonly Regex TimeRangeRegex =
        new(@"(\d{1,2}:\d{2})\s*[-–—~～至到]\s*(\d{1,2}:\d{2})", RegexOptions.Compiled);

    private static readonly Regex BreakKeyword =
        new(@"^(课间|休息|午休|午饭|午餐|大课间|break|间隔|自习\s*\(.*\))", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex WeekDayRegex = new(
        @"星期([一二三四五六日天])|周([一二三四五六日天])|\b(monday|tuesday|wednesday|thursday|friday|saturday|sunday|mon|tue|wed|thu|fri|sat|sun)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Dictionary<char, int> ChineseDayMap = new()
    {
        ['一'] = 1, ['二'] = 2, ['三'] = 3, ['四'] = 4,
        ['五'] = 5, ['六'] = 6, ['日'] = 0, ['天'] = 0
    };

    private static readonly Dictionary<string, int> EnglishDayMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["monday"] = 1, ["mon"] = 1, ["tuesday"] = 2, ["tue"] = 2,
        ["wednesday"] = 3, ["wed"] = 3, ["thursday"] = 4, ["thu"] = 4,
        ["friday"] = 5, ["fri"] = 5, ["saturday"] = 6, ["sat"] = 6,
        ["sunday"] = 0, ["sun"] = 0,
    };

    private record DaySection(string DayName, int? WeekDay, List<string> Lines);

    public static TimetableImportData Parse(string text)
    {
        var allLines = text.Split('\n').Select(l => l.Trim()).ToList();
        var sections = SplitBySections(allLines);

        var seenKeys = new HashSet<string>();
        var allPeriods = new List<ImportPeriod>();
        foreach (var s in sections)
        {
            var (periods, _) = ParseLines(s.Lines);
            foreach (var p in periods)
                if (seenKeys.Add($"{p.Start}-{p.End}-{p.Type}"))
                    allPeriods.Add(p);
        }

        if (allPeriods.Count == 0)
            throw new InvalidOperationException("未能识别到任何时间段。请确保每行包含形如「08:00-08:45 语文」的格式。");

        allPeriods.Sort((a, b) => string.Compare(a.Start, b.Start, StringComparison.Ordinal));

        var classPlanList = new List<ImportClassPlan>();
        foreach (var s in sections)
        {
            var (_, classes) = ParseLines(s.Lines);
            if (classes.Count == 0 && !s.WeekDay.HasValue) continue;
            classPlanList.Add(new ImportClassPlan
            {
                Name = s.WeekDay.HasValue ? $"{s.DayName}课表" : "手动输入的课表",
                Classes = classes,
                WeekDay = s.WeekDay,
                DayName = s.DayName
            });
        }

        if (classPlanList.Count == 0)
            classPlanList.Add(new ImportClassPlan { Name = "手动输入的课表" });

        return new TimetableImportData
        {
            TimeLayout = new ImportTimeLayout { Name = "手动输入的时间表", Periods = allPeriods },
            ClassPlans = classPlanList
        };
    }

    private static List<DaySection> SplitBySections(List<string> lines)
    {
        bool hasAnyDayHeader = lines.Any(l => !string.IsNullOrEmpty(l) && TryParseWeekDay(l, out _));
        if (!hasAnyDayHeader)
            return [new DaySection("", null, lines.Where(l => !string.IsNullOrEmpty(l)).ToList())];

        var sections = new List<DaySection>();
        string currentDayName = "";
        int? currentWeekDay = null;
        var currentLines = new List<string>();

        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line)) continue;
            if (TryParseWeekDay(line, out int wd))
            {
                if (currentWeekDay.HasValue || currentLines.Count > 0)
                    sections.Add(new DaySection(currentDayName, currentWeekDay, currentLines));
                currentDayName = line;
                currentWeekDay = wd;
                currentLines = [];
            }
            else
            {
                currentLines.Add(line);
            }
        }
        if (currentWeekDay.HasValue || currentLines.Count > 0)
            sections.Add(new DaySection(currentDayName, currentWeekDay, currentLines));

        return sections;
    }

    private static (List<ImportPeriod> periods, List<string> classes) ParseLines(List<string> lines)
    {
        var periods = new List<ImportPeriod>();
        var classes = new List<string>();
        foreach (var line in lines)
        {
            var m = TimeRangeRegex.Match(line);
            if (!m.Success) continue;
            var start = NormalizeTime(m.Groups[1].Value);
            var end = NormalizeTime(m.Groups[2].Value);
            var label = TimeRangeRegex.Replace(line, "").Trim().TrimEnd('-', '–', '—').Trim();
            bool isBreak = string.IsNullOrEmpty(label) || BreakKeyword.IsMatch(label);
            periods.Add(new ImportPeriod { Start = start, End = end, Type = isBreak ? "break" : "class" });
            if (!isBreak) classes.Add(label);
        }
        return (periods, classes);
    }

    private static bool TryParseWeekDay(string line, out int weekDay)
    {
        weekDay = -1;
        if (TimeRangeRegex.IsMatch(line)) return false;
        var m = WeekDayRegex.Match(line);
        if (!m.Success) return false;
        if (m.Groups[1].Success) return ChineseDayMap.TryGetValue(m.Groups[1].Value[0], out weekDay);
        if (m.Groups[2].Success) return ChineseDayMap.TryGetValue(m.Groups[2].Value[0], out weekDay);
        if (m.Groups[3].Success) return EnglishDayMap.TryGetValue(m.Groups[3].Value, out weekDay);
        return false;
    }

    private static string NormalizeTime(string t)
    {
        var parts = t.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out var h))
            return $"{h:D2}:{parts[1]}";
        return t;
    }
}
