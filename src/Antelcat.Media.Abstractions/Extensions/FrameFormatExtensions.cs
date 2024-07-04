using Antelcat.Media.Abstractions.Enums;
using EasyPathology.Abstractions.DataTypes;

namespace Antelcat.Media.Abstractions.Extensions;

public static class FrameFormatExtensions
{
    public static Fraction GetBytesPerPixel(this VideoFrameFormat format)
    {
        return format switch
        {
            VideoFrameFormat.Yv12 => 3,
            VideoFrameFormat.NV12 => new Fraction(3, 2),
            VideoFrameFormat.YUY2 => 2,
            VideoFrameFormat.RGB24 => 3,
            VideoFrameFormat.RGBA32 => 4,
            VideoFrameFormat.Grayscale => 1,
            _ => throw new NotSupportedException()
        };
    }
}