using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using DsBatteryIndicator.Models;
using DsBatteryIndicator.Services;

namespace DsBatteryIndicator.ViewModels;

/// <summary>
/// 主窗口 ViewModel，管理 UI 状态和 HID 服务交互。
/// </summary>
public class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly HidService _hidService;
    private bool _lowBatteryNotified;

    private DeviceStatus _status = DeviceStatus.Disconnected;
    private int _batteryLevel;
    private bool _isCharging;
    private string _batteryText = "——";
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
                    StopBlinking();
                    _lowBatteryNotified = false;
                    break;

                case DeviceStatus.Charging:
                    AccentColor = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80)); // 绿
                    StopBlinking();
                    _lowBatteryNotified = false;
                    break;

                case DeviceStatus.LowBattery:
                    AccentColor = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)); // 红
                    StartBlinking();
                    if (!_lowBatteryNotified)
                    {
                        NotificationService.ShowLowBatteryNotification(device.BatteryLevel);
                        _lowBatteryNotified = true;
                    }
                    break;

                case DeviceStatus.Disconnected:
                    AccentColor = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)); // 灰
                    IsCharging = false;
                    StopBlinking();
                    _lowBatteryNotified = false;
                    break;
            }

            // 在 AccentColor 之后设置，确保充电环拿到正确颜色
            IsCharging = device.IsCharging;
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
                AccentColor = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
                StopBlinking();
                _lowBatteryNotified = false;
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
        _hidService.Dispose();
    }
}
