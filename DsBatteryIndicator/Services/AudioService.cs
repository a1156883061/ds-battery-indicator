using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace DsBatteryIndicator.Services;

/// <summary>
/// 通过 USB 音频设备向 DualSense 手柄扬声器播放音频。
/// 内置蜂鸣：代码生成短促 PCM 蜂鸣音。自定义音频：播放用户选择的文件。
/// </summary>
public static class AudioService
{
    public static bool PlayCustomAudio(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            if (!HasControllerAudioDevice())
                return false;

            Task.Run(() => PlayCustomAudioOnMtaThread(filePath));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool PlayBuiltinBeep()
    {
        try
        {
            if (!HasControllerAudioDevice())
                return false;

            var cfg = AppSettings.Instance;
            double volume = cfg.ControllerSpeakerVolume / 100.0;
            int durationMs = Math.Clamp(cfg.ControllerSpeakerDurationMs, 100, 3000);

            Task.Run(() => PlayBuiltinBeepOnMtaThread(volume, durationMs));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 返回诊断信息：所有可用音频设备列表和匹配结果。
    /// </summary>
    public static string GetDiagnosticInfo()
    {
        try
        {
            var enumerator = new MMDeviceEnumerator();
            var allDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
            var match = allDevices.FirstOrDefault(d =>
                d.FriendlyName.Contains("Wireless Controller") ||
                d.FriendlyName.Contains("DualSense"));

            var lines = new System.Text.StringBuilder();
            lines.AppendLine($"可用音频渲染设备: {allDevices.Count} 个");
            foreach (var d in allDevices)
                lines.AppendLine($"  [{d.State}] {d.FriendlyName}");
            lines.AppendLine();
            if (match != null)
                lines.AppendLine($"匹配的手柄设备: {match.FriendlyName}");
            else
                lines.AppendLine("未匹配到手柄设备");
            return lines.ToString();
        }
        catch (Exception ex)
        {
            return $"获取诊断信息异常: {ex.Message}";
        }
    }

    private static void PlayBuiltinBeepOnMtaThread(double volume, int durationMs)
    {
        try
        {
            var device = FindControllerDeviceOnMtaThread();
            if (device == null) return;

            var format = new WaveFormat(44100, 16, 1);
            byte[] pcm = GenerateBeepPcm(format, volume, durationMs);
            using var stream = new MemoryStream(pcm);
            using var source = new RawSourceWaveStream(stream, format);
            using var output = new WasapiOut(device, AudioClientShareMode.Shared, false, 100);
            output.Init(source);
            output.Play();
            while (output.PlaybackState == PlaybackState.Playing)
                System.Threading.Thread.Sleep(50);
            device.Dispose();
        }
        catch { }
    }

    private static void PlayCustomAudioOnMtaThread(string filePath)
    {
        try
        {
            var device = FindControllerDeviceOnMtaThread();
            if (device == null) return;

            using var reader = new AudioFileReader(filePath);
            using var output = new WasapiOut(device, AudioClientShareMode.Shared, false, 100);
            output.Init(reader);
            output.Play();
            while (output.PlaybackState == PlaybackState.Playing)
                System.Threading.Thread.Sleep(50);
            device.Dispose();
        }
        catch { }
    }

    private static MMDevice? FindControllerDeviceOnMtaThread()
    {
        var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .FirstOrDefault(d =>
                d.FriendlyName.Contains("Wireless Controller") ||
                d.FriendlyName.Contains("DualSense"));
    }

    private static bool HasControllerAudioDevice()
    {
        var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Any(d =>
                d.FriendlyName.Contains("Wireless Controller") ||
                d.FriendlyName.Contains("DualSense"));
    }

    private static byte[] GenerateBeepPcm(WaveFormat format, double volume, int durationMs)
    {
        const double freq = 800.0;
        const double toneMs = 200.0;
        const double silenceMs = 100.0;
        double amplitude = 0.8 * volume;

        int totalSamples = (int)(format.SampleRate * durationMs / 1000.0);
        int samplesPerTone = (int)(format.SampleRate * toneMs / 1000.0);
        int samplesPerSilence = (int)(format.SampleRate * silenceMs / 1000.0);
        short maxAmp = (short)(short.MaxValue * amplitude);

        var samples = new short[totalSamples];
        int elapsed = 0;
        int phaseOffset = 0;

        while (elapsed < totalSamples)
        {
            int remaining = totalSamples - elapsed;
            for (int i = 0; i < Math.Min(samplesPerTone, remaining); i++)
            {
                double t = (double)(phaseOffset + i) / format.SampleRate;
                samples[elapsed++] = (short)(maxAmp * Math.Sin(2.0 * Math.PI * freq * t));
            }
            phaseOffset += Math.Min(samplesPerTone, remaining);
            int silenceLen = Math.Min(samplesPerSilence, totalSamples - elapsed);
            for (int i = 0; i < silenceLen; i++)
                samples[elapsed++] = 0;
            phaseOffset += silenceLen;
        }

        byte[] result = new byte[totalSamples * 2];
        Buffer.BlockCopy(samples, 0, result, 0, result.Length);
        return result;
    }
}
