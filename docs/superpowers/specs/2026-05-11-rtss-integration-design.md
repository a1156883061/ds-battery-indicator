# RTSS 集成设计

## 概述

通过写入 RTSS 共享内存（`RTSSSharedMemoryV2`），将 DualSense 电量数据推送到 RivaTuner Statistics Server 的 OSD 叠加层。RTSS 未运行时静默降级，不影响主体功能。

## 技术方案

| 项目 | 选择 |
|------|------|
| 通信方式 | Windows 共享内存映射文件（Memory-Mapped File） |
| 协议 | RTSSSharedMemoryV2（社区逆向） |
| P/Invoke | `OpenFileMapping` / `MapViewOfFile` / `CreateMutex` |
| 架构 | 独立插件模块，`Plugins/RtssPlugin/` 目录 |

## 项目结构

```
DsBatteryIndicator/
├── Plugins/
│   └── RtssPlugin/
│       └── RtssService.cs          # RTSS 共享内存写入
├── Services/
│   ├── HidService.cs
│   ├── BatteryParser.cs
│   ├── NotificationService.cs
│   └── AppSettings.cs
├── ...
```

## 插件接口约定

每个 Plugin 自包含，对外暴露统一生命周期：

```csharp
// 插件标准接口
interface IPlugin
{
    bool IsAvailable { get; }       // 当前是否可用（RTSS 是否运行）
    string Name { get; }            // 插件名称
    void Initialize();              // 启动时调用，尝试连接
    void Shutdown();                // 退出时调用，释放资源
}
```

## 核心类：RtssService

### RTSS 共享内存结构

```csharp
// Header（32 字节）
struct RtssHeader {
    uint Signature;   // 魔数校验
    uint Version;
    uint SlotCount;   // 最大 128
    uint Flags;
    // ...
};

// OSD Slot
struct RtssOsdSlot {
    // 256 bytes: slot name (wide char)
    // 4 bytes:  float value
    // 16 bytes: unit string
    // 4 bytes:  flags (visible, formatting)
    // 4 bytes:  color
    // ...
};
```

### P/Invoke 调用链

1. `OpenFileMapping` → 打开 `Global\RTSSSharedMemoryV2`
2. `MapViewOfFile` → 映射到进程地址空间
3. `WaitForSingleObject` → 获取 Mutex（防止竞争）
4. 写入 slot 结构体 → 更新 value 字段
5. `ReleaseMutex` → 释放锁

### API

| 方法 | 说明 |
|------|------|
| `bool TryConnect()` | 尝试打开共享内存，返回是否成功 |
| `void SetSlot(int index, string name, float value, string unit)` | 写入槽位 |
| `void UpdateBattery(int percent)` | 便捷方法：更新 slot[0] 电量 |
| `void Shutdown()` | 释放共享内存和 Mutex |

## 数据流

```
HidService → BatteryParser → MainViewModel
                                   │
                                   ├── UI 绑定（现有）
                                   │
                                   └── RtssService.SetSlot(0, "DS Battery", 70, "%")
                                         │
                                         ▼
                                   Global\RTSSSharedMemoryV2
                                         │
                                         ▼
                                   RTSS 引擎 → OSD 渲染
```

## 与 MainViewModel 集成

```csharp
// MainViewModel.OnBatteryDataReceived 中：
if (_rtssService != null && _rtssService.IsAvailable)
{
    _rtssService.SetSlot(0, "DS Battery", device.BatteryLevel, "%");
}
```

## 槽位规划

| Slot | 名称 | 用途 | 状态 |
|------|------|------|------|
| 0 | DS Battery | 电量百分比 | ✅ 实现 |
| 1-3 | 预留 | 预估时间、充电功率等 | 待定 |

## 错误处理

| 场景 | 策略 |
|------|------|
| RTSS 未运行 | `OpenFileMapping` 返回 NULL → `IsAvailable=false`，静默跳过 |
| 版本不兼容 | 校验 Signature 魔数，不匹配则不写入 |
| Mutex 超时 | 等待 100ms 后放弃本次写入，不阻塞 |
| 写入冲突 | Mutex 保护临界区 |
| 生命周期 | `IDisposable`，Application.Exit 时释放 |

## 右键菜单

MainWindow 右键菜单新增 "RTSS 叠加" Checkable 项：
- 勾选 → 启用 RtssService
- 取消 → 关闭 RtssService（RTSS OSD 不再显示 DS 数据）
- 设置持久化：`AppSettings.Instance.RtssEnabled`

## 不纳入本期

- 多 slot 自定义配置 UI（仅代码预留）
- 其他叠加层支持（GamePP、PresentMon）
- RTSS 颜色/字体自定义
