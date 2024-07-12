using System.Collections.Generic;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;
using Antelcat.Media.Abstractions.Interfaces;
using Antelcat.Media.Internal;
using Antelcat.Media.Extensions;

namespace Antelcat.Media.Encoders.Video;

public class FFmpegVideoEncoder : IVideoEncoder
{
    public IEnumerable<EncodedVideoFormat> SupportedFormats { get; } = new[]
    {
        EncodedVideoFormat.H264, EncodedVideoFormat.Hevc,
        EncodedVideoFormat.Vp8, EncodedVideoFormat.Vp9
    };

    public EncodedVideoFormat Format { get; set; }

    public int Bitrate { get; set; } = 400000;

    public event IVideoEncoder.OpeningHandler? Opening;
    public event IVideoEncoder.FrameEncodedHandler? FrameEncoded;
    public event IVideoEncoder.ClosingHandler? Closing;

    internal FFmpegEncoder Encoder { get; } = new();

    public void Open(VideoInputDevice device)
    {
        Encoder.Open(device, Format.ToCodecId(), Bitrate);
        Opening?.Invoke(device, this);
    }

    public void EncodeFrame(VideoInputDevice device, RawVideoFrame frame)
    {
        foreach (var packet in Encoder.EncodeFrame(frame))
        {
            FrameEncoded?.Invoke(new FFmpegVideoPacket(packet, Format));
        }
    }

    public void Close(VideoInputDevice device)
    {
        Closing?.Invoke(device, this);
        Encoder.Close();
    }
}