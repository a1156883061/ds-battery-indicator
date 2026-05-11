# RTSS 集成实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 通过 Windows 共享内存将 DualSense 电量写入 RTSS OSD 叠加层，RTSS 未运行时静默降级。

**Architecture:** 新建 Plugins/RtssPlugin/RtssService.cs，通过 P/Invoke 打开 `Global\RTSSSharedMemoryV2` 映射文件，按社区逆向协议的 OSD Slot 结构写入电量数据。MainViewModel 在电池数据更新时调用 RtssService.UpdateBattery()。插件通过 IPlugin 接口与主程序解耦。

**Tech Stack:** C# P/Invoke (kernel32.dll), Windows Memory-Mapped File, RTSSSharedMemoryV2 协议

---

### Task 1: 创建插件接口和目录结构

**Files:**
- Create: `DsBatteryIndicator/Plugins/IPlugin.cs`
- Create: `DsBatteryIndicator/Plugins/RtssPlugin/RtssService.cs`（空壳）

- [ ] **Step 1: 创建目录结构**

```bash
mkdir -p D:/project/ds-battery-indicator/DsBatteryIndicator/Plugins/RtssPlugin
```

- [ ] **Step 2: 写入 IPlugin 接口**

创建 `DsBatteryIndicator/Plugins/IPlugin.cs`：

```csharp
namespace DsBatteryIndicator.Plugins;

/// <summary>
/// 插件标准生命周期接口。每个插件自包含，互不依赖。
/// </summary>
public interface IPlugin
{
    string Name { get; }
    bool IsAvailable { get; }
    void Initialize();
    void Shutdown();
}
```

- [ ] **Step 3: 写入 RtssService 空壳**

创建 `DsBatteryIndicator/Plugins/RtssPlugin/RtssService.cs`：

```csharp
namespace DsBatteryIndicator.Plugins.RtssPlugin;

public class RtssService : IPlugin, IDisposable
{
    public string Name => "RTSS Overlay";
    public bool IsAvailable => false;

    public void Initialize()
    {
    }

    public void UpdateBattery(int percent)
    {
    }

    public void Shutdown()
    {
    }

    public void Dispose()
    {
        Shutdown();
    }
}
```

- [ ] **Step 4: 编译验证**

```bash
cd D:/project/ds-battery-indicator/DsBatteryIndicator && dotnet build
```

预期：编译成功。

- [ ] **Step 5: 提交**

```bash
git add Plugins/
git commit -m "feat: 创建插件接口和 RtssService 空壳"
```

---

### Task 2: 实现 RtssService 核心逻辑

**Files:**
- Modify: `DsBatteryIndicator/Plugins/RtssPlugin/RtssService.cs`

- [ ] **Step 1: 写入完整 RtssService**

用以下代码覆盖 `DsBatteryIndicator/Plugins/RtssPlugin/RtssService.cs`：

