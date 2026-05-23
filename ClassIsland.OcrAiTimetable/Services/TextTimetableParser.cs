using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ClassIsland.OcrAiTimetable.Models;

namespace ClassIsland.OcrAiTimetable.Services;

/// <summary>
/// 从纯文本解析课表，无需 AI。
/// 支持格式（每行一条）：
///   08:00-08:45 语文
///   课间 08:45-09:00
///   09:00-09:45
///   （空行或纯"课间"视为课间休息）
/// </summary>
public static class TextTimetableParser
{
    // 匹配形如 "08:00-08:45" 或 "8:00-8:45" 的时间段
    private static readonly Regex TimeRangeRegex =
        new(@"(\d{1,2}:\d{2})\s*[-–—~～至到]\s*(\d{1,2}:\d{2})", RegexOptions.Compiled);

    private static readonly Regex BreakKeyword =
        new(@"^(课间|休息|break|间隔|自习\s*\(.*\))", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static AiTimetableResponse Parse(string text)
    {
        var periods = new List<AiPeriod>();
        var classes = new List<string>();

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var m = TimeRangeRegex.Match(line);
            if (!m.Success) continue;

            var start = m.Groups[1].Value;
            var end = m.Groups[2].Value;

            // 去掉时间段本身，剩余文字作为科目名（或课间标识）
            var label = TimeRangeRegex.Replace(line, "").Trim().TrimEnd('-', '–', '—').Trim();

            bool isBreak = string.IsNullOrEmpty(label) || BreakKeyword.IsMatch(label);

            periods.Add(new AiPeriod
            {
                Start = NormalizeTime(start),
                End = NormalizeTime(end),
                Type = isBreak ? "break" : "class"
            });

            if (!isBreak)
                classes.Add(string.IsNullOrEmpty(label) ? "" : label);
        }

        if (periods.Count == 0)
            throw new InvalidOperationException("未能识别到任何时间段。请确保每行包含形如「08:00-08:45 语文」的格式。");

        return new AiTimetableResponse
        {
            TimeLayout = new AiTimeLayout { Name = "手动输入的时间表", Periods = periods },
            ClassPlan = new AiClassPlan { Name = "手动输入的课表", Classes = classes }
        };
    }

    // 将 "8:05" 补齐为 "08:05"
    private static string NormalizeTime(string t)
    {
        var parts = t.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out var h))
            return $"{h:D2}:{parts[1]}";
        return t;
    }
}
