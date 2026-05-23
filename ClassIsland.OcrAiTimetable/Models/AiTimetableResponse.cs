using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ClassIsland.OcrAiTimetable.Models;

public class AiTimetableResponse
{
    [JsonPropertyName("timeLayout")]
    public AiTimeLayout TimeLayout { get; set; } = new();

    [JsonPropertyName("classPlan")]
    public AiClassPlan ClassPlan { get; set; } = new();
}

public class AiTimeLayout
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "导入的时间表";

    [JsonPropertyName("periods")]
    public List<AiPeriod> Periods { get; set; } = [];
}

public class AiPeriod
{
    // "08:00"
    [JsonPropertyName("start")]
    public string Start { get; set; } = "";

    // "08:45"
    [JsonPropertyName("end")]
    public string End { get; set; } = "";

    // "class" 或 "break"
    [JsonPropertyName("type")]
    public string Type { get; set; } = "class";
}

public class AiClassPlan
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "导入的课表";

    [JsonPropertyName("classes")]
    public List<string> Classes { get; set; } = [];
}
