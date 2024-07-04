using Antelcat.Media.Abstractions;
using Antelcat.Media.Extensions;
using Sdcb.FFmpeg.Codecs;

namespace Antelcat.Media.Internal; 

public class FFmpegAudioPacket : RawAudioPacket { 
    public Packet Packet { get; }

    public FFmpegAudioPacket(Packet packet, EncodedAudioFormat format) :
        base(packet.Data.Pointer, packet.Data.Length, format) {
        Packet = packet;
        Dts = packet.Dts.ToTimeSpan(packet.TimeBase);
        Pts = packet.Pts.ToTimeSpan(packet.TimeBase);
        Duration = packet.Duration.ToTimeSpan(packet.TimeBase);
    }
}