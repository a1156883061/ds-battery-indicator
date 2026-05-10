# DS Battery Indicator 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建 Windows 11 浮动窗口应用，通过 USB HID 协议读取 DualSense 手柄电量并实时显示。

**Architecture:** WPF (.NET 8) + MVVM 模式，Windows.Devices.Hid 读取 HID 报告，BatteryParser 解析电量/充电状态，MainViewModel 驱动四状态 UI（正常/充电中/低电量/未连接），BatteryRing 自定义控件绘制环形进度。

**Tech Stack:** C# 12, WPF, .NET 8, Windows.Devices.Hid (WinRT), .resx 国际化, Windows.UI.Notifications (Toast)

---

### Task 1: 初始化项目并配置依赖

**Files:**
- Create: `DsBatteryIndicator/DsBatteryIndicator.csproj`

- [ ] **Step 1: 使用 dotnet CLI 创建 WPF 项目**

```bash
mkdir -p DsBatteryIndicator
cd DsBatteryIndicator
```

后续步骤使用 Write 工具直接写入文件。

- [ ] **Step 2: 写入 .csproj 项目文件**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows10.0.22621.0</TargetFramework>
    <Nullable>enable</Nullable>
    <UseWPF>true</UseWPF>
    <ImplicitUsings>enable</ImplicitUsings>
    <ApplicationIcon>icon.ico</ApplicationIcon>
    <SupportedOSPlatformVersion>10.0.22621.0</SupportedOSPlatformVersion>
    <UseWindowsForms>true</UseWindowsForms>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.Windows.SDK.NET.Ref" />
  </ItemGroup>

</Project>
```

**说明：**
- `net8.0-windows10.0.22621.0` 提供 WinRT API（含 Windows.Devices.Hid）
- `UseWindowsForms=true` 为系统托盘 NotifyIcon 提供支持
- `Microsoft.Windows.SDK.NET.Ref` 框架引用提供 Windows Runtime API 调用链

- [ ] **Step 3: 创建项目目录结构**

```bash
mkdir -p DsBatteryIndicator/Models
mkdir -p DsBatteryIndicator/Services
mkdir -p DsBatteryIndicator/Resources
mkdir -p DsBatteryIndicator/ViewModels
mkdir -p DsBatteryIndicator/Views
```

---

### Task 2: 创建数据模型

**Files:**
- Create: `DsBatteryIndicator/Models/DualSenseDevice.cs`

- [ ] **Step 1: 写入设备信息模型**

```csharp
namespace DsBatteryIndicator.Models;

/// <summary>
/// DualSense 设备连接状态
/// </summary>
public enum DeviceStatus
{
    Disconnected,  // 未连接
    Normal,        // 正常（电量 > 10% 且未充电）
    Charging,      // 充电中
    LowBattery     // 低电量（≤ 10% 且未充电）
}

/// <summary>
/// DualSense 设备信息，承载一次 HID 报告解析后的结果
/// </summary>
public class DualSenseDevice
{
    public string DeviceId { get; init; } = string.Empty;
    public int BatteryLevel { get; init; }
    public bool IsCharging { get; init; }
    public DeviceStatus Status { get; init; }
}
```

---

### Task 3: 创建 BatteryParser 服务

**Files:**
- Create: `DsBatteryIndicator/Services/BatteryParser.cs`

- [ ] **Step 1: 写入电池报告解析器**

```csharp
using DsBatteryIndicator.Models;

namespace DsBatteryIndicator.Services;

/// <summary>
/// 解析 DualSense HID 输入报告，提取电量百分比和充电状态
/// </summary>
public static class BatteryParser
{
    // DualSense USB 输入报告大小
    private const int UsbReportLength = 64;

