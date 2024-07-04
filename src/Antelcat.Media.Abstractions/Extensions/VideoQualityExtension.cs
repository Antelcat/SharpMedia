using Antelcat.Media.Abstractions.Enums;

namespace Antelcat.Media.Abstractions.Extensions;

public static class VideoQualityExtension
{
    public static VideoInputDevice.CreatePreference ToCreatePreference(
        this VideoQuality quality,
        VideoFrameFormat videoFrameFormat = VideoFrameFormat.Unset)
    {
        return quality switch
        {
            VideoQuality.Low => new VideoInputDevice.CreatePreference(640, 480, 15, videoFrameFormat),
            VideoQuality.Medium => new VideoInputDevice.CreatePreference(1280, 720, 24, videoFrameFormat),
            VideoQuality.High => new VideoInputDevice.CreatePreference(1920, 1080, 30, videoFrameFormat),
            _ => new VideoInputDevice.CreatePreference()
        };
    }

    /// <summary>
    /// 获取最佳码率 (kbps)
    /// </summary>
    /// <param name="quality"></param>
    /// <returns></returns>
    public static int GetOptimalBitrate(this VideoQuality quality)
    {
        return quality switch
        {
            VideoQuality.Low => 800000,
            VideoQuality.Medium => 1500000,
            VideoQuality.High => 4000000,
            _ => 4000
        };
    }
}