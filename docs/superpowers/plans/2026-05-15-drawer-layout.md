# 低电量通知抽屉式折叠布局 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 将 6 个开关从扁平 12 行压缩为可折叠抽屉 + 2 列 grid + 关闭蒙版，共 3 行。

**Architecture:** 总开关行（CheckBox + 文字 + ▼箭头）→ 子开关暗底面板（Grid 2列 + 半透明蒙版叠加层）→ Collapsed/Visibility 绑定控制折叠和蒙版显隐。

**Tech Stack:** WPF XAML Grid, CheckBox ToggleSwitch Style, Border, Visibility binding

**Spec:** `docs/superpowers/specs/2026-05-15-low-battery-notification-layout.md`

---

## 文件结构

| 文件 | 职责 |
|------|------|
| `Views/HapticSettingsWindow.xaml` | 低电量通知区域 XAML 完全重写；其他区域 Row 号下移 |
| `Views/HapticSettingsWindow.xaml.cs` | 新增折叠切换逻辑、蒙版显隐、移除旧标签 |

---

### Task 1: 重写低电量通知区域 XAML

**Files:**
- Modify: `DsBatteryIndicator/Views/HapticSettingsWindow.xaml`

低电量通知区域当前占用 Row 0-19（20 行）。重写为 Row 0-10（11 行）。Row 0（标题）保留，Row 2-19 全部替换。

Grid.RowDefinitions 从 49 行缩减到 40 行（删除 9 行 spacing+switch）。

- [ ] **Step 1: 替换低电量通知区域 XAML**

将当前 Row 0-19 的低电量通知内容（标题 + 6 个开关行 + 电量阈值/重复提醒/提醒间隔）整体替换为：