    /// <summary>
    /// 从 64 字节 HID 报告解析电池数据
    /// </summary>
    /// <param name="report">HID 输入报告原始字节</param>
    /// <returns>解析后的设备信息；报告长度不符则返回 null</returns>
    public static DualSenseDevice? Parse(byte[] report, string deviceId)
    {
        if (report.Length < UsbReportLength)
            return null;

        // 字节 29: 电量百分比 (0-100)
        int batteryLevel = report[29];

        // 字节 52 bit 4: 充电状态 (0=放电, 1=充电中/充满)
        bool isCharging = (report[52] & 0x10) != 0;

        // 如果电量值 > 100，尝试取低 4 位（某些固件用 0-10 表示 0-100%）
        if (batteryLevel > 100)
            batteryLevel = (report[29] & 0x0F) * 10;

        batteryLevel = Math.Clamp(batteryLevel, 0, 100);

        DeviceStatus status;
        if (isCharging)
            status = DeviceStatus.Charging;
        else if (batteryLevel <= 10)
            status = DeviceStatus.LowBattery;
        else
            status = DeviceStatus.Normal;

        return new DualSenseDevice
        {
            DeviceId = deviceId,
            BatteryLevel = batteryLevel,
            IsCharging = isCharging,
            Status = status
        };
    }
}
```

**说明：** 字节 29 的电量值在不同固件版本中可能是 0-100 或 0-10（取低 4 位 × 10）。代码优先假设 0-100，若 > 100 则回退到 0-10 模式。

---

### Task 4: 创建 HidService

**Files:**
- Create: `DsBatteryIndicator/Services/HidService.cs`

- [ ] **Step 1: 写入 HID 通信服务**

```csharp
using System.Diagnostics;
using Windows.Devices.Enumeration;
using Windows.Devices.Hid;
using DsBatteryIndicator.Models;

namespace DsBatteryIndicator.Services;

/// <summary>
/// HID 设备枚举、连接、数据读取服务。
/// 通过 Windows.Devices.Hid API 与 DualSense 通信。
/// </summary>
public class HidService : IDisposable
{
    // DualSense USB: Sony VID + DualSense PID
    private const ushort SonyVid = 0x054C;
    private const ushort DualSensePid = 0x0CE6;

    private HidDevice? _device;
    private DeviceWatcher? _watcher;
    private int _retryCount;
    private bool _disposed;

    /// <summary>电池数据更新事件</summary>
    public event Action<DualSenseDevice>? BatteryDataReceived;

    /// <summary>设备连接状态变化事件</summary>
    public event Action<bool>? ConnectionChanged;

    /// <summary>
    /// 开始监听 DualSense 设备插拔
    /// </summary>
    public void StartWatching()
    {
        string selector = HidDevice.GetDeviceSelector(usagePage: 0xFF00, usageId: 0x0001,
            vendorId: SonyVid, productId: DualSensePid);

        _watcher = DeviceInformation.CreateWatcher(selector);
        _watcher.Added += OnDeviceAdded;
        _watcher.Removed += OnDeviceRemoved;
        _watcher.Start();

        // 检查是否已有设备连接
        _ = TryConnectAsync();
    }

    private async void OnDeviceAdded(DeviceWatcher sender, DeviceInformation info)
    {
        await TryConnectAsync();
    }

