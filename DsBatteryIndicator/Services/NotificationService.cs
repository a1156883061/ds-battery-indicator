using DsBatteryIndicator.Resources;

namespace DsBatteryIndicator.Services;

/// <summary>
/// 低电量通知服务。三通道并行，不打断游戏操作：
/// 1. 系统托盘气泡（BalloonTip）
/// 2. 声音提示（系统 Exclamation 音效）
/// 3. 手柄震动+灯带（通过 HidService）
/// </summary>
public static class NotificationService
{
    public static void NotifyLowBattery(int batteryLevel,
        System.Windows.Forms.NotifyIcon? trayIcon, HidService? hidService)
    {
        ShowBalloonTip(trayIcon, batteryLevel);
        PlayAlertSound();
        TriggerControllerHaptic(hidService);
    }

    private static void ShowBalloonTip(System.Windows.Forms.NotifyIcon? trayIcon, int batteryLevel)
    {
        if (trayIcon == null) return;
        try
        {
            trayIcon.ShowBalloonTip(5000, Strings.AppName,
                string.Format(Strings.Toast_LowBattery, batteryLevel),
                System.Windows.Forms.ToolTipIcon.None);
        }
        catch { }
    }

    private static void PlayAlertSound()
    {
        try { System.Media.SystemSounds.Exclamation.Play(); }
        catch { }
    }

    private static void TriggerControllerHaptic(HidService? hidService)
    {
        hidService?.SendHapticPulse();
    }
}
