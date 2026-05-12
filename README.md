# DS Battery Indicator

[English](README.en.md)

Windows 10/11 桌面应用，通过浮动窗口显示 DualSense 手柄电量。USB 连接时读取 HID 报告获取实时电量，支持低电量三通道提醒。

## 功能

- **浮动电量窗口**：暗色圆角卡片，环形进度条 + 百分比数字，可拖拽、置顶、调整透明度
- **低电量提醒**：托盘气泡 + 提示音 + 手柄震动/灯带变色（三通道并行，不打断游戏）
- **充电动画**：蓝色静电环切换为绿色旋转虚线，充电时不触发低电量提醒
- **系统托盘**：鼠标悬停显示电量状态，双击切换窗口显隐
- **RTSS 叠加**：通过 RivaTuner Statistics Server 在游戏内显示电量
- **中英双语**：自动检测系统语言，右键菜单一键切换
- **开机自启**：支持注册表写入
- **窗口位置/显隐状态持久化**：重启恢复

## 运行要求

- Windows 10/11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- DualSense 手柄（USB 连接，VID 0x054C / PID 0x0CE6）
- RTSS 叠加功能需要安装 RivaTuner Statistics Server

## 构建

```bash
cd DsBatteryIndicator
dotnet build
dotnet run
```

## 发布

双击 `publish.bat` 或手动执行：

```bash
cd DsBatteryIndicator
dotnet publish -c Release -o ../publish
```

## 项目结构

```
DsBatteryIndicator/
├── App.xaml/.cs              入口 + 系统托盘
├── Models/
│   └── DualSenseDevice.cs    设备状态模型
├── Services/
│   ├── HidService.cs         HID 通信（设备枚举/读取/输出报告）
│   ├── BatteryParser.cs      报告解析（电量/充电状态）
│   ├── NotificationService.cs 低电量通知
│   └── AppSettings.cs        JSON 设置持久化
├── Resources/
│   └── Strings.cs            中英双语字符串
├── Plugins/
│   ├── IPlugin.cs            插件接口
│   └── RtssPlugin/
│       └── RtssService.cs    RTSS 共享内存写入
├── ViewModels/
│   └── MainViewModel.cs      MVVM ViewModel
└── Views/
    ├── MainWindow.xaml/.cs   浮动窗口
    ├── BatteryRing.xaml/.cs  环形进度条控件
    └── HapticSettingsWindow  设置窗口
```

## 技术参考

- DualSense USB HID 报告格式：Linux 内核 `hid-playstation.c`
- RTSS 共享内存：RTSS SDK `RTSSSharedMemory.h`
- 输出报告格式：`daidr/dualsense-tester`
