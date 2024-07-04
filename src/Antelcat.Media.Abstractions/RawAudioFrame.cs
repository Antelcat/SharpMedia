using System;

namespace Antelcat.Media.Abstractions;

public class RawAudioFrame : RawFrame<AudioFrameFormat>
{
    public int SampleCount { get; set; }

    public RawAudioFrame(int sampleCount, int length, AudioFrameFormat format) : base(length, format)
    {
        SampleCount = sampleCount;
    }

    public RawAudioFrame(int sampleCount, IntPtr data, int length, AudioFrameFormat format) : base(data, length, format)
    {
        SampleCount = sampleCount;
    }
}