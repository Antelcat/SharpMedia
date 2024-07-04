using System.Runtime.InteropServices;

namespace Antelcat.Media.Abstractions;

public class RawFrame : IDisposable
{
    public IntPtr Data { get; private set; }

    public int Length { get; private set; }

    public TimeSpan Time { get; init; } = TimeSpan.MinValue;

    public TimeSpan Duration { get; init; } = TimeSpan.MinValue;

    public bool IsDataOwner { get; }

    /// <summary>
    /// 分配一个内存空间，数据是自己拥有的，Dispose会释放
    /// </summary>
    /// <param name="length"></param>
    protected RawFrame(int length)
    {
        Data = length == 0 ? IntPtr.Zero : Marshal.AllocHGlobal((IntPtr)length);
        Length = length;
        IsDataOwner = true;
    }

    protected RawFrame(IntPtr data, int length)
    {
        Data = data;
        Length = length;
        IsDataOwner = false;
    }

    ~RawFrame()
    {
        Dispose();
    }

    public unsafe Span<byte> AsSpan()
    {
        return new Span<byte>((void*)Data, (int)Length);
    }

    public unsafe Span<T> AsSpan<T>() where T : struct
    {
        return new Span<T>((void*)Data, (int)Length / Marshal.SizeOf<T>());
    }

    public byte[] ToArray()
    {
        return AsSpan().ToArray();
    }

    public virtual void Dispose()
    {
        if (IsDataOwner && Data != IntPtr.Zero)
        {
            var data = Data;
            Data = IntPtr.Zero;
            Length = 0;
            Marshal.FreeHGlobal(data);
        }

        GC.SuppressFinalize(this);
    }
}

public abstract class RawFrame<TFormat> : RawFrame
{
    public TFormat Format { get; }

    /// <summary>
    /// 分配一个内存空间，数据是自己拥有的，Dispose会释放
    /// </summary>
    /// <param name="length"></param>
    /// <param name="format"></param>
    protected RawFrame(int length, TFormat format) : base(length)
    {
        Format = format;
    }

    protected RawFrame(IntPtr data, int length, TFormat format) : base(data, length)
    {
        Format = format;
    }
}