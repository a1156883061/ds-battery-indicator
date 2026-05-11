using System.Diagnostics;
using HidLibrary;
using DsBatteryIndicator.Models;

namespace DsBatteryIndicator.Services;

/// <summary>
/// HID 设备枚举、连接、数据读取服务。
/// </summary>
public class HidService : IDisposable
{
    private const ushort SonyVid = 0x054C;
    private const ushort DualSensePid = 0x0CE6;

    private static readonly string LogPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DsBatteryIndicator", "debug.log");

    private static void Log(string msg)
    {
        try
        {
            string dir = System.IO.Path.GetDirectoryName(LogPath)!;
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {msg}\n");
        }
        catch { }
    }

    private HidDevice? _device;
    private bool _disposed;
    private int _retryCount;
    private CancellationTokenSource? _cts;
    private int _consecutiveNullReports;
    private bool _firstReportLogged;

    public event Action<DualSenseDevice>? BatteryDataReceived;
    public event Action<bool>? ConnectionChanged;

    public void StartWatching()
    {
        _cts = new CancellationTokenSource();
        _ = TryConnectAsync();
        _ = ReadLoopAsync(_cts.Token);
    }

    private async Task TryConnectAsync()
    {
        if (_device != null)
            return;

        try
        {
            // 不使用 usage page 过滤，只用 VID/PID
            var allDevices = HidDevices.Enumerate(SonyVid, DualSensePid).ToList();
            Log($"[HidService] 枚举到 {allDevices.Count} 个 Sony 设备");

            foreach (var d in allDevices)
            {
                Log($"[HidService]   设备: Path={d.DevicePath}, Connected={d.IsConnected}, VID={d.Attributes.VendorId:X4}, PID={d.Attributes.ProductId:X4}, UsagePage={d.Capabilities.UsagePage:X4}, Usage={d.Capabilities.Usage:X4}");
            }

            if (allDevices.Count == 0)
            {
                ConnectionChanged?.Invoke(false);
                return;
            }

            // 优先选择 UsagePage=0x0001, Usage=0x0005 的设备（标准游戏手柄）
            _device = allDevices.FirstOrDefault(d =>
                d.Capabilities.UsagePage == 0x0001 && d.Capabilities.Usage == 0x0005)
                ?? allDevices[0];

            Log($"[HidService] 选择设备: {_device.DevicePath}, UsagePage={_device.Capabilities.UsagePage:X4}");

            _device.OpenDevice();

            if (!_device.IsConnected)
            {
                Log("[HidService] 设备打开后未连接");
                _device = null;
                ConnectionChanged?.Invoke(false);
                return;
            }

            _retryCount = 0;
            _consecutiveNullReports = 0;
            ConnectionChanged?.Invoke(true);
            Log($"[HidService] 已连接: {_device.DevicePath}, InputReportByteLength={_device.Capabilities.InputReportByteLength}");
        }
        catch (Exception ex)
        {
            Log($"[HidService] 连接失败: {ex.Message}");
            _device = null;
            ConnectionChanged?.Invoke(false);

            _retryCount++;
            if (_retryCount >= 3)
                return;

            await Task.Delay(2000);
            await TryConnectAsync();
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(50, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_device == null)
            {
                await TryConnectAsync();
                continue;
            }

            if (!_device.IsConnected)
            {
                Log("[HidService] 设备断开，重连中...");
                DisconnectDevice();
                continue;
            }

            try
            {
                var report = await Task.Run(() => _device.ReadReport(100), ct);

                if (report?.Data != null && report.Data.Length > 0)
                {
                    _consecutiveNullReports = 0;

                    if (!_firstReportLogged)
                    {
                        _firstReportLogged = true;
                        Log($"[HidService] 首份报告示例: {report.Data.Length} 字节, ReportID={report.ReportId}");
                        Log($"[HidService] 原始数据(hex,前64): {BitConverter.ToString(report.Data.Take(64).ToArray())}");
                    }

                    var device = BatteryParser.Parse(report.Data, _device.DevicePath);
                    if (device != null)
                    {
                        Log($"[HidService] 电量: {device.BatteryLevel}%, 充电: {device.IsCharging}, 状态: {device.Status}");
                        BatteryDataReceived?.Invoke(device);
                    }
                }
                else
                {
                    _consecutiveNullReports++;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Log($"[HidService] 读取异常: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private void DisconnectDevice()
    {
        if (_device != null)
        {
            _device.CloseDevice();
            _device = null;
        }
        _retryCount = 0;
        ConnectionChanged?.Invoke(false);
    }

    /// <summary>
    /// 向 DualSense 发送震动脉冲+灯带变色（低电量触觉反馈）。
    /// 参考 daidr/dualsense-tester outputStruct.ts payload 格式。
    /// validFlag1 bit2=1 触发灯带更新，bit3 保持 0。
    /// </summary>
    public void SendHapticPulse()
    {
        if (_device == null || !_device.IsConnected) return;

        try
        {
            byte[] report = new byte[48];
            report[0] = 0x02;   // Report ID

            report[1] = 0x03;   // validFlag0: 启用左右马达(bit0+bit1)
            report[2] = 0xF7;   // validFlag1: 默认值，bit2=1 已含
            report[3] = 255;    // 右马达
            report[4] = 255;    // 左马达

            // 灯带：仅设置 RGB，不覆盖 lightbarSetup/ledBrightness
            report[42] = 0x02;  // lightbarSetup: 启用外部控制
            report[43] = 0x03;  // ledBrightness: 中等亮度（0-4）
            report[44] = 0x04;  // playerIndicator: 中间 LED
            report[45] = 255;   // R
            report[46] = 0;     // G
            report[47] = 0;     // B

            bool ok = _device.Write(report);
            HapticLog($"红色灯带+震动: Write={ok}");

            // 800ms 后恢复蓝色灯带，停止震动
            Task.Delay(800).ContinueWith(_ =>
            {
                try
                {
                    byte[] stop = new byte[48];
                    stop[0] = 0x02;
                    stop[1] = 0x00;   // 马达停止
                    stop[2] = 0xF7;   // 保持灯带控制位
                    stop[42] = 0x02;
                    stop[43] = 0x03;
                    stop[44] = 0x04;
                    stop[45] = 0;
                    stop[46] = 0;
                    stop[47] = 255;   // 蓝色
                    _device?.Write(stop);
                    HapticLog("蓝色灯带+停止震动");
                }
                catch { }
            });
        }
        catch (Exception ex)
        {
            HapticLog($"异常: {ex.Message}");
        }
    }

    private static void HapticLog(string msg)
    {
        try
        {
            string path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DsBatteryIndicator", "haptic.log");
            string dir = System.IO.Path.GetDirectoryName(path)!;
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff} {msg}\n");
        }
        catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        _cts?.Dispose();
        DisconnectDevice();
    }
}
