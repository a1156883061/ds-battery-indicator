# DS Battery Indicator 设计文档

## 概述

Windows 11 桌面应用，通过浮动窗口显示 DualSense 手柄电量。USB 连接时通过 HID 协议读取电量，低电量时窗口变红闪烁并弹出系统通知。

## 技术选型

| 项目 | 选择 |
|------|------|
| 语言/框架 | C# + WPF (.NET 8) |
| 架构模式 | MVVM |
| HID 通信 | Windows.Devices.Hid（WinRT 互操作） |
| 平台 | Windows 11 |
| 国际化 | .NET 资源文件 (.resx)，默认中文 + 英文 |
| 连接方式 | USB 优先（一期不支持蓝牙） |

## 项目结构

```
DsBatteryIndicator/
├── App.xaml
├── Models/
│   └── DualSenseDevice.cs        # 设备信息模型
├── Services/
│   ├── HidService.cs             # HID 设备枚举、连接、读取
│   ├── BatteryParser.cs          # 报告解析，提取电量/充电状态
│   └── NotificationService.cs    # Windows Toast 通知
├── Resources/
│   ├── Strings.resx              # 默认语言 (中文)
│   └── Strings.en.resx           # 英文
├── ViewModels/
│   └── MainViewModel.cs          # UI 数据绑定
└── Views/
    ├── MainWindow.xaml           # 浮动窗口
    └── BatteryRing.xaml          # 环形进度条自定义控件
```

## 数据流

```
HID 输入报告 (64字节)
  → HidService (InputReportReceived 事件驱动)
    → BatteryParser (提取字节29: 电量, 字节52: 充电状态)
      → MainViewModel (更新 BatteryLevel, IsCharging, Status)
        → UI 绑定 (环形进度 + 数字 + 颜色/动画)
          → (BatteryLevel ≤ 10% 且 未充电) → NotificationService
```

## DualSense HID 协议（USB）

- **VID**: 0x054C (Sony), **PID**: 0x0CE6 (DualSense)
- **输入报告大小**: 64 字节
- **关键字段**:
  - 字节 0: 报告 ID (0x01)
  - 字节 29: 电量百分比 (0-100%，取低 4 位或完整字节，需固件交叉验证)
  - 字节 52 bit 4: 充电状态 (0=放电, 1=充电中/充满)
- **注意**: 不同固件版本电量字段位置可能有差异，实现时需验证

## UI 设计

### 浮动窗口 (MainWindow)

- WindowStyle=None, AllowsTransparency=True, Topmost=True
- 背景: #1A1A2E (暗色卡片), 边框 #2A2A3E, CornerRadius=14
- 阴影: DropShadowEffect
- 布局: 环形进度(左) + 百分比数字(右)，水平排列
- 拖拽: MouseLeftButtonDown + DragMove()
- 默认位置: 屏幕右下角，距边缘 40px
- 位置持久化: Settings.settings 保存上次位置
- 右键菜单: 置顶开关 / 开机自启 / 关于 / 退出
- 系统托盘: 支持后台运行，双击托盘恢复窗口

### 环形进度条 (BatteryRing)

- 底环: 固定色 #2A2A3E，始终完整 360°
- 前景环: 动态色，Stroke 角度 0-360° 对应 0-100%
- 中心: 手柄图标（emoji 或 Path 绘制）
- 动画: 电量变化时 Arc 端点平滑过渡 (DoubleAnimation, 300ms)

### 四状态行为

| 状态 | 条件 | 环形颜色 | 动画 | 通知 |
|------|------|----------|------|------|
| 正常 | 电量 > 10% 且未充电 | #4ADE80 (绿) | 无 | 无 |
| 充电中 | 充电状态位=1 | #60A5FA (蓝) | 虚线旋转 (2s/圈) | 无 |
| 低电量 | 电量 ≤ 10% 且未充电 | #EF4444 (红) | 窗口闪烁 (1.0↔0.5, 800ms) | Toast 弹一次 |
| 未连接 | 设备未枚举到 | #666666 (灰) | 无，数字显示"——" | 无 |

### 状态切换规则

- 未连接 → 设备插入 → 正常
- 正常 → 插入充电线 → 充电中
- 正常 → 电量 ≤ 10% → 低电量
- 低电量 → 插入充电线 → 充电中
- 低电量 → 电量回升 &gt; 10% → 正常
- 充电中 → 拔出充电线 → 正常
- 任意状态 → 设备拔出 → 未连接

## 国际化

- **方案**: .NET 资源文件 (.resx)，WPF 原生支持，无需第三方库
- **默认语言**: 简体中文 (Strings.resx)
- **支持语言**: 简体中文、英文 (Strings.en.resx)
- **语言检测**: 启动时读取 `CultureInfo.CurrentUICulture`，自动匹配对应资源文件；未匹配到则回退到中文
- **右键菜单切换**: 菜单中提供"语言 / Language"子菜单，手动切换后保存偏好到 Settings
- **需国际化的文本**:
  - 右键菜单：置顶/Topmost、开机自启/Auto Start、关于/About、退出/Exit
  - Toast 通知："DualSense 电量不足 (X%)，请充电" / "DualSense battery low (X%), please charge"
  - 关于窗口：应用名称、版本、说明文字
- **非文本 UI 元素不需要国际化**：数字百分比、环形进度条、颜色状态均为通用视觉符号

## 错误处理

| 场景 | 处理策略 |
|------|----------|
| 设备未找到 | 显示"未连接"，每 2s 自动重新枚举 |
| 设备热插拔 | DeviceWatcher.Added/Removed 自动切换状态 |
| HID 读取失败 | 静默重试 3 次 (间隔 500ms)，仍失败视为断开 |
| 多手柄 | 仅处理第一个枚举到的设备 |
| 蓝牙连接 | 一期不支持，显示"未连接"，架构预留扩展点 |
| 系统休眠/唤醒 | 监听 PowerModeChanged，Resume 时重新枚举 |
| 权限不足 | 弹窗提示用户 |
| 开机自启 | 右键菜单开关，写入注册表 HKCU\...\Run |

## 不纳入一期范围

- 蓝牙电量获取
- 多手柄支持
- 手柄按键/摇杆数据
- 自定义铃声/音效
