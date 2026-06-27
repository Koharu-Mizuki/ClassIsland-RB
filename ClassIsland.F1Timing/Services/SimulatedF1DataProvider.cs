using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClassIsland.F1Timing.Models;

namespace ClassIsland.F1Timing.Services;

/// <summary>
/// 模拟比赛数据源（演示 / 测试用）。无需网络，产出一场不断推进的虚拟正赛：
/// 位次变化、进站换胎、最快圈、安全车 / 旗帜、天气演变、车队无线电等，
/// 让全部功能在没有真实比赛时也能演示与验证。每次 <see cref="FetchAsync"/> 推进一圈。
/// </summary>
public sealed class SimulatedF1DataProvider : IF1DataProvider
{
    public string Id => "simulated";
    public string DisplayName => "🎬 模拟比赛（演示）";

    private const int TotalLaps = 57;
    private const int BaseLapMs = 90_000; // 1:30.000

    // 2026 赛季官方确认阵容（22 车手 / 11 车队，含 Audi 接管 Sauber、Cadillac 新加入）。
    // 车号据官方：诺里斯 2025 夺冠挂 1 号，维斯塔潘改 3 号。
    private static readonly (int Num, string Tla, string Name, string Team, string Colour)[] Grid =
    {
        (1,  "NOR", "Lando Norris",      "McLaren",         "#FF8000"),
        (81, "PIA", "Oscar Piastri",     "McLaren",         "#FF8000"),
        (16, "LEC", "Charles Leclerc",   "Ferrari",         "#E8002D"),
        (44, "HAM", "Lewis Hamilton",    "Ferrari",         "#E8002D"),
        (3,  "VER", "Max Verstappen",    "Red Bull Racing", "#3671C6"),
        (6,  "HAD", "Isack Hadjar",      "Red Bull Racing", "#3671C6"),
        (63, "RUS", "George Russell",    "Mercedes",        "#27F4D2"),
        (12, "ANT", "Kimi Antonelli",    "Mercedes",        "#27F4D2"),
        (23, "ALB", "Alexander Albon",   "Williams",        "#64C4FF"),
        (55, "SAI", "Carlos Sainz",      "Williams",        "#64C4FF"),
        (14, "ALO", "Fernando Alonso",   "Aston Martin",    "#229971"),
        (18, "STR", "Lance Stroll",      "Aston Martin",    "#229971"),
        (30, "LAW", "Liam Lawson",       "Racing Bulls",    "#6C98FF"),
        (41, "LIN", "Arvid Lindblad",    "Racing Bulls",    "#6C98FF"),
        (10, "GAS", "Pierre Gasly",      "Alpine",          "#0093CC"),
        (43, "COL", "Franco Colapinto",  "Alpine",          "#0093CC"),
        (31, "OCO", "Esteban Ocon",      "Haas",            "#B6BABD"),
        (87, "BEA", "Oliver Bearman",    "Haas",            "#B6BABD"),
        (27, "HUL", "Nico Hülkenberg",   "Audi",            "#F50537"),
        (5,  "BOR", "Gabriel Bortoleto", "Audi",            "#F50537"),
        (11, "PER", "Sergio Pérez",      "Cadillac",        "#16284B"),
        (77, "BOT", "Valtteri Bottas",   "Cadillac",        "#16284B"),
    };

    private readonly Random _rng = new();

    private List<int> _order = new();
    private TyreCompound[] _tyre = Array.Empty<TyreCompound>();
    private int[] _stint = Array.Empty<int>();
    private int[] _tyreAge = Array.Empty<int>();
    private int[] _pits = Array.Empty<int>();
    private int[] _lastMs = Array.Empty<int>();
    private int[] _bestMs = Array.Empty<int>();
    private bool[] _inPit = Array.Empty<bool>();
    private bool[] _retired = Array.Empty<bool>();

    private int _lap;
    private int _scLapsLeft;
    private bool _drsAnnounced;
    private int _overallBestMs;
    private int _overallBestIdx = -1;

    private double _airTemp, _trackTemp, _humidity, _wind, _rainProb;
    private bool _raining;

