using System.Linq;
using System.Threading;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;

namespace Antelcat.Media.Modifiers;

public class AggregateVideoModifier(params IVideoModifier[] modifiers) : IVideoModifier
{
    public VideoFrameFormat TargetFormat
    {
        get
        {
            var i = modifiers.Length - 1;
            var format = modifiers[i].TargetFormat;
            while (format == VideoFrameFormat.Unset && i > 0)
            {
                i--;
                format = modifiers[i].TargetFormat;
            }

            return format;
        }
    }

    public void Open(VideoInputDevice device, VideoFrameFormat srcFormat)
    {
        foreach (var modifier in modifiers)
        {
            modifier.Open(device, srcFormat);
            if (modifier.TargetFormat != VideoFrameFormat.Unset)
            {
                srcFormat = modifier.TargetFormat;
            }
        }
    }

    public RawVideoFrame ModifyFrame(VideoInputDevice device, RawVideoFrame frame, CancellationToken cancellationToken)
    {
        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var modifier in modifiers)
        {
            frame = modifier.ModifyFrame(device, frame, cancellationToken);
        }

        return frame;
    }

    public void Close(VideoInputDevice device)
    {
        foreach (var modifier in modifiers)
        {
            modifier.Close(device);
        }
    }
}

public static class AggregateVideoModifierExtension
{
    public static IVideoModifier Aggregate(this IVideoModifier? modifier, params IVideoModifier[] modifiers)
    {
        if (modifier == null)
        {
            return new AggregateVideoModifier(modifiers);
        }

        return new AggregateVideoModifier(new[] { modifier }.Concat(modifiers).ToArray());
    }
}