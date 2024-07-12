using System;
using System.Threading;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Interfaces;

namespace Antelcat.Media.Modifiers;

public class AudioVolumeModifier : IAudioModifier
{
    /// <summary>
    /// 0 ~ 2
    /// </summary>
    public double Volume
    {
        set => adjustedVolume = Math.Pow(value, 3);
    }

    private double adjustedVolume = 1d;

    public AudioFrameFormat? TargetFormat { get; private set; }

    public void Open(AudioInputDevice device, AudioFrameFormat srcFormat)
    {
        TargetFormat = srcFormat;
    }

    public RawAudioFrame ModifyFrame(AudioInputDevice device, RawAudioFrame frame, CancellationToken cancellationToken)
    {
        var byteDepth = frame.Format.BitsPerSample / 8;
        var channels = device.Format.ChannelCount;
        var isFloat = device.Format.IsFloat;
        var isPlanar = device.Format.IsPlanar;

        var sampleCount = (int)(frame.Length / (byteDepth * channels));
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (sampleCount == 0 || adjustedVolume == 1d)
        {
            return frame;
        }
        if (adjustedVolume <= 0)
        {
            frame.AsSpan().Clear();
            return frame;
        }

        void SetVolume<T>(Span<T> span, Func<T, double, T> volumeSetter) where T : struct
        {
            for (var i = 0; i < sampleCount; i++)
            {
                for (var j = 0; j < channels; j++)
                {
                    var index = isPlanar ? i + j * sampleCount : i * channels + j;
                    span[index] = volumeSetter(span[index], adjustedVolume);
                }
            }
        }

        switch (byteDepth)
        {
            case 1:
                SetVolume(frame.AsSpan<byte>(), static (value, volume) => (byte)Math.Clamp(value * volume, byte.MinValue, byte.MaxValue));
                break;
            case 2:
                SetVolume(frame.AsSpan<short>(), static (value, volume) => (short)Math.Clamp(value * volume, short.MinValue, short.MaxValue));
                break;
            case 4 when isFloat:
                SetVolume(frame.AsSpan<float>(), static (value, volume) => (float)Math.Clamp(value * volume, float.MinValue, float.MaxValue));
                break;
            case 4:
                SetVolume(frame.AsSpan<int>(), static (value, volume) => (int)Math.Clamp(value * volume, int.MinValue, int.MaxValue));
                break;
            case 8 when isFloat:
                SetVolume(frame.AsSpan<double>(), static (value, volume) => value * volume);
                break;
            default:
                throw new ArgumentOutOfRangeException($"Unsupported byte depth {byteDepth}");
        }

        return frame;
    }

    public void Close(AudioInputDevice device) { }
}