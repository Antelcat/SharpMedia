namespace Antelcat.Media.Abstractions;

/// <summary>
/// 输入的是<see cref="RawVideoFrame"/>，输出的是<see cref="RawVideoPacket"/>
/// </summary>
public interface IVideoEncoder : IEncoder<IVideoEncoder, VideoInputDevice, EncodedVideoFormat, RawVideoFrame, RawVideoPacket>;