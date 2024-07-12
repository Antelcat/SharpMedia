namespace Antelcat.Media.Abstractions.Interfaces;

public interface IEncoder<TEncoder, TDevice, TFormat, TFrame, TPacket>
    where TEncoder : IEncoder<TEncoder, TDevice, TFormat, TFrame, TPacket>
    where TDevice : InputDevice
{
    /// <summary>
    /// 支持的所有格式
    /// </summary>
    IEnumerable<TFormat> SupportedFormats { get; }
    
    /// <summary>
    /// 要编码的格式
    /// </summary>
    TFormat Format { get; set; }
    
    /// <summary>
    /// 编码比特率
    /// </summary>
    int Bitrate { get; }

    #region events

    delegate void OpeningHandler(TDevice device, TEncoder encoder);
    event OpeningHandler Opening;

    delegate void FrameEncodedHandler(TPacket packet);
    event FrameEncodedHandler FrameEncoded;

    delegate void ClosingHandler(TDevice device, TEncoder encoder);
    /// <summary>
    /// 在Encoder刚被调用，还没有释放资源之前触发该事件
    /// </summary>
    event ClosingHandler Closing;

    #endregion
    
    /// <summary>
    /// 打开的时候执行，创建编码器
    /// </summary>
    /// <param name="device"></param>
    void Open(TDevice device);

    /// <summary>
    /// 编码
    /// </summary>
    /// <param name="device"></param>
    /// <param name="frame"></param>
    /// <returns></returns>
    void EncodeFrame(TDevice device, TFrame frame);

    /// <summary>
    /// 关闭的时候执行
    /// </summary>
    /// <param name="device"></param>
    void Close(TDevice device);
}