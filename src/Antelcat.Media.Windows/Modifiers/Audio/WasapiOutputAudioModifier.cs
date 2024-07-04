using Antelcat.Media.Abstractions;
using Antelcat.Media.Streams;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Antelcat.Media.Windows.Modifiers.Audio;

public class WasapiOutputAudioModifier : IAudioModifier, IWaveProvider
{
    public AudioFrameFormat? TargetFormat => null;

    public bool IsPlaying { get; set; }

    /// <summary>
    /// 0-1
    /// </summary>
    public float Volume
    {
        get => output?.Volume ?? volume;
        set
        {
            value = Math.Clamp(value, 0, 1);
            volume = value;
            if (output != null) output.Volume = value;
        }
    }

    private float volume = 0.5f;

    private readonly MediaDeviceInformation info;
    private WasapiOut? output;

    private const int Latency = 100;

    public WasapiOutputAudioModifier(MediaDeviceInformation info)
    {
        if (info.Type != MediaDeviceType.Speaker)
        {
            throw new ArgumentException("info.Type != MediaDeviceType.Speaker");
        }

        this.info = info;
    }

    public void Open(AudioInputDevice device, AudioFrameFormat srcFormat)
    {
        var mmDevice = new MMDeviceEnumerator().GetDevice(info.Uid);
        output = new WasapiOut(mmDevice, AudioClientShareMode.Shared, true, Latency)
        {
            Volume = volume
        };

        WaveFormat = srcFormat.IsFloat switch
        {
            true when srcFormat.BitsPerSample == 32 => WaveFormat.CreateIeeeFloatWaveFormat(srcFormat.SampleRate, srcFormat.ChannelCount),
            false => new WaveFormat(srcFormat.SampleRate, srcFormat.BitsPerSample, srcFormat.ChannelCount),
            _ => throw new NotSupportedException("Unsupported audio format")
        };

        lock (waveBufferLock)
        {
            waveBuffer = new ConstantMemoryStream((ulong)srcFormat.AverageBytesPerSecond * Latency / 100);
        }

        output.Init(this);
        output.Play();
    }

    public RawAudioFrame ModifyFrame(AudioInputDevice device, RawAudioFrame frame, CancellationToken cancellationToken)
    {
        Monitor.Enter(waveBufferLock);
        if (waveBuffer == null)
        {
            Monitor.Exit(waveBufferLock);
            return frame;
        }

        while ((long)waveBuffer.Capacity - waveBuffer.Length < frame.Length)
        {
            Monitor.Exit(waveBufferLock);
            Thread.Sleep(Latency);
            if (cancellationToken.IsCancellationRequested) return frame;
            Monitor.Enter(waveBufferLock);
        }

        var array = frame.ToArray();
        waveBuffer.Write(array, 0, array.Length);
        waveBufferWait.Set();
        Monitor.Exit(waveBufferLock);
        return frame;
    }

    public void Close(AudioInputDevice device)
    {
        WaveFormat = null;
        lock (waveBufferLock)
        {
            waveBufferWait.Set();
            if (waveBuffer != null)
            {
                waveBuffer.Flush();
                waveBuffer = null;
            }
        }
        if (output != null)
        {
            output.Dispose();
            output = null;
        }
    }

    public void Flush()
    {
        lock (waveBufferLock)
        {
            waveBufferWait.Set();
            waveBuffer?.Flush();
        }
    }

    #region IWaveProvider

    public WaveFormat? WaveFormat { get; private set; }

    private ConstantMemoryStream? waveBuffer;
    private readonly object waveBufferLock = new();
    private readonly AutoResetEvent waveBufferWait = new(false);

    public int Read(byte[] buffer, int offset, int count)
    {
        while (waveBuffer != null && IsPlaying)
        {
            if (waveBuffer.Length < count)
            {
                waveBufferWait.WaitOne();
                continue;
            }

            break;
        }

        if (waveBuffer != null && IsPlaying)
        {
            lock (waveBufferLock)
            {
                if (waveBuffer != null && IsPlaying)
                {
                    return waveBuffer.Read(buffer, offset, count);
                }
            }
        }

        // 读取全0数据，达到即时停止的效果
        Array.Clear(buffer, offset, count);
        return count;
    }

    #endregion
}