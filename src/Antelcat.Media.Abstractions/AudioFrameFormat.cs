namespace Antelcat.Media.Abstractions;

public record AudioFrameFormat
{
    public int SampleRate { get; set; }

    public int BitsPerSample { get; set; }

    public int ChannelCount { get; set; }

    public int BlockAlign { get; set; }

    /// <summary>
    /// Planar，平铺: AAA BBB CCC
    /// Non-planar (Packed，交错): ABC ABC ABC
    /// </summary>
    public bool IsPlanar { get; set; }

    public bool IsFloat { get; set; }

    public int AverageBytesPerSecond => ChannelCount * BitsPerSample / 8 * SampleRate;

    public AudioFrameFormat() { }

    public AudioFrameFormat(int sampleRate, int bitsPerSample, int channelCount)
    {
        if (bitsPerSample / 8 * 8 != bitsPerSample)
        {
            throw new ArgumentException("bitsPerSample must be a multiple of 8");
        }

        SampleRate = sampleRate;
        BitsPerSample = bitsPerSample;
        ChannelCount = channelCount;
        IsFloat = bitsPerSample >= 32;
    }
}