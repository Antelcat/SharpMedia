using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using Antelcat.Media.Abstractions;

namespace Antelcat.Media.Modifiers;

/// <summary>
/// 计量音频的实时音量
/// </summary>
public class VolumeMeteringModifier : IAudioModifier, INotifyPropertyChanged
{
    /// <summary>
    /// 0 ~ 1
    /// </summary>
    public double Volume { get; private set; }

    /// <summary>
    /// -80dB ~ 0dB
    /// </summary>
    public double VolumeInDecibel => 20 * Math.Log10(Volume);

    /// <summary>
    /// 对应每个Channel
    /// </summary>
    private double[]? maxSamples;

    public AudioFrameFormat? TargetFormat => null;

    public void Open(AudioInputDevice device, AudioFrameFormat srcFormat)
    {
        maxSamples = new double[device.Format.ChannelCount];
    }

    public RawAudioFrame ModifyFrame(AudioInputDevice device, RawAudioFrame frame, CancellationToken cancellationToken)
    {
        if (maxSamples == null)
        {
            return frame;
        }

        Array.Clear(maxSamples);
        var byteDepth = frame.Format.BitsPerSample / 8;
        var channels = device.Format.ChannelCount;
        var isFloat = device.Format.IsFloat;
        var isPlanar = device.Format.IsPlanar;

        var sampleCount = (int)(frame.Length / (byteDepth * channels)); // 计算采样点数
        if (sampleCount == 0)
        {
            return frame;
        }

        void GetVolume<T>(Span<T> span, Func<T, double> volumeGetter) where T : struct
        {
            for (var i = 0; i < sampleCount; i++)
            {
                for (var j = 0; j < channels; j++)
                {
                    var index = isPlanar ? i + j * sampleCount : i * channels + j;
                    maxSamples[j] = Math.Max(maxSamples[j], Math.Abs(volumeGetter(span[index])));
                }
            }
        }

        // 计算Volume，作为分贝值
        switch (byteDepth)
        {
            case 1:
                GetVolume(frame.AsSpan<byte>(), value => value / (double)byte.MaxValue);
                break;
            case 2:
                GetVolume(frame.AsSpan<short>(), value => value / (double)short.MaxValue);
                break;
            case 4 when isFloat:
                GetVolume(frame.AsSpan<float>(), value => value / (double)float.MaxValue);
                break;
            case 4:
                GetVolume(frame.AsSpan<int>(), value => value / (double)int.MaxValue);
                break;
            case 8 when isFloat:
                GetVolume(frame.AsSpan<double>(), value => value);
                break;
            default:
                throw new ArgumentOutOfRangeException($"Unsupported byte depth {byteDepth}");
        }
        
        // Calculate the volume in dB
        Volume = Math.Max(Math.Min(maxSamples.Max(), 1d), 0d);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Volume)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VolumeInDecibel)));

        return frame;
    }

    public void Close(AudioInputDevice device)
    {
        Volume = 0;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Volume)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VolumeInDecibel)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}