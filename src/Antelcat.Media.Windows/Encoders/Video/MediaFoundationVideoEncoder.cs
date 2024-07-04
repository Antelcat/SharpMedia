using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;
using Antelcat.Media.Windows.Abstractions;
using Antelcat.Media.Windows.Abstractions.Interfaces;
using Antelcat.Media.Windows.Extensions;
using SharpDX.MediaFoundation;
using static Antelcat.Media.Abstractions.IEncoder<Antelcat.Media.Abstractions.IVideoEncoder,Antelcat.Media.Abstractions.VideoInputDevice,Antelcat.Media.Abstractions.EncodedVideoFormat,Antelcat.Media.Abstractions.RawVideoFrame,Antelcat.Media.Abstractions.RawVideoPacket>;

namespace Antelcat.Media.Windows.Encoders.Video;

public class MediaFoundationVideoEncoder : IMediaFoundationVideoEncoder
{
    public IEnumerable<EncodedVideoFormat> SupportedFormats => new[]
    {
        EncodedVideoFormat.H264, EncodedVideoFormat.Hevc,
        EncodedVideoFormat.Vp8, EncodedVideoFormat.Vp9
    };

    public EncodedVideoFormat Format { get; set; }

    public int Bitrate { get; set; } = 800000;

    public MediaType InputMediaType { get; }

    public MediaType OutputMediaType { get; }

    public event OpeningHandler? Opening;
    public event FrameEncodedHandler? FrameEncoded;
    public event ClosingHandler? Closing;

    public MediaFoundationVideoEncoder()
    {
        InputMediaType = new MediaType();
        OutputMediaType = new MediaType();
        OutputMediaType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
    }

    public void Open(VideoInputDevice device)
    {
        var outputSubType = Format switch
        {
            EncodedVideoFormat.H264 => VideoFormatGuids.H264,
            EncodedVideoFormat.Hevc => VideoFormatGuids.Hevc,
            EncodedVideoFormat.Vp8 => VideoFormatGuids.Vp80,
            EncodedVideoFormat.Vp9 => VideoFormatGuids.Vp90,
            _ => throw new NotSupportedException()
        };
        OutputMediaType.Set(MediaTypeAttributeKeys.Subtype, outputSubType);
        OutputMediaType.Set(MediaTypeAttributeKeys.AvgBitrate, Bitrate);

        InputMediaType.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
        var inputSubType = device.Format switch
        {
            VideoFrameFormat.RGBA32 => VideoFormatGuids.Rgb32,
            VideoFrameFormat.RGB24 => VideoFormatGuids.Rgb24,
            VideoFrameFormat.NV12 => VideoFormatGuids.NV12,
            VideoFrameFormat.YUY2 => VideoFormatGuids.YUY2,
            VideoFrameFormat.Yv12 => VideoFormatGuids.Yv12,
            VideoFrameFormat.MJPG => VideoFormatGuids.Mjpg,
            VideoFrameFormat.Grayscale => VideoFormatGuids.Rgb8,
            _ => throw new NotSupportedException()
        };
        InputMediaType.Set(MediaTypeAttributeKeys.Subtype, inputSubType);
        InputMediaType.Set(MediaTypeAttributeKeys.InterlaceMode, (int)VideoInterlaceMode.Progressive);
        InputMediaType.Set(MediaTypeAttributeKeys.FrameSize, ((uint)device.FrameWidth, (uint)device.FrameHeight).Pack());
        InputMediaType.Set(MediaTypeAttributeKeys.FrameRate, device.FrameRate.Pack());
        InputMediaType.Set(MediaTypeAttributeKeys.PixelAspectRatio, (1u, 1u).Pack());

        OutputMediaType.Set(MediaTypeAttributeKeys.InterlaceMode, (int)VideoInterlaceMode.Progressive);
        OutputMediaType.Set(MediaTypeAttributeKeys.FrameSize, ((uint)device.FrameWidth, (uint)device.FrameHeight).Pack());
        OutputMediaType.Set(MediaTypeAttributeKeys.FrameRate, device.FrameRate.Pack());
        OutputMediaType.Set(MediaTypeAttributeKeys.PixelAspectRatio, (1u, 1u).Pack());

        Opening?.Invoke(device, this);
    }

    public void EncodeFrame(VideoInputDevice device, RawVideoFrame frame)
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

        FrameEncoded?.Invoke(new MediaFoundationVideoPacket(sample, Format));
    }

    public void Close(VideoInputDevice device)
    {
        Closing?.Invoke(device, this);
    }
}