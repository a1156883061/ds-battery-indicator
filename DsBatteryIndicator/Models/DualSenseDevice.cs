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
