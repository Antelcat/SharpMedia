using Antelcat.Media.Abstractions.Enums;
using Antelcat.Media.Abstractions.Extensions;
using Antelcat.Media.Abstractions.Interfaces;

namespace Antelcat.Media.Abstractions;

/// <summary>
/// 视频输入设备
/// </summary>
public abstract class VideoInputDevice : InputDevice
{
    public IVideoEncoder? Encoder { get; set; }

    public IVideoModifier? Modifier { get; set; }

    public abstract Fraction FrameRate { get; }

    public abstract int FrameWidth { get; }

    public abstract int FrameHeight { get; }

    public Fraction BytesPerPixel => Format.GetBytesPerPixel();

    public VideoFrameFormat Format
    {
        get
        {
            if (Modifier == null)
            {
                return OriginalFormat;
            }

            var format = Modifier.TargetFormat;
            return format == VideoFrameFormat.Unset ? OriginalFormat : format;
        }
    }

    /// <summary>
    /// 原始的格式，由于可能经过了<see cref="Modifier"/>的转换，所以输出格式不等于原始格式
    /// </summary>
    public abstract VideoFrameFormat OriginalFormat { get; }

    /// <summary>
    /// 一帧原始画面有多少字节
    /// </summary>
    public long FrameBytesLength => FrameWidth * FrameHeight * BytesPerPixel.Number / BytesPerPixel.Denominator;

    public override long AverageBytesPerSecond => FrameBytesLength * BytesPerPixel.Number / BytesPerPixel.Denominator;

    protected override void Opening()
    {
        Encoder?.Open(this);
        Modifier?.Open(this, OriginalFormat);
    }

    protected override void Closing()
    {
        Modifier?.Close(this);
        Encoder?.Close(this);
    }

    protected override void ProcessFrame(RawFrame frame, CancellationToken token)
    {
        try
        {
            if (frame is not RawVideoFrame videoFrame)
            {
                throw new InvalidOperationException("Frame is not video frame.");
            }

            if (Modifier != null)
            {
                videoFrame = Modifier.ModifyFrame(this, videoFrame, token);
            }

            Encoder?.EncodeFrame(this, videoFrame);
        }
        finally
        {
            frame.Dispose();
        }
    }

    public record CreatePreference(
        uint DesiredFrameWidth = 0,
        uint DesiredFrameHeight = 0,
        Fraction DesiredFrameRate = default,
        VideoFrameFormat DesiredFormat = VideoFrameFormat.Unset);
}