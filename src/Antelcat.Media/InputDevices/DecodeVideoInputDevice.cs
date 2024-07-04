using System.Threading;
using EasyPathology.Abstractions.DataTypes;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;

namespace Antelcat.Media;

/// <summary>
/// 解码作为视频输入
/// </summary>
public class DecodeVideoInputDevice : VideoInputDevice
{
    public override bool IsReady => true;

    public override Fraction FrameRate => frameRate;

    public override int FrameWidth => frameWidth;

    public override int FrameHeight => frameHeight;

    private Fraction frameRate;
    private int frameWidth, frameHeight;
    private readonly IVideoDecoder decoder;

    public override VideoFrameFormat OriginalFormat => decoder.FrameFormat;
    
    /// <summary>
    /// 解码作为视频输入
    /// </summary>
    public DecodeVideoInputDevice(IVideoDecoder decoder)
    {
        this.decoder = decoder;
        decoder.FrameDecoded += decodedFrame => ProcessFrame(decodedFrame, default);
    }

    protected override void RunLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            waitHandle.WaitOne();
            CurrentState = State.Running;
            var result = decoder.Decode(cancellationToken);
            if (result == DecodeResult.Cancelled) return;
        }
    }

    protected override void Closing()
    {
        base.Closing();
        frameWidth = frameHeight = 0;
    }
}