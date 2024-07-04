using System;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;
using OpenCvSharp;

namespace Antelcat.Media.Internal;

internal static class OpenCvExtension
{
    /// <summary>
    /// 这个方法不会创建新的内存，而是直接使用原始数据
    /// </summary>
    /// <param name="frame"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public static Mat AsMat(this RawVideoFrame frame)
    {
        if (frame is OpenCvMatVideoFrame { Mat: { } mat })
        {
            return mat;
        }

        return frame.Format switch
        {
            VideoFrameFormat.YUY2 => new Mat(frame.Height, frame.Width, MatType.CV_8UC2, frame.Data, frame.Pitch),
            VideoFrameFormat.Yv12 => new Mat(frame.Height, frame.Width, MatType.CV_8UC3, frame.Data, frame.Pitch),
            VideoFrameFormat.NV12 => new Mat(frame.Height / 2 * 3, frame.Width, MatType.CV_8U, frame.Data, frame.Pitch / 3 * 2),
            VideoFrameFormat.RGB24 => new Mat(frame.Height, frame.Width, MatType.CV_8UC3, frame.Data, frame.Pitch),
            VideoFrameFormat.RGBA32 => new Mat(frame.Height, frame.Width, MatType.CV_8UC4, frame.Data, frame.Pitch),
            VideoFrameFormat.Grayscale => new Mat(frame.Height, frame.Width, MatType.CV_8U, frame.Data, frame.Pitch),
            VideoFrameFormat.MJPG => Cv2.ImDecode(frame.AsSpan(), ImreadModes.Color),
            _ => throw new NotSupportedException()
        };
    }

    public static ColorConversionCodes FrameFormat2ColorConversionCodes(VideoFrameFormat originalFormat, VideoFrameFormat destFormat)
    {
        return originalFormat switch
        {
            VideoFrameFormat.NV12 => destFormat switch
            {
                VideoFrameFormat.RGBA32 => ColorConversionCodes.YUV2BGRA_NV12,
                VideoFrameFormat.RGB24 => ColorConversionCodes.YUV2BGR_NV12,
                _ => throw new NotSupportedException()
            },
            VideoFrameFormat.MJPG => destFormat switch
            {
                VideoFrameFormat.Yv12 => ColorConversionCodes.BGR2YUV_YV12,
                VideoFrameFormat.RGBA32 => ColorConversionCodes.RGB2RGBA,
                _ => throw new NotSupportedException(),
            },
            VideoFrameFormat.RGB24 => destFormat switch
            {
                VideoFrameFormat.Yv12 => ColorConversionCodes.BGR2YUV_YV12,
                VideoFrameFormat.RGBA32 => ColorConversionCodes.RGB2RGBA,
                _ => throw new NotSupportedException(),
            },
            VideoFrameFormat.RGBA32 => destFormat switch
            {
                VideoFrameFormat.Yv12 => ColorConversionCodes.BGRA2YUV_YV12,
                VideoFrameFormat.RGB24 => ColorConversionCodes.RGBA2RGB,
                _ => throw new NotSupportedException(),
            },
            VideoFrameFormat.YUY2 => destFormat switch
            {
                VideoFrameFormat.RGB24 => ColorConversionCodes.YUV2BGR_YUY2,
                VideoFrameFormat.RGBA32 => ColorConversionCodes.YUV2BGRA_YUY2,
                _ => throw new NotSupportedException(),
            },
            _ => throw new NotSupportedException(),
        };
    }
}