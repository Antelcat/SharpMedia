using Antelcat.Media.Abstractions;
using Antelcat.Media.Windows.Abstractions.Interfaces;
using SharpDX.MediaFoundation;

namespace Antelcat.Media.Windows.Abstractions;

public sealed class MediaFoundationAudioPacket : RawAudioPacket, IMediaFoundationPacket
{
    public Sample Sample { get; }

    private readonly MediaBuffer mediaBuffer;

    private MediaFoundationAudioPacket(Sample sample, MediaBuffer mediaBuffer, EncodedAudioFormat format) :
        base(mediaBuffer.Lock(out _, out _), mediaBuffer.CurrentLength, format)
    {

        Sample = sample;
        this.mediaBuffer = mediaBuffer;
    }

    public MediaFoundationAudioPacket(Sample sample, EncodedAudioFormat format) :
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