    public void Reset()
    {
        var n = Grid.Length;
        _order = Enumerable.Range(0, n).ToList();
        _tyre = new TyreCompound[n];
        _stint = new int[n];
        _tyreAge = new int[n];
        _pits = new int[n];
        _lastMs = new int[n];
        _bestMs = new int[n];
        _inPit = new bool[n];
        _retired = new bool[n];
        for (var i = 0; i < n; i++)
        {
            _tyre[i] = i < 10 ? TyreCompound.Soft : TyreCompound.Medium;
            _bestMs[i] = int.MaxValue;
        }
        _lap = 0;
        _scLapsLeft = 0;
        _drsAnnounced = false;
        _overallBestMs = int.MaxValue;
        _overallBestIdx = -1;
        _airTemp = 26; _trackTemp = 39; _humidity = 48; _wind = 2.4; _rainProb = 0.08; _raining = false;
    }

    public Task<F1FetchResult> FetchAsync(CancellationToken ct)
    {
        if (_order.Count == 0)
            Reset();

        var msgs = new List<F1RaceControlMessage>();
        var flag = TrackFlag.Green;

        if (_lap < TotalLaps)
            _lap++;

        // —— 旗帜 / 安全车脚本 ——
        if (_lap == 1)
        {
            Msg(msgs, RaceControlCategory.Flag, "熄灯！正赛开始 🟢");
        }
        if (_lap == 3 && !_drsAnnounced)
        {
            _drsAnnounced = true;
            Msg(msgs, RaceControlCategory.Drs, "DRS 已启用");
        }
        if (_lap == 20 && _scLapsLeft == 0)
        {
            _scLapsLeft = 3;
            Msg(msgs, RaceControlCategory.SafetyCar, "🟡 安全车出动 (SAFETY CAR)");
        }

        if (_scLapsLeft > 0)
        {
            flag = TrackFlag.SafetyCar;
            _scLapsLeft--;
            if (_scLapsLeft == 0)
                Msg(msgs, RaceControlCategory.SafetyCar, "🟢 安全车本圈进站 — 即将恢复比赛");
        }
        else if (_lap == 33)
        {
            flag = TrackFlag.VirtualSafetyCar;
            Msg(msgs, RaceControlCategory.SafetyCar, "🟡 虚拟安全车 (VSC) — 注意减速");
        }

        if (_lap >= TotalLaps)
            flag = TrackFlag.Chequered;

        // —— 退赛脚本（25 圈一辆掉队车 DNF） ——
        if (_lap == 25)
        {
            var victim = -1;
            for (var k = _order.Count - 1; k >= 0; k--)
                if (!_retired[_order[k]]) { victim = _order[k]; break; }
            if (victim >= 0)
            {
                _retired[victim] = true;
                _order.Remove(victim);
                _order.Add(victim);
                Msg(msgs, RaceControlCategory.Incident, $"{Grid[victim].Tla} 退赛 — 动力单元故障", Grid[victim].Tla);
            }
        }

        // —— 圈速 / 轮胎磨损 ——
        var racing = flag is TrackFlag.Green or TrackFlag.Chequered;
        for (var i = 0; i < Grid.Length; i++)
        {
            if (_retired[i]) continue;
            _stint[i]++;
            _tyreAge[i]++;
            var comp = _tyre[i] switch
            {
                TyreCompound.Soft => -500,
                TyreCompound.Medium => 0,
                TyreCompound.Hard => 320,
                TyreCompound.Intermediate => 3200,
                TyreCompound.Wet => 6500,
                _ => 0
            };
            var deg = _stint[i] * 70;
            var jitter = _rng.Next(-450, 500);
            var scPenalty = racing ? 0 : 18_000; // 安全车/VSC 圈速变慢
            _lastMs[i] = BaseLapMs + comp + deg + jitter + scPenalty + _rng.Next(0, Grid.Length) * 40;
            if (_inPit[i]) _lastMs[i] += 22_000; // 上一圈进站的进站损失
            if (_lastMs[i] < _bestMs[i] && racing && !_inPit[i])
                _bestMs[i] = _lastMs[i];
            _inPit[i] = false;
        }

        // —— 进站窗口 ——
        if (racing && ((_lap is >= 16 and <= 24) || (_lap is >= 34 and <= 44)))
        {
            foreach (var i in _order.Where(i => !_retired[i]).ToList())
            {
                var due = _pits[i] == 0 && _lap >= 18 && _rng.NextDouble() < 0.18;
                var second = _pits[i] == 1 && _lap >= 36 && _rng.NextDouble() < 0.14;
                if (due || second)
                {
                    _inPit[i] = true;
                    _pits[i]++;
                    _stint[i] = 0;
                    _tyre[i] = _pits[i] == 1 ? TyreCompound.Hard : TyreCompound.Medium;
                    Msg(msgs, RaceControlCategory.PitLane,
                        $"{Grid[i].Tla} 进站换 {CompoundCn(_tyre[i])}（第 {_pits[i]} 次）", Grid[i].Tla);
                }
            }
        }

        // —— 超车（重新排序） ——
        if (racing)
        {
            var swaps = _rng.Next(0, 3);
            for (var s = 0; s < swaps; s++)
            {
                var k = _rng.Next(1, _order.Count);
                var a = _order[k];
                var b = _order[k - 1];
                if (_retired[a] || _retired[b]) continue;
                _order[k] = b;
                _order[k - 1] = a;
                Msg(msgs, RaceControlCategory.Info, $"{Grid[a].Tla} 超越 {Grid[b].Tla}，升至 P{k}", Grid[a].Tla);
            }
        }
        // 进站中的车滑到竞争车之后、退赛车垫底
        _order = ReorderWithPits();

        // —— 全场最快圈 ——
        var prevBest = _overallBestMs;
        for (var i = 0; i < Grid.Length; i++)
        {
            if (!_retired[i] && _bestMs[i] < _overallBestMs)
            {
                _overallBestMs = _bestMs[i];
                _overallBestIdx = i;
            }
        }
        if (_overallBestMs < prevBest && _overallBestIdx >= 0 && _lap > 1)
            Msg(msgs, RaceControlCategory.Info,
                $"最快圈：{Grid[_overallBestIdx].Tla} {LapText(_overallBestMs)} 💜", Grid[_overallBestIdx].Tla);

        // —— 天气演变 ——
        _airTemp += (_rng.NextDouble() - 0.5) * 0.6;
        _trackTemp += (_rng.NextDouble() - 0.5) * 1.0;
        _humidity = Math.Clamp(_humidity + (_rng.NextDouble() - 0.45) * 2, 35, 85);
        _wind = Math.Clamp(_wind + (_rng.NextDouble() - 0.5) * 0.5, 0.5, 7);
        if (_lap >= 40)
            _rainProb = Math.Clamp(_rainProb + 0.04, 0, 0.9);
        if (_rainProb > 0.6 && !_raining && _rng.NextDouble() < 0.5)
        {
            _raining = true;
            Msg(msgs, RaceControlCategory.Weather, "🌦️ 2 号弯区域报告小雨");
        }

        // —— 偶发车队无线电 ——
        if (racing && _rng.NextDouble() < 0.35)
        {
            var i = _order[_rng.Next(0, Math.Min(8, _order.Count))];
            Msg(msgs, RaceControlCategory.Radio, RandomRadio(), Grid[i].Tla);
        }

        if (_lap >= TotalLaps && _order.Count > 0)
        {
            var win = _order[0];
            Msg(msgs, RaceControlCategory.Flag, $"🏁 方格旗！{Grid[win].Tla} 赢得模拟大奖赛！", Grid[win].Tla);
        }

        return Task.FromResult(BuildResult(flag, msgs));
    }

