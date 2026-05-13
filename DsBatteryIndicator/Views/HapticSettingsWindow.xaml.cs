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

        Title = Strings.HapticSettings;
        BtnTest.Content = Strings.BtnTest;
        BtnSave.Content = Strings.BtnSave;

        // XAML 标签本地化
        InitLocalization();

        var cfg = AppSettings.Instance;

        // 加载配置值
        SliderIntensity.Value = cfg.HapticIntensity;
        TxtIntensity.Text = cfg.HapticIntensity.ToString();

        TxtHapticTime.Text = cfg.HapticDurationMs.ToString();

        TxtLightTime.Text = cfg.LightbarDurationMs.ToString();

        TxtR.Text = cfg.LightbarColorR.ToString();
        TxtG.Text = cfg.LightbarColorG.ToString();
        TxtB.Text = cfg.LightbarColorB.ToString();

        // 电量不足设置
        ChkAlertEnabled.IsChecked = cfg.LowBatteryAlertEnabled;
        SliderThreshold.Value = cfg.LowBatteryThreshold;
        TxtThreshold.Text = cfg.LowBatteryThreshold.ToString();
        ChkRepeatEnabled.IsChecked = cfg.LowBatteryRepeatEnabled;
        TxtRepeatInterval.Text = (cfg.LowBatteryRepeatIntervalMs / 1000).ToString();

        int opacityPct = (int)(cfg.WindowOpacity * 100);
        SliderOpacity.Value = opacityPct;
        TxtOpacity.Text = opacityPct.ToString();

        // 手柄扬声器
        ChkControllerSpeaker.IsChecked = cfg.ControllerSpeakerEnabled;
        _selectedAudioPath = string.IsNullOrWhiteSpace(cfg.ControllerAudioPath)
            ? null : cfg.ControllerAudioPath;
        SliderSpeakerVolume.Value = cfg.ControllerSpeakerVolume;
        TxtSpeakerVolume.Text = cfg.ControllerSpeakerVolume.ToString();
        TxtSpeakerDuration.Text = cfg.ControllerSpeakerDurationMs.ToString();
        UpdateControllerSpeakerLabel();
        UpdateAudioPathDisplay();

        // 滑块 ↔ 输入框 双向同步
        BindSlider(SliderIntensity, TxtIntensity, 0, 255);

        BindSlider(SliderThreshold, TxtThreshold, 10, 90);
        BindSlider(SliderSpeakerVolume, TxtSpeakerVolume, 10, 100);
        BindSlider(SliderOpacity, TxtOpacity, 30, 100);

        // RGB 输入验证
        BindRgbInput(TxtR);
        BindRgbInput(TxtG);
        BindRgbInput(TxtB);

        // 预设间隔按钮
        InitPresetButtons();

        // 测试按钮
        BtnTest.Click += (s, e) =>
        {
            ApplyToConfig();
            if (Owner is MainWindow mw) mw.Opacity = cfg.WindowOpacity;
            Application.Current.Dispatcher.Invoke(() =>
                (Owner as MainWindow)?.ViewModel.SendHapticTest());

            // 同时测试手柄扬声器
            if (cfg.ControllerSpeakerEnabled)
            {
                bool ok;
                if (!string.IsNullOrWhiteSpace(cfg.ControllerAudioPath))
                    ok = AudioService.PlayCustomAudio(cfg.ControllerAudioPath);
                else
                    ok = AudioService.PlayBuiltinBeep();

                if (!ok)
                {
                    string diag = AudioService.GetDiagnosticInfo();
                    System.Windows.MessageBox.Show(
                        "未找到手柄音频设备。\n请确认手柄通过 USB 连接。\n（蓝牙模式下扬声器不可用）\n\n诊断信息：\n" + diag,
                        Strings.AppName,
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
        };

        // 保存按钮
        BtnSave.Click += (s, e) =>
        {
            ApplyToConfig();
            cfg.Save();
            if (Owner is MainWindow mw) mw.Opacity = cfg.WindowOpacity;
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
        LblTitle.Text = Strings.HapticSettings;
        LblHapticIntensity.Text = Strings.HapticIntensity;
        LblHapticDuration.Text = Strings.HapticDuration;
        LblLightbarDuration.Text = Strings.LightbarDuration;
        LblLightbarColor.Text = Strings.LightbarColor;
        LblLowBatterySection.Text = Strings.LowBatterySection;
        LblAlertEnabled.Text = Strings.AlertEnabled;
        LblAlertThreshold.Text = Strings.AlertThreshold;
        LblRepeatEnabled.Text = Strings.RepeatEnabled;
        LblRepeatInterval.Text = Strings.RepeatInterval;
        LblOpacity.Text = Strings.WindowOpacity;
        LblControllerSpeaker.Text = Strings.ControllerSpeaker;
        LblSpeakerVolume.Text = Strings.SpeakerVolume;
        LblSpeakerDuration.Text = Strings.SpeakerDuration;
        LblCustomAudio.Text = Strings.CustomAudioFile;
        BtnSelectAudio.Content = Strings.SelectAudioFile;
    }

    private void InitPresetButtons()
    {
        // 提醒间隔
        StyleBtn(BtnInterval300);
        StyleBtn(BtnInterval600);
        StyleBtn(BtnInterval1800);
        StyleBtn(BtnInterval3600);
        // 震动时间 - 从 XAML 按名称查找
        StyleBtnByName("BtnHaptic200");
        StyleBtnByName("BtnHaptic500");
        StyleBtnByName("BtnHaptic800");
        StyleBtnByName("BtnHaptic1000");
        // 灯带时间
        StyleBtnByName("BtnLight1000");
        StyleBtnByName("BtnLight3000");
        StyleBtnByName("BtnLight5000");
        StyleBtnByName("BtnLight10000");
        // 蜂鸣时长
        StyleBtnByName("BtnSpk200");
        StyleBtnByName("BtnSpk500");
        StyleBtnByName("BtnSpk800");
        StyleBtnByName("BtnSpk1000");
        StyleBtnByName("BtnSelectAudio");
    }

    private void StyleBtnByName(string name)
    {
        var btn = FindName(name) as System.Windows.Controls.Button;
        if (btn != null) StyleBtn(btn);
    }

    private static void StyleBtn(System.Windows.Controls.Button btn)
    {
        btn.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x3E));
        btn.Foreground = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD8));
        btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x45));
        btn.BorderThickness = new Thickness(1);
    }

    private void PresetInterval_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && int.TryParse(btn.Tag?.ToString(), out int val))
            TxtRepeatInterval.Text = val.ToString();
    }

    private void PresetHapticTime_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && int.TryParse(btn.Tag?.ToString(), out int val))
            TxtHapticTime.Text = val.ToString();
    }

    private void PresetLightTime_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && int.TryParse(btn.Tag?.ToString(), out int val))
            TxtLightTime.Text = val.ToString();
    }

    private void PresetSpeakerTime_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && int.TryParse(btn.Tag?.ToString(), out int val))
            TxtSpeakerDuration.Text = val.ToString();
    }

    private void ChkControllerSpeaker_Changed(object sender, RoutedEventArgs e)
    {
        UpdateControllerSpeakerLabel();
    }

    private void BtnSelectAudio_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = Strings.AudioFileFilter,
            Title = Strings.SelectAudioFile
        };
        if (dlg.ShowDialog() == true)
        {
            _selectedAudioPath = dlg.FileName;
            UpdateAudioPathDisplay();
        }
    }

    private void BtnClearAudio_Click(object sender, RoutedEventArgs e)
    {
        _selectedAudioPath = null;
        UpdateAudioPathDisplay();
    }

    private void UpdateControllerSpeakerLabel()
    {
        ChkControllerSpeaker.Content = ChkControllerSpeaker.IsChecked == true
            ? Strings.ControllerSpeakerEnabled
            : Strings.ControllerSpeakerDisabled;
    }

    private void UpdateAudioPathDisplay()
    {
        bool hasCustom = !string.IsNullOrWhiteSpace(_selectedAudioPath);
        if (hasCustom)
        {
            TxtAudioPath.Text = System.IO.Path.GetFileName(_selectedAudioPath!);
            TxtAudioPath.Foreground = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD8));
        }
        else
        {
            TxtAudioPath.Text = Strings.NoFileSelected;
            TxtAudioPath.Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x88));
        }
        BtnClearAudio.Visibility = hasCustom ? Visibility.Visible : Visibility.Collapsed;
    }

    private string? _selectedAudioPath;

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
        cfg.LowBatteryRepeatIntervalMs = Math.Max(1, ClampInt(TxtRepeatInterval.Text, 1, int.MaxValue)) * 1000;
        cfg.WindowOpacity = Math.Clamp(ClampInt(TxtOpacity.Text, 30, 100), 30, 100) / 100.0;

        cfg.ControllerSpeakerEnabled = ChkControllerSpeaker.IsChecked == true;
        cfg.ControllerSpeakerVolume = ClampInt(TxtSpeakerVolume.Text, 10, 100);
        cfg.ControllerSpeakerDurationMs = ClampInt(TxtSpeakerDuration.Text, 100, 3000);
        cfg.ControllerAudioPath = _selectedAudioPath ?? "";
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