```xml
            <!-- 0: 低电量通知（标题） -->
            <TextBlock x:Name="LblLowBatterySection" Grid.Row="0" Grid.ColumnSpan="5"
                       FontSize="13" FontWeight="SemiBold" Foreground="#E0E0E8"/>

            <!-- 2: 总开关行 -->
            <Border Grid.Row="2" Grid.ColumnSpan="5" Margin="0,0,0,2"
                    Background="#1F1F2C" CornerRadius="6" BorderBrush="#2A2A35" BorderThickness="1"
                    Cursor="Hand" MouseLeftButtonDown="MasterSwitchRow_Click">
                <Grid Margin="8,6">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    <StackPanel Grid.Column="0" Orientation="Horizontal" VerticalAlignment="Center">
                        <CheckBox x:Name="ChkAlertEnabled" VerticalAlignment="Center"
                                  Checked="MasterSwitch_Changed" Unchecked="MasterSwitch_Changed"/>
                        <TextBlock x:Name="LblAlertEnabled" Text="总开关" VerticalAlignment="Center"
                                   FontSize="12" FontWeight="SemiBold" Foreground="#E0E0E8" Margin="10,0,0,0"/>
                    </StackPanel>
                    <TextBlock x:Name="TxtCollapseArrow" Grid.Column="2" Text="▼"
                               FontSize="10" Foreground="#60A5FA" VerticalAlignment="Center"/>
                </Grid>
            </Border>

            <!-- 3: 子开关面板 -->
            <Border x:Name="SubSwitchPanel" Grid.Row="3" Grid.ColumnSpan="5" Margin="0,4,0,6"
                    Background="#14141D" CornerRadius="6" BorderBrush="#252535" BorderThickness="1"
                    Padding="10,12">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="16"/>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="6"/>
                        <RowDefinition Height="Auto"/>
                    </Grid.RowDefinitions>

                    <StackPanel Grid.Row="0" Grid.Column="0" Orientation="Horizontal">
                        <TextBlock x:Name="LblHapticSwitch" Text="手柄震动" VerticalAlignment="Center" FontSize="11" Width="52"/>
                        <CheckBox x:Name="ChkHaptic" VerticalAlignment="Center" HorizontalAlignment="Right"/>
                    </StackPanel>

                    <StackPanel Grid.Row="0" Grid.Column="2" Orientation="Horizontal">
                        <TextBlock x:Name="LblLightbarSwitch" Text="灯带变色" VerticalAlignment="Center" FontSize="11" Width="52"/>
                        <CheckBox x:Name="ChkLightbar" VerticalAlignment="Center" HorizontalAlignment="Right"/>
                    </StackPanel>

                    <StackPanel Grid.Row="2" Grid.Column="0" Orientation="Horizontal">
                        <TextBlock x:Name="LblControllerSpeaker" Text="手柄音频" VerticalAlignment="Center" FontSize="11" Width="52"/>
                        <CheckBox x:Name="ChkControllerSpeaker" VerticalAlignment="Center" HorizontalAlignment="Right"/>
                    </StackPanel>

                    <StackPanel Grid.Row="2" Grid.Column="2" Orientation="Horizontal">
                        <TextBlock x:Name="LblBalloonTipSwitch" Text="托盘气泡" VerticalAlignment="Center" FontSize="11" Width="52"/>
                        <CheckBox x:Name="ChkBalloonTip" VerticalAlignment="Center" HorizontalAlignment="Right"/>
                    </StackPanel>
                </Grid>

                <!-- 第 5 个子开关：跨两列 -->
                <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,6,0,0">
                    <TextBlock x:Name="LblAlertSoundSwitch" Text="系统提示音" VerticalAlignment="Center" FontSize="11" Width="60"/>
                    <CheckBox x:Name="ChkAlertSound" VerticalAlignment="Center" HorizontalAlignment="Right"/>
                </StackPanel>

                <!-- 蒙版遮罩 -->
                <Border x:Name="SubSwitchMask" Background="#99000000" CornerRadius="6"
                        Visibility="Collapsed" IsHitTestVisible="False">
                    <TextBlock Text="提醒已全部禁用" Foreground="#808088" FontSize="11"
                               HorizontalAlignment="Center" VerticalAlignment="Center"
                               Padding="4,2" Margin="0"/>
                </Border>
            </Border>

            <!-- 5: 电量阈值 -->
            <TextBlock x:Name="LblAlertThreshold" Grid.Row="5" Grid.Column="0"
                       VerticalAlignment="Center" FontSize="12"/>
            <Slider x:Name="SliderThreshold" Grid.Row="5" Grid.Column="2"
                    Style="{StaticResource SlimSlider}" Minimum="10" Maximum="90" Value="10"/>
            <StackPanel Grid.Row="5" Grid.Column="4" Orientation="Horizontal" VerticalAlignment="Center">
                <TextBox x:Name="TxtThreshold" Style="{StaticResource NumInput}"/>
                <TextBlock Text="%" FontSize="10" Foreground="#808088" VerticalAlignment="Center" Margin="3,0,0,0"/>
            </StackPanel>

            <!-- 7: 重复提醒 -->
            <TextBlock x:Name="LblRepeatEnabled" Grid.Row="7" Grid.Column="0"
                       VerticalAlignment="Center" FontSize="12"/>
            <CheckBox x:Name="ChkRepeatEnabled" Grid.Row="7" Grid.Column="4" VerticalAlignment="Center"/>

            <!-- 9: 提醒间隔 -->
            <TextBlock x:Name="LblRepeatInterval" Grid.Row="9" Grid.Column="0"
                       VerticalAlignment="Center" FontSize="12"/>
            <StackPanel Grid.Row="9" Grid.Column="2" Orientation="Horizontal" VerticalAlignment="Center">
                <Button x:Name="BtnInterval300" Content="5min" Tag="300" Width="46" Height="22"
                        FontSize="11" Margin="0,0,4,0" Cursor="Hand" Click="PresetInterval_Click"/>
                <Button x:Name="BtnInterval600" Content="10min" Tag="600" Width="46" Height="22"
                        FontSize="11" Margin="0,0,4,0" Cursor="Hand" Click="PresetInterval_Click"/>
                <Button x:Name="BtnInterval1800" Content="30min" Tag="1800" Width="46" Height="22"
                        FontSize="11" Margin="0,0,4,0" Cursor="Hand" Click="PresetInterval_Click"/>
                <Button x:Name="BtnInterval3600" Content="1h" Tag="3600" Width="46" Height="22"
                        FontSize="11" Margin="0,0,8,0" Cursor="Hand" Click="PresetInterval_Click"/>
            </StackPanel>
            <StackPanel Grid.Row="9" Grid.Column="4" Orientation="Horizontal" VerticalAlignment="Center">
                <TextBox x:Name="TxtRepeatInterval" Style="{StaticResource NumInput}"/>
                <TextBlock Text="s" FontSize="10" Foreground="#808088" VerticalAlignment="Center" Margin="3,0,0,0"/>
            </StackPanel>

            <!-- 11: 分隔线 -->
            <Border Grid.Row="11" Grid.ColumnSpan="5" Height="1" Background="#2A2A35"/>

            <!-- 13: 震动强度 -->
            <TextBlock x:Name="LblHapticIntensity" Grid.Row="13" Grid.Column="0"
                       VerticalAlignment="Center" FontSize="12"/>
```

- [ ] **Step 2: 后续行号全部调整**

旧行 → 新行映射：

| 旧Row | 内容 | 新Row |
|:--:|------|:--:|
| 22 | 震动强度 | 13 |
| 24 | 震动时间 | 15 |
| 26 | 灯带时间 | 17 |
| 28 | 分隔线 | 19 |
| 30 | 灯带颜色 | 21 |
| 32 | 分隔线 | 23 |
| 34 | 窗口透明度 | 25 |
| 36 | 分隔线 | 27 |
| 38 | 音频提醒配置 | 29 |
| 40 | 扬声器音量 | 31 |
| 42 | 蜂鸣时长 | 33 |
| 44 | 自定义音频 | 35 |
| 46 | 分隔线 | 37 |
| 48 | 轮询时间 | 39 |

将 XAML 中所有旧 Grid.Row 值按上表递增替换。分隔线编号为所有奇数行（11,13,15,...,39）。

- [ ] **Step 3: 替换 RowDefinitions**

当前 49 行，缩减到 40 行。低电量通知区域用（0-11），后续区域（11-39 共 29 行）保持不变但编号集体减 9：