    private List<int> ReorderWithPits()
    {
        // 保持竞争车按当前 _order，进站中的排在其后、退赛的垫底
        var racing = _order.Where(i => !_retired[i] && !_inPit[i]);
        var pitting = _order.Where(i => !_retired[i] && _inPit[i]);
        var retired = _order.Where(i => _retired[i]);
        return racing.Concat(pitting).Concat(retired).ToList();
    }

    private F1FetchResult BuildResult(TrackFlag flag, List<F1RaceControlMessage> msgs)
    {
        var rows = new List<F1DriverTiming>();
        double cum = 0;
        var pos = 0;
        var scLike = flag is TrackFlag.SafetyCar or TrackFlag.VirtualSafetyCar;
        foreach (var i in _order)
        {
            pos++;
            var g = Grid[i];
            string gap, itv;
            if (_retired[i])
            {
                gap = "DNF"; itv = "";
            }
            else if (pos == 1)
            {
                gap = ""; itv = "";
            }
            else
            {
                var spacing = scLike ? 0.3 + _rng.NextDouble() * 0.4 : 0.2 + _rng.NextDouble() * 1.9;
                cum += spacing;
                gap = "+" + cum.ToString("0.000");
                itv = "+" + spacing.ToString("0.000");
            }

            rows.Add(new F1DriverTiming
            {
                DriverNumber = g.Num,
                Tla = g.Tla,
                FullName = g.Name,
                TeamName = g.Team,
                TeamColour = g.Colour,
                Position = pos,
                GapToLeader = gap,
                Interval = itv,
                LastLapTime = _retired[i] ? "" : LapText(_lastMs[i]),
                BestLapTime = _bestMs[i] == int.MaxValue ? "" : LapText(_bestMs[i]),
                IsOverallFastest = i == _overallBestIdx,
                IsPersonalBest = !_retired[i] && _lastMs[i] == _bestMs[i] && _bestMs[i] != int.MaxValue,
                Tyre = _tyre[i],
                StintLaps = _stint[i],
                TyreAge = _tyreAge[i],
                PitStops = _pits[i],
                InPit = _inPit[i],
                IsRetired = _retired[i],
                Drs = !_retired[i] && !_inPit[i] && flag == TrackFlag.Green && _lap >= 3 && pos > 1 && _rng.NextDouble() < 0.4
            });
        }

        return new F1FetchResult
        {
            Ok = true,
            IsLive = true,
            SessionName = "Race · 模拟演示",
            SessionType = SessionType.Race,
            CircuitName = "Demo Grand Prix",
            CountryName = "模拟赛道",
            CurrentLap = _lap,
            TotalLaps = TotalLaps,
            Flag = flag,
            StatusMessage = "模拟比赛进行中",
            Drivers = rows,
            NewMessages = msgs,
            HasWeather = true,
            Weather = new F1Weather
            {
                AirTemp = Math.Round(_airTemp, 1),
                TrackTemp = Math.Round(_trackTemp, 1),
                Humidity = Math.Round(_humidity),
                WindSpeed = Math.Round(_wind, 1),
                RainfallProbability = Math.Round(_rainProb, 2),
                IsRaining = _raining,
                Summary = _raining ? "小雨" : _rainProb > 0.5 ? "多云转雨" : "晴"
            },
            FastestLapTla = _overallBestIdx >= 0 ? Grid[_overallBestIdx].Tla : "",
            FastestLapTime = _overallBestMs == int.MaxValue ? "" : LapText(_overallBestMs)
        };
    }

