# 手柄扬声器警告音 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 新增手柄扬声器低电量警告音，内置蜂鸣通过 Feature Report 0x80 触发，自定义音频通过 USB PCM 播放。

**Architecture:** 双路径 — 有自定义音频文件走 NAudio USB PCM（方案 B），无自定义音频走 HID Feature Report 0x80 固件内置 1kHz 正弦波（方案 A）。

**Tech Stack:** .NET 8 WPF, HidLibrary 3.3.40, NAudio 2.2.1

**Spec:** `docs/superpowers/specs/2026-05-14-controller-speaker-alert-design.md`

---

### Task 1: 修正 AppSettings（新增蜂鸣持续时间）

**Files:**
- Modify: `DsBatteryIndicator/Services/AppSettings.cs`

- [ ] **Step 1: 新增 ControllerSpeakerDurationMs 属性**

在 `ControllerAudioPath` 属性前插入：

```csharp
    public int ControllerSpeakerDurationMs { get; set; } = 800;
```

完整上下文（在 `ControllerSpeakerVolume` 和 `ControllerAudioPath` 之间）：

```csharp
    // 手柄扬声器配置
    public bool ControllerSpeakerEnabled { get; set; } = true;
    public int ControllerSpeakerVolume { get; set; } = 80;
    public int ControllerSpeakerDurationMs { get; set; } = 800;
    public string ControllerAudioPath { get; set; } = "";
```

---

### Task 2: 修正 Strings（新增持续时间标签）

**Files:**
- Modify: `DsBatteryIndicator/Resources/Strings.cs`

- [ ] **Step 1: 添加中文字符串**

在 `["ControllerSpeaker"]` 前插入：

```csharp
            ["SpeakerDuration"] = "蜂鸣时长",
```

- [ ] **Step 2: 添加英文字符串**

在 `["ControllerSpeaker"]` 前插入：

```csharp
            ["SpeakerDuration"] = "Beep Duration",
```

- [ ] **Step 3: 添加公共属性**

在 `public static string ControllerSpeaker =>` 前插入：

```csharp
    public static string SpeakerDuration => Get("SpeakerDuration");
```

---

### Task 3: 重写 HidService（修正输出报告 + 新增 SendSpeakerBeep）

**Files:**
- Modify: `DsBatteryIndicator/Services/HidService.cs`

- [ ] **Step 1: 修正 BuildOutputReport 音频字节**

将 132-154 行替换为正确的字节偏移：

```csharp
    private static byte[] BuildOutputReport(byte rightMotor, byte leftMotor, byte r, byte g, byte b)
    {
        var cfg = AppSettings.Instance;
        byte spkVol = (byte)(cfg.ControllerSpeakerVolume * 255 / 100);
        var report = new byte[48];
        report[0] = 0x02;                                     // Report ID
        report[1] = (byte)((rightMotor > 0 || leftMotor > 0) ? 0x03 : 0x00); // 马达标志
        report[2] = 0xF7;                                     // 功能掩码
        report[3] = rightMotor;                               // 右马达
        report[4] = leftMotor;                                // 左马达
        report[5] = 0;                                        // headphoneVolume = 0（静音耳机）
        report[6] = spkVol;                                   // speakerVolume
        report[8] = 0x30;                                     // audioControl = 扬声器路由
        report[42] = 0x02;                                    // 灯带控制
        report[43] = 0x03;
        report[44] = 0x04;
        report[45] = r;                                       // 灯带 R
        report[46] = g;                                       // 灯带 G
        report[47] = b;                                       // 灯带 B
        return report;
    }
```

- [ ] **Step 2: 新增 SendSpeakerBeep 方法**

在 `BuildOutputReport` 方法之后（154 行后）插入：

