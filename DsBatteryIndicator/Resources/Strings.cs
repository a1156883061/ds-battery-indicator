using System.Globalization;
using DsBatteryIndicator.Services;

namespace DsBatteryIndicator.Resources;

/// <summary>
/// 多语言字符串资源。默认中文，支持英文切换。
/// </summary>
public static class Strings
{
    private static readonly Dictionary<string, Dictionary<string, string>> Resources = new()
    {
        ["zh-CN"] = new()
        {
            ["AppName"] = "DS 电池指示器",
            ["Topmost"] = "置顶",
            ["AutoStart"] = "开机自启",
            ["Language"] = "语言",
            ["About"] = "关于",
            ["Exit"] = "退出",
            ["ShowHide"] = "隐藏",
            ["RtssOverlay"] = "RTSS 叠加",
            ["HapticSettings"] = "其它设置",
            ["TestNotify"] = "测试通知",
            ["Show"] = "显示",
            ["Hide"] = "隐藏",
            ["Disconnected"] = "未连接",
            ["Charging"] = "充电中",
            ["LowBattery"] = "电量不足",
            ["Toast_LowBattery"] = "DualSense 电量不足 ({0}%)，请充电",
            ["About_Text"] = "DS 电池指示器 v1.0\n显示 DualSense 手柄电量",
            // 设置窗口
                        ["HapticIntensity"] = "震动强度",
            ["HapticDuration"] = "震动时间",
            ["LightbarDuration"] = "灯带时间",
            ["LightbarColor"] = "灯带颜色",
            ["LowBatterySection"] = "电量不足提示",
            ["AlertEnabled"] = "启用提示",
            ["AlertThreshold"] = "电量阈值",
            ["RepeatEnabled"] = "重复提醒",
            ["RepeatInterval"] = "提醒间隔",
            ["WindowOpacity"] = "窗口透明度",
            ["BtnTest"] = "测试",
            ["BtnSave"] = "保存",
            // 测试通知
            ["TestNotifyText"] = "测试通知：托盘气泡+提示音+手柄震动",
            ["TestNotifyResult"] = "三通道测试触发完成：\n• 托盘气泡\n• 提示音\n• 手柄震动+灯带",
            ["ControllerSpeaker"] = "手柄扬声器",
            ["ControllerSpeakerEnabled"] = "已启用",
            ["ControllerSpeakerDisabled"] = "已禁用",
            ["CustomAudioFile"] = "自定义音频",
            ["SelectAudioFile"] = "选择文件",
            ["NoFileSelected"] = "未选择（使用内置蜂鸣）",
            ["SpeakerDuration"] = "蜂鸣时长",
            ["SpeakerVolume"] = "扬声器音量",
            ["AudioFileFilter"] = "音频文件|*.wav;*.mp3|所有文件|*.*",
        },
        ["en"] = new()
        {
            ["AppName"] = "DS Battery Indicator",
            ["Topmost"] = "Topmost",
            ["AutoStart"] = "Auto Start",
            ["Language"] = "Language",
            ["About"] = "About",
            ["Exit"] = "Exit",
            ["ShowHide"] = "Hide",
            ["RtssOverlay"] = "RTSS Overlay",
            ["HapticSettings"] = "Other Settings",
            ["TestNotify"] = "Test Notification",
            ["Show"] = "Show",
            ["Hide"] = "Hide",
            ["Disconnected"] = "Disconnected",
            ["Charging"] = "Charging",
            ["LowBattery"] = "Low Battery",
            ["Toast_LowBattery"] = "DualSense battery low ({0}%), please charge",
            ["About_Text"] = "DS Battery Indicator v1.0\nDisplay DualSense controller battery level",
                        ["HapticIntensity"] = "Intensity",
            ["HapticDuration"] = "Duration",
            ["LightbarDuration"] = "Lightbar Time",
            ["LightbarColor"] = "Lightbar Color",
            ["LowBatterySection"] = "Low Battery Alert",
            ["AlertEnabled"] = "Alert Enabled",
            ["AlertThreshold"] = "Threshold",
            ["RepeatEnabled"] = "Repeat",
            ["RepeatInterval"] = "Interval",
            ["WindowOpacity"] = "Window Opacity",
            ["BtnTest"] = "Test",
            ["BtnSave"] = "Save",
            ["TestNotifyText"] = "Test: tray bubble + sound + controller haptic",
            ["TestNotifyResult"] = "Test triggered:\n* Tray bubble\n* Alert sound\n* Controller haptic + lightbar",
            ["ControllerSpeaker"] = "Controller Speaker",
            ["ControllerSpeakerEnabled"] = "Enabled",
            ["ControllerSpeakerDisabled"] = "Disabled",
            ["CustomAudioFile"] = "Custom Audio",
            ["SelectAudioFile"] = "Select File",
            ["NoFileSelected"] = "None (built-in beep)",
            ["SpeakerDuration"] = "Beep Duration",
            ["SpeakerVolume"] = "Speaker Volume",
            ["AudioFileFilter"] = "Audio Files|*.wav;*.mp3|All Files|*.*",
        },
    };

