using System.Threading;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Interfaces;
using SoundTouch;

namespace Antelcat.Media.Modifiers;

public class AudioSpeedModifier : IAudioModifier
{
    public AudioFrameFormat? TargetFormat => null;

    /// <summary>
    /// 只改变速度，不改变音调
    /// </summary>
    public double Tempo
    {
        // ReSharper disable once InconsistentlySynchronizedField
        get => processor.Tempo;
        set
        {
            lock (processor)
            {
                processor.Tempo = value;
            }
        }
    }
    
    /// <summary>
    /// 只改变音调，不改变速度
    /// </summary>
    public double Pitch
    {
        // ReSharper disable once InconsistentlySynchronizedField
        get => processor.Pitch;
        set
        {
            lock (processor)
            {
                processor.Pitch = value;
            }
        }
    }
    
    /// <summary>
    /// 改变速度和音调
    /// </summary>
    public double Rate
    {
        // ReSharper disable once InconsistentlySynchronizedField
        get => processor.Rate;
        set
        {
            lock (processor)
            {
                processor.Rate = value;
            }
        }
    }

    private readonly SoundTouchProcessor processor = new();

    /// <summary>
    /// Change settings to optimize for Speech.
    /// </summary>
    public AudioSpeedModifier OptimizeForSpeech()
    {
        lock (processor)
        {
            processor.SetSetting(SettingId.SequenceDurationMs, 50);
            processor.SetSetting(SettingId.SeekWindowDurationMs, 10);
            processor.SetSetting(SettingId.OverlapDurationMs, 20);
            processor.SetSetting(SettingId.UseQuickSeek, 0);
        }

        return this;
    }

    public void Open(AudioInputDevice device, AudioFrameFormat srcFormat)
    {
        lock (processor)
        {
            processor.SampleRate = srcFormat.SampleRate;
            processor.Channels = srcFormat.ChannelCount;
            processor.Clear();
        }
    }

    public RawAudioFrame ModifyFrame(AudioInputDevice device, RawAudioFrame frame, CancellationToken cancellationToken)
    {
        lock (processor)
        {
            using (frame)
            {
                var span = frame.AsSpan<float>();
                processor.PutSamples(span, span.Length / frame.Format.ChannelCount);

                var availableSampleSizeInBytes = processor.AvailableSamples * sizeof(float);
                var output = new RawAudioFrame(
                    availableSampleSizeInBytes * 8 / frame.Format.BitsPerSample,
                    availableSampleSizeInBytes,
                    frame.Format);
                output.SampleCount = processor.ReceiveSamples(output.AsSpan<float>(), processor.AvailableSamples / frame.Format.ChannelCount);
                return output;
            }
        }
    }

    public void Close(AudioInputDevice device)
    {
        lock (processor)
        {
            processor.Flush();
        }
    }
    
    public void Flush()
    {
        lock (processor)
        {
            processor.Flush();
        }
    }
}