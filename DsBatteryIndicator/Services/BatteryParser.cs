using DsBatteryIndicator.Models;

namespace DsBatteryIndicator.Services;

/// <summary>
/// DualSense USB HID 输入报告解析器。
/// 参考 Linux hid-playstation.c: byte[52] bits[3:0]=电量档位(×10=%), bits[7:4]=充电状态
/// </summary>
public static class BatteryParser
{
    public static DualSenseDevice? Parse(byte[] report, string deviceId)
    {
        if (report.Length < 53) return null;

        int rawBattery = report[52];
        int level = rawBattery & 0x0F;
        int batteryLevel = Math.Clamp(level * 10, 0, 100);

        int chargeState = (rawBattery >> 4) & 0x0F;
        bool isCharging = chargeState == 1 || chargeState == 2;

        DeviceStatus status;
        if (isCharging)
            status = DeviceStatus.Charging;
        else if (batteryLevel <= AppSettings.Instance.LowBatteryThreshold)
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
