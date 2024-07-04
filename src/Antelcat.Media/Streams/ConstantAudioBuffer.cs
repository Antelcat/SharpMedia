using System;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Internal;

namespace Antelcat.Media.Streams;

public class ConstantAudioBuffer(AudioFrameFormat format, TimeSpan bufferSize)
    : ConstantMemoryStream((ulong)(format.SampleRate * format.BitsPerSample * bufferSize.TotalSeconds))
{
    /// <summary>
    /// 每次调用Read都会更新，记录了一共读取了多少sample。单位为音频的采样数
    /// </summary>
    public long SamplesReadCount { get; private set; }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = base.Read(buffer, offset, count);
        SamplesReadCount += bytesRead * 8 / format.BitsPerSample / format.ChannelCount;
        return bytesRead;
    }

    public override int Read(IntPtr buffer, int offset, int count)
    {
        var bytesRead = base.Read(buffer, offset, count);
        SamplesReadCount += bytesRead * 8 / format.BitsPerSample / format.ChannelCount;
        return bytesRead;
    }
}