using System.ComponentModel;
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

        Strings.DetectLanguage();
        NotificationService.EnsureShortcut();

        _mainWindow = new MainWindow();
        _mainWindow.Show();

        // 监听 ViewModel 状态变化，更新托盘提示
        _mainWindow.ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        CreateSystemTray();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModels.MainViewModel.TrayTooltip)
            && _notifyIcon != null
            && _mainWindow != null)
        {
            _notifyIcon.Text = _mainWindow.ViewModel.TrayTooltip;
        }
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
