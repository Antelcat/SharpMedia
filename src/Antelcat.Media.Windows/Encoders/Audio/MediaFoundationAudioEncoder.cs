using Antelcat.Media.Abstractions;
using Antelcat.Media.Windows.Abstractions;
using Antelcat.Media.Windows.Abstractions.Interfaces;
using SharpDX.MediaFoundation;
using static Antelcat.Media.Abstractions.IEncoder<Antelcat.Media.Abstractions.IAudioEncoder,Antelcat.Media.Abstractions.AudioInputDevice,Antelcat.Media.Abstractions.EncodedAudioFormat,Antelcat.Media.Abstractions.RawAudioFrame,Antelcat.Media.Abstractions.RawAudioPacket>;

namespace Antelcat.Media.Windows.Encoders.Audio;

public class MediaFoundationAudioEncoder : IMediaFoundationAudioEncoder
{
    public IEnumerable<EncodedAudioFormat> SupportedFormats => new[]
    {
        EncodedAudioFormat.Mp3, EncodedAudioFormat.Aac, EncodedAudioFormat.Wma
    };

    public EncodedAudioFormat Format { get; set; }

    public int Bitrate { get; set; }

    public MediaType InputMediaType { get; }

    public MediaType OutputMediaType { get; }

    public event OpeningHandler? Opening;
    public event FrameEncodedHandler? FrameEncoded;
    public event ClosingHandler? Closing;

    public MediaFoundationAudioEncoder()
    {
        InputMediaType = new MediaType();

        OutputMediaType = new MediaType();
        OutputMediaType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Audio);
    }

    public void Open(AudioInputDevice device)
    {
        InputMediaType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Audio);
        InputMediaType.Set(MediaTypeAttributeKeys.Subtype, device.Format.IsFloat ? AudioFormatGuids.Float : AudioFormatGuids.Pcm);
        InputMediaType.Set(MediaTypeAttributeKeys.AudioSamplesPerSecond, device.Format.SampleRate);
        InputMediaType.Set(MediaTypeAttributeKeys.AudioBitsPerSample, device.Format.BitsPerSample);
        InputMediaType.Set(MediaTypeAttributeKeys.AudioNumChannels, device.Format.ChannelCount);
        InputMediaType.Set(MediaTypeAttributeKeys.AudioBlockAlignment, device.Format.BlockAlign);
        InputMediaType.Set(MediaTypeAttributeKeys.AllSamplesIndependent, 1);

        OutputMediaType.Set(MediaTypeAttributeKeys.AudioSamplesPerSecond, device.Format.SampleRate);
        OutputMediaType.Set(MediaTypeAttributeKeys.AudioBitsPerSample, device.Format.BitsPerSample);
        OutputMediaType.Set(MediaTypeAttributeKeys.AudioNumChannels, device.Format.ChannelCount);
        OutputMediaType.Set(MediaTypeAttributeKeys.AudioBlockAlignment, device.Format.BlockAlign);
        OutputMediaType.Set(MediaTypeAttributeKeys.AllSamplesIndependent, 1);
        var outputSubType = Format switch
        {
            EncodedAudioFormat.Mp3 => AudioFormatGuids.Mp3,
            EncodedAudioFormat.Aac => AudioFormatGuids.Aac,
            EncodedAudioFormat.Wma => AudioFormatGuids.WMAudioLossless,
            _ => throw new NotSupportedException()
        };
        OutputMediaType.Set(MediaTypeAttributeKeys.Subtype, outputSubType);
        OutputMediaType.Set(MediaTypeAttributeKeys.AudioAvgBytesPerSecond, Bitrate / 8);

        Opening?.Invoke(device, this);
    }

    public void EncodeFrame(AudioInputDevice device, RawAudioFrame frame)
    {
        using var buffer = MediaFactory.CreateMemoryBuffer(frame.Length);
        var bufferPtr = buffer.Lock(out _, out _);
        unsafe
        {
            Buffer.MemoryCopy(frame.Data.ToPointer(), bufferPtr.ToPointer(), frame.Length, frame.Length);
        }
        buffer.Unlock();
        buffer.CurrentLength = frame.Length;
        using var sample = MediaFactory.CreateSample();
        sample.AddBuffer(buffer);
        sample.SampleTime = frame.Time.Ticks;
        sample.SampleDuration = frame.Duration.Ticks;

        FrameEncoded?.Invoke(new MediaFoundationAudioPacket(sample, Format));
    }

    public void Close(AudioInputDevice device)
    {
        Closing?.Invoke(device, this);
    }
}