```csharp
using System.Runtime.InteropServices;

namespace DsBatteryIndicator.Plugins.RtssPlugin;

/// <summary>
/// RTSS 集成服务。通过 Windows 共享内存写入 OSD 数据。
/// RTSS 未运行或版本不兼容时静默降级。
/// </summary>
public class RtssService : IPlugin, IDisposable
{
    // P/Invoke 声明
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenFileMapping(uint dwDesiredAccess, bool bInheritHandle, string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess,
        uint dwFileOffsetHigh, uint dwFileOffsetLow, uint dwNumberOfBytesToMap);

    [DllImport("kernel32.dll")]
    private static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenMutex(uint dwDesiredAccess, bool bInheritHandle, string lpName);

    [DllImport("kernel32.dll")]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll")]
    private static extern bool ReleaseMutex(IntPtr hMutex);

    private const uint FILE_MAP_WRITE = 0x0002;
    private const uint MUTEX_ALL_ACCESS = 0x1F0001;
    private const uint WAIT_OBJECT_0 = 0;
    private const uint WAIT_TIMEOUT = 258;
    private const string SharedMemoryName = "Global\\RTSSSharedMemoryV2";
    private const string MutexName = "Global\\RTSSSharedMemoryV2_Mutex";
    private const uint RtssSignature = 0x52545353; // "RTSS"
    private const int MaxSlotCount = 128;

    // RTSS 共享内存头部（32 字节）
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct RtssHeader
    {
        public uint Signature;
        public uint Version;
        public uint TotalSize;
        public uint Flags;
        public uint SlotSize;
        public uint SlotCount;
        public uint Padding1;
        public uint Padding2;
    }

    // RTSS OSD 槽位
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
    private struct RtssOsdSlot
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Name;
        public float Value;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public string Unit;
        public uint Flags;
        public uint Color;
        public float ValueMax;
        public float ValueMin;
        public uint Reserved1;
        public uint Reserved2;
        public uint Reserved3;
        public uint Reserved4;
    }

    private IntPtr _hMapFile = IntPtr.Zero;
    private IntPtr _pView = IntPtr.Zero;
    private bool _disposed;

    public string Name => "RTSS Overlay";
    public bool IsAvailable { get; private set; }

    public void Initialize()
    {
        try
        {
            _hMapFile = OpenFileMapping(FILE_MAP_WRITE, false, SharedMemoryName);
            if (_hMapFile == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("[RtssService] RTSS 共享内存未找到，RTSS 可能未运行");
                IsAvailable = false;
                return;
            }

            _pView = MapViewOfFile(_hMapFile, FILE_MAP_WRITE, 0, 0, 0);
            if (_pView == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("[RtssService] 映射共享内存失败");
                IsAvailable = false;
                return;
            }

            // 校验签名
            var header = Marshal.PtrToStructure<RtssHeader>(_pView);
            if (header.Signature != RtssSignature)
            {
                System.Diagnostics.Debug.WriteLine($"[RtssService] 签名不匹配: 0x{header.Signature:X8}");
                UnmapViewOfFile(_pView);
                _pView = IntPtr.Zero;
                IsAvailable = false;
                return;
            }

            IsAvailable = true;
            System.Diagnostics.Debug.WriteLine($"[RtssService] 已连接, version={header.Version}, slots={header.SlotCount}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RtssService] 初始化失败: {ex.Message}");
            IsAvailable = false;
        }
    }

    /// <summary>
    /// 更新电池电量到 RTSS OSD Slot 0
    /// </summary>
    public void UpdateBattery(int percent)
    {
        SetSlot(0, "DS Battery", percent, "%");
    }

    /// <summary>
    /// 写入指定 OSD 槽位。slotIndex 超出范围或服务不可用时静默跳过。
    /// </summary>
    public void SetSlot(int slotIndex, string name, float value, string unit)
    {
        if (!IsAvailable || _pView == IntPtr.Zero || slotIndex < 0 || slotIndex >= MaxSlotCount)
            return;

        try
        {
            // 获取 Mutex 锁
            IntPtr hMutex = OpenMutex(MUTEX_ALL_ACCESS, false, MutexName);
            if (hMutex != IntPtr.Zero)
            {
                uint result = WaitForSingleObject(hMutex, 100);
                if (result != WAIT_OBJECT_0)
                {
                    CloseHandle(hMutex);
                    return; // 获取锁超时，跳过本次写入
                }
            }

            // 计算 slot 地址
            int headerSize = Marshal.SizeOf<RtssHeader>();
            int slotSize = Marshal.SizeOf<RtssOsdSlot>();
            IntPtr slotPtr = IntPtr.Add(_pView, headerSize + slotIndex * slotSize);

            var slot = new RtssOsdSlot
            {
                Name = name,
                Value = value,
                Unit = unit,
                Flags = 1,   // enabled
            };

            Marshal.StructureToPtr(slot, slotPtr, false);

            if (hMutex != IntPtr.Zero)
            {
                ReleaseMutex(hMutex);
                CloseHandle(hMutex);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RtssService] 写入 Slot {slotIndex} 失败: {ex.Message}");
        }
    }

    public void Shutdown()
    {
        if (_pView != IntPtr.Zero)
        {
            UnmapViewOfFile(_pView);
            _pView = IntPtr.Zero;
        }
        if (_hMapFile != IntPtr.Zero)
        {
            CloseHandle(_hMapFile);
            _hMapFile = IntPtr.Zero;
        }
        IsAvailable = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Shutdown();
    }
}
```

- [ ] **Step 2: 编译验证**

```bash
cd D:/project/ds-battery-indicator/DsBatteryIndicator && dotnet build
```

预期：编译成功。注意 `Marshal.PtrToStructure<T>` 在 .NET 8 中标记为过时，警告可忽略；实际用 `Marshal.PtrToStructure(IntPtr, Type)` 替代。

- [ ] **Step 3: 修复可能的过时警告**

如果编译出现 `CS0618` 警告，改为使用非泛型版本：

```csharp
var header = (RtssHeader)Marshal.PtrToStructure(_pView, typeof(RtssHeader))!;
```

- [ ] **Step 4: 提交**