    private static string _currentLanguage = "zh-CN";

    public static event Action? LanguageChanged;

    /// <summary>当前语言代码</summary>
    public static string CurrentLanguage => _currentLanguage;

    /// <summary>切换语言，保存偏好</summary>
    public static void SetLanguage(string langCode)
    {
        if (Resources.ContainsKey(langCode) && _currentLanguage != langCode)
        {
            _currentLanguage = langCode;
            AppSettings.Instance.Language = langCode;
            AppSettings.Instance.Save();
            LanguageChanged?.Invoke();
        }
    }

    /// <summary>根据系统 CultureInfo 自动选择语言</summary>
    public static void DetectLanguage()
    {
        string saved = AppSettings.Instance.Language;
        if (!string.IsNullOrEmpty(saved) && Resources.ContainsKey(saved))
        {
            _currentLanguage = saved;
            return;
        }

        string culture = CultureInfo.CurrentUICulture.Name;
        // 精确匹配
        if (Resources.ContainsKey(culture))
        {
            _currentLanguage = culture;
            return;
        }
        // 只匹配主要语言（如 "zh" → "zh-CN"）
        string primary = culture.Split('-')[0];
        var match = Resources.Keys.FirstOrDefault(k => k.StartsWith(primary));
        if (match != null)
        {
            _currentLanguage = match;
        }
        // 否则保持默认中文
    }

    public static string AppName => Get("AppName");
    public static string Topmost => Get("Topmost");
    public static string AutoStart => Get("AutoStart");
    public static string Language => Get("Language");
    public static string About => Get("About");
    public static string Exit => Get("Exit");
    public static string ShowHide => Get("ShowHide");
    public static string RtssOverlay => Get("RtssOverlay");
    public static string HapticSettings => Get("HapticSettings");
    public static string TestNotify => Get("TestNotify");
    public static string Show => Get("Show");
    public static string Hide => Get("Hide");
    public static string Disconnected => Get("Disconnected");
    public static string Charging => Get("Charging");
    public static string LowBattery => Get("LowBattery");
    public static string Toast_LowBattery => Get("Toast_LowBattery");
    public static string About_Text => Get("About_Text");
    public static string HapticIntensity => Get("HapticIntensity");
    public static string HapticDuration => Get("HapticDuration");
    public static string LightbarDuration => Get("LightbarDuration");
    public static string LightbarColor => Get("LightbarColor");
    public static string LowBatterySection => Get("LowBatterySection");
    public static string AlertEnabled => Get("AlertEnabled");
    public static string AlertThreshold => Get("AlertThreshold");
    public static string RepeatEnabled => Get("RepeatEnabled");
    public static string RepeatInterval => Get("RepeatInterval");
    public static string WindowOpacity => Get("WindowOpacity");
    public static string BtnTest => Get("BtnTest");
    public static string BtnSave => Get("BtnSave");
    public static string TestNotifyText => Get("TestNotifyText");
    public static string TestNotifyResult => Get("TestNotifyResult");
    public static string ControllerSpeaker => Get("ControllerSpeaker");
    public static string ControllerSpeakerEnabled => Get("ControllerSpeakerEnabled");
    public static string ControllerSpeakerDisabled => Get("ControllerSpeakerDisabled");
    public static string CustomAudioFile => Get("CustomAudioFile");
    public static string SelectAudioFile => Get("SelectAudioFile");
    public static string NoFileSelected => Get("NoFileSelected");
    public static string SpeakerDuration => Get("SpeakerDuration");
    public static string SpeakerVolume => Get("SpeakerVolume");
    public static string AudioFileFilter => Get("AudioFileFilter");

    private static string Get(string key)
    {
        if (Resources.TryGetValue(_currentLanguage, out var dict) && dict.TryGetValue(key, out var value))
            return value;
        // 回退到中文
        if (Resources.TryGetValue("zh-CN", out var fallback) && fallback.TryGetValue(key, out var fb))
            return fb;
        return key;
    }
}
