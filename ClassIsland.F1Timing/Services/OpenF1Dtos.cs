using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClassIsland.F1Timing.Services;

// OpenF1 (https://openf1.org) 各端点的反序列化模型。字段名经真实接口验证。

internal sealed class SessionDto
{
    [JsonPropertyName("session_key")] public long SessionKey { get; set; }
    [JsonPropertyName("session_name")] public string? SessionName { get; set; }
    [JsonPropertyName("session_type")] public string? SessionType { get; set; }
    [JsonPropertyName("date_start")] public DateTimeOffset? DateStart { get; set; }
    [JsonPropertyName("date_end")] public DateTimeOffset? DateEnd { get; set; }
    [JsonPropertyName("circuit_short_name")] public string? CircuitShortName { get; set; }
    [JsonPropertyName("country_name")] public string? CountryName { get; set; }
    [JsonPropertyName("location")] public string? Location { get; set; }
    [JsonPropertyName("year")] public int Year { get; set; }
}

internal sealed class DriverDto
{
    [JsonPropertyName("driver_number")] public int DriverNumber { get; set; }
    [JsonPropertyName("name_acronym")] public string? NameAcronym { get; set; }
    [JsonPropertyName("full_name")] public string? FullName { get; set; }
    [JsonPropertyName("team_name")] public string? TeamName { get; set; }
    [JsonPropertyName("team_colour")] public string? TeamColour { get; set; }
}

internal sealed class PositionDto
{
    [JsonPropertyName("driver_number")] public int DriverNumber { get; set; }
    [JsonPropertyName("position")] public int? Position { get; set; }
    [JsonPropertyName("date")] public DateTimeOffset Date { get; set; }
}

internal sealed class IntervalDto
{
    [JsonPropertyName("driver_number")] public int DriverNumber { get; set; }
    // gap_to_leader / interval 多数为数字（秒），被套圈时为字符串（如 "+1 LAP"），故用 JsonElement。
    [JsonPropertyName("gap_to_leader")] public JsonElement GapToLeader { get; set; }
    [JsonPropertyName("interval")] public JsonElement Interval { get; set; }
    [JsonPropertyName("date")] public DateTimeOffset Date { get; set; }
}

internal sealed class LapDto
{
    [JsonPropertyName("driver_number")] public int DriverNumber { get; set; }
    [JsonPropertyName("lap_number")] public int LapNumber { get; set; }
    [JsonPropertyName("lap_duration")] public double? LapDuration { get; set; }
    [JsonPropertyName("is_pit_out_lap")] public bool IsPitOutLap { get; set; }
    [JsonPropertyName("date_start")] public DateTimeOffset? DateStart { get; set; }
    [JsonPropertyName("duration_sector_1")] public double? DurationSector1 { get; set; }
    [JsonPropertyName("duration_sector_2")] public double? DurationSector2 { get; set; }
    [JsonPropertyName("duration_sector_3")] public double? DurationSector3 { get; set; }
}

internal sealed class StintDto
{
    [JsonPropertyName("driver_number")] public int DriverNumber { get; set; }
    [JsonPropertyName("stint_number")] public int StintNumber { get; set; }
    [JsonPropertyName("compound")] public string? Compound { get; set; }
    [JsonPropertyName("lap_start")] public int LapStart { get; set; }
    [JsonPropertyName("lap_end")] public int LapEnd { get; set; }
    [JsonPropertyName("tyre_age_at_start")] public int TyreAgeAtStart { get; set; }
}

internal sealed class RaceControlDto
{
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("flag")] public string? Flag { get; set; }
    [JsonPropertyName("scope")] public string? Scope { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("date")] public DateTimeOffset Date { get; set; }
}

internal sealed class CarDataDto
{
    [JsonPropertyName("driver_number")] public int DriverNumber { get; set; }
    // DRS：0/1=关，8=可用，10/12/14=开启。
    [JsonPropertyName("drs")] public int Drs { get; set; }
    [JsonPropertyName("date")] public DateTimeOffset Date { get; set; }
}