    private void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        if (_device != null && _device.DeviceId == update.Id)
        {
            DisconnectDevice();
        }
    }

    private async Task TryConnectAsync()
    {
        if (_device != null)
            return;

        try
        {
            string selector = HidDevice.GetDeviceSelector(usagePage: 0xFF00, usageId: 0x0001,
                vendorId: SonyVid, productId: DualSensePid);

            var devices = await DeviceInformation.FindAllAsync(selector);
            if (devices.Count == 0)
            {
                ConnectionChanged?.Invoke(false);
                return;
            }

            _device = await HidDevice.FromIdAsync(devices[0].Id, Windows.Storage.FileAccessMode.Read);
            if (_device == null)
            {
                ConnectionChanged?.Invoke(false);
                return;
            }

            _device.InputReportReceived += OnInputReportReceived;
            ConnectionChanged?.Invoke(true);
            _retryCount = 0;
            Debug.WriteLine($"[HidService] 已连接: {_device.DeviceId}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HidService] 连接失败: {ex.Message}");
            ConnectionChanged?.Invoke(false);

            _retryCount++;
            if (_retryCount < 3)
            {
                await Task.Delay(2000);
                await TryConnectAsync();
            }
        }
    }

    private void OnInputReportReceived(HidDevice sender, HidInputReportReceivedEventArgs args)
    {
        try
        {
            var report = args.Report;
            var reader = Windows.Storage.Streams.DataReader.FromBuffer(report.Data);
            byte[] data = new byte[report.Data.Length];
            reader.ReadBytes(data);

            var device = BatteryParser.Parse(data, sender.DeviceId);
            if (device != null)
            {
                BatteryDataReceived?.Invoke(device);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HidService] 读取报告失败: {ex.Message}");
            _retryCount++;
            if (_retryCount >= 3)
            {
                DisconnectDevice();
            }
        }
    }

    private void DisconnectDevice()
    {
        if (_device != null)
        {
            _device.InputReportReceived -= OnInputReportReceived;
            _device.Dispose();
            _device = null;
        }
        _retryCount = 0;
        ConnectionChanged?.Invoke(false);
        Debug.WriteLine("[HidService] 设备已断开");

        // 自动重连
        _ = Task.Delay(2000).ContinueWith(_ => TryConnectAsync());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _watcher?.Stop();
        _watcher = null;
        DisconnectDevice();
    }
}
```

**说明：** `usagePage: 0xFF00, usageId: 0x0001` 匹配 DualSense 的 HID 顶层集合。HID 报告事件在后台线程触发，事件订阅方（ViewModel）需要用 `Dispatcher.Invoke` 回到 UI 线程。

---

### Task 5: 创建 NotificationService

**Files:**
- Create: `DsBatteryIndicator/Services/NotificationService.cs`

- [ ] **Step 1: 写入 Toast 通知服务**

```csharp
using System.Diagnostics;
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
```

**说明：** `AppId` 必须与开始菜单快捷方式的 AUMID 一致。Toast 失败时静默降级——窗口红色闪烁已经提供了视觉提示。

---

### Task 6: 创建国际化资源

**Files:**
- Create: `DsBatteryIndicator/Resources/Strings.resx`
- Create: `DsBatteryIndicator/Resources/Strings.en.resx`
- Create: `DsBatteryIndicator/Resources/Strings.Designer.cs`（或改用代码生成方案）
- Create: `DsBatteryIndicator/Services/LocalizationService.cs`

**替代方案：** WPF .resx 资源文件需要 Visual Studio 自动生成 `Strings.Designer.cs`。由于我们在 CLI 环境下，改用纯 C# 静态类 + `ResourceDictionary` 方案更可控。

- [ ] **Step 1: 写入默认中文资源**

创建 `DsBatteryIndicator/Resources/Strings.cs`：

```csharp
using System.Globalization;

namespace DsBatteryIndicator.Resources;

/// <summary>
/// 多语言字符串资源。默认中文，支持英文切换。
/// </summary>
public static class Strings
{
    private static readonly Dictionary<string, Dictionary<string, string>> Resources = new()
    {
        ["zh-CN"] = new()
        {
            ["AppName"] = "DS 电池指示器",
            ["Topmost"] = "置顶",
            ["AutoStart"] = "开机自启",
            ["Language"] = "语言",
            ["About"] = "关于",
            ["Exit"] = "退出",
            ["Toast_LowBattery"] = "DualSense 电量不足 ({0}%)，请充电",
            ["About_Text"] = "DS 电池指示器 v1.0\n显示 DualSense 手柄电量",
        },
        ["en"] = new()
        {
            ["AppName"] = "DS Battery Indicator",
            ["Topmost"] = "Topmost",
            ["AutoStart"] = "Auto Start",
            ["Language"] = "Language",
            ["About"] = "About",
            ["Exit"] = "Exit",
            ["Toast_LowBattery"] = "DualSense battery low ({0}%), please charge",
            ["About_Text"] = "DS Battery Indicator v1.0\nDisplay DualSense controller battery level",
        },
    };

    private static string _currentLanguage = "zh-CN";

    public static event Action? LanguageChanged;

    /// <summary>当前语言代码</summary>
    public static string CurrentLanguage => _currentLanguage;

    /// <summary>切换语言，保存偏好</summary>
    public static void SetLanguage(string langCode)
    {
        if (Resources.ContainsKey(langCode) && _currentLanguage != langCode)
        {
            _currentLanguage = langCode;
            Properties.Settings.Default.Language = langCode;
            Properties.Settings.Default.Save();
            LanguageChanged?.Invoke();
        }
    }

    /// <summary>根据系统 CultureInfo 自动选择语言</summary>
    public static void DetectLanguage()
    {
        string saved = Properties.Settings.Default.Language;
        if (!string.IsNullOrEmpty(saved) && Resources.ContainsKey(saved))
        {
            _currentLanguage = saved;
            return;
        }

        string culture = CultureInfo.CurrentUICulture.Name;
        // 精确匹配
        if (Resources.ContainsKey(culture))
        {
            _currentLanguage = culture;
            return;
        }
        // 只匹配主要语言（如 "zh" → "zh-CN"）
        string primary = culture.Split('-')[0];
        var match = Resources.Keys.FirstOrDefault(k => k.StartsWith(primary));
        if (match != null)
        {
            _currentLanguage = match;
        }
        // 否则保持默认中文
    }