```
<!-- 低电量通知 -->
<RowDefinition Height="Auto"/>     <!-- 0: 标题 -->
<RowDefinition Height="6"/>        <!-- 1 -->
<RowDefinition Height="Auto"/>     <!-- 2: 总开关 -->
<RowDefinition Height="Auto"/>     <!-- 3: 子开关面板 -->
<RowDefinition Height="Auto"/>     <!-- 4: 蒙版（叠在面板内，此行为 0 高度占位用——实际不需要蒙版独立行） -->
```

Wait, the 蒙版 is INSIDE the panel as an overlay (same row). No need for separate row.

Let me recalculate:

```
0: Auto (标题)
1: 6
2: Auto (总开关行)
3: Auto (子开关面板) ← Margin top=4 bottom=6 takes care of spacing
4: Auto → dummy, was removed

Actually: 4,6,8,10,12 all removed (old sub-switch rows)
```

OK, new RowDefinitions from 0-11 (replacing old 0-19):

```
0: Auto (标题)
1: 6
2: Auto (总开关)
3: Auto (子面板)
4: Auto (电量阈值) ← was row 14
5: 6 ← was row 15
6: Auto (重复提醒) ← was row 16
7: 6 ← was row 17
8: Auto (提醒间隔) ← was row 18
9: 12 ← was row 19
10: Auto (分隔线) ← was row 20
11: 12 ← was row 21
```

Then rows 12-39 continue from old rows 22-48.

Total: 40 rows (0-39).

---

### Task 2: 更新代码后置

**Files:**
- Modify: `DsBatteryIndicator/Views/HapticSettingsWindow.xaml.cs`

- [ ] **Step 1: 添加折叠和蒙版控制字段与方法**

在类中添加 `_isSubPanelExpanded` 字段。在构造函数中初始化默认展开。新增方法：

```csharp
private bool _isSubPanelExpanded = true;

private void MasterSwitchRow_Click(object sender, MouseButtonEventArgs e)
{
    // 只有点击到箭头区域才切换折叠
    _isSubPanelExpanded = !_isSubPanelExpanded;
    SubSwitchPanel.Visibility = _isSubPanelExpanded ? Visibility.Visible : Visibility.Collapsed;
    TxtCollapseArrow.Text = _isSubPanelExpanded ? "▼" : "▶";
}

private void MasterSwitch_Changed(object sender, RoutedEventArgs e)
{
    bool on = ChkAlertEnabled.IsChecked == true;
    SubSwitchMask.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
    ChkHaptic.IsEnabled = on;
    ChkLightbar.IsEnabled = on;
    ChkControllerSpeaker.IsEnabled = on;
    ChkBalloonTip.IsEnabled = on;
    ChkAlertSound.IsEnabled = on;
}
```

- [ ] **Step 2: 修改构造函数初始化**

替换现有的 6 行开关加载代码。将加载代码更新为与新的 XAML 布局一致：

```csharp
// 低电量通知开关
ChkAlertEnabled.IsChecked = cfg.LowBatteryAlertEnabled;
ChkHaptic.IsChecked = cfg.HapticEnabled;
ChkLightbar.IsChecked = cfg.LightbarEnabled;
ChkControllerSpeaker.IsChecked = cfg.ControllerSpeakerEnabled;
ChkBalloonTip.IsChecked = cfg.BalloonTipEnabled;
ChkAlertSound.IsChecked = cfg.AlertSoundEnabled;
// 初始化蒙版状态
SubSwitchMask.Visibility = cfg.LowBatteryAlertEnabled ? Visibility.Collapsed : Visibility.Visible;

// 低电量通知设置
SliderThreshold.Value = cfg.LowBatteryThreshold;
```

- [ ] **Step 3: 移除 InitLocalization 中多余的标签行**

旧 5 个开关在第一列有独立 `LblXxx` 标签，新布局中文字直接写在 `TextBlock` 内。无需删除 `InitLocalization` 中的赋值（保留 `LblHapticSwitch.Text` 等——即使无效果也不报错）。可选移除 `LblBalloonTipSwitch`、`LblAlertSoundSwitch` 的赋值行。

- [ ] **Step 4: ApplyToConfig 不变**

保存逻辑无需修改——`ChkHaptic`, `ChkLightbar` 等控件的 x:Name 不变，现有保存代码继续工作。

---

### Task 3: 编译验证

- [ ] **Step 1: 编译**

```bash
dotnet build "D:/project/ds-battery-indicator/DsBatteryIndicator/DsBatteryIndicator.csproj" --no-restore
```
Expected: 0 errors, 0 warnings

- [ ] **Step 2: 功能验证**

1. 打开设置窗口 → 低电量通知区域显示总开关行 + 展开的子开关面板
2. 点击 ▼ → 面板折叠，箭头变 ▶
3. 点击 ▶ → 面板展开
4. 关掉总开关 → 蒙版出现，子开关灰掉不可操作
5. 打开总开关 → 蒙版消失
6. 保存设置后重新打开 → 折叠/展开状态重置为默认展开（每次打开都是展开态——符合预期）
