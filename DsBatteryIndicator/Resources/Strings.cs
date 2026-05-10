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
            ["Show"] = "显示",
            ["Hide"] = "隐藏",
            ["Disconnected"] = "未连接",
            ["Charging"] = "充电中",
            ["LowBattery"] = "电量不足",
            ["Toast_LowBattery"] = "DualSense 电量不足 ({0}%)，请充电",
            ["About_Text"] = "DS 电池指示器 v1.0\n显示 DualSense 手柄电量",
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
            ["Show"] = "Show",
            ["Hide"] = "Hide",
            ["Disconnected"] = "Disconnected",
            ["Charging"] = "Charging",
            ["LowBattery"] = "Low Battery",
            ["Toast_LowBattery"] = "DualSense battery low ({0}%), please charge",
            ["About_Text"] = "DS Battery Indicator v1.0\nDisplay DualSense controller battery level",
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
    public static string Show => Get("Show");
    public static string Hide => Get("Hide");
    public static string Disconnected => Get("Disconnected");
    public static string Charging => Get("Charging");
    public static string LowBattery => Get("LowBattery");
    public static string Toast_LowBattery => Get("Toast_LowBattery");
    public static string About_Text => Get("About_Text");

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
