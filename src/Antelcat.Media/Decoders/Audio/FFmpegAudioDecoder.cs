using System;
using System.Threading;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Interfaces;
using Antelcat.Media.Extensions;

namespace Antelcat.Media.Decoders;

public class FFmpegAudioDecoder : IAudioDecoder
{
    public AudioFrameFormat FrameFormat { get; }
    public TimeSpan CurrentTime => decoder.CurrentTime;
    public event Action<RawAudioFrame>? FrameDecoded;

    private readonly FFmpegDecoder decoder;

    public FFmpegAudioDecoder(FFmpegDecoderContext decoderContext, AudioFrameFormat dstFormat)
    {
        FrameFormat = dstFormat;
        decoder = new FFmpegDecoder(decoderContext, new FFmpegAudioFrameConverter(dstFormat),
            decodedFrame =>
            {
                if (FrameDecoded == null) return;
                FrameDecoded(new RawAudioFrame(
                    decodedFrame.NbSamples,
                    decodedFrame.Data._0,
                    decodedFrame.NbSamples * FrameFormat.ChannelCount * FrameFormat.BitsPerSample / 8,
                    FrameFormat)
                {
                    Time = decodedFrame.Pts.ToTimeSpan(decodedFrame.TimeBase),
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