using System.Drawing;
using System.Drawing.Imaging;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;
using SharpDX.DXGI;

namespace Antelcat.Media.Windows.Extensions;

public static class VideoFrameExtensions
{
    public static Bitmap ToBitmap(this RawVideoFrame rawVideoFrame)
    {
        var bitmap = new Bitmap(rawVideoFrame.Width, rawVideoFrame.Height, rawVideoFrame.Format switch
        {
            VideoFrameFormat.RGB24 => PixelFormat.Format24bppRgb,
            VideoFrameFormat.RGBA32 => PixelFormat.Format32bppArgb,
            _ => throw new NotSupportedException()
        });
        var boundsRect = new Rectangle(0, 0, rawVideoFrame.Width, rawVideoFrame.Height);
        var bmpData = bitmap.LockBits(boundsRect, ImageLockMode.WriteOnly, bitmap.PixelFormat);

        unsafe
        {
            var srcData = (byte*)rawVideoFrame.Data;
            var destData = (byte*)bmpData.Scan0;

            for (var y = 0; y < rawVideoFrame.Height; y++)
            {
                Buffer.MemoryCopy(srcData, destData, bmpData.Stride, Math.Min(bmpData.Stride, rawVideoFrame.Pitch));
                srcData += rawVideoFrame.Pitch;
                destData += bmpData.Stride;
            }
        }

        bitmap.UnlockBits(bmpData);
        return bitmap;
    }

    public static Format ToDxgiFormat(this VideoFrameFormat format)
    {
        return format switch
        {
            VideoFrameFormat.NV12 => Format.NV12,
            VideoFrameFormat.YUY2 => Format.YUY2,
            VideoFrameFormat.RGBA32 => Format.B8G8R8A8_UNorm,
            _ => Format.Unknown
        };
    }

    public static VideoFrameFormat ToVideoFrameFormat(this Format format)
    {
        return format switch
        {
            Format.NV12 => VideoFrameFormat.NV12,
            Format.YUY2 => VideoFrameFormat.YUY2,
            Format.B8G8R8A8_UNorm => VideoFrameFormat.RGBA32,
            _ => VideoFrameFormat.Unset
        };
    }
}