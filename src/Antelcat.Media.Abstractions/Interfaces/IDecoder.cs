namespace Antelcat.Media.Abstractions; 

public enum DecodeResult
{
    Success,
    Again,
    Eof,
    Cancelled
}

public interface IDecoder<TDecoder, out TFrameFormat, out TFrame> : IDisposable where TDecoder : IDecoder<TDecoder, TFrameFormat, TFrame>
{
    /// <summary>
    /// 解码后Frame的格式
    /// </summary>
    TFrameFormat FrameFormat { get; }
    
    /// <summary>
    /// 最后一次成功DecodeOneFrame的帧时间
    /// </summary>
    TimeSpan CurrentTime { get; }

    event Action<TFrame> FrameDecoded;
    
    /// <summary>
    /// 每次调用可能会产出多帧，FrameDecoded被触发多次
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    DecodeResult Decode(CancellationToken cancellationToken);
}