    private void Msg(List<F1RaceControlMessage> list, RaceControlCategory cat, string text, string tla = "") =>
        list.Add(new F1RaceControlMessage
        {
            Time = DateTime.Now,
            TimeText = $"L{_lap}",
            Category = cat,
            Message = text,
            DriverTla = tla
        });

    private static string LapText(int ms)
    {
        var t = TimeSpan.FromMilliseconds(ms);
        return $"{t.Minutes}:{t.Seconds:00}.{t.Milliseconds:000}";
    }

    private static string CompoundCn(TyreCompound c) => c switch
    {
        TyreCompound.Soft => "软胎",
        TyreCompound.Medium => "中性胎",
        TyreCompound.Hard => "硬胎",
        TyreCompound.Intermediate => "半雨胎",
        TyreCompound.Wet => "全雨胎",
        _ => "轮胎"
    };

    private string RandomRadio()
    {
        string[] lines =
        {
            "Box box box，本圈进站！",
            "Get in there! 干得漂亮！",
            "前车在掉速，给我推。",
            "轮胎还能再撑几圈吗？— Copy，能撑。",
            "注意 DRS，他在你尾流里。",
            "稳住节奏，差距我们在控。",
            "引擎模式调高，可以攻了。",
            "蓝旗，让一下被套圈的车。"
        };
        return lines[_rng.Next(lines.Length)];
    }
}