    public static string AppName => Get("AppName");
    public static string Topmost => Get("Topmost");
    public static string AutoStart => Get("AutoStart");
    public static string Language => Get("Language");
    public static string About => Get("About");
    public static string Exit => Get("Exit");
    public static string Toast_LowBattery => Get("Toast_LowBattery");
    public static string About_Text => Get("About_Text");

    private static string Get(string key)
    {
        if (Resources.TryGetValue(_currentLanguage, out var dict) && dict.TryGetValue(key, out var value))
            return value;
        // 回退到中文
        if (Resources.TryGetValue("zh-CN", out var fallback) && fallback.TryGetValue(key, out var fb))
            return fb;
        return key;
    }
}
```

**说明：** 避免依赖 Visual Studio 的 `.resx` 代码生成工具，改用纯 C# 字典存储翻译，CLI 环境下更可控。语言偏好保存到 `Settings.settings`。

---

### Task 7: 创建 BatteryRing 自定义控件

**Files:**
- Create: `DsBatteryIndicator/Views/BatteryRing.xaml`
- Create: `DsBatteryIndicator/Views/BatteryRing.xaml.cs`

- [ ] **Step 1: 写入 BatteryRing XAML**

```xml
<UserControl x:Class="DsBatteryIndicator.Views.BatteryRing"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Width="48" Height="48">
    <Grid>
        <!-- 底环（始终完整 360°，灰色） -->
        <Path Stroke="#2A2A3E" StrokeThickness="3" Fill="Transparent">
            <Path.Data>
                <PathGeometry>
                    <PathFigure StartPoint="24,4">
                        <ArcSegment Point="24,44" Size="20,20" SweepDirection="Clockwise" IsLargeArc="True"/>
                        <ArcSegment Point="24,4" Size="20,20" SweepDirection="Clockwise" IsLargeArc="True"/>
                    </PathFigure>
                </PathGeometry>
            </Path.Data>
        </Path>

        <!-- 前景环（动态色，角度对应电量百分比） -->
        <Path x:Name="ForegroundArc" Stroke="#4ADE80" StrokeThickness="3"
              StrokeStartLineCap="Round" StrokeEndLineCap="Round" Fill="Transparent">
            <Path.Data>
                <PathGeometry>
                    <PathFigure x:Name="ArcFigure" StartPoint="24,4">
                        <ArcSegment x:Name="ArcSegment" Point="24,4" Size="20,20"
                                    SweepDirection="Clockwise" IsLargeArc="False"/>
                    </PathFigure>
                </PathGeometry>
            </Path.Data>
        </Path>

        <!-- 充电动画：虚线旋转覆盖层 -->
        <Path x:Name="ChargingOverlay" Stroke="#60A5FA" StrokeThickness="3"
              StrokeDashArray="5,3" Fill="Transparent" Visibility="Collapsed"
              RenderTransformOrigin="0.5,0.5">
            <Path.Data>
                <PathGeometry>
                    <PathFigure StartPoint="24,4">
                        <ArcSegment Point="24,44" Size="20,20" SweepDirection="Clockwise" IsLargeArc="True"/>
                        <ArcSegment Point="24,4" Size="20,20" SweepDirection="Clockwise" IsLargeArc="True"/>
                    </PathFigure>
                </PathGeometry>
            </Path.Data>
            <Path.RenderTransform>
                <RotateTransform Angle="0"/>
            </Path.RenderTransform>
        </Path>

        <!-- 中心手柄图标 -->
        <TextBlock Text="🎮" FontSize="18" HorizontalAlignment="Center" VerticalAlignment="Center"
                   RenderTransformOrigin="0.5,0.5">
            <TextBlock.RenderTransform>
                <TranslateTransform Y="-0.5"/>
            </TextBlock.RenderTransform>
        </TextBlock>
    </Grid>
