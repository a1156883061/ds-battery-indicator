using System.Diagnostics;
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
    private const uint RtssSignature = 0x52545353;
    private const int MaxSlotCount = 128;

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
                Debug.WriteLine("[RtssService] RTSS 共享内存未找到，RTSS 可能未运行");
                IsAvailable = false;
                return;
            }

            _pView = MapViewOfFile(_hMapFile, FILE_MAP_WRITE, 0, 0, 0);
            if (_pView == IntPtr.Zero)
            {
                Debug.WriteLine("[RtssService] 映射共享内存失败");
                IsAvailable = false;
                return;
            }

            var header = (RtssHeader)Marshal.PtrToStructure(_pView, typeof(RtssHeader))!;
            if (header.Signature != RtssSignature)
            {
                Debug.WriteLine($"[RtssService] 签名不匹配: 0x{header.Signature:X8}");
                UnmapViewOfFile(_pView);
                _pView = IntPtr.Zero;
                IsAvailable = false;
                return;
            }

            IsAvailable = true;
            Debug.WriteLine($"[RtssService] 已连接, version={header.Version}, slots={header.SlotCount}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RtssService] 初始化失败: {ex.Message}");
            IsAvailable = false;
        }
    }

    public void UpdateBattery(int percent)
    {
        SetSlot(0, "DS Battery", percent, "%");
    }

    public void SetSlot(int slotIndex, string name, float value, string unit)
    {
        if (!IsAvailable || _pView == IntPtr.Zero || slotIndex < 0 || slotIndex >= MaxSlotCount)
            return;

        try
        {
            IntPtr hMutex = OpenMutex(MUTEX_ALL_ACCESS, false, MutexName);
            if (hMutex != IntPtr.Zero)
            {
                uint result = WaitForSingleObject(hMutex, 100);
                if (result != WAIT_OBJECT_0)
                {
                    CloseHandle(hMutex);
                    return;
                }
            }

            int headerSize = Marshal.SizeOf<RtssHeader>();
            int slotSize = Marshal.SizeOf<RtssOsdSlot>();
            IntPtr slotPtr = IntPtr.Add(_pView, headerSize + slotIndex * slotSize);

            var slot = new RtssOsdSlot
            {
                Name = name,
                Value = value,
                Unit = unit,
                Flags = 1,
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
            Debug.WriteLine($"[RtssService] 写入 Slot {slotIndex} 失败: {ex.Message}");
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
