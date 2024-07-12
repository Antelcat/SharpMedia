using System.Threading;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;
using Antelcat.Media.Abstractions.Interfaces;
using Antelcat.Media.Internal;
using OpenCvSharp;

namespace Antelcat.Media.Modifiers;

/// <summary>
/// 视频镜像
/// </summary>
public class MirrorVideoModifier : IVideoModifier
{
    public VideoFrameFormat TargetFormat => VideoFrameFormat.Unset;

    public bool HorizontalMirrored { get; set; }

    public bool VerticalMirrored { get; set; }

    public void Open(VideoInputDevice device, VideoFrameFormat srcFormat) { }

    public RawVideoFrame ModifyFrame(VideoInputDevice device, RawVideoFrame frame, CancellationToken cancellationToken)
    {
        if (!HorizontalMirrored && !VerticalMirrored)
        {
            return frame;
        }

        var frameMat = frame.AsMat();

        if (HorizontalMirrored && VerticalMirrored)
        {
            Cv2.Flip(frameMat, frameMat, FlipMode.XY);
        }
        else if (HorizontalMirrored)
        {
            Cv2.Flip(frameMat, frameMat, FlipMode.Y);
        }
        else
        {
            Cv2.Flip(frameMat, frameMat, FlipMode.X);
        }

        return frame;
    }

    public void Close(VideoInputDevice device) { }
}