</UserControl>
```

- [ ] **Step 2: 写入 BatteryRing 代码后置**

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DsBatteryIndicator.Views;

/// <summary>
/// 环形电量进度条自定义控件。
/// 依赖属性 Progress (0-100), AccentColor, IsCharging。
/// </summary>
public partial class BatteryRing : UserControl
{
    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(nameof(Progress), typeof(double), typeof(BatteryRing),
            new PropertyMetadata(0d, OnProgressChanged));

    public static readonly DependencyProperty AccentColorProperty =
        DependencyProperty.Register(nameof(AccentColor), typeof(Brush), typeof(BatteryRing),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80)), OnAccentColorChanged));

    public static readonly DependencyProperty IsChargingProperty =
        DependencyProperty.Register(nameof(IsCharging), typeof(bool), typeof(BatteryRing),
            new PropertyMetadata(false, OnIsChargingChanged));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public Brush AccentColor
    {
        get => (Brush)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    public bool IsCharging
    {
        get => (bool)GetValue(IsChargingProperty);
        set => SetValue(IsChargingProperty, value);
    }

    public BatteryRing()
    {
        InitializeComponent();
    }

    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BatteryRing ring)
            ring.UpdateArc((double)e.NewValue);
    }

    private static void OnAccentColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BatteryRing ring && e.NewValue is Brush brush)
            ring.ForegroundArc.Stroke = brush;
    }

    private static void OnIsChargingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BatteryRing ring)
            ring.UpdateChargingAnimation((bool)e.NewValue);
    }

    private void UpdateArc(double progress)
    {
        double angle = (progress / 100.0) * 360.0;
        double radians = (angle - 90) * Math.PI / 180.0;
        double r = 20; // 环形半径
        double cx = 24, cy = 24; // 中心点

        double x = cx + r * Math.Cos(radians);
        double y = cy + r * Math.Sin(radians);

        ArcSegment.Point = new Point(x, y);
        ArcSegment.IsLargeArc = angle > 180;
    }

    private void UpdateChargingAnimation(bool isCharging)
    {
        if (isCharging)
        {
            ChargingOverlay.Visibility = Visibility.Visible;
            ChargingOverlay.Stroke = AccentColor;

            var animation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(2),
                RepeatBehavior = RepeatBehavior.Forever
            };
            var transform = (RotateTransform)ChargingOverlay.RenderTransform;
            transform.BeginAnimation(RotateTransform.AngleProperty, animation);
        }
        else
        {
            ChargingOverlay.Visibility = Visibility.Collapsed;
            var transform = (RotateTransform)ChargingOverlay.RenderTransform;
            transform.BeginAnimation(RotateTransform.AngleProperty, null);
        }
    }
}
```

**说明：** 环形使用两个 ArcSegment 组成完整圆：上半弧 (4→44) 和下半弧 (44→4)。前景环从顶部 (12点钟方向, -90°) 开始顺时针绘制。充电动画使用虚线 + 旋转实现电流流动感。

---

### Task 8: 创建 MainViewModel

**Files:**
- Create: `DsBatteryIndicator/ViewModels/MainViewModel.cs`

