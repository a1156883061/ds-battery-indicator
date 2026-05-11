namespace DsBatteryIndicator.Plugins.RtssPlugin;

public class RtssService : IPlugin, IDisposable
{
    public string Name => "RTSS Overlay";
    public bool IsAvailable => false;

    public void Initialize()
    {
    }

    public void UpdateBattery(int percent)
    {
    }

    public void Shutdown()
    {
    }

    public void Dispose()
    {
        Shutdown();
    }
}
