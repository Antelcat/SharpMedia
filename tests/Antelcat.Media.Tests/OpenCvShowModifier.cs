using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;
using OpenCvSharp;

namespace Antelcat.Media.Tests;

/// <summary>
/// 使用OpenCV显示视频，方便调试
/// </summary>
internal class OpenCvShowModifier(bool needSwapRb = false, int maxWidth = 0, int maxHeight = 0) : IVideoModifier
{
    public VideoFrameFormat TargetFormat => VideoFrameFormat.Unset;

    public void Open(VideoInputDevice device, VideoFrameFormat srcFormat) { }

    public RawVideoFrame ModifyFrame(VideoInputDevice device, RawVideoFrame frame, CancellationToken cancellationToken)
    {
        using var mat = new Mat();
        frame.AsMat().CopyTo(mat);

        if (frame.Format is not VideoFrameFormat.RGB24 and not VideoFrameFormat.RGBA32)
        {
            Cv2.CvtColor(mat, mat, OpenCvExtension.FrameFormat2ColorConversionCodes(frame.Format, VideoFrameFormat.RGB24));
        }

        if (needSwapRb)
        {
            switch (frame.Format)
            {
                case VideoFrameFormat.RGB24:
                {
                    Cv2.CvtColor(mat, mat, ColorConversionCodes.RGB2BGR);
                    break;
                }
                case VideoFrameFormat.RGBA32:
                {
                    Cv2.CvtColor(mat, mat, ColorConversionCodes.RGBA2BGRA);
                    break;
                }
            }
        }
        
        if (maxWidth > 0 || maxHeight > 0)
        {
            var scale = Math.Min(
                (maxWidth <= 0 ? double.PositiveInfinity : maxWidth) / mat.Width, 
                (maxHeight <= 0 ? double.PositiveInfinity : maxHeight) / mat.Height);
            if (scale < 1)
            {
                Cv2.Resize(mat, mat, new Size(), scale, scale);
            }
        }

        Cv2.ImShow(nameof(OpenCvShowModifier), mat);
        Cv2.WaitKey(1);
        return frame;
    }

    public void Close(VideoInputDevice device)
    {
        try
        {
            Cv2.DestroyWindow(nameof(OpenCvShowModifier));
        }
        catch
        {
            // Ignore
        }
    }
}