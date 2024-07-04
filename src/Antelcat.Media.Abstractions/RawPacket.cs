namespace Antelcat.Media.Abstractions;

public class RawPacket : RawFrame
{
    /// <summary>
    /// Decoding Time Stamp
    /// </summary>
    public TimeSpan Dts { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Presentation timestamp
    /// </summary>
    public TimeSpan Pts { get; init; } = TimeSpan.Zero;

    public RawPacket(int length) : base(length) { }

    public RawPacket(IntPtr data, int length) : base(data, length) { }
}

public abstract class RawPacket<TFormat> : RawPacket
{
    public TFormat Format { get; }

    /// <summary>
    /// 分配一个内存空间，数据是自己拥有的，Dispose会释放
    /// </summary>
    /// <param name="length"></param>
    /// <param name="format"></param>
    protected RawPacket(int length, TFormat format) : base(length)
    {
        Format = format;
    }

    protected RawPacket(IntPtr data, int length, TFormat format) : base(data, length)
    {
        Format = format;
    }
}