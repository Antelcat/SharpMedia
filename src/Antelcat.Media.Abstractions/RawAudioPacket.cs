using Antelcat.Media.Abstractions.Enums;

namespace Antelcat.Media.Abstractions;

/// <summary>
/// TODO 尚未实现，我还不知道需要哪些属性，参考VideoEncoderPacket
/// </summary>
public class RawAudioPacket : RawPacket<EncodedAudioFormat>
{
    public RawAudioPacket(int length, EncodedAudioFormat format) : base(length, format) { }

    public RawAudioPacket(IntPtr data, int length, EncodedAudioFormat format) : base(data, length, format) { }
}