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
    /// 向 DualSense 发送震动脉冲（低电量触觉反馈）。
    /// DualSense USB 输出报告：ReportID=0x02, 右马达=byte[3], 左马达=byte[4]
    /// </summary>
    public void SendHapticPulse()
    {
        if (_device == null || !_device.IsConnected) return;

        try
        {
            // DualSense USB 输出报告（48 字节，含 Report ID 0x02）
            byte[] outputReport = new byte[48];
            outputReport[0] = 0x02;  // Report ID
            outputReport[1] = 0x03;  // 启用震动 + 灯带
            outputReport[3] = 128;   // 右握把马达 50% 强度（短暂脉冲）
            outputReport[4] = 128;   // 左握把马达 50% 强度

            // 灯带变红（11 段 RGB，每段 3 字节）
            for (int i = 0; i < 11; i++)
            {
                outputReport[11 + i * 3 + 0] = 255; // R
                outputReport[11 + i * 3 + 1] = 0;   // G
                outputReport[11 + i * 3 + 2] = 0;   // B
            }
            outputReport[45] = 0x05; // Player LED: 仅中间亮（5 号位）

            _device.Write(outputReport);

            // 250ms 后停止震动，恢复灯带
            Task.Delay(250).ContinueWith(_ =>
            {
                try
                {
                    byte[] stopReport = new byte[48];
                    stopReport[0] = 0x02;
                    stopReport[1] = 0x03;
                    // 马达 = 0（停止震动）
                    // 灯带恢复蓝色
                    for (int i = 0; i < 11; i++)
                    {
                        stopReport[11 + i * 3 + 0] = 0;
                        stopReport[11 + i * 3 + 1] = 0;
                        stopReport[11 + i * 3 + 2] = 255;
                    }
                    stopReport[45] = 0x05;
                    _device?.Write(stopReport);
                }
                catch { }
            });
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