- [ ] **Step 1: 写入 MainViewModel**

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
            IsCharging = device.IsCharging;
            BatteryText = $"{device.BatteryLevel}%";

            switch (device.Status)
            {
                case DeviceStatus.Normal:
                    AccentColor = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80)); // 绿
                    StopBlinking();
                    _lowBatteryNotified = false;
                    break;

                case DeviceStatus.Charging:
                    AccentColor = new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA)); // 蓝
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
                    BatteryText = "——";
                    StopBlinking();
                    _lowBatteryNotified = false;
                    break;
            }
        });
    }

    private void OnConnectionChanged(bool connected)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (!connected)
            {
                Status = DeviceStatus.Disconnected;
                BatteryText = "——";
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
```

**说明：** HID 事件在后台线程触发，用 `Dispatcher.Invoke` 回 UI 线程更新绑定。`_lowBatteryNotified` 标志位确保低电量期间 Toast 只弹一次，电量恢复正常后重置。

---

### Task 9: 创建 MainWindow 浮动窗口

**Files:**
- Create: `DsBatteryIndicator/Views/MainWindow.xaml`
- Create: `DsBatteryIndicator/Views/MainWindow.xaml.cs`

- [ ] **Step 1: 写入 MainWindow XAML**

```xml
<Window x:Class="DsBatteryIndicator.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:views="clr-namespace:DsBatteryIndicator.Views"
        WindowStyle="None" AllowsTransparency="True" Topmost="True"
        Background="Transparent" ShowInTaskbar="False"
        Width="140" Height="64" ResizeMode="NoResize"
        MouseLeftButtonDown="Window_MouseLeftButtonDown">

    <Window.Resources>
        <DropShadowEffect x:Key="CardShadow" BlurRadius="20" ShadowDepth="2"
                          Opacity="0.5" Color="Black"/>
    </Window.Resources>

    <Border Background="#1A1A2E" CornerRadius="14" BorderBrush="#2A2A3E"
            BorderThickness="1" Effect="{StaticResource CardShadow}"
            RenderOptions.BitmapScalingMode="HighQuality">

        <Grid Margin="12,8">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="8"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <!-- 环形进度条 -->
            <views:BatteryRing Grid.Column="0"
                Progress="{Binding BatteryLevel}"
                AccentColor="{Binding AccentColor}"
                IsCharging="{Binding IsCharging}"/>

            <!-- 百分比数字 -->
            <TextBlock Grid.Column="2"
                       Text="{Binding BatteryText}"
                       Foreground="{Binding AccentColor}"
                       FontSize="22" FontWeight="Bold"
                       VerticalAlignment="Center"
                       FontFamily="Segoe UI"/>
        </Grid>
    </Border>

    <Window.ContextMenu>
        <ContextMenu>
            <MenuItem x:Name="MenuTopmost" IsCheckable="True" IsChecked="True"/>
            <MenuItem x:Name="MenuAutoStart" IsCheckable="True"/>
            <MenuItem x:Name="MenuLanguage" />
            <Separator/>
            <MenuItem x:Name="MenuAbout"/>
            <MenuItem x:Name="MenuExit"/>
        </ContextMenu>
    </Window.ContextMenu>
</Window>
```

- [ ] **Step 2: 写入 MainWindow 代码后置**

```csharp
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

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        _viewModel.BlinkRequested += StartBlink;
        _viewModel.BlinkStopped += StopBlink;

        // 恢复窗口位置
        double savedLeft = Properties.Settings.Default.WindowLeft;
        double savedTop = Properties.Settings.Default.WindowTop;
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
        _isAutoStart = Properties.Settings.Default.AutoStart;
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

        Closing += MainWindow_Closing;
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
        Properties.Settings.Default.AutoStart = enable;
        Properties.Settings.Default.Save();

        string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(runKey, writable: true);
        if (key == null) return;

        string exePath = Environment.ProcessPath ?? "";
        if (enable)
            key.SetValue("DsBatteryIndicator", $"\"{exePath}\"");
        else
            key.DeleteValue("DsBatteryIndicator", throwOnMissingValue: false);
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        // 保存窗口位置
        Properties.Settings.Default.WindowLeft = Left;
        Properties.Settings.Default.WindowTop = Top;
        Properties.Settings.Default.Save();

        _viewModel.Dispose();
    }
}
```

**说明：** 双击窗口重置位置到右下角。拖拽时记录位置，关闭时保存到 Settings。开机自启通过注册表 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 实现。

---

### Task 10: 创建 App 入口和系统托盘

**Files:**
- Create: `DsBatteryIndicator/App.xaml`
- Create: `DsBatteryIndicator/App.xaml.cs`

- [ ] **Step 1: 写入 App.xaml**

```xml
<Application x:Class="DsBatteryIndicator.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown">
    <Application.Resources>
        <Style TargetType="ContextMenu">
            <Setter Property="Background" Value="#2A2A2E"/>
            <Setter Property="Foreground" Value="#CCCCCC"/>
            <Setter Property="BorderBrush" Value="#3A3A3E"/>
        </Style>
        <Style TargetType="MenuItem">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="Foreground" Value="#CCCCCC"/>
        </Style>
    </Application.Resources>
</Application>
```

- [ ] **Step 2: 写入 App.xaml.cs**

```csharp
using System.Drawing;
using System.Windows;
using DsBatteryIndicator.Resources;
using DsBatteryIndicator.Services;
using DsBatteryIndicator.Views;

namespace DsBatteryIndicator;

