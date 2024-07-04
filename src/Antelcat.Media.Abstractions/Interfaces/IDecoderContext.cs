namespace Antelcat.Media.Abstractions;

/// <summary>
/// 解码器上下文
/// </summary>
public interface IDecoderContext
{
    DecodeResult ReadPacket(out RawPacket? rawPacket, CancellationToken cancellationToken);
}