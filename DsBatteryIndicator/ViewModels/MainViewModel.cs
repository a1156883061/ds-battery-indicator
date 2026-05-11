using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using DsBatteryIndicator.Models;
using DsBatteryIndicator.Resources;
using DsBatteryIndicator.Services;
using DsBatteryIndicator.Plugins.RtssPlugin;

namespace DsBatteryIndicator.ViewModels;

/// <summary>
/// 主窗口 ViewModel，管理 UI 状态和 HID 服务交互。
/// </summary>
public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly HidService _hidService;
    private RtssService? _rtssService;
    private System.Timers.Timer? _lowBatteryTimer;
    private bool _lowBatteryNotifiedOnce;

    /// <summary>由 App 注入，用于托盘气泡通知</summary>
    public System.Windows.Forms.NotifyIcon? TrayIcon { get; set; }

    private DeviceStatus _status = DeviceStatus.Disconnected;
    private int _batteryLevel;
    private bool _isCharging;
    private string _batteryText = "——";
    private string _trayTooltip = "DS 电池指示器";
    private Brush _accentColor = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
    private bool _isBlinking;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? BlinkRequested;
    public event Action? BlinkStopped;

    public MainViewModel()
    {
        _hidService = new HidService();
        _hidService.BatteryDataReceived += OnBatteryDataReceived;
        _hidService.ConnectionChanged += OnConnectionChanged;
        _hidService.StartWatching();

        if (AppSettings.Instance.RtssEnabled)
        {
            _rtssService = new RtssService();
            _rtssService.Initialize();
        }
    }

    public void EnableRtss()
    {
        _rtssService ??= new RtssService();
        _rtssService.Initialize();
    }

    public void DisableRtss()
    {
        _rtssService?.Shutdown();
        _rtssService = null;
    }

    public void SendHapticTest()
    {
        _hidService.SendHapticPulse();
    }

    private void HandleLowBattery(int batteryLevel)
    {
        var cfg = AppSettings.Instance;
        if (!cfg.LowBatteryAlertEnabled) return;

        if (!_lowBatteryNotifiedOnce)
        {
            _lowBatteryNotifiedOnce = true;
            NotificationService.NotifyLowBattery(batteryLevel, TrayIcon, _hidService);
        }

        // 重复提醒
        if (cfg.LowBatteryRepeatEnabled && _lowBatteryTimer == null)
        {
            _lowBatteryTimer = new System.Timers.Timer(cfg.LowBatteryRepeatIntervalMs);
            _lowBatteryTimer.Elapsed += (s, e) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!cfg.LowBatteryAlertEnabled) { StopLowBatteryRepeat(); return; }
                    NotificationService.NotifyLowBattery(batteryLevel, TrayIcon, _hidService);
                });
            };
            _lowBatteryTimer.AutoReset = true;
            _lowBatteryTimer.Start();
        }
    }

    private void StopLowBatteryRepeat()
    {
        _lowBatteryTimer?.Stop();
        _lowBatteryTimer?.Dispose();
        _lowBatteryTimer = null;
        _lowBatteryNotifiedOnce = false;
    }

    public DeviceStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public int BatteryLevel
    {
        get => _batteryLevel;
        set { _batteryLevel = value; OnPropertyChanged(); }
    }

    public bool IsCharging
    {
        get => _isCharging;
        set { _isCharging = value; OnPropertyChanged(); }
    }

    public string BatteryText
    {
        get => _batteryText;
        set { _batteryText = value; OnPropertyChanged(); }
    }

    public string TrayTooltip
    {
        get => _trayTooltip;
        set { _trayTooltip = value; OnPropertyChanged(); }
    }

    public Brush AccentColor
    {
        get => _accentColor;
        set { _accentColor = value; OnPropertyChanged(); }
    }

    public bool IsBlinking
    {
        get => _isBlinking;
        set { _isBlinking = value; OnPropertyChanged(); }
    }

    private void OnBatteryDataReceived(DualSenseDevice device)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Status = device.Status;
            BatteryLevel = device.BatteryLevel;
            BatteryText = $"{device.BatteryLevel}%";

            switch (device.Status)
            {
                case DeviceStatus.Normal:
                    AccentColor = new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA)); // 蓝
                    TrayTooltip = $"{Strings.AppName} — {device.BatteryLevel}%";
                    StopBlinking();
                    StopLowBatteryRepeat();
                    break;

                case DeviceStatus.Charging:
                    AccentColor = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80)); // 绿
                    TrayTooltip = $"{Strings.AppName} — {Strings.Charging} {device.BatteryLevel}%";
                    StopBlinking();
                    StopLowBatteryRepeat();
                    break;

                case DeviceStatus.LowBattery:
                    AccentColor = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)); // 红
                    TrayTooltip = $"{Strings.AppName} — {Strings.LowBattery} {device.BatteryLevel}%";
                    StartBlinking();
                    HandleLowBattery(device.BatteryLevel);
                    break;

                case DeviceStatus.Disconnected:
                    AccentColor = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)); // 灰
                    TrayTooltip = $"{Strings.AppName} — {Strings.Disconnected}";
                    IsCharging = false;
                    StopBlinking();
                    StopLowBatteryRepeat();
                    break;
            }

            // 在 AccentColor 之后设置，确保充电环拿到正确颜色
            IsCharging = device.IsCharging;
            _rtssService?.UpdateBattery(device.BatteryLevel);
        });
    }

    private void OnConnectionChanged(bool connected)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (!connected)
            {
                Status = DeviceStatus.Disconnected;
                IsCharging = false;
                TrayTooltip = $"{Strings.AppName} — {Strings.Disconnected}";
                AccentColor = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
                StopBlinking();
                StopLowBatteryRepeat();
            }
        });
    }

    private void StartBlinking()
    {
        if (_isBlinking) return;
        _isBlinking = true;
        OnPropertyChanged(nameof(IsBlinking));
        BlinkRequested?.Invoke();
    }

    private void StopBlinking()
    {
        if (!_isBlinking) return;
        _isBlinking = false;
        OnPropertyChanged(nameof(IsBlinking));
        BlinkStopped?.Invoke();
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public void Dispose()
    {
        StopLowBatteryRepeat();
        _rtssService?.Dispose();
        _hidService.Dispose();
    }
}
