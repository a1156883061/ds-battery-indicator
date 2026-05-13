using System.IO;

namespace DsBatteryIndicator.Services;

/// <summary>
/// 简易文件日志，输出到 %LOCALAPPDATA%\DsBatteryIndicator\debug.log
/// </summary>
public static class DebugLog
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DsBatteryIndicator", "debug.log");

    private static readonly object _lock = new();

    public static void Write(string message)
    {
        try
        {
            string dir = Path.GetDirectoryName(LogPath)!;
            Directory.CreateDirectory(dir);
            lock (_lock)
            {
                File.AppendAllText(LogPath,
                    $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
        }
        catch { }
    }

    public static string LogFilePath => LogPath;
}
