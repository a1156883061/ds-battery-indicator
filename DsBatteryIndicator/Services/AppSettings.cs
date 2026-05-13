using System.IO;
using System.Text.Json;

namespace DsBatteryIndicator.Services;

/// <summary>
/// 应用设置持久化（JSON 文件方案，替代 Settings.settings）
/// </summary>
public class AppSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DsBatteryIndicator", "settings.json");

    private static AppSettings? _instance;
    private static readonly object _lock = new();

    public static AppSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= Load();
                }
            }
            return _instance;
        }
    }

    public double WindowLeft { get; set; }
    public double WindowTop { get; set; }
    public bool AutoStart { get; set; }
    public string Language { get; set; } = "zh-CN";
    public bool Topmost { get; set; } = true;
    public bool RtssEnabled { get; set; } = false;
    public bool WindowVisible { get; set; } = true;

    // 震动/灯带配置
    public int HapticDurationMs { get; set; } = 800;
    public int HapticIntensity { get; set; } = 255;
    public int LightbarDurationMs { get; set; } = 3000;
    public byte LightbarColorR { get; set; } = 255;
    public byte LightbarColorG { get; set; } = 0;
    public byte LightbarColorB { get; set; } = 0;

    // 电量不足提示配置
    public bool LowBatteryAlertEnabled { get; set; } = true;
    public int LowBatteryThreshold { get; set; } = 10;
    public bool LowBatteryRepeatEnabled { get; set; } = true;
    public int LowBatteryRepeatIntervalMs { get; set; } = 600000;
    public double WindowOpacity { get; set; } = 1.0;

    // 手柄扬声器配置
    public bool ControllerSpeakerEnabled { get; set; } = true;
    public int ControllerSpeakerVolume { get; set; } = 80;
    public int ControllerSpeakerDurationMs { get; set; } = 800;
    public string ControllerAudioPath { get; set; } = "";

    public void Save()
    {
        string dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }
}
