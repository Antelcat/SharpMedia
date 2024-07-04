using System.Threading;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;

namespace Antelcat.Media.Modifiers;

public class SwitchableVideoModifier<T>(T modifier) : IVideoModifier where T : IVideoModifier
{
    public bool IsEnabled { get; set; }

    public T Modifier { get; } = modifier;

    public VideoFrameFormat TargetFormat => Modifier.TargetFormat;

    public void Open(VideoInputDevice device, VideoFrameFormat srcFormat)
    {
        if (IsEnabled)
        {
            Modifier.Open(device, srcFormat);
        }
    }

    public RawVideoFrame ModifyFrame(VideoInputDevice device, RawVideoFrame frame, CancellationToken cancellationToken)
    {
        if (IsEnabled)
        {
            return Modifier.ModifyFrame(device, frame, cancellationToken);
        }

        return frame;
    }

    public void Close(VideoInputDevice device)
    {
        if (IsEnabled)
        {
            Modifier.Close(device);
        }
    }
}