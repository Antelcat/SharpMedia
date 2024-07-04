namespace Antelcat.Media.Abstractions.Extensions;

public static class AudioQualityExtension
{
    public static AudioFrameFormat ToCreatePreference(this AudioQuality quality)
    {
        return quality switch
        {
            AudioQuality.Low => new AudioFrameFormat(44100, 8, 1),
            AudioQuality.Medium => new AudioFrameFormat(44100, 16, 1),
            AudioQuality.High => new AudioFrameFormat(48000, 32, 2),
            _ => new AudioFrameFormat(48000, 32, 2)
        };
    }

    /// <summary>
    /// 获取最佳码率 (kbps)
    /// </summary>
    /// <param name="quality"></param>
    /// <returns></returns>
    public static int GetOptimalBitrate(this AudioQuality quality)
    {
        return quality switch
        {
            AudioQuality.Low => 64000,
            AudioQuality.Medium => 128000,
            AudioQuality.High => 256000,
            _ => 256000
        };
    }

    public static byte[] Resample(this byte[] pcm, int inRate, int outRate)
    {
        if (inRate == outRate)
        {
            return pcm;
        }

        return inRate switch
        {
            8000 when outRate == 16000 => pcm.SelectMany(x => new[] { x, x }).ToArray(),
            8000 when outRate == 48000 => pcm.SelectMany(x => new[] { x, x, x, x, x, x }).ToArray(),
            16000 when outRate == 8000 => pcm.Where((_, i) => i % 2 == 0).ToArray(),
            16000 when outRate == 48000 => pcm.SelectMany(x => new[] { x, x, x }).ToArray(),
            _ => throw new ApplicationException(
                $"Sorry don't know how to re-sample PCM from {inRate} to {outRate}.")
        };
    }
}