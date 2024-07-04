namespace Antelcat.Media.Abstractions;

/// <summary>
/// 通过麦克风录音
/// </summary>
public abstract class AudioInputDevice : InputDevice
{
    /// <summary>
    /// 原始的格式，由于可能经过了<see cref="Modifier"/>的转换，所以输出格式不等于原始格式
    /// </summary>
    public abstract AudioFrameFormat OriginalFormat { get; }

    public IAudioEncoder? Encoder { get; set; }

    public IAudioModifier? Modifier { get; set; }

    public AudioFrameFormat Format
    {
        get
        {
            if (Modifier == null)
            {
                return OriginalFormat;
            }

            return Modifier?.TargetFormat ?? OriginalFormat;
        }
    }

    public override long AverageBytesPerSecond => Format.AverageBytesPerSecond;

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
            if (frame is not RawAudioFrame audioFrame)
            {
                throw new InvalidOperationException("Frame is not audio frame.");
            }

            if (Modifier != null)
            {
                audioFrame = Modifier.ModifyFrame(this, audioFrame, token);
            }

            Encoder?.EncodeFrame(this, audioFrame);
        }
        finally
        {
            frame.Dispose();
        }
    }
}