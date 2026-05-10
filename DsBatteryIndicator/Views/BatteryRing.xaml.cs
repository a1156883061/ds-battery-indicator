using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DsBatteryIndicator.Views;

/// <summary>
/// 环形电量进度条自定义控件。
/// 依赖属性 Progress (0-100), AccentColor, IsCharging。
/// </summary>
public partial class BatteryRing : UserControl
{
    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(nameof(Progress), typeof(double), typeof(BatteryRing),
            new PropertyMetadata(0d, OnProgressChanged));

    public static readonly DependencyProperty AccentColorProperty =
        DependencyProperty.Register(nameof(AccentColor), typeof(Brush), typeof(BatteryRing),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80)), OnAccentColorChanged));

    public static readonly DependencyProperty IsChargingProperty =
        DependencyProperty.Register(nameof(IsCharging), typeof(bool), typeof(BatteryRing),
            new PropertyMetadata(false, OnIsChargingChanged));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public Brush AccentColor
    {
        get => (Brush)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    public bool IsCharging
    {
        get => (bool)GetValue(IsChargingProperty);
        set => SetValue(IsChargingProperty, value);
    }

    public BatteryRing()
    {
        InitializeComponent();
    }

    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BatteryRing ring)
            ring.UpdateArc((double)e.NewValue);
    }

    private static void OnAccentColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BatteryRing ring && e.NewValue is Brush brush)
        {
            ring.ForegroundArc.Stroke = brush;
            ring.ChargingOverlay.Stroke = brush;
        }
    }

    private static void OnIsChargingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BatteryRing ring)
            ring.UpdateChargingAnimation((bool)e.NewValue);
    }

    private void UpdateArc(double progress)
    {
        double angle = (progress / 100.0) * 360.0;
        double radians = (angle - 90) * Math.PI / 180.0;
        double r = 20; // 环形半径
        double cx = 24, cy = 24; // 中心点

        double x = cx + r * Math.Cos(radians);
        double y = cy + r * Math.Sin(radians);

        ArcSegment.Point = new Point(x, y);
        ArcSegment.IsLargeArc = angle > 180;
    }

    private void UpdateChargingAnimation(bool isCharging)
    {
        if (isCharging)
        {
            ForegroundArc.Visibility = Visibility.Collapsed;
            ChargingOverlay.Visibility = Visibility.Visible;
            ChargingOverlay.Stroke = AccentColor;

            var animation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(2),
                RepeatBehavior = RepeatBehavior.Forever
            };
            var transform = (RotateTransform)ChargingOverlay.RenderTransform;
            transform.BeginAnimation(RotateTransform.AngleProperty, animation);
        }
        else
        {
            ForegroundArc.Visibility = Visibility.Visible;
            ChargingOverlay.Visibility = Visibility.Collapsed;
            var transform = (RotateTransform)ChargingOverlay.RenderTransform;
            transform.BeginAnimation(RotateTransform.AngleProperty, null);
        }
    }
}
