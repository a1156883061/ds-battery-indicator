using System.Diagnostics;
using System.IO;
using Windows.UI.Notifications;
using DsBatteryIndicator.Resources;

namespace DsBatteryIndicator.Services;

/// <summary>
/// Windows Toast 通知服务。
/// 需要应用在开始菜单有 AUMID 快捷方式且注册了 COM 激活器才能正常工作。
/// 如果初始化失败，回退到系统托盘气泡通知。
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

        Log($"快捷方式路径: {startMenuPath}");
        Log($"快捷方式存在: {File.Exists(startMenuPath)}");

        if (File.Exists(startMenuPath))
        {
            // 验证已有快捷方式
            try { Log($"快捷方式大小: {new FileInfo(startMenuPath).Length} bytes"); } catch { }
            return;
        }

        try
        {
            string exePath = Environment.ProcessPath ?? "";
            Log($"当前 exe: {exePath}");

            if (string.IsNullOrEmpty(exePath))
                return;

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('{startMenuPath}'); $s.TargetPath = '{exePath}'; $s.AppUserModelID = '{AppId}'; $s.Save()\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            var p = Process.Start(psi);
            if (p != null)
            {
                p.WaitForExit(5000);
                string err = p.StandardError.ReadToEnd();
                string stdout = p.StandardOutput.ReadToEnd();
                Log($"快捷方式创建 exit={p.ExitCode}, err='{err.Trim()}', out='{stdout.Trim()}'");
                Log($"创建后文件存在: {File.Exists(startMenuPath)}");
            }
        }
        catch (Exception ex)
        {
            Log($"创建快捷方式异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 手动测试通知（右键菜单触发）
    /// </summary>
    public static bool ShowTestNotification()
    {
        try
        {
            Log("=== 手动测试通知 ===");

            string xml = $"""
                <toast>
                    <visual>
                        <binding template="ToastGeneric">
                            <text>{Strings.AppName} - 测试</text>
                            <text>这是一条测试通知，如果你能看到这条消息，说明通知系统工作正常。</text>
                        </binding>
                    </visual>
                </toast>
                """;

            var doc = new Windows.Data.Xml.Dom.XmlDocument();
            doc.LoadXml(xml);

            var toast = new ToastNotification(doc);
            ToastNotificationManager.CreateToastNotifier(AppId).Show(toast);
            Log("Toast 已发送，无异常");
            return true;
        }
        catch (Exception ex)
        {
            string error = $"Toast 失败: {ex.GetType().Name}: {ex.Message}";
            Log(error);

            // 尝试更详细诊断
            try
            {
                var notifier = ToastNotificationManager.CreateToastNotifier(AppId);
                var setting = notifier.Setting;
                Log($"通知设置: {setting}"); // Enabled/DisabledForApplication/DisabledForUser等
            }
            catch (Exception ex2)
            {
                Log($"CreateToastNotifier 也失败: {ex2.Message}");
            }
            return false;
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
            Log($"低电量通知: {message}");

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
            Log("Toast 发送成功");
        }
        catch (Exception ex)
        {
            Log($"Toast 失败: {ex.GetType().Name}: {ex.Message}");

            // 诊断
            try
            {
                var notifier = ToastNotificationManager.CreateToastNotifier(AppId);
                Log($"通知设置: {notifier.Setting}");
            }
            catch (Exception ex2)
            {
                Log($"CreateToastNotifier 失败: {ex2.Message}");
            }
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
