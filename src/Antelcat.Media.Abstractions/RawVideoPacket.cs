namespace Antelcat.Media.Abstractions;

public class RawVideoPacket(IntPtr data, int length, EncodedVideoFormat format) :
    RawPacket<EncodedVideoFormat>(data, length, format)
{
    /// <summary>
    /// 是否为关键帧，估计Muxer要用
    /// </summary>
    public bool IsKeyFrame { get; init; }
}