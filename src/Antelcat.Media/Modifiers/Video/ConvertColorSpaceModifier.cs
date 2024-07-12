using System.Threading;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;
using Antelcat.Media.Abstractions.Interfaces;
using Antelcat.Media.Internal;
using OpenCvSharp;

namespace Antelcat.Media.Modifiers;

/// <summary>
/// 转换色彩空间
/// </summary>
public class ConvertColorSpaceModifier(VideoFrameFormat targetFormat) : IVideoModifier
{
    public VideoFrameFormat TargetFormat { get; } = targetFormat;

    public void Open(VideoInputDevice device, VideoFrameFormat srcFormat) { }

    public RawVideoFrame ModifyFrame(VideoInputDevice device, RawVideoFrame frame, CancellationToken cancellationToken)
    {
        if (frame.Format == TargetFormat)
        {
            return frame;
        }

        // 这个构造函数不会拷贝内存
        var srcMat = frame.AsMat();
        var dstMat = new Mat();

        Cv2.CvtColor(srcMat, dstMat, OpenCvExtension.FrameFormat2ColorConversionCodes(frame.Format, TargetFormat));
        frame.Dispose();

        return new OpenCvMatVideoFrame(dstMat, TargetFormat)
        {
            Time = frame.Time,
            Duration = frame.Duration
        };
    }

    public void Close(VideoInputDevice device) { }
}