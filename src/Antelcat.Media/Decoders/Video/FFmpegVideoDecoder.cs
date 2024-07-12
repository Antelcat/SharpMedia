using System;
using System.Threading;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;
using Antelcat.Media.Abstractions.Interfaces;
using Antelcat.Media.Extensions;

namespace Antelcat.Media.Decoders;

public class FFmpegVideoDecoder : IVideoDecoder
{
    public VideoFrameFormat FrameFormat { get; }
    public TimeSpan CurrentTime => decoder.CurrentTime;
    public event Action<RawVideoFrame>? FrameDecoded;

    private readonly FFmpegDecoder decoder;

    public FFmpegVideoDecoder(FFmpegDecoderContext decoderContext, VideoFrameFormat dstFormat)
    {
        FrameFormat = dstFormat;
        decoder = new FFmpegDecoder(decoderContext, new FFmpegVideoFrameConverter(dstFormat),
            decodedFrame =>
            {
                if (FrameDecoded == null) return;
                FrameDecoded(new RawVideoFrame(
                    decodedFrame.Width,
                    decodedFrame.Height,
                    decodedFrame.Linesize[0] * decodedFrame.Height,
                    decodedFrame.Data._0,
                    FrameFormat)
                {
                    Time = decodedFrame.PktDts.ToTimeSpan(decodedFrame.TimeBase),
                    Duration = decodedFrame.Duration.ToTimeSpan(decodedFrame.TimeBase),
                });
            });
    }

    public DecodeResult Decode(CancellationToken cancellationToken)
    {
        return decoder.Decode(cancellationToken);
    }

    public void Dispose()
    {
        decoder.Dispose();
        GC.SuppressFinalize(this);
    }
}