using System.Threading;
using Antelcat.Media.Abstractions;

namespace Antelcat.Media;

/// <summary>
/// 解码作为音频输入
/// </summary>
public class DecodedAudioInputDevice : AudioInputDevice
{
    private readonly IAudioDecoder decoder;

    /// <summary>
    /// 解码作为音频输入
    /// </summary>
    public DecodedAudioInputDevice(IAudioDecoder decoder)
    {
        this.decoder = decoder;
        decoder.FrameDecoded += decodedFrame => ProcessFrame(decodedFrame, default);
    }

    public override bool IsReady => true;

    public override AudioFrameFormat OriginalFormat => decoder.FrameFormat;

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
}