using System.Diagnostics;
using DsBatteryIndicator.Models;

namespace DsBatteryIndicator.Services;

/// <summary>
/// DualSense USB HID 输入报告解析器。
/// 参考: Linux 内核 hid-playstation.c (struct dualsense_input_report)
/// 电量: byte[0x34]=byte[52], bits[3:0] → 0-10 档位 × 10 = 0-100%
/// 充电: byte[52] bits[7:4] → 0=放电, 1=充电中, 2=充满, 0xA=错误
/// </summary>
public static class BatteryParser
{
    private static int _logCount;
    private static readonly object _lock = new();

    public static DualSenseDevice? Parse(byte[] report, string deviceId)
    {
        if (report.Length < 53) return null;

        // 电量：byte[52] 低 4 位 → 0-10 档位 → ×10 = 0-100%
        int rawBattery = report[52];
        int level = rawBattery & 0x0F;
        int batteryLevel = Math.Clamp(level * 10, 0, 100);

        // 充电状态：byte[52] 高 4 位 → 0=放电, 1=充电中, 2=充满
        int chargeState = (rawBattery >> 4) & 0x0F;
        bool isCharging = chargeState == 1 || chargeState == 2;

        if (_logCount < 5)
        {
            _logCount++;
            Log($"[解析] byte[52]=0x{rawBattery:X2} (电量档位={level}→{batteryLevel}%, 充电状态高4位={chargeState})");
        }

        DeviceStatus status;
        if (isCharging)
            status = DeviceStatus.Charging;
        else if (batteryLevel <= 60)
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

    private static void Log(string msg)
    {
        lock (_lock)
        {
            try
            {
                string path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DsBatteryIndicator", "parser.log");
                string dir = System.IO.Path.GetDirectoryName(path)!;
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff} {msg}\n");
            }
            catch { }
        }
    }
}
