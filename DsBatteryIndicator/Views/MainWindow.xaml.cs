using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using DsBatteryIndicator.Resources;
using DsBatteryIndicator.Services;
using DsBatteryIndicator.ViewModels;

namespace DsBatteryIndicator.Views;

/// <summary>
/// 浮动电量显示窗口。
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _isTopmost = true;
    private bool _isAutoStart;

    public MainViewModel ViewModel => _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        _viewModel.BlinkRequested += StartBlink;
        _viewModel.BlinkStopped += StopBlink;

        // 恢复窗口位置
        double savedLeft = AppSettings.Instance.WindowLeft;
        double savedTop = AppSettings.Instance.WindowTop;
        if (savedLeft > 0 || savedTop > 0)
        {
            Left = savedLeft;
            Top = savedTop;
        }
        else
        {
            // 默认右下角
            Left = SystemParameters.WorkArea.Width - Width - 40;
            Top = SystemParameters.WorkArea.Height - Height - 40;
        }

        // 恢复开机自启
        _isAutoStart = AppSettings.Instance.AutoStart;
        MenuAutoStart.IsChecked = _isAutoStart;

        // 本地化菜单
        UpdateMenuTexts();
        Strings.LanguageChanged += UpdateMenuTexts;

        MenuTopmost.IsChecked = true;
        MenuTopmost.Click += (s, e) =>
        {
            _isTopmost = !_isTopmost;
            Topmost = _isTopmost;
            MenuTopmost.IsChecked = _isTopmost;
        };

        MenuAutoStart.Click += (s, e) =>
        {
            _isAutoStart = !_isAutoStart;
            MenuAutoStart.IsChecked = _isAutoStart;
            SetAutoStart(_isAutoStart);
        };

        MenuLanguage.Click += (s, e) =>
        {
            string next = Strings.CurrentLanguage == "zh-CN" ? "en" : "zh-CN";
            Strings.SetLanguage(next);
        };

        MenuAbout.Click += (s, e) =>
        {
            MessageBox.Show(Strings.About_Text, Strings.AppName,
                MessageBoxButton.OK, MessageBoxImage.Information);
        };

        MenuExit.Click += (s, e) => Application.Current.Shutdown();

        // 关闭窗口时隐藏到托盘，保存位置
        Closing += (s, e) =>
        {
            e.Cancel = true;
            Hide();
            AppSettings.Instance.WindowLeft = Left;
            AppSettings.Instance.WindowTop = Top;
            AppSettings.Instance.Save();
        };
    }

    public void ShowWindow()
    {
        Show();
        Activate();
    }

    private void UpdateMenuTexts()
    {
        MenuTopmost.Header = Strings.Topmost;
        MenuAutoStart.Header = Strings.AutoStart;
        MenuLanguage.Header = Strings.Language;
        MenuAbout.Header = Strings.About;
        MenuExit.Header = Strings.Exit;
    }

    private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // 双击重置位置到右下角
            Left = SystemParameters.WorkArea.Width - Width - 40;
            Top = SystemParameters.WorkArea.Height - Height - 40;
        }
        else
        {
            DragMove();
        }
    }

    private void StartBlink()
    {
        var animation = new DoubleAnimation
        {
            From = 1.0,
            To = 0.5,
            Duration = TimeSpan.FromMilliseconds(400),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        BeginAnimation(OpacityProperty, animation);
    }

    private void StopBlink()
    {
        BeginAnimation(OpacityProperty, null);
        Opacity = 1.0;
    }

    private void SetAutoStart(bool enable)
    {
        AppSettings.Instance.AutoStart = enable;
        AppSettings.Instance.Save();

        string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(runKey, writable: true);
        if (key == null) return;

        string exePath = Environment.ProcessPath ?? "";
        if (enable)
            key.SetValue("DsBatteryIndicator", $"\"{exePath}\"");
        else
            key.DeleteValue("DsBatteryIndicator", throwOnMissingValue: false);
    }
}