public partial class App : Application
{
    private NotifyIcon? _notifyIcon;
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
        _notifyIcon = new NotifyIcon
        {
            Text = Strings.AppName,
            Visible = true,
            Icon = SystemIcons.Shield // 编译时会替换为 icon.ico
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
```

**说明：** `ShutdownMode="OnExplicitShutdown"` 确保关闭窗口时应用不退出，仅隐藏到系统托盘。

---

### Task 11: 创建 Settings 配置文件

**Files:**
- Create: `DsBatteryIndicator/Properties/Settings.settings`
- Create: `DsBatteryIndicator/Properties/Settings.Designer.cs`

由于 CLI 环境下 `Settings.settings` 需要 Visual Studio 生成代码，改用 `appsettings.json` 方案更可控。

- [ ] **Step 1: 创建设置服务**

创建 `DsBatteryIndicator/Services/AppSettings.cs`：

```csharp
using System.Text.Json;

namespace DsBatteryIndicator.Services;

/// <summary>
/// 应用设置持久化（JSON 文件方案，替代 Settings.settings）
/// </summary>
public class AppSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DsBatteryIndicator", "settings.json");

    private static AppSettings? _instance;
    private static readonly object _lock = new();

    public static AppSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= Load();
                }
            }
            return _instance;
        }
    }

    public double WindowLeft { get; set; }
    public double WindowTop { get; set; }
    public bool AutoStart { get; set; }
    public string Language { get; set; } = "zh-CN";
    public bool Topmost { get; set; } = true;

    public void Save()
    {
        string dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }
}
```

- [ ] **Step 2: 更新引用 AppSettings 的代码**

以下是 MainWindow 和 Strings 中需要修改的部分更新。

更新 `Strings.cs` 中的 `DetectLanguage` 和 `SetLanguage` 方法，替换 `Properties.Settings.Default` 为 `AppSettings.Instance`：

`DetectLanguage` 中的语言读取：
```csharp
// 替换 Properties.Settings.Default.Language
string saved = AppSettings.Instance.Language;
```

`SetLanguage` 中的语言保存：
```csharp
// 替换 Properties.Settings.Default.Language = langCode; Properties.Settings.Default.Save();
AppSettings.Instance.Language = langCode;
AppSettings.Instance.Save();
```

更新 `MainWindow.xaml.cs` 中的位置和自启设置，替换 `Properties.Settings.Default` 为 `AppSettings.Instance`。

**注意：** 这个 Task 在实现阶段需要更新之前 Tasks 中引用 `Properties.Settings.Default` 的代码位置。具体修改在实现时由工程师处理。

- [ ] **Step 3: 移除 .csproj 中的 `<UseWindowsForms>true</UseWindowsForms>` 后改为 false**

稍后 Task 11 的 Step 1 写入 AppSettings.cs。在实现阶段，工程师需要：
1. 创建 `DsBatteryIndicator/Services/AppSettings.cs`
2. 更新 `Strings.cs:DetectLanguage` — 将 `Properties.Settings.Default.Language` 替换为 `AppSettings.Instance.Language`
3. 更新 `Strings.cs:SetLanguage` — 将 `Properties.Settings.Default.Language = ...` 两行替换为 `AppSettings.Instance.Language = ...; AppSettings.Instance.Save();`
4. 更新 `MainWindow.xaml.cs` — 将所有 `Properties.Settings.Default.XXX` 替换为 `AppSettings.Instance.XXX`，添加 `AppSettings.Instance.Save()` 调用

---

### Task 12: 编译验证和集成测试

- [ ] **Step 1: 还原并编译项目**

```bash
cd D:/project/ds-battery-indicator/DsBatteryIndicator
dotnet restore
dotnet build
```

预期：编译成功，无错误。

- [ ] **Step 2: 运行应用**

```bash
dotnet run
```

预期：浮动窗口出现在屏幕右下角，显示"——"（未连接状态），系统托盘出现图标。

- [ ] **Step 3: 连接 DualSense 测试**

通过 USB 连接 DualSense 手柄，预期：
- 窗口自动从"——"切换为绿色电量百分比
- 环形进度条对应电量百分比
- 拔下 USB 后窗口自动恢复"——"

- [ ] **Step 4: 充电测试**

连接充电线，预期：
- 窗口切换为蓝色充电状态
- 环形进度条显示虚线旋转动画
- 不触发低电量通知

- [ ] **Step 5: 拖拽和位置测试**

- 鼠标拖拽窗口移动到任意位置
- 关闭并重新启动应用，窗口应出现在上次位置
- 双击窗口，应重置到右下角

- [ ] **Step 6: 右键菜单测试**

- 右键点击窗口，弹出菜单
- "置顶"勾选/取消，窗口置顶状态切换
- "语言"点击，菜单文字在中英文间切换
- "退出"点击，应用退出

---

### 实现顺序说明

任务按依赖关系排列，必须串行执行：
1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10 → 11 → 12

Task 11 横切所有文件，创建后在 Task 12 前统一替换引用。
