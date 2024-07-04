using Antelcat.Media.Abstractions;
using Antelcat.Media.Windows.Abstractions.Interfaces;
using SharpDX.MediaFoundation;

namespace Antelcat.Media.Windows.Abstractions;

public sealed class MediaFoundationVideoPacket : RawVideoPacket, IMediaFoundationPacket
{
    public Sample Sample { get; }

    private readonly MediaBuffer mediaBuffer;

    private MediaFoundationVideoPacket(Sample sample, MediaBuffer mediaBuffer, EncodedVideoFormat format) :
        base(mediaBuffer.Lock(out _, out _), mediaBuffer.CurrentLength, format)
    {

        Sample = sample;
        this.mediaBuffer = mediaBuffer;
    }

    public MediaFoundationVideoPacket(Sample sample, EncodedVideoFormat format) :
        this(sample, sample.ConvertToContiguousBuffer(), format)
    {
    }

    public override void Dispose()
    {
        base.Dispose();
        mediaBuffer.Unlock();
        mediaBuffer.Dispose();
    }
}