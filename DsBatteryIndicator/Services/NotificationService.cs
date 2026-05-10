using System.Diagnostics;
using System.IO;
using Windows.UI.Notifications;
using DsBatteryIndicator.Resources;

namespace DsBatteryIndicator.Services;

/// <summary>
/// Windows Toast 通知服务。
/// 需要应用在开始菜单有快捷方式且声明了 AUMID 才能正常工作。
/// </summary>
public static class NotificationService
{
    private const string AppId = "DsBatteryIndicator";

    /// <summary>
    /// 应用启动时调用一次，确保 AUMID 快捷方式存在
    /// </summary>
    public static void EnsureShortcut()
    {
        string startMenuPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs", "DsBatteryIndicator.lnk");

        if (File.Exists(startMenuPath))
            return;

        try
        {
            string exePath = Environment.ProcessPath ?? "";
            if (string.IsNullOrEmpty(exePath))
                return;

            // 使用 PowerShell 创建快捷方式
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('{startMenuPath}'); $s.TargetPath = '{exePath}'; $s.AppUserModelID = '{AppId}'; $s.Save()\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(psi)?.WaitForExit(2000);
        }
        catch
        {
            // 创建快捷方式失败不影响主体功能
        }
    }

    /// <summary>
    /// 发送低电量通知
    /// </summary>
    public static void ShowLowBatteryNotification(int batteryLevel)
    {
        try
        {
            string message = string.Format(Strings.Toast_LowBattery, batteryLevel);

            // 使用 Toast 模板
            string xml = $"""
                <toast>
                    <visual>
                        <binding template="ToastGeneric">
                            <text>{Strings.AppName}</text>
                            <text>{message}</text>
                        </binding>
                    </visual>
                </toast>
                """;

            var doc = new Windows.Data.Xml.Dom.XmlDocument();
            doc.LoadXml(xml);

            var toast = new ToastNotification(doc);
            ToastNotificationManager.CreateToastNotifier(AppId).Show(toast);
        }
        catch
        {
            // Toast 发送失败时静默处理，窗口闪烁仍是有效的视觉提示
            Debug.WriteLine("[Notification] Toast 发送失败");
        }
    }
}
