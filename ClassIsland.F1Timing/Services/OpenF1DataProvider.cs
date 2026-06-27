using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClassIsland.F1Timing.Models;

namespace ClassIsland.F1Timing.Services;

/// <summary>
/// 基于 OpenF1 REST API 的数据源。内部维护增量游标与每位车手的累积状态，
/// 每次拉取仅请求自上次以来的新数据，再合并出完整快照。
/// </summary>
public sealed class OpenF1DataProvider : IF1DataProvider
{
    private const string BaseUrl = "https://api.openf1.org/v1/";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    // 赛道短名 → 比赛总圈数（OpenF1 不直接提供），覆盖当前赛历主要赛道。
    private static readonly Dictionary<string, int> TrackLaps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Sakhir"] = 57, ["Jeddah"] = 50, ["Melbourne"] = 58, ["Suzuka"] = 53,
        ["Shanghai"] = 56, ["Miami"] = 57, ["Imola"] = 63, ["Monaco"] = 78,
        ["Catalunya"] = 66, ["Montreal"] = 70, ["Spielberg"] = 71, ["Silverstone"] = 52,
        ["Hungaroring"] = 70, ["Spa-Francorchamps"] = 44, ["Zandvoort"] = 72, ["Monza"] = 53,
        ["Baku"] = 51, ["Singapore"] = 62, ["Austin"] = 56, ["Mexico City"] = 71,
        ["Interlagos"] = 71, ["Las Vegas"] = 50, ["Lusail"] = 57, ["Yas Marina"] = 58
    };

    private readonly HttpClient _http;

    // —— 累积状态（跨轮询）——
    private long _sessionKey;
    private readonly Dictionary<int, F1DriverTiming> _rows = new();
    private readonly Dictionary<int, double> _bestLap = new();
    private readonly double[] _bestSector = { double.MaxValue, double.MaxValue, double.MaxValue };
    private readonly Dictionary<int, double[]> _driverBestSector = new();
    private DateTime _cursorUtc;
    private int _currentLap;
    private double _overallFastest = double.MaxValue;
    private int _overallFastestDriver = -1;
    private TrackFlag _flag = TrackFlag.None;

    public OpenF1DataProvider()
    {
        _http = new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("ClassIsland-F1Timing/1.0");
    }

    public string Id => "openf1";

    public string DisplayName => "OpenF1";

    public void Reset()
    {
        _sessionKey = 0;
        _rows.Clear();
        _bestLap.Clear();
        Array.Fill(_bestSector, double.MaxValue);
        _driverBestSector.Clear();
        _cursorUtc = DateTime.MinValue;
        _currentLap = 0;
        _overallFastest = double.MaxValue;
        _overallFastestDriver = -1;
        _flag = TrackFlag.None;
    }

    public async Task<F1FetchResult> FetchAsync(CancellationToken ct)
    {
        var result = new F1FetchResult();
        try
        {
            // 1) 当前会话
            var sessions = await GetListAsync<SessionDto>("sessions?session_key=latest", ct);
            var session = sessions.LastOrDefault();
            if (session == null)
            {
                result.Ok = false;
                result.StatusMessage = "暂时无法获取赛事信息";
                result.Drivers = SortedRows();
                return result;
            }

            // 会话变更 → 重置并加载车手基础信息
            if (session.SessionKey != _sessionKey)
            {
                Reset();
                _sessionKey = session.SessionKey;
                var drivers = await GetListAsync<DriverDto>($"drivers?session_key={_sessionKey}", ct);
                foreach (var d in drivers)
                {
                    _rows[d.DriverNumber] = new F1DriverTiming
                    {
                        DriverNumber = d.DriverNumber,
                        Tla = d.NameAcronym ?? d.DriverNumber.ToString(),
                        FullName = d.FullName ?? "",
                        TeamName = d.TeamName ?? "",
                        TeamColour = NormalizeColour(d.TeamColour)
                    };
                }
                _cursorUtc = session.DateStart?.UtcDateTime ?? DateTime.UtcNow.AddMinutes(-30);
            }

            var now = DateTime.UtcNow;
            var cursorIso = Iso(_cursorUtc);
            var lapCursorIso = Iso(_cursorUtc.AddSeconds(-120)); // 圈速跨界圈需更大重叠
            var ivCursorIso = Iso(now.AddMinutes(-3));           // 间隔只需最近瞬时值
            // 2) 位次先顺序拿（关键数据，确保第一个请求完整，不与其他竞争限速）
            var positions = await GetListAsync<PositionDto>(
                $"position?session_key={_sessionKey}&date>={cursorIso}", ct);

            // 3-7) 其余五类并行拉取，省去 4 个 RTT
            // 注：stints 端点无 date 字段，只能按 session_key 全量拉（数据量小）
            var lapsTask    = GetListAsync<LapDto>($"laps?session_key={_sessionKey}&date_start>={lapCursorIso}", ct);
            var ivTask      = GetListAsync<IntervalDto>($"intervals?session_key={_sessionKey}&date>={ivCursorIso}", ct);
            var stintsTask  = GetListAsync<StintDto>($"stints?session_key={_sessionKey}", ct);
            var carDataTask = GetListAsync<CarDataDto>($"car_data?session_key={_sessionKey}&date>={Iso(now.AddSeconds(-6))}", ct);
            var rcsTask     = GetListAsync<RaceControlDto>($"race_control?session_key={_sessionKey}&date>={cursorIso}", ct);

            await Task.WhenAll(lapsTask, ivTask, stintsTask, carDataTask, rcsTask);

            var laps      = lapsTask.Result;
            var intervals = ivTask.Result;
            var stints    = stintsTask.Result;
            var carData   = carDataTask.Result;
            var rcs       = rcsTask.Result;

            // 位次
            foreach (var g in positions.GroupBy(p => p.DriverNumber))
            {
                var latest = g.OrderBy(p => p.Date).Last();
                if (latest.Position is > 0 && _rows.TryGetValue(g.Key, out var row))
                    row.Position = latest.Position.Value;
            }

            // 3) 圈速 + 分段（增量）
            foreach (var g in laps.GroupBy(l => l.DriverNumber))
            {
                if (!_rows.TryGetValue(g.Key, out var row)) continue;
                if (!_driverBestSector.TryGetValue(g.Key, out var dbs))
                {
                    dbs = new[] { double.MaxValue, double.MaxValue, double.MaxValue };
                    _driverBestSector[g.Key] = dbs;
                }

                LapDto? lastTimed = null;
                foreach (var lap in g.OrderBy(l => l.LapNumber))
                {
                    if (lap.LapNumber > _currentLap) _currentLap = lap.LapNumber;
                    UpdateSectorBest(0, lap.DurationSector1, dbs);
                    UpdateSectorBest(1, lap.DurationSector2, dbs);
                    UpdateSectorBest(2, lap.DurationSector3, dbs);
                    if (lap is { LapDuration: > 0, IsPitOutLap: false })
                    {
                        var v = lap.LapDuration!.Value;
                        if (!_bestLap.TryGetValue(g.Key, out var best) || v < best)
                            _bestLap[g.Key] = v;
                        if (v < _overallFastest)
                        {
                            _overallFastest = v;
                            _overallFastestDriver = g.Key;
                        }
                        lastTimed = lap;
                    }
                }

                if (lastTimed != null)
                {
                    if (lastTimed.LapDuration is > 0)
                    {
                        row.LastLapTime = FormatLap(lastTimed.LapDuration.Value);
                        row.IsPersonalBest = _bestLap.TryGetValue(g.Key, out var pb) &&
                                             Math.Abs(pb - lastTimed.LapDuration.Value) < 0.0005;
                    }
                    ApplySector(row, 0, lastTimed.DurationSector1, dbs);
                    ApplySector(row, 1, lastTimed.DurationSector2, dbs);
                    ApplySector(row, 2, lastTimed.DurationSector3, dbs);
                }
                if (_bestLap.TryGetValue(g.Key, out var bl))
                    row.BestLapTime = FormatLap(bl);
            }
            foreach (var row in _rows.Values)
                row.IsOverallFastest = row.DriverNumber == _overallFastestDriver;

            // 4) 间隔
            foreach (var g in intervals.GroupBy(i => i.DriverNumber))
            {
                if (!_rows.TryGetValue(g.Key, out var row)) continue;
                var latest = g.OrderBy(i => i.Date).Last();
                row.GapToLeader = FormatDelta(latest.GapToLeader);
                row.Interval = FormatDelta(latest.Interval);
            }

            // 5) 轮胎与进站（增量合并，按车手取最新 stint）
            foreach (var g in stints.GroupBy(s => s.DriverNumber))
            {
                if (!_rows.TryGetValue(g.Key, out var row)) continue;
                var ordered = g.OrderBy(s => s.StintNumber).ToList();
                var current = ordered.Last();
                row.Tyre = ParseCompound(current.Compound);
                row.PitStops = Math.Max(0, ordered.Count - 1);
                row.StintLaps = Math.Max(0, _currentLap - current.LapStart + 1);
                row.TyreAge = current.TyreAgeAtStart + Math.Max(0, _currentLap - current.LapStart);
            }

            // 6) DRS
            foreach (var g in carData.GroupBy(c => c.DriverNumber))
            {
                if (!_rows.TryGetValue(g.Key, out var row)) continue;
                row.Drs = g.OrderBy(c => c.Date).Last().Drs >= 10;
            }

            // 7) 旗帜 / 安全车（增量状态机）
            foreach (var rc in rcs.OrderBy(r => r.Date))
                ApplyRaceControl(rc);

            // 推进游标（留 5 秒重叠防漏）
            _cursorUtc = now.AddSeconds(-5);

            // 组装结果
            result.Ok = true;
            result.SessionName = session.SessionName ?? "";
            result.SessionType = ParseSessionType(session.SessionType);
            result.CircuitName = session.CircuitShortName ?? session.Location ?? "";
            result.CountryName = session.CountryName ?? "";
            result.CurrentLap = _currentLap;
            result.TotalLaps = TrackLaps.GetValueOrDefault(session.CircuitShortName ?? "", 0);
            result.Flag = _flag;

            var endUtc = session.DateEnd?.UtcDateTime ?? now;
            var startUtc = session.DateStart?.UtcDateTime ?? now;
            result.IsLive = now >= startUtc.AddMinutes(-5) && now <= endUtc.AddMinutes(20);
            result.StatusMessage = result.IsLive
                ? ""
                : $"显示最近一场：{session.CountryName} {session.SessionName}（已结束）";
            result.Drivers = SortedRows();
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.Ok = false;
            result.StatusMessage = $"数据获取失败：{ex.Message}";
            result.Drivers = SortedRows();
            return result;
        }
    }

    private List<F1DriverTiming> SortedRows() =>
        _rows.Values
            .Where(r => r.Position > 0)
            .OrderBy(r => r.Position)
            .ToList();

    private void UpdateSectorBest(int i, double? val, double[] dbs)
    {
        if (val is not > 0) return;
        var v = val.Value;
        if (v < dbs[i]) dbs[i] = v;
        if (v < _bestSector[i]) _bestSector[i] = v;
    }

    private void ApplySector(F1DriverTiming row, int i, double? val, double[] dbs)
    {
        if (val is not > 0) return;
        var v = val.Value;
        var time = v.ToString("0.000", CultureInfo.InvariantCulture);
        var state = v <= _bestSector[i] + 0.0005 ? SectorState.Purple
            : v <= dbs[i] + 0.0005 ? SectorState.Green
            : SectorState.Yellow;
        switch (i)
        {
            case 0: row.Sector1 = time; row.Sector1State = state; break;
            case 1: row.Sector2 = time; row.Sector2State = state; break;
            case 2: row.Sector3 = time; row.Sector3State = state; break;
        }
    }

    private async Task<List<T>> GetListAsync<T>(string relative, CancellationToken ct)
    {
        using var resp = await _http.GetAsync(relative, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode)
            return new List<T>();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        try
        {
            var list = await JsonSerializer.DeserializeAsync<List<T>>(stream, JsonOpts, ct);
            return list ?? new List<T>();
        }
        catch (JsonException)
        {
            // 端点返回非数组格式（如错误对象）时静默忽略
            return new List<T>();
        }
    }

    private void ApplyRaceControl(RaceControlDto rc)
    {
        var cat = rc.Category ?? "";
        var msg = (rc.Message ?? "").ToUpperInvariant();
        if (cat.Equals("SafetyCar", StringComparison.OrdinalIgnoreCase))
        {
            var isVirtual = msg.Contains("VIRTUAL") || msg.Contains("VSC");
            if (msg.Contains("ENDING") || msg.Contains("IN THIS LAP"))
            {
                _flag = TrackFlag.Green;
            }
            else if (msg.Contains("DEPLOYED") || msg.Contains("DECLARED"))
            {
                _flag = isVirtual ? TrackFlag.VirtualSafetyCar : TrackFlag.SafetyCar;
            }
            return;
        }
        if (cat.Equals("Flag", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(rc.Scope, "Track", StringComparison.OrdinalIgnoreCase))
        {
            _flag = (rc.Flag ?? "").ToUpperInvariant() switch
            {
                "GREEN" => TrackFlag.Green,
                "CLEAR" => TrackFlag.Green,
                "YELLOW" => TrackFlag.Yellow,
                "DOUBLE YELLOW" => TrackFlag.DoubleYellow,
                "RED" => TrackFlag.Red,
                "CHEQUERED" => TrackFlag.Chequered,
                _ => _flag
            };
        }
    }

    private static string Iso(DateTime utc) =>
        utc.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    private static string NormalizeColour(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "#888888";
        raw = raw.Trim();
        return raw.StartsWith('#') ? raw : "#" + raw;
    }

    private static string FormatDelta(JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Number:
                var d = e.GetDouble();
                return d <= 0 ? "" : "+" + d.ToString("0.000", CultureInfo.InvariantCulture);
            case JsonValueKind.String:
                return e.GetString() ?? "";
            default:
                return "";
        }
    }

    private static string FormatLap(double seconds)
    {
        if (seconds <= 0) return "";
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalSeconds >= 60
            ? $"{(int)ts.TotalMinutes}:{ts.Seconds:00}.{ts.Milliseconds:000}"
            : $"{ts.Seconds}.{ts.Milliseconds:000}";
    }

    private static TyreCompound ParseCompound(string? c) => (c ?? "").ToUpperInvariant() switch
    {
        "SOFT" => TyreCompound.Soft,
        "MEDIUM" => TyreCompound.Medium,
        "HARD" => TyreCompound.Hard,
        "INTERMEDIATE" => TyreCompound.Intermediate,
        "WET" => TyreCompound.Wet,
        _ => TyreCompound.Unknown
    };

    private static SessionType ParseSessionType(string? t)
    {
        var s = (t ?? "").ToLowerInvariant();
        if (s.Contains("race")) return SessionType.Race;
        if (s.Contains("sprint")) return s.Contains("qual") ? SessionType.SprintQualifying : SessionType.Sprint;
        if (s.Contains("qual")) return SessionType.Qualifying;
        if (s.Contains("practice")) return SessionType.Practice;
        return SessionType.Unknown;
    }
}
