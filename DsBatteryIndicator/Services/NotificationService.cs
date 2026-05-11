using System.Diagnostics;
using System.IO;
using DsBatteryIndicator.Resources;

namespace DsBatteryIndicator.Services;

/// <summary>
/// 低电量通知服务。支持三种不打断游戏操作的通道：
/// 1. 系统托盘气泡（BalloonTip）
/// 2. 声音提示（系统 Exclamation 音效）
/// 3. 手柄震动反馈（通过 HidService 发送 DualSense 输出报告）
/// </summary>
public static class NotificationService
{
    private static DateTime _lastNotifyTime = DateTime.MinValue;
    private static readonly TimeSpan MinNotifyInterval = TimeSpan.FromMinutes(2);

    /// <summary>
    /// 触发低电量通知（三通道并行，2分钟内不重复）
    /// </summary>
    public static void NotifyLowBattery(int batteryLevel, System.Windows.Forms.NotifyIcon? trayIcon,
        HidService? hidService)
    {
        // 防重复：2 分钟内不重复通知
        if (DateTime.Now - _lastNotifyTime < MinNotifyInterval)
            return;
        _lastNotifyTime = DateTime.Now;

        Log($"触发低电量通知: {batteryLevel}%");

        // A: 托盘气泡
        ShowBalloonTip(trayIcon, batteryLevel);

        // C: 声音
        PlayAlertSound();

        // D: 手柄震动
        TriggerControllerHaptic(hidService);
    }

    private static void ShowBalloonTip(System.Windows.Forms.NotifyIcon? trayIcon, int batteryLevel)
    {
        if (trayIcon == null) return;

        try
        {
            string msg = string.Format(Strings.Toast_LowBattery, batteryLevel);
            trayIcon.ShowBalloonTip(5000, Strings.AppName, msg,
                System.Windows.Forms.ToolTipIcon.Warning);
            Log("托盘气泡已发送");
        }
        catch (Exception ex)
        {
            Log($"托盘气泡失败: {ex.Message}");
        }
    }

    private static void PlayAlertSound()
    {
        try
        {
            System.Media.SystemSounds.Exclamation.Play();
            Log("声音已播放");
        }
        catch (Exception ex)
        {
            Log($"声音播放失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 发送 DualSense 输出报告触发短暂震动
    /// </summary>
    private static void TriggerControllerHaptic(HidService? hidService)
    {
        if (hidService == null) return;

        try
        {
            hidService.SendHapticPulse();
            Log("手柄震动已发送");
        }
        catch (Exception ex)
        {
            Log($"手柄震动失败: {ex.Message}");
        }
    }

    // 保留：Toast 快捷方式（兼容旧代码）
    public static void EnsureShortcut() { }

    // 保留：手动测试（兼容旧代码）
    public static bool ShowTestNotification()
    {
        try
        {
            System.Media.SystemSounds.Exclamation.Play();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void Log(string msg)
    {
        try
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DsBatteryIndicator", "notification.log");
            string dir = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(dir);
            File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff} {msg}\n");
        }
        catch { }
    }
}
