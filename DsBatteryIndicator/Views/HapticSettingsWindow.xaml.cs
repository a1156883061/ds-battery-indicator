using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using DsBatteryIndicator.Resources;
using DsBatteryIndicator.Services;

namespace DsBatteryIndicator.Views;

public partial class HapticSettingsWindow : Window
{
    public HapticSettingsWindow()
    {
        InitializeComponent();

        Title = Strings.SettingsTitle;
        BtnTest.Content = Strings.BtnTest;
        BtnSave.Content = Strings.BtnSave;

        // XAML 标签本地化
        InitLocalization();

        var cfg = AppSettings.Instance;

        // 加载配置值
        SliderIntensity.Value = cfg.HapticIntensity;
        TxtIntensity.Text = cfg.HapticIntensity.ToString();

        SliderHapticTime.Value = cfg.HapticDurationMs;
        TxtHapticTime.Text = cfg.HapticDurationMs.ToString();

        SliderLightTime.Value = cfg.LightbarDurationMs;
        TxtLightTime.Text = cfg.LightbarDurationMs.ToString();

        TxtR.Text = cfg.LightbarColorR.ToString();
        TxtG.Text = cfg.LightbarColorG.ToString();
        TxtB.Text = cfg.LightbarColorB.ToString();

        // 电量不足设置
        ChkAlertEnabled.IsChecked = cfg.LowBatteryAlertEnabled;
        SliderThreshold.Value = cfg.LowBatteryThreshold;
        TxtThreshold.Text = cfg.LowBatteryThreshold.ToString();
        ChkRepeatEnabled.IsChecked = cfg.LowBatteryRepeatEnabled;
        SliderRepeatInterval.Value = cfg.LowBatteryRepeatIntervalMs / 1000;
        TxtRepeatInterval.Text = (cfg.LowBatteryRepeatIntervalMs / 1000).ToString();

        // 滑块 ↔ 输入框 双向同步
        BindSlider(SliderIntensity, TxtIntensity, 0, 255);
        BindSlider(SliderHapticTime, TxtHapticTime, 100, 3000);
        BindSlider(SliderLightTime, TxtLightTime, 500, 10000);
        BindSlider(SliderThreshold, TxtThreshold, 10, 90);
        BindSlider(SliderRepeatInterval, TxtRepeatInterval, 15, 300);

        // RGB 输入验证
        BindRgbInput(TxtR);
        BindRgbInput(TxtG);
        BindRgbInput(TxtB);

        // 测试按钮
        BtnTest.Click += (s, e) =>
        {
            ApplyToConfig();
            Application.Current.Dispatcher.Invoke(() =>
                (Owner as MainWindow)?.ViewModel.SendHapticTest());
        };

        // 保存按钮
        BtnSave.Click += (s, e) =>
        {
            ApplyToConfig();
            cfg.Save();
            Close();
        };
    }

    private void BindSlider(Slider slider, TextBox textBox, int min, int max)
    {
        // Slider → TextBox
        slider.ValueChanged += (s, e) =>
        {
            int val = (int)e.NewValue;
            textBox.Text = val.ToString();
        };

        // TextBox → Slider
        textBox.TextChanged += (s, e) =>
        {
            if (int.TryParse(textBox.Text, out int val))
            {
                val = Math.Clamp(val, min, max);
                if (Math.Abs(slider.Value - val) > 0.5)
                    slider.Value = val;
            }
        };

        textBox.LostFocus += (s, e) =>
        {
            if (!int.TryParse(textBox.Text, out int val))
                val = (int)slider.Value;
            val = Math.Clamp(val, min, max);
            textBox.Text = val.ToString();
            slider.Value = val;
        };
    }

    private void BindRgbInput(TextBox textBox)
    {
        textBox.TextChanged += (s, e) =>
        {
            if (int.TryParse(textBox.Text, out int val))
            {
                if (val < 0 || val > 255)
                {
                    // 延迟修正（等用户输入完）
                }
            }
        };

        textBox.LostFocus += (s, e) =>
        {
            if (!int.TryParse(textBox.Text, out int val) || val < 0)
                val = 0;
            val = Math.Clamp(val, 0, 255);
            textBox.Text = val.ToString();
        };
    }

    private void InitLocalization()
    {
        LblTitle.Text = Strings.SettingsTitle;
        LblHapticIntensity.Text = Strings.HapticIntensity;
        LblHapticDuration.Text = Strings.HapticDuration;
        LblLightbarDuration.Text = Strings.LightbarDuration;
        LblLightbarColor.Text = Strings.LightbarColor;
        LblLowBatterySection.Text = Strings.LowBatterySection;
        LblAlertEnabled.Text = Strings.AlertEnabled;
        LblAlertThreshold.Text = Strings.AlertThreshold;
        LblRepeatEnabled.Text = Strings.RepeatEnabled;
        LblRepeatInterval.Text = Strings.RepeatInterval;
    }

    private void ApplyToConfig()
    {
        var cfg = AppSettings.Instance;
        cfg.HapticIntensity = ClampInt(TxtIntensity.Text, 0, 255);
        cfg.HapticDurationMs = ClampInt(TxtHapticTime.Text, 100, 3000);
        cfg.LightbarDurationMs = ClampInt(TxtLightTime.Text, 500, 10000);
        cfg.LightbarColorR = (byte)ClampInt(TxtR.Text, 0, 255);
        cfg.LightbarColorG = (byte)ClampInt(TxtG.Text, 0, 255);
        cfg.LightbarColorB = (byte)ClampInt(TxtB.Text, 0, 255);

        cfg.LowBatteryAlertEnabled = ChkAlertEnabled.IsChecked == true;
        cfg.LowBatteryThreshold = ClampInt(TxtThreshold.Text, 10, 90);
        cfg.LowBatteryRepeatEnabled = ChkRepeatEnabled.IsChecked == true;
        cfg.LowBatteryRepeatIntervalMs = ClampInt(TxtRepeatInterval.Text, 15, 300) * 1000;
    }

    private static int ClampInt(string text, int min, int max)
    {
        if (!int.TryParse(text, out int val)) val = min;
        return Math.Clamp(val, min, max);
    }
}

/// <summary>RGB 三个 TextBox 值转为 SolidColorBrush</summary>
public class RgbConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        byte r = ParseByte(values[0]?.ToString());
        byte g = ParseByte(values[1]?.ToString());
        byte b = ParseByte(values[2]?.ToString());
        return Color.FromRgb(r, g, b);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();

    private static byte ParseByte(string? s)
    {
        if (int.TryParse(s, out int v))
            return (byte)Math.Clamp(v, 0, 255);
        return 0;
    }
}
