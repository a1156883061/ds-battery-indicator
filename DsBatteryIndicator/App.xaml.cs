using System.ComponentModel;
using System.Windows;
using DsBatteryIndicator.Resources;
using DsBatteryIndicator.Services;
using DsBatteryIndicator.Views;

namespace DsBatteryIndicator;

public partial class App : Application
{
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private System.Windows.Forms.ToolStripMenuItem? _trayShowHideItem;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Strings.DetectLanguage();

        CreateSystemTray();

        _mainWindow = new MainWindow();
        _mainWindow.ViewModel.TrayIcon = _notifyIcon;
        if (AppSettings.Instance.WindowVisible)
            _mainWindow.Show();

        _mainWindow.ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        _mainWindow.IsVisibleChanged += (s, e) => UpdateTrayShowHideText();
        Strings.LanguageChanged += () => UpdateTrayShowHideText();

        UpdateTrayShowHideText();
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

    private void ToggleWindow()
    {
        if (_mainWindow == null) return;

        if (_mainWindow.IsVisible)
        {
            _mainWindow.Hide();
            AppSettings.Instance.WindowVisible = false;
        }
        else
        {
            _mainWindow.ShowWindow();
        }

        AppSettings.Instance.Save();
        UpdateTrayShowHideText();
    }

    private void UpdateTrayShowHideText()
    {
        if (_trayShowHideItem != null && _mainWindow != null)
        {
            _trayShowHideItem.Text = _mainWindow.IsVisible ? Strings.Hide : Strings.Show;
        }
    }

    private void CreateSystemTray()
    {
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = Strings.AppName,
            Visible = true,
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)
        };

        _notifyIcon.DoubleClick += (s, e) => ToggleWindow();

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        _trayShowHideItem = new System.Windows.Forms.ToolStripMenuItem();
        _trayShowHideItem.Click += (s, e) => ToggleWindow();
        contextMenu.Items.Add(_trayShowHideItem);
        contextMenu.Items.Add(Strings.Topmost, null, (s, e) =>
        {
            if (_mainWindow != null)
                _mainWindow.Topmost = !_mainWindow.Topmost;
        });
        contextMenu.Items.Add(Strings.Exit, null, (s, e) =>
        {
            AppSettings.Instance.WindowVisible = _mainWindow?.IsVisible ?? false;
            AppSettings.Instance.Save();
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
