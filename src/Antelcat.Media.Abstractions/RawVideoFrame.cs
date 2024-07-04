using Antelcat.Media.Abstractions.Enums;

namespace Antelcat.Media.Abstractions;

public class RawVideoFrame : RawFrame<VideoFrameFormat>
{
    public int Width { get; }

    public virtual int Pitch => Length / Height;

    public int Height { get; }

    /// <summary>
    /// 分配一个内存空间，数据是自己拥有的，Dispose会释放
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="length"></param>
    /// <param name="format"></param>
    public RawVideoFrame(int width, int height, int length, VideoFrameFormat format) : base(length, format)
    {
        Width = width;
        Height = height;
    }

    public RawVideoFrame(int width, int height, int length, IntPtr data, VideoFrameFormat format) : base(data, length, format)
    {
        Width = width;
        Height = height;
    }
}