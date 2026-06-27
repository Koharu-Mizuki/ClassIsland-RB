using System;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace ClassIsland.F1Timing.Helpers;

internal static class AnimationHelper
{
    internal static Task InterpolateAsync(
        Action<double> setter, double from, double to, int durationMs, Func<double, double> easing)
    {
        var tcs = new TaskCompletionSource();
        var start = DateTime.UtcNow;
        setter(from);

        DispatcherTimer timer = null!;
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) =>
        {
            var t = Math.Min((DateTime.UtcNow - start).TotalMilliseconds / durationMs, 1.0);
            setter(from + (to - from) * easing(t));
            if (t >= 1.0)
            {
                setter(to);
                timer.Stop();
                tcs.SetResult();
            }
        };
        timer.Start();
        return tcs.Task;
    }

    internal static double EaseQuarticOut(double t) => 1 - Math.Pow(1 - t, 4);
    internal static double EaseQuadIn(double t)     => t * t;
}
