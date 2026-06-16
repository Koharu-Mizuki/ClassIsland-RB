using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;
using CiProfile = ClassIsland.Shared.Models.Profile.Profile;
using Subject = ClassIsland.Shared.Models.Profile.Subject;
using TimeLayout = ClassIsland.Shared.Models.Profile.TimeLayout;
using TimeLayoutItem = ClassIsland.Shared.Models.Profile.TimeLayoutItem;
using ClassPlan = ClassIsland.Shared.Models.Profile.ClassPlan;
using ClassInfo = ClassIsland.Shared.Models.Profile.ClassInfo;

namespace ClassIsland.Models.OcrAiTimetable;

/// <summary>
/// 视觉模型识别课表图片后输出的结构化结果。字段名与提示词中要求模型返回的 JSON 一致。
/// </summary>
public class OcrTimetableResult
{
    /// <summary>
    /// 识别到的科目列表。
    /// </summary>
    [JsonPropertyName("subjects")]
    public List<OcrSubject> Subjects { get; set; } = [];

    /// <summary>
    /// 识别到的每日课表列表。
    /// </summary>
    [JsonPropertyName("schedules")]
    public List<OcrSchedule> Schedules { get; set; } = [];

    private static TimeSpan ParseTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TimeSpan.Zero;
        }

        // 兼容 "8:00"、"08:00"、"08:00:00" 等写法
        var formats = new[] { @"h\:mm", @"hh\:mm", @"h\:mm\:ss", @"hh\:mm\:ss" };
        if (TimeSpan.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, out var ts))
        {
            return ts;
        }

        return TimeSpan.TryParse(value.Trim(), CultureInfo.InvariantCulture, out ts) ? ts : TimeSpan.Zero;
    }

    /// <summary>
    /// 将识别结果转换为 ClassIsland 档案。逻辑参照 <see cref="ClassIsland.Shared.Extensions.CsesExtensions"/>：
    /// 复用时间相同的时间表，按科目名去重，每个每日课表生成一个课表项。
    /// </summary>
    /// <param name="mergeProfile">需要合并进的现有档案；为 <c>null</c> 时新建。</param>
    public CiProfile ToProfile(CiProfile? mergeProfile = null)
    {
        var result = mergeProfile ?? new CiProfile();
        var subjectsCache = new Dictionary<string, Guid>();

        foreach (var i in result.Subjects)
        {
            subjectsCache[i.Value.Name] = i.Key;
        }

        // 合并科目（按名称去重，已存在的补全教师/简称）
        foreach (var s in Subjects.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
        {
            if (!subjectsCache.TryGetValue(s.Name, out var id))
            {
                id = Guid.NewGuid();
                subjectsCache[s.Name] = id;
                result.Subjects[id] = new Subject();
            }

            result.Subjects[id].Name = s.Name;
            if (!string.IsNullOrWhiteSpace(s.Initial))
            {
                result.Subjects[id].Initial = s.Initial;
            }
            if (!string.IsNullOrWhiteSpace(s.Teacher))
            {
                result.Subjects[id].TeacherName = s.Teacher;
            }
        }

        foreach (var schedule in Schedules)
        {
            var classes = schedule.Classes.Where(x => !string.IsNullOrWhiteSpace(x.Subject)).ToList();
            if (classes.Count == 0)
            {
                continue;
            }

            // 找到时间点完全一致的现有时间表以复用，否则新建
            var timeLayoutKey = result.TimeLayouts.FirstOrDefault(x =>
            {
                var validItems = x.Value.Layouts.Where(y => y.TimeType == 0).ToList();
                if (validItems.Count != classes.Count)
                {
                    return false;
                }
                for (var j = 0; j < validItems.Count; j++)
                {
                    if (validItems[j].StartTime != ParseTime(classes[j].Start) ||
                        validItems[j].EndTime != ParseTime(classes[j].End))
                    {
                        return false;
                    }
                }
                return true;
            }).Key;

            var isNewTimeLayout = timeLayoutKey == Guid.Empty;
            var timeLayout = isNewTimeLayout ? new TimeLayout() : result.TimeLayouts[timeLayoutKey];
            if (isNewTimeLayout)
            {
                timeLayoutKey = Guid.NewGuid();
            }

            var classPlan = new ClassPlan();
            for (var j = 0; j < classes.Count; j++)
            {
                subjectsCache.TryGetValue(classes[j].Subject, out var subjectId);
                classPlan.Classes.Add(new ClassInfo
                {
                    SubjectId = subjectId
                });

                if (isNewTimeLayout)
                {
                    var start = ParseTime(classes[j].Start);
                    var end = ParseTime(classes[j].End);
                    timeLayout.Layouts.Add(new TimeLayoutItem
                    {
                        TimeType = 0,
                        StartTime = start,
                        EndTime = end
                    });
                    // 在相邻两节课之间补一个课间
                    if (j < classes.Count - 1)
                    {
                        timeLayout.Layouts.Add(new TimeLayoutItem
                        {
                            TimeType = 1,
                            StartTime = end,
                            EndTime = ParseTime(classes[j + 1].Start)
                        });
                    }
                }
            }

            if (isNewTimeLayout)
            {
                timeLayout.Name = string.IsNullOrWhiteSpace(schedule.Name) ? "AI 识别时间表" : $"{schedule.Name} 时间表";
                result.TimeLayouts[timeLayoutKey] = timeLayout;
            }

            classPlan.TimeLayoutId = timeLayoutKey;
            classPlan.TimeRule.WeekDay = schedule.DayOfWeek is >= 0 and <= 6 ? schedule.DayOfWeek : 0;
            classPlan.Name = string.IsNullOrWhiteSpace(schedule.Name)
                ? OcrSchedule.DayOfWeekToName(schedule.DayOfWeek)
                : schedule.Name;
            result.ClassPlans[Guid.NewGuid()] = classPlan;
        }

        return result;
    }
}

/// <summary>
/// 识别到的单个科目。
/// </summary>
public class OcrSubject
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("teacher")] public string Teacher { get; set; } = "";
    [JsonPropertyName("initial")] public string Initial { get; set; } = "";
}

/// <summary>
/// 识别到的某一天的课表。
/// </summary>
public class OcrSchedule
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";

    /// <summary>
    /// 星期几。0=周日，1=周一 …… 6=周六（与 <see cref="System.DayOfWeek"/> 一致）。
    /// </summary>
    [JsonPropertyName("dayOfWeek")] public int DayOfWeek { get; set; }

    [JsonPropertyName("classes")] public List<OcrClass> Classes { get; set; } = [];

    /// <summary>
    /// 将星期序号转为中文名称。
    /// </summary>
    public static string DayOfWeekToName(int day) => day switch
    {
        0 => "周日",
        1 => "周一",
        2 => "周二",
        3 => "周三",
        4 => "周四",
        5 => "周五",
        6 => "周六",
        _ => "课表"
    };
}

/// <summary>
/// 识别到的一节课。
/// </summary>
public class OcrClass
{
    [JsonPropertyName("subject")] public string Subject { get; set; } = "";

    /// <summary>开始时间，格式 <c>HH:mm</c>。</summary>
    [JsonPropertyName("start")] public string Start { get; set; } = "";

    /// <summary>结束时间，格式 <c>HH:mm</c>。</summary>
    [JsonPropertyName("end")] public string End { get; set; } = "";
}