```csharp
    /// <summary>
    /// 通过 Feature Report 0x80 触发 DualSense 固件内置 1kHz 正弦波。
    /// 参考 daidr/dualsense-tester AudioControlWidget.startSPKWaveout。
    /// </summary>
    public void SendSpeakerBeep()
    {
        if (_device == null || !_device.IsConnected) return;

        var cfg = AppSettings.Instance;
        int durationMs = Math.Clamp(cfg.ControllerSpeakerDurationMs, 100, 3000);
        byte spkVol = (byte)(cfg.ControllerSpeakerVolume * 255 / 100);

        try
        {
            // 1. 先通过 Output Report 设置扬声器音量和路由
            var audioReport = new byte[48];
            audioReport[0] = 0x02;
            audioReport[2] = 0xF7;
            audioReport[5] = 0;          // headphoneVolume = 0
            audioReport[6] = spkVol;     // speakerVolume
            audioReport[8] = 0x30;       // audioControl = 扬声器
            _device.Write(audioReport);

            // 2. 配置音频路径 → 扬声器（Feature Report 0x80）
            var calibParams = new byte[22];   // 2 header + 20 params
            calibParams[0] = 0x06;            // deviceId = AUDIO
            calibParams[1] = 0x04;            // actionId = BUILTIN_MIC_CALIB_DATA_VERIFY
            calibParams[4] = 8;               // params[2] = 8 (speaker routing)
            WriteFeatureReport(calibParams);

            // 3. 启动波形输出（Feature Report 0x80）
            var ctrlParams = new byte[5];     // 2 header + 3 params
            ctrlParams[0] = 0x06;             // deviceId = AUDIO
            ctrlParams[1] = 0x02;             // actionId = WAVEOUT_CTRL
            ctrlParams[2] = 1;                // enable = true
            ctrlParams[3] = 1;
            ctrlParams[4] = 0;
            WriteFeatureReport(ctrlParams);

            // 4. 等待持续时间后停止
            Task.Delay(durationMs).ContinueWith(_ =>
            {
                try
                {
                    if (_device == null || !_device.IsConnected) return;
                    ctrlParams[2] = 0;        // enable = false
                    WriteFeatureReport(ctrlParams);
                }
                catch { }
            });
        }
        catch { }
    }

    private void WriteFeatureReport(byte[] payload)
    {
        // payload: [deviceId, actionId, ...params]
        // WriteFeatureData 需要完整报告（含 Report ID），补 0x80 前缀
        var report = new byte[payload.Length + 1];
        report[0] = 0x80;   // Feature Report ID
        Array.Copy(payload, 0, report, 1, payload.Length);
        _device?.WriteFeatureData(report);
    }
```

---

### Task 4: 重写 AudioService（简化为仅自定义音频）

**Files:**
- Modify: `DsBatteryIndicator/Services/AudioService.cs`

- [ ] **Step 1: 完整替换文件内容**

将整个文件替换为：

```csharp
using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace DsBatteryIndicator.Services;

/// <summary>
/// 通过 USB 音频设备向 DualSense 手柄扬声器播放自定义音频文件。
/// 仅用于方案 B（用户选择了自定义音频）；内置蜂鸣走 HidService.SendSpeakerBeep（方案 A）。
/// </summary>
public static class AudioService
{
    /// <summary>
    /// 在手柄 USB 音频设备上播放指定音频文件。
    /// 返回 true 表示找到设备并开始播放。
    /// </summary>
    public static bool PlayCustomAudio(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            var device = FindControllerAudioDevice();
            if (device == null) return false;

            Task.Run(() =>
            {
                try
                {
                    using var reader = new AudioFileReader(filePath);
                    using var output = new WasapiOut(device, AudioClientShareMode.Shared, false, 100);
                    output.Init(reader);
                    output.Play();
                    while (output.PlaybackState == PlaybackState.Playing)
                        System.Threading.Thread.Sleep(50);
                }
                catch { }
                finally
                {
                    device.Dispose();
                }
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static MMDevice? FindControllerAudioDevice()
    {
        var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .FirstOrDefault(d =>
                d.FriendlyName.Contains("Wireless Controller") ||
                d.FriendlyName.Contains("DualSense"));
    }
}
```

---

### Task 5: 修正 NotificationService（按路径分发）

**Files:**
- Modify: `DsBatteryIndicator/Services/NotificationService.cs`

- [ ] **Step 1: 修改 NotifyLowBattery 调用，传入 hidService**

```csharp
    public static void NotifyLowBattery(int batteryLevel,
        System.Windows.Forms.NotifyIcon? trayIcon, HidService? hidService)
    {
        ShowBalloonTip(trayIcon, batteryLevel);
        PlayAlertSound();
        TriggerControllerHaptic(hidService);
        TriggerControllerSpeaker(hidService);
    }
```

- [ ] **Step 2: 替换 TriggerControllerSpeaker 方法签名和实现**

```csharp
    private static void TriggerControllerSpeaker(HidService? hidService)
    {
        var cfg = AppSettings.Instance;
        if (!cfg.ControllerSpeakerEnabled) return;

        if (!string.IsNullOrWhiteSpace(cfg.ControllerAudioPath))
            AudioService.PlayCustomAudio(cfg.ControllerAudioPath);
        else
            hidService?.SendSpeakerBeep();
    }
```

---

### Task 6: 更新设置窗口 UI（添加蜂鸣持续时间行）

**Files:**
- Modify: `DsBatteryIndicator/Views/HapticSettingsWindow.xaml`

- [ ] **Step 1: 在音量行（Row 29）和自定义音频行（Row 30）之间插入持续时间行**

当前 Grid 有 Row 0-30。需要把自定义音频移到 Row 31，在 Row 29（音量）和 Row 31 之间插入 Row 30（蜂鸣时长）。

先新增一个 RowDefinition（在第 131 行 `</Grid.RowDefinitions>` 前添加）：

```xml
                <RowDefinition Height="Auto"/>
```

