using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DsBatteryIndicator.Plugins.RtssPlugin;

/// <summary>
/// RTSS 集成服务。通过 Windows 共享内存写入 OSD 文本数据。
/// 参考 RTSS SDK RTSSSharedMemory.h v2.0 官方结构定义。
/// OSD 槽位使用纯文本 szOSD[256]，无需浮点字段。
/// </summary>
public class RtssService : IPlugin, IDisposable
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenFileMapping(uint dwDesiredAccess, bool bInheritHandle, string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess,
        uint dwFileOffsetHigh, uint dwFileOffsetLow, uint dwNumberOfBytesToMap);

    [DllImport("kernel32.dll")]
    private static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    private const uint FILE_MAP_READ = 0x0004;
    private const uint FILE_MAP_WRITE = 0x0002;
    private const string SharedMemoryName = "Global\\RTSSSharedMemoryV2";
    private const uint RtssSignature = 0x52545353;
    private const string OwnerId = "DS Battery Indicator";
    private const int MaxRetries = 3;

    private IntPtr _hMapFile = IntPtr.Zero;
    private IntPtr _pView = IntPtr.Zero;
    private int _osdSlotCount;
    private int _osdEntrySize;
    private int _osdArrOffset;
    private int _osdFrameOffset;
    private int _busyOffset = -1;     // v2.14+
    private uint _version;
    private bool _disposed;
    private int _claimedSlot = -1;
    private bool _initFailed;

    public string Name => "RTSS Overlay";
    public bool IsAvailable { get; private set; }

    public void Initialize()
    {
        if (_initFailed) return;

        try
        {
            _hMapFile = OpenFileMapping(FILE_MAP_READ | FILE_MAP_WRITE, false, SharedMemoryName);
            if (_hMapFile == IntPtr.Zero)
            {
                Debug.WriteLine("[RtssService] RTSS 共享内存未找到，RTSS 可能未运行");
                _initFailed = true;
                return;
            }

            _pView = MapViewOfFile(_hMapFile, FILE_MAP_READ | FILE_MAP_WRITE, 0, 0, 0);
            if (_pView == IntPtr.Zero)
            {
                Debug.WriteLine("[RtssService] 映射共享内存失败");
                _initFailed = true;
                return;
            }

            // 解析头部（按 v2.0 结构顺序）
            int off = 0;
            uint signature = (uint)Marshal.ReadInt32(_pView, off); off += 4;
            _version = (uint)Marshal.ReadInt32(_pView, off); off += 4;

            if (signature != RtssSignature)
            {
                Debug.WriteLine($"[RtssService] 签名不匹配: 0x{signature:X8}");
                CleanupMapping();
                return;
            }

            // dwAppEntrySize, dwAppArrOffset, dwAppArrSize
            off += 12;

            _osdEntrySize = Marshal.ReadInt32(_pView, off); off += 4;
            _osdArrOffset = Marshal.ReadInt32(_pView, off); off += 4;
            _osdSlotCount = Marshal.ReadInt32(_pView, off); off += 4;

            // dwOSDFrame
            _osdFrameOffset = off;
            off += 4;

            // dwBusy (v2.14+, version >= 2.14)
            uint major = (_version >> 16) & 0xFFFF;
            uint minor = _version & 0xFFFF;
            if (major > 2 || (major == 2 && minor >= 14))
            {
                _busyOffset = off;
            }

            if (_osdEntrySize <= 0 || _osdArrOffset <= 0 || _osdSlotCount <= 0)
            {
                Debug.WriteLine("[RtssService] OSD 数组信息无效");
                CleanupMapping();
                return;
            }

            IsAvailable = true;
            Debug.WriteLine($"[RtssService] 已连接 v{major}.{minor}, OSD slots={_osdSlotCount}, entrySize={_osdEntrySize}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RtssService] 初始化失败: {ex.Message}");
            CleanupMapping();
        }
    }

    public void UpdateBattery(int percent)
    {
        if (!IsAvailable || _pView == IntPtr.Zero)
            return;

        string text = $"DS Battery: {percent}%";
        WriteOsdText(text);
    }

    private void WriteOsdText(string text)
    {
        for (int retry = 0; retry < MaxRetries; retry++)
        {
            try
            {
                if (_busyOffset >= 0)
                {
                    // 检查并设置 busy 锁
                    int busy = Marshal.ReadInt32(_pView, _busyOffset);
                    if ((busy & 1) != 0)
                    {
                        // 其他客户端正在写入，等待后重试
                        if (retry < MaxRetries - 1)
                        {
                            Thread.Sleep(5);
                            continue;
                        }
                        return;
                    }
                    Marshal.WriteInt32(_pView, _busyOffset, busy | 1);
                }

                int slotIndex = FindOrClaimSlot();
                if (slotIndex < 0)
                    break;

                int slotBase = _osdArrOffset + slotIndex * _osdEntrySize;

                // 写入 szOSD（偏移 0，256 字节）
                WriteFixedString(IntPtr.Add(_pView, slotBase), text, 256);

                // 写入 szOSDOwner（偏移 256，256 字节）
                WriteFixedString(IntPtr.Add(_pView, slotBase + 256), OwnerId, 256);

                // 递增 dwOSDFrame
                int frame = Marshal.ReadInt32(_pView, _osdFrameOffset);
                Marshal.WriteInt32(_pView, _osdFrameOffset, frame + 1);

                if (_busyOffset >= 0)
                {
                    Marshal.WriteInt32(_pView, _busyOffset, 0);
                }
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RtssService] 写入失败: {ex.Message}");
                try { if (_busyOffset >= 0) Marshal.WriteInt32(_pView, _busyOffset, 0); } catch { }
                return;
            }
        }
    }

    private static void WriteFixedString(IntPtr dest, string text, int maxBytes)
    {
        byte[] buffer = new byte[maxBytes];
        byte[] src = Encoding.ASCII.GetBytes(text);
        int len = Math.Min(src.Length, maxBytes - 1);
        Array.Copy(src, buffer, len);
        Marshal.Copy(buffer, 0, dest, maxBytes);
    }

    private int FindOrClaimSlot()
    {
        // 检查已认领槽位是否仍有效
        if (_claimedSlot >= 0 && _claimedSlot < _osdSlotCount)
        {
            int checkBase = _osdArrOffset + _claimedSlot * _osdEntrySize;
            string existing = ReadFixedString(IntPtr.Add(_pView, checkBase + 256), 256);
            if (existing == OwnerId || string.IsNullOrEmpty(existing))
                return _claimedSlot;
            _claimedSlot = -1;
        }

        // 搜索可用槽位
        for (int i = 0; i < _osdSlotCount; i++)
        {
            int slotBase = _osdArrOffset + i * _osdEntrySize;
            string owner = ReadFixedString(IntPtr.Add(_pView, slotBase + 256), 256);

            if (string.IsNullOrEmpty(owner) || owner == OwnerId)
            {
                _claimedSlot = i;
                return i;
            }
        }

        return -1;
    }

    private static string ReadFixedString(IntPtr src, int maxBytes)
    {
        byte[] buffer = new byte[maxBytes];
        Marshal.Copy(src, buffer, 0, maxBytes);
        int nullIdx = Array.IndexOf(buffer, (byte)0);
        return Encoding.ASCII.GetString(buffer, 0, nullIdx >= 0 ? nullIdx : maxBytes);
    }

    public void Shutdown()
    {
        if (_pView != IntPtr.Zero && _claimedSlot >= 0)
        {
            try
            {
                int slotBase = _osdArrOffset + _claimedSlot * _osdEntrySize;
                byte[] empty = new byte[256];
                Marshal.Copy(empty, 0, IntPtr.Add(_pView, slotBase), 256);
                Marshal.Copy(empty, 0, IntPtr.Add(_pView, slotBase + 256), 256);
            }
            catch { }
        }
        CleanupMapping();
        _claimedSlot = -1;
    }

    private void CleanupMapping()
    {
        if (_pView != IntPtr.Zero) { UnmapViewOfFile(_pView); _pView = IntPtr.Zero; }
        if (_hMapFile != IntPtr.Zero) { CloseHandle(_hMapFile); _hMapFile = IntPtr.Zero; }
        IsAvailable = false;
        _initFailed = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Shutdown();
    }
}
