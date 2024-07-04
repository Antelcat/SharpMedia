using Antelcat.Media.Abstractions.Enums;
using Antelcat.Media.Decoders;
using Sdcb.FFmpeg.Raw;

namespace Antelcat.Media.Tests;

internal class DecoderTests
{
    [Test]
    public async Task TestH264Decoder()
    {
        var source = new FFmpegStreamingDecoderContext(File.OpenRead(@".\Resources\Videos\output.h264"), AVCodecID.H264);
        var decoder = new FFmpegVideoDecoder(source, VideoFrameFormat.RGBA32);
        var device = new DecodeVideoInputDevice(decoder)
        {
            Modifier = new OpenCvShowModifier()
        };
        await device.OpenAsync();
        device.Start();

        await Task.Delay(20000);
        await device.CloseAsync();
    }
}