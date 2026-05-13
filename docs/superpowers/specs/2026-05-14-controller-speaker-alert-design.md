# 手柄扬声器警告音 — 设计文档

## 背景

低电量提醒原有的震动+灯带通道会被游戏 HID 输出覆盖。新增手柄扬声器作为独立通知通道。

调研参考：[daidr/dualsense-tester](https://github.com/daidr/dualsense-tester)

## 核心发现

- **1kHz 正弦波由 DualSense 固件内部生成**，通过 Feature Report 0x80 触发，不需软件生成 PCM
- 输出报告（Output Report 0x02）中音频相关字节偏移：
  - data[4] (report[5]): `headphoneVolume` — 耳机音量
  - data[5] (report[6]): `speakerVolume` — 扬声器音量
  - data[7] (report[8]): `audioControl` — 0x30=扬声器路由，0x00=耳机路由

## 架构

两条路径，根据是否设置了自定义音频文件自动选择：

```
NotifyLowBattery()
  └─ TriggerControllerSpeaker()
       ├─ 有自定义音频？ → AudioService.PlayCustomAudio()  [方案 B: USB PCM]
       └─ 无自定义音频？ → HidService.SendSpeakerBeep()    [方案 A: Feature Report]
```

## 方案 A：内置蜂鸣（Feature Report 0x80）

完全模仿 `AudioControlWidget.vue` 的 `startSPKWaveout` 调用链。

### Feature Report 格式

Report ID 0x80，payload 结构：
```
Byte 0: deviceId (0x06 = AUDIO)
Byte 1: actionId (0x02 = WAVEOUT_CTRL / 0x04 = BUILTIN_MIC_CALIB_DATA_VERIFY)
Bytes 2+: 参数
```

### 播放流程

```
1. 发送 Output Report 0x02   → 设置 speakerVolume + audioControl=0x30
2. 发送 Feature Report 0x80  → BUILTIN_MIC_CALIB_DATA_VERIFY ({0,0,8, ...×17个0})
3. 发送 Feature Report 0x80  → WAVEOUT_CTRL ({1, 1, 0})  ← 启动波形
   ↓ 等待 cfg.ControllerSpeakerDurationMs
4. 发送 Feature Report 0x80  → WAVEOUT_CTRL ({0, 1, 0})  ← 停止波形
```

### 持续时间配置

新增 `ControllerSpeakerDurationMs`（默认 800ms），与震动时间 `HapticDurationMs` 配置方式完全一致：
- 设置窗口增加一行：预设按钮（200ms / 500ms / 800ms / 1s）+ 文本框直接输入 ms
- 值域：100ms - 3000ms

### HidLibrary API

使用 `HidDevice.WriteFeature(byte[] data)`，其中 `data[0] = 0x80`。

### 适用条件

- 蓝牙或 USB 均可（Feature Report 走 HID 协议，不依赖 USB Audio）
- 不需要自定义音频文件

## 方案 B：自定义音频（USB PCM + 修正输出报告）

### 输出报告修正

`BuildOutputReport` 中设置正确的音频字节：

| report 索引 | 字段 | 值 |
|---|---|---|
| report[5] | headphoneVolume | 0 |
| report[6] | speakerVolume | `cfg.ControllerSpeakerVolume * 255 / 100` |
| report[8] | audioControl | 0x30（扬声器） |

### 播放流程

```
1. 发送修正后的 Output Report 0x02 → 设置扬声器音量+路由
2. NAudio 找到 USB 音频设备（FriendlyName 含 "Wireless Controller"）
3. WasapiOut 播放自定义音频文件
```

### 适用条件

- USB 连接（需要 USB Audio 设备）
- 用户选择了自定义 .wav/.mp3 文件

## 设置项汇总

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `ControllerSpeakerEnabled` | bool | true | 手柄扬声器开关 |
| `ControllerSpeakerVolume` | int | 80 | 扬声器音量 (10-100%) |
| `ControllerSpeakerDurationMs` | int | 800 | 蜂鸣持续时间 (100-3000ms) |
| `ControllerAudioPath` | string | "" | 自定义音频路径，空=内置蜂鸣 |

## 改动文件

| 文件 | 操作 | 说明 |
|------|------|------|
| `Services/HidService.cs` | 修改 | 新增 `SendSpeakerBeep()`；修正 `BuildOutputReport` 音频字节 |
| `Services/AudioService.cs` | 修改 | 删除内置蜂鸣生成；仅保留 `PlayCustomAudio()` |
| `Services/AppSettings.cs` | 修改 | 新增 `ControllerSpeakerDurationMs` |
| `Services/NotificationService.cs` | 修改 | `TriggerControllerSpeaker()` 按有无自定义音频分发 |
| `Resources/Strings.cs` | 修改 | 新增 `SpeakerDuration` 中英文字符串 |
| `Views/HapticSettingsWindow.xaml` | 修改 | 新增蜂鸣持续时间行（预设按钮 + 文本框） |
| `Views/HapticSettingsWindow.xaml.cs` | 修改 | 加载/保存持续时间配置 |

## 边界条件

- **蓝牙连接**：方案 B 不可用，自动回退方案 A
- **Feature Report 失败**：静默失败，不影响其他三通道
- **自定义文件不存在**：回退方案 A
- **开关关闭**：`TriggerControllerSpeaker()` 直接返回

## 验证方式

1. USB 连接 DualSense，将电量阈值调到 100%，确认手柄扬声器发出蜂鸣
2. 调整蜂鸣持续时间，确认生效
3. 选择 WAV/MP3 文件，再次触发，确认播放自定义音频
4. 关闭手柄扬声器开关，确认不播放
5. 断开 USB 改用蓝牙，确认蜂鸣仍可播放（方案 A）
6. 调整音量滑块，确认音量变化
