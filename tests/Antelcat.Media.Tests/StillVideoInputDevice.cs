using System.Diagnostics;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;
using EasyPathology.Abstractions.DataTypes;
using OpenCvSharp;

namespace Antelcat.Media.Tests;

internal class StillVideoInputDevice(Mat image, Fraction frameRate) : VideoInputDevice
{
    public override bool IsReady => true;
    public override Fraction FrameRate { get; } = frameRate;
    public override int FrameWidth { get; } = image.Width;
    public override int FrameHeight { get; } = image.Height;
    public override VideoFrameFormat OriginalFormat => VideoFrameFormat.RGB24;

    public StillVideoInputDevice(string imageFilePath, Fraction frameRate) :
        this(Cv2.ImRead(imageFilePath), frameRate)
    {
    }

    protected override async void RunLoop(CancellationToken cancellationToken)
    {
        CurrentState = State.Running;
        var sw = Stopwatch.StartNew();
        while (!cancellationToken.IsCancellationRequested)
        {
            var startTime = sw.Elapsed;
            var mat = new Mat();
            image.CopyTo(mat);
            var frame = new OpenCvMatVideoFrame(mat, VideoFrameFormat.RGB24) as RawVideoFrame;
            if (Modifier != null)
            {
                frame = Modifier.ModifyFrame(this, frame, cancellationToken);
            }
            Encoder?.EncodeFrame(this, frame);
            await Task.Delay((int)(1000 / FrameRate.ToDouble()) - (int)sw.ElapsedMilliseconds + (int)startTime.TotalMilliseconds, cancellationToken);
        }
    }
}