namespace DsBatteryIndicator.Plugins;

/// <summary>
/// 插件标准生命周期接口。每个插件自包含，互不依赖。
/// </summary>
public interface IPlugin
{
    string Name { get; }
    bool IsAvailable { get; }
    void Initialize();
    void Shutdown();
}