```bash
git add Plugins/RtssPlugin/RtssService.cs
git commit -m "feat: 实现 RtssService 共享内存写入逻辑"
```

---

### Task 3: 添加设置项和视图集成

**Files:**
- Modify: `DsBatteryIndicator/Services/AppSettings.cs` — 添加 `RtssEnabled` 属性
- Modify: `DsBatteryIndicator/Resources/Strings.cs` — 添加 "RTSS 叠加" 字符串
- Modify: `DsBatteryIndicator/Views/MainWindow.xaml` — 添加菜单项
- Modify: `DsBatteryIndicator/Views/MainWindow.xaml.cs` — 处理菜单事件

- [ ] **Step 1: 修改 AppSettings.cs**

读取 `DsBatteryIndicator/Services/AppSettings.cs`，添加属性：

```csharp
public bool RtssEnabled { get; set; } = false;
```

- [ ] **Step 2: 修改 Strings.cs**

读取 `DsBatteryIndicator/Resources/Strings.cs`，在两种语言中各添加：

```
zh-CN: ["RtssOverlay"] = "RTSS 叠加",
en:    ["RtssOverlay"] = "RTSS Overlay",
```

添加属性：
```csharp
public static string RtssOverlay => Get("RtssOverlay");
```

- [ ] **Step 3: 修改 MainWindow.xaml**

在 `<ContextMenu>` 中添加新的 MenuItem（放在 Separator 之前）：

```xml
<MenuItem x:Name="MenuRtss" IsCheckable="True"/>
```

- [ ] **Step 4: 修改 MainWindow.xaml.cs**

在构造函数中添加：

```csharp
// RTSS 菜单
MenuRtss.IsChecked = AppSettings.Instance.RtssEnabled;
MenuRtss.Click += (s, e) =>
{
    bool enable = MenuRtss.IsChecked;
    AppSettings.Instance.RtssEnabled = enable;
    AppSettings.Instance.Save();
    if (enable)
        _viewModel.EnableRtss();
    else
        _viewModel.DisableRtss();
};
```

在 `UpdateMenuTexts()` 中添加：
```csharp
MenuRtss.Header = Strings.RtssOverlay;
```

- [ ] **Step 5: 提交**

```bash
git add -A
git commit -m "feat: RTSS 叠加菜单项与设置持久化"
```

---

### Task 4: 集成到 MainViewModel

**Files:**
- Modify: `DsBatteryIndicator/ViewModels/MainViewModel.cs`

- [ ] **Step 1: 添加 RtssService 字段和初始化**

在 MainViewModel.cs 中添加：

```csharp
// using DsBatteryIndicator.Plugins.RtssPlugin;（顶部 using）

private RtssService? _rtssService;

// 在 MainViewModel() 构造函数末尾：
if (AppSettings.Instance.RtssEnabled)
{
    _rtssService = new RtssService();
    _rtssService.Initialize();
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
```

- [ ] **Step 2: 在电池数据回调中写入 RTSS**

在 `OnBatteryDataReceived` 方法中，`Dispatcher.Invoke` 回调末尾（`IsCharging = device.IsCharging;` 之后）添加：

```csharp
_rtssService?.UpdateBattery(device.BatteryLevel);
```

- [ ] **Step 3: 在 Dispose 中清理**

在 `MainViewModel.Dispose()` 中添加：

```csharp
_rtssService?.Dispose();
```

- [ ] **Step 4: 编译验证**

```bash
cd D:/project/ds-battery-indicator/DsBatteryIndicator && dotnet build
```

- [ ] **Step 5: 提交**

```bash
git add -A
git commit -m "feat: MainViewModel 集成 RtssService"
```

---

### Task 5: 功能测试和验证

- [ ] **Step 1: 编译**

```bash
cd D:/project/ds-battery-indicator/DsBatteryIndicator && dotnet build
```

预期：0 错误。

- [ ] **Step 2: 启动 RTSS 后运行应用**

```bash
dotnet run
```

右键菜单勾选 "RTSS 叠加"，预期：
- 如果 RTSS 正在运行 → OSD 显示 "DS Battery: 70%"（或其他当前电量值）
- 如果 RTSS 未运行 → 无任何影响，应用正常工作
- 取消勾选 → OSD 中 DS Battery 条目消失

- [ ] **Step 3: 提交**

```bash
git add -A
git commit -m "chore: RTSS 集成自测通过"
```

---

### 实现顺序

Task 1 → Task 2 → Task 3 → Task 4 → Task 5（严格串行）
