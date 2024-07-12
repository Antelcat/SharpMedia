using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;
using Antelcat.Media.Extensions;
using Sdcb.FFmpeg.Codecs;

namespace Antelcat.Media.Internal;

internal class FFmpegVideoPacket : RawVideoPacket
{
    public Packet Packet { get; }

    public FFmpegVideoPacket(Packet packet, EncodedVideoFormat format) :
        base(packet.Data.Pointer, packet.Data.Length, format)
    {
        Packet = packet;
        Dts = packet.Dts.ToTimeSpan(packet.TimeBase);
        Pts = packet.Pts.ToTimeSpan(packet.TimeBase);
        Duration = packet.Duration.ToTimeSpan(packet.TimeBase);
    }
}