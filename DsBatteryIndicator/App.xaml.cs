using System.Windows;
using DsBatteryIndicator.Resources;
using DsBatteryIndicator.Services;
using DsBatteryIndicator.Views;

namespace DsBatteryIndicator;

public partial class App : Application
{
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 初始化语言
        Strings.DetectLanguage();

        // 创建开始菜单快捷方式（Toast 通知需要）
        NotificationService.EnsureShortcut();

        // 创建主窗口
        _mainWindow = new MainWindow();
        _mainWindow.Show();

        // 创建系统托盘
        CreateSystemTray();
    }

    private void CreateSystemTray()
    {
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = Strings.AppName,
            Visible = true,
            Icon = System.Drawing.SystemIcons.Shield
        };

        _notifyIcon.DoubleClick += (s, e) =>
        {
            _mainWindow?.Show();
            _mainWindow?.Activate();
        };

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        contextMenu.Items.Add(Strings.Topmost, null, (s, e) =>
        {
            if (_mainWindow != null)
                _mainWindow.Topmost = !_mainWindow.Topmost;
        });
        contextMenu.Items.Add(Strings.Exit, null, (s, e) =>
        {
            _notifyIcon?.Dispose();
            Shutdown();
        });

        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _notifyIcon?.Dispose();
        base.OnExit(e);
    }
}