然后，在音量行（`<!-- 29: 扬声器音量 -->`）之后、自定义音频行（`<!-- 30: 自定义音频文件 -->`）之前插入：

```xml
            <!-- 30: 蜂鸣时长 -->
            <TextBlock x:Name="LblSpeakerDuration" Grid.Row="30" Grid.Column="0"
                       VerticalAlignment="Center" FontSize="12"/>
            <StackPanel Grid.Row="30" Grid.Column="2" Orientation="Horizontal" VerticalAlignment="Center">
                <Button x:Name="BtnSpk200" Content="200ms" Tag="200" Width="46" Height="22" FontSize="11"
                        Margin="0,0,4,0" Cursor="Hand" Click="PresetSpeakerTime_Click"/>
                <Button x:Name="BtnSpk500" Content="500ms" Tag="500" Width="46" Height="22" FontSize="11"
                        Margin="0,0,4,0" Cursor="Hand" Click="PresetSpeakerTime_Click"/>
                <Button x:Name="BtnSpk800" Content="800ms" Tag="800" Width="46" Height="22" FontSize="11"
                        Margin="0,0,4,0" Cursor="Hand" Click="PresetSpeakerTime_Click"/>
                <Button x:Name="BtnSpk1000" Content="1s" Tag="1000" Width="46" Height="22" FontSize="11"
                        Margin="0,0,8,0" Cursor="Hand" Click="PresetSpeakerTime_Click"/>
            </StackPanel>
            <StackPanel Grid.Row="30" Grid.Column="4" Orientation="Horizontal" VerticalAlignment="Center">
                <TextBox x:Name="TxtSpeakerDuration" Style="{StaticResource NumInput}"/>
                <TextBlock Text="ms" FontSize="10" Foreground="#808088" VerticalAlignment="Center" Margin="3,0,0,0"/>
            </StackPanel>
```

- [ ] **Step 2: 将自定义音频行的 Grid.Row 从 30 改为 31**

```xml
            <!-- 31: 自定义音频文件 -->
            <TextBlock x:Name="LblCustomAudio" Grid.Row="31" Grid.Column="0"
```

同时将 `StackPanel Grid.Row="31"`：

```xml
            <StackPanel Grid.Row="31" Grid.Column="2" Grid.ColumnSpan="2" Orientation="Horizontal"
```

---

### Task 7: 更新设置窗口代码后置（持续时间读写）

**Files:**
- Modify: `DsBatteryIndicator/Views/HapticSettingsWindow.xaml.cs`

- [ ] **Step 1: 在 InitLocalization 中添加标签**

在 `LblSpeakerVolume.Text = Strings.SpeakerVolume;` 之后添加：

```csharp
        LblSpeakerDuration.Text = Strings.SpeakerDuration;
```

- [ ] **Step 2: 在构造函数中加载持续时间**

在 `SliderSpeakerVolume.Value = cfg.ControllerSpeakerVolume;` 之后添加：

```csharp
        TxtSpeakerDuration.Text = cfg.ControllerSpeakerDurationMs.ToString();
```

- [ ] **Step 3: 在 InitPresetButtons 中添加按钮样式**

在 `StyleBtnByName("BtnSelectAudio");` 之后添加：

```csharp
        StyleBtnByName("BtnSpk200");
        StyleBtnByName("BtnSpk500");
        StyleBtnByName("BtnSpk800");
        StyleBtnByName("BtnSpk1000");
```

- [ ] **Step 4: 添加预设按钮事件处理器**

在 `PresetLightTime_Click` 方法之后添加：

```csharp
    private void PresetSpeakerTime_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && int.TryParse(btn.Tag?.ToString(), out int val))
            TxtSpeakerDuration.Text = val.ToString();
    }
```

- [ ] **Step 5: 在 ApplyToConfig 中保存持续时间**

在 `cfg.ControllerSpeakerVolume = ClampInt(TxtSpeakerVolume.Text, 10, 100);` 之后添加：

```csharp
        cfg.ControllerSpeakerDurationMs = ClampInt(TxtSpeakerDuration.Text, 100, 3000);
```

---

### Task 8: 编译验证

- [ ] **Step 1: 编译**

```bash
dotnet build "D:/project/ds-battery-indicator/DsBatteryIndicator/DsBatteryIndicator.csproj"
```

预期：0 错误 0 警告

- [ ] **Step 2: 功能验证**

1. USB 连接 DualSense，打开设置窗口，确认新增的"蜂鸣时长"行显示正确（预设按钮 + 文本框）
2. 将电量阈值调到 100%（触发低电量），确认手柄扬声器发出蜂鸣
3. 调整蜂鸣时长预设按钮，点击测试，确认时长变化生效
4. 选择 WAV/MP3 自定义音频文件，触发提醒，确认播放自定义音频
5. 点击 ✕ 清除自定义音频，触发提醒，确认回退到内置蜂鸣
6. 关闭手柄扬声器开关，确认不播放
