using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ClassIsland.F1Timing.Models;
using Microsoft.Extensions.Logging;

namespace ClassIsland.F1Timing.Services;

/// <summary>
/// 从 Jolpica/Ergast API 拉取 F1 积分榜数据。
/// 数据缓存 1 小时，避免频繁请求。
/// </summary>
public sealed class ErgastStandingsService
{
    private const string BaseUrl = "https://api.jolpi.ca/ergast/f1/";
    private const int CacheMinutes = 60;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly ILogger<ErgastStandingsService>? _logger;

    private List<F1StandingEntry>? _driverCache;
    private List<F1ConstructorStandingEntry>? _constructorCache;
    private DateTime _lastFetch = DateTime.MinValue;

    public ErgastStandingsService(ILogger<ErgastStandingsService>? logger = null)
    {
        _logger = logger;
        _http = new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("ClassIsland-F1Timing/1.0");
    }

    /// <summary>获取车手积分榜（缓存 1 小时）。</summary>
    public async Task<List<F1StandingEntry>> GetDriverStandingsAsync(CancellationToken ct = default)
    {
        if (_driverCache != null && (DateTime.Now - _lastFetch).TotalMinutes < CacheMinutes)
            return _driverCache;

        try
        {
            var json = await _http.GetStringAsync("current/driverStandings.json", ct);
            var root = JsonSerializer.Deserialize<ErgastRoot>(json, JsonOpts);
            var standings = root?.MrData?.StandingsTable?.StandingsLists;
            if (standings == null || standings.Count == 0)
                return _driverCache ?? new List<F1StandingEntry>();

            var list = new List<F1StandingEntry>();
            foreach (var s in standings[0].DriverStandings ?? new())
            {
                var d = s.Driver;
                var constructors = s.Constructors;
                list.Add(new F1StandingEntry
                {
                    Position = int.TryParse(s.PositionText, out var p) ? p : 0,
                    Tla = d?.Code ?? d?.DriverId?.Substring(0, Math.Min(3, d.DriverId?.Length ?? 0)).ToUpper() ?? "",
                    FullName = $"{d?.GivenName} {d?.FamilyName}",
                    Nationality = d?.Nationality ?? "",
                    TeamName = constructors?.Count > 0 ? constructors[0].Name : "",
                    TeamColour = "#888888",
                    Points = int.TryParse(s.Points, out var pts) ? pts : 0,
                    Wins = int.TryParse(s.Wins, out var w) ? w : 0
                });
            }
            _driverCache = list;
            _lastFetch = DateTime.Now;
            return list;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "获取车手积分榜失败");
            return _driverCache ?? new List<F1StandingEntry>();
        }
    }

    /// <summary>获取车队积分榜（缓存 1 小时）。</summary>
    public async Task<List<F1ConstructorStandingEntry>> GetConstructorStandingsAsync(CancellationToken ct = default)
    {
        if (_constructorCache != null && (DateTime.Now - _lastFetch).TotalMinutes < CacheMinutes)
            return _constructorCache;

        try
        {
            var json = await _http.GetStringAsync("current/constructorStandings.json", ct);
            var root = JsonSerializer.Deserialize<ErgastRoot>(json, JsonOpts);
            var standings = root?.MrData?.StandingsTable?.StandingsLists;
            if (standings == null || standings.Count == 0)
                return _constructorCache ?? new List<F1ConstructorStandingEntry>();

            var list = new List<F1ConstructorStandingEntry>();
            foreach (var s in standings[0].ConstructorStandings ?? new())
            {
                var c = s.Constructor;
                list.Add(new F1ConstructorStandingEntry
                {
                    Position = int.TryParse(s.PositionText, out var p) ? p : 0,
                    TeamName = c?.Name ?? "",
                    TeamColour = "#888888",
                    Points = int.TryParse(s.Points, out var pts) ? pts : 0,
                    Wins = int.TryParse(s.Wins, out var w) ? w : 0
                });
            }
            _constructorCache = list;
            _lastFetch = DateTime.Now;
            return list;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "获取车队积分榜失败");
            return _constructorCache ?? new List<F1ConstructorStandingEntry>();
        }
    }

    /// <summary>强制刷新缓存。</summary>
    public void InvalidateCache()
    {
        _driverCache = null;
        _constructorCache = null;
        _lastFetch = DateTime.MinValue;
    }

    // —— Ergast JSON DTOs ——

    private sealed class ErgastRoot
    {
        [JsonPropertyName("MRData")]
        public ErgastMrData? MrData { get; set; }
    }

    private sealed class ErgastMrData
    {
        [JsonPropertyName("StandingsTable")]
        public ErgastStandingsTable? StandingsTable { get; set; }
    }

    private sealed class ErgastStandingsTable
    {
        [JsonPropertyName("StandingsLists")]
        public List<ErgastStandingsList>? StandingsLists { get; set; }
    }

    private sealed class ErgastStandingsList
    {
        [JsonPropertyName("DriverStandings")]
        public List<ErgastDriverStanding>? DriverStandings { get; set; }

        [JsonPropertyName("ConstructorStandings")]
        public List<ErgastConstructorStanding>? ConstructorStandings { get; set; }
    }

    private sealed class ErgastDriverStanding
    {
        [JsonPropertyName("positionText")]
        public string? PositionText { get; set; }

        [JsonPropertyName("points")]
        public string? Points { get; set; }

        [JsonPropertyName("wins")]
        public string? Wins { get; set; }

        [JsonPropertyName("Driver")]
        public ErgastDriver? Driver { get; set; }

        [JsonPropertyName("Constructors")]
        public List<ErgastConstructor>? Constructors { get; set; }
    }

    private sealed class ErgastConstructorStanding
    {
        [JsonPropertyName("positionText")]
        public string? PositionText { get; set; }

        [JsonPropertyName("points")]
        public string? Points { get; set; }

        [JsonPropertyName("wins")]
        public string? Wins { get; set; }

        [JsonPropertyName("Constructor")]
        public ErgastConstructor? Constructor { get; set; }
    }

    private sealed class ErgastDriver
    {
        [JsonPropertyName("driverId")]
        public string? DriverId { get; set; }

        [JsonPropertyName("givenName")]
        public string? GivenName { get; set; }

        [JsonPropertyName("familyName")]
        public string? FamilyName { get; set; }

        [JsonPropertyName("nationality")]
        public string? Nationality { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }
    }

    private sealed class ErgastConstructor
    {
        [JsonPropertyName("constructorId")]
        public string? ConstructorId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("nationality")]
        public string? Nationality { get; set; }
    }
}
