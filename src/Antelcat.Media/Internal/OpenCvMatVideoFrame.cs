using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;
using OpenCvSharp;

namespace Antelcat.Media.Internal;

internal class OpenCvMatVideoFrame(Mat mat, VideoFrameFormat format) :
    RawVideoFrame(
        mat.Width,
        mat.Height,
        (mat.DataEnd - mat.DataStart).ToInt32(),
        mat.Data,
        format)
{
    public Mat Mat { get; } = mat;

    public override void Dispose()
    {
        base.Dispose();
        Mat.Dispose();
    }
}