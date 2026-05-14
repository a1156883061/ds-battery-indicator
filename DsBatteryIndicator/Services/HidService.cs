using HidLibrary;
using DsBatteryIndicator.Models;

namespace DsBatteryIndicator.Services;

/// <summary>
/// HID 设备枚举、连接、数据读取服务。通过 HidLibrary 轮询读取 DualSense 输入报告。
/// </summary>
public class HidService : IDisposable
{
    private const ushort SonyVid = 0x054C;
    private const ushort DualSensePid = 0x0CE6;

    private HidDevice? _device;
    private bool _disposed;
    private int _retryCount;
    private CancellationTokenSource? _cts;

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
        if (_device != null) return;

        try
        {
            var devices = HidDevices.Enumerate(SonyVid, DualSensePid).ToList();
            if (devices.Count == 0)
            {
                ConnectionChanged?.Invoke(false);
                return;
            }

            _device = devices.FirstOrDefault(d =>
                d.Capabilities.UsagePage == 0x0001 && d.Capabilities.Usage == 0x0005)
                ?? devices[0];

            _device.OpenDevice();

            if (!_device.IsConnected)
            {
                _device = null;
                ConnectionChanged?.Invoke(false);
                return;
            }

            _retryCount = 0;
            ConnectionChanged?.Invoke(true);
        }
        catch
        {
            _device = null;
            ConnectionChanged?.Invoke(false);
            if (++_retryCount >= 3) return;
            await Task.Delay(2000);
            await TryConnectAsync();
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(50, ct); }
            catch (OperationCanceledException) { return; }

            if (_device == null) { await TryConnectAsync(); continue; }
            if (!_device.IsConnected) { DisconnectDevice(); continue; }

            try
            {
                var report = await Task.Run(() => _device.ReadReport(100), ct);
                if (report?.Data == null || report.Data.Length == 0) continue;

                var device = BatteryParser.Parse(report.Data, _device.DevicePath);
                if (device != null)
                    BatteryDataReceived?.Invoke(device);
            }
            catch (OperationCanceledException) { return; }
            catch { }
        }
    }

    private void DisconnectDevice()
    {
        _device?.CloseDevice();
        _device = null;
        _retryCount = 0;
        ConnectionChanged?.Invoke(false);
    }

    /// <summary>
    /// DualSense 震动脉冲+灯带变色。参考 daidr/dualsense-tester outputStruct.ts。
    /// </summary>
    public void SendHapticPulse()
    {
        if (_device == null || !_device.IsConnected) return;

        var cfg = AppSettings.Instance;
        byte intensity = (byte)Math.Clamp(cfg.HapticIntensity, 0, 255);
        byte r = cfg.LightbarColorR, g = cfg.LightbarColorG, b = cfg.LightbarColorB;
        int hapticMs = cfg.HapticDurationMs, lightbarMs = cfg.LightbarDurationMs;

        try
        {
            var report = BuildOutputReport((byte)intensity, (byte)intensity, r, g, b);
            _device.Write(report);

            Task.Delay(hapticMs).ContinueWith(_ =>
            {
                try { _device?.Write(BuildOutputReport(0, 0, r, g, b)); }
                catch { }
            });

            Task.Delay(lightbarMs).ContinueWith(_ =>
            {
                try { _device?.Write(BuildOutputReport(0, 0, 0, 0, 255)); }
                catch { }
            });
        }
        catch { }
    }

    private static byte[] BuildOutputReport(byte rightMotor, byte leftMotor, byte r, byte g, byte b)
    {
        var cfg = AppSettings.Instance;
        byte spkVol = (byte)(cfg.ControllerSpeakerVolume * 255 / 100);
        var report = new byte[48];
        report[0] = 0x02;                                     // Report ID
        // validFlag0: bit0=motorR, bit1=motorL, bit5=speakerVol, bit7=audioCtrl
        report[1] = (byte)((rightMotor > 0 || leftMotor > 0) ? 0xA3 : 0xA0);
        report[2] = 0xF7;                                     // 功能掩码
        report[3] = rightMotor;                               // 右马达
        report[4] = leftMotor;                                // 左马达
        report[5] = 0;                                        // headphoneVolume = 0
        report[6] = spkVol;                                   // speakerVolume
        report[8] = 0x30;                                     // audioControl = 扬声器路由
        report[42] = 0x02;                                    // 灯带控制
        report[43] = 0x03;
        report[44] = 0x04;
        report[45] = r;                                       // 灯带 R
        report[46] = g;                                       // 灯带 G
        report[47] = b;                                       // 灯带 B
        return report;
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
