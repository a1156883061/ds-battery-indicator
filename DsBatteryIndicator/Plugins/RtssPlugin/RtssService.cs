using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace DsBatteryIndicator.Plugins.RtssPlugin;

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
    // RTSS 7.x uses session-local, 6.x and some configs use Global\
    private static readonly string[] SharedMemoryNames = { "RTSSSharedMemoryV2", "Global\\RTSSSharedMemoryV2" };
    private const uint RtssSignature = 0x52545353;
    private const string OwnerId = "DS Battery Indicator";

    private IntPtr _hMapFile = IntPtr.Zero;
    private IntPtr _pView = IntPtr.Zero;
    private int _osdSlotCount;
    private int _osdEntrySize;
    private int _osdArrOffset;
    private int _osdFrameOffset;
    private int _busyOffset = -1;
    private uint _version;
    private bool _disposed;
    private int _claimedSlot = -1;
    private int _writeCount;

    public string Name => "RTSS Overlay";
    public bool IsAvailable { get; private set; }

    public void Initialize()
    {
        try
        {
            Log("=== Init Start ===");

            string usedName = "";
            foreach (var name in SharedMemoryNames)
            {
                _hMapFile = OpenFileMapping(FILE_MAP_READ | FILE_MAP_WRITE, false, name);
                if (_hMapFile != IntPtr.Zero) { usedName = name; break; }
                Log($"OpenFileMapping '{name}' FAILED: {Marshal.GetLastWin32Error()}");
            }
            if (_hMapFile == IntPtr.Zero)
            {
                Log("All shared memory names failed");
                return;
            }
            Log($"OpenFileMapping '{usedName}' OK");

            _pView = MapViewOfFile(_hMapFile, FILE_MAP_READ | FILE_MAP_WRITE, 0, 0, 0);
            if (_pView == IntPtr.Zero)
            {
                Log($"MapViewOfFile FAILED: {Marshal.GetLastWin32Error()}");
                _hMapFile = IntPtr.Zero;
                return;
            }
            Log("MapViewOfFile OK");

            int off = 0;
            uint signature = (uint)Marshal.ReadInt32(_pView, off); off += 4;
            _version = (uint)Marshal.ReadInt32(_pView, off); off += 4;
            Log($"Signature=0x{signature:X8} (expected 0x{RtssSignature:X8}), Version={_version >> 16}.{_version & 0xFFFF}");

            if (signature != RtssSignature)
            {
                Log("BAD SIGNATURE - aborting");
                CleanupMapping();
                return;
            }

            int appEntrySize = Marshal.ReadInt32(_pView, off); off += 4;
            int appArrOffset = Marshal.ReadInt32(_pView, off); off += 4;
            int appArrSize = Marshal.ReadInt32(_pView, off); off += 4;
            Log($"App: entrySize={appEntrySize}, arrOffset={appArrOffset}, arrSize={appArrSize}");

            _osdEntrySize = Marshal.ReadInt32(_pView, off); off += 4;
            _osdArrOffset = Marshal.ReadInt32(_pView, off); off += 4;
            _osdSlotCount = Marshal.ReadInt32(_pView, off); off += 4;
            Log($"OSD: entrySize={_osdEntrySize}, arrOffset=0x{_osdArrOffset:X}, slotCount={_osdSlotCount}");

            _osdFrameOffset = off;
            int frame = Marshal.ReadInt32(_pView, _osdFrameOffset);
            Log($"dwOSDFrame offset=0x{_osdFrameOffset:X}, value={frame}");

            uint major = (_version >> 16) & 0xFFFF;
            uint minor = _version & 0xFFFF;
            if (major > 2 || (major == 2 && minor >= 14))
            {
                _busyOffset = off + 4; // after dwOSDFrame
                Log($"dwBusy offset=0x{_busyOffset:X} (v{major}.{minor} >= 2.14)");
            }

            // Dump all OSD slots' owner info
            for (int i = 0; i < Math.Min(_osdSlotCount, 8); i++)
            {
                int slotBase = _osdArrOffset + i * _osdEntrySize;
                string text = ReadFixedString(IntPtr.Add(_pView, slotBase), 40);
                string owner = ReadFixedString(IntPtr.Add(_pView, slotBase + 256), 40);
                Log($"  Slot[{i}]: text='{text}' owner='{owner}'");
            }

            IsAvailable = true;
            Log("=== Init OK ===");
        }
        catch (Exception ex)
        {
            Log($"Init exception: {ex}");
            CleanupMapping();
        }
    }

    public void UpdateBattery(int percent)
    {
        if (!IsAvailable || _pView == IntPtr.Zero) return;

        _writeCount++;
        string text = $"DS Battery: {percent}%";
        Log($"UpdateBattery #{_writeCount}: {text}");

        try
        {
            // dwBusy 锁
            if (_busyOffset >= 0)
            {
                int busy = Marshal.ReadInt32(_pView, _busyOffset);
                if ((busy & 1) != 0)
                {
                    Log($"  Busy locked (0x{busy:X8}), skipping");
                    return;
                }
                Marshal.WriteInt32(_pView, _busyOffset, busy | 1);
            }

            int slotIndex = FindOrClaimSlot();
            if (slotIndex < 0)
            {
                Log("  No available OSD slot!");
                if (_busyOffset >= 0) Marshal.WriteInt32(_pView, _busyOffset, 0);
                return;
            }
            Log($"  Writing to slot[{slotIndex}]");

            int slotBase = _osdArrOffset + slotIndex * _osdEntrySize;
            WriteFixedString(IntPtr.Add(_pView, slotBase), text, 256);
            WriteFixedString(IntPtr.Add(_pView, slotBase + 256), OwnerId, 256);

            int frame = Marshal.ReadInt32(_pView, _osdFrameOffset);
            Marshal.WriteInt32(_pView, _osdFrameOffset, frame + 1);
            Log($"  Written, frame {frame}→{frame + 1}");

            if (_busyOffset >= 0)
                Marshal.WriteInt32(_pView, _busyOffset, 0);
        }
        catch (Exception ex)
        {
            Log($"  Write exception: {ex.Message}");
            try { if (_busyOffset >= 0) Marshal.WriteInt32(_pView, _busyOffset, 0); } catch { }
        }
    }

    private int FindOrClaimSlot()
    {
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

    private static void WriteFixedString(IntPtr dest, string text, int maxBytes)
    {
        byte[] buffer = new byte[maxBytes];
        byte[] src = Encoding.ASCII.GetBytes(text);
        int len = Math.Min(src.Length, maxBytes - 1);
        Array.Copy(src, buffer, len);
        Marshal.Copy(buffer, 0, dest, maxBytes);
    }

    private static string ReadFixedString(IntPtr src, int maxBytes)
    {
        byte[] buffer = new byte[maxBytes];
        Marshal.Copy(src, buffer, 0, maxBytes);
        int nullIdx = Array.IndexOf(buffer, (byte)0);
        return Encoding.ASCII.GetString(buffer, 0, nullIdx >= 0 ? nullIdx : Math.Min(maxBytes, 40));
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
        Log("Shutdown");
    }

    private void CleanupMapping()
    {
        if (_pView != IntPtr.Zero) { UnmapViewOfFile(_pView); _pView = IntPtr.Zero; }
        if (_hMapFile != IntPtr.Zero) { CloseHandle(_hMapFile); _hMapFile = IntPtr.Zero; }
        IsAvailable = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Shutdown();
    }

    private static void Log(string msg)
    {
        try
        {
            string path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DsBatteryIndicator", "rtss.log");
            string dir = System.IO.Path.GetDirectoryName(path)!;
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff} {msg}\n");
        }
        catch { }
    }
}
