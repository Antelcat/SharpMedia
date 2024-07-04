using System.IO;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;

namespace Antelcat.Media.Windows.Tests;

public class DirectWriteToStreamModifier(Stream stream) : IVideoModifier, IAudioModifier
{
    public VideoFrameFormat TargetFormat => VideoFrameFormat.Unset;

    AudioFrameFormat? IModifier<AudioInputDevice, AudioFrameFormat, RawAudioFrame>.TargetFormat => null;

    public void Open(AudioInputDevice device, AudioFrameFormat srcFormat) { }

    public RawAudioFrame ModifyFrame(AudioInputDevice device, RawAudioFrame frame, CancellationToken cancellationToken)
    {
        stream.Write(frame.ToArray());
        return frame;
    }

    public void Close(AudioInputDevice device) { }

    public void Open(VideoInputDevice device, VideoFrameFormat srcFormat) { }

    public RawVideoFrame ModifyFrame(VideoInputDevice device, RawVideoFrame frame, CancellationToken cancellationToken)
    {
        stream.Write(frame.ToArray());
        return frame;
    }

    public void Close(VideoInputDevice device) { }
}