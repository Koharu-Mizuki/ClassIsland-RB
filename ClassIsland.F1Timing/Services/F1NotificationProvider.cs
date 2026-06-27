using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Notification;
using ClassIsland.F1Timing.Models.Settings;

namespace ClassIsland.F1Timing.Services;

/// <summary>
/// F1 赛况通知提供方。当旗帜/安全车状态变化或关注车手发生事件时，
/// 通过 ClassIsland 通知系统弹出醒目提醒。
/// </summary>
[NotificationProviderInfo("A1B2C3D4-E5F6-7890-ABCD-EF1234567890", "F1 赛况提醒",
    "", "在旗帜变化、安全车出动、关注车手事件时发出提醒。")]
[NotificationChannelInfo(FlagChannelId, "旗帜通知", "",
    description: "赛道旗帜状态变化时发出提醒。")]
[NotificationChannelInfo(EventChannelId, "赛事事件", "",
    description: "安全车、关注车手事件时发出提醒。")]
[NotificationChannelInfo(FastestLapChannelId, "最快圈", "",
    description: "全场最快圈刷新时发出紫色蒙版提醒。")]
public class F1NotificationProvider : NotificationProviderBase<F1NotificationProviderSettings>
{
    public const string FlagChannelId = "a3ff2f23-467d-42c1-b7b3-42a8ac893d10";
    public const string EventChannelId = "07de0934-8bdf-4059-8018-cd57694b86c4";
    public const string FastestLapChannelId = "c84f1e02-3a97-4db0-b156-9ade507f2c81";

    public static F1NotificationProvider? Instance { get; private set; }

    public F1NotificationProvider() { Instance = this; }

    /// <summary>通过旗帜渠道发送通知。</summary>
    public void ShowFlagNotification(NotificationRequest request) =>
        Channel(FlagChannelId).ShowNotification(request);

    /// <summary>通过事件渠道发送通知。</summary>
    public void ShowEventNotification(NotificationRequest request) =>
        Channel(EventChannelId).ShowNotification(request);

    /// <summary>通过最快圈渠道发送紫色蒙版通知。</summary>
    public void ShowFastestLapNotification(NotificationRequest request) =>
        Channel(FastestLapChannelId).ShowNotification(request);
}
