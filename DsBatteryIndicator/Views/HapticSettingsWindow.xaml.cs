using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using DsBatteryIndicator.Services;

namespace DsBatteryIndicator.Views;

public partial class HapticSettingsWindow : Window
{
    public HapticSettingsWindow()
    {
        InitializeComponent();

        var cfg = AppSettings.Instance;

        SliderHapticTime.Value = cfg.HapticDurationMs;
        SliderIntensity.Value = cfg.HapticIntensity;
        SliderLightTime.Value = cfg.LightbarDurationMs;
        SliderR.Value = cfg.LightbarColorR;
        SliderG.Value = cfg.LightbarColorG;
        SliderB.Value = cfg.LightbarColorB;

        BtnTest.Click += (s, e) =>
        {
            // 临时应用当前值测试
            cfg.HapticDurationMs = (int)SliderHapticTime.Value;
            cfg.HapticIntensity = (int)SliderIntensity.Value;
            cfg.LightbarDurationMs = (int)SliderLightTime.Value;
            cfg.LightbarColorR = (byte)SliderR.Value;
            cfg.LightbarColorG = (byte)SliderG.Value;
            cfg.LightbarColorB = (byte)SliderB.Value;
            (Owner as MainWindow)?.ViewModel.SendHapticTest();
        };

        BtnSave.Click += (s, e) =>
        {
            cfg.HapticDurationMs = (int)SliderHapticTime.Value;
            cfg.HapticIntensity = (int)SliderIntensity.Value;
            cfg.LightbarDurationMs = (int)SliderLightTime.Value;
            cfg.LightbarColorR = (byte)SliderR.Value;
            cfg.LightbarColorG = (byte)SliderG.Value;
            cfg.LightbarColorB = (byte)SliderB.Value;
            cfg.Save();
            Close();
        };
    }
}

/// <summary>RGB 三个 Slider 值转为 SolidColorBrush</summary>
public class RgbConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        byte r = System.Convert.ToByte(values[0]);
        byte g = System.Convert.ToByte(values[1]);
        byte b = System.Convert.ToByte(values[2]);
        return Color.FromRgb(r, g, b);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
