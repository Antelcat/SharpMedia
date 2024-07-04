using System.Diagnostics;
using System.IO;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;
using Antelcat.Media.Abstractions.Extensions;
using Antelcat.Media.Encoders.Audio;
using Antelcat.Media.Encoders.Video;
using Antelcat.Media.Extensions;
using Antelcat.Media.Modifiers;
using Antelcat.Media.Muxers;
using Antelcat.Media.Windows.Encoders.Audio;
using Antelcat.Media.Windows.Encoders.Video;
using Antelcat.Media.Windows.Muxers;
using OpenCvSharp;
using Sdcb.FFmpeg.Common;

namespace Antelcat.Media.Windows.Tests;

public class DeviceTests
{
    private readonly IInputDeviceManager deviceManagement = new InputDeviceManager();

    [SetUp]
    public void Setup()
    {
        Trace.Listeners.Add(new ConsoleTraceListener());
    }

    [Test]
    public void TestGetDevices()
    {
        var management = new InputDeviceManager();

        Trace.WriteLine("摄像头：");
        foreach (var camera in management.GetCameras())
        {
            Trace.WriteLine(camera.FriendlyName);
        }

        Trace.WriteLine("\n麦克风：");
        foreach (var microphone in management.GetMicrophones())
        {
            Trace.WriteLine(microphone.FriendlyName);
        }

        Trace.WriteLine("\n默认麦克风：");
        var defaultMicrophone = management.GetDefaultMicrophone();
        if (defaultMicrophone != null)
        {
            Trace.WriteLine(defaultMicrophone.FriendlyName);
        }
    }

    [Test]
    public async Task TestFFmpegMuxerNoAudio()
    {
        var videoDeviceInfos = deviceManagement.GetCameras().ToArray();
        if (videoDeviceInfos.Length == 0)
        {
            Assert.Fail("No video recording device found.");
            return;
        }

        Trace.WriteLine("视频设备：");
        foreach (var device in videoDeviceInfos)
        {
            Trace.WriteLine(device.FriendlyName);
        }

        var preference = new VideoInputDevice.CreatePreference(640, 480, 30, VideoFrameFormat.NV12);
        var camera = deviceManagement.CreateCamera(videoDeviceInfos[^1], preference);
        if (camera == null)
        {
            Assert.Fail("No video recording device meets CreatePreference.");
            return;
        }

        BindErrorHandler(camera);
        camera.Modifier = new AggregateVideoModifier(
            new ConvertColorSpaceModifier(VideoFrameFormat.RGBA32),
            new OpenCvShowModifier());
        camera.Encoder = new NvCodecVideoEncoder(new NvCodecVideoEncoder.VbrEncodeOptions { MaxBitRate = 1_000_000 });

        await using var fs = new FileStream(@".\ffmpeg_video_only.mp4", FileMode.Create);
        var muxer = new FFmpegMuxer();
        muxer.AddVideoEncoder(camera.Encoder);
        muxer.Open(fs, "mp4");

        await camera.OpenAsync();
        camera.Start();
        await Task.Delay(20000);
        await camera.CloseAsync();
    }

    [Test]
    public async Task TestFFmpegMuxerNoAudioWithMJpg()
    {
        var videoDeviceInfos = deviceManagement.GetCameras().ToArray();
        if (videoDeviceInfos.Length == 0)
        {
            Assert.Fail("No video recording device found.");
            return;
        }

        Trace.WriteLine("视频设备：");
        foreach (var device in videoDeviceInfos)
        {
            Trace.WriteLine(device.FriendlyName);
        }

        var preference = new VideoInputDevice.CreatePreference(1920, 1080, 30, VideoFrameFormat.MJPG);
        var camera = deviceManagement.CreateCamera(videoDeviceInfos[^1], preference);
        if (camera == null)
        {
            Assert.Fail("No video recording device meets CreatePreference.");
            return;
        }

        BindErrorHandler(camera);
        camera.Modifier = new AggregateVideoModifier(
            new ConvertColorSpaceModifier(VideoFrameFormat.RGBA32),
            new OpenCvShowModifier());

        camera.Encoder = new NvCodecVideoEncoder(new NvCodecVideoEncoder.CbrEncodeOptions { AverageBitRate = 2_000_000 });

        await using var fs = new FileStream(@".\ffmpeg_video_only_mjpg.mp4", FileMode.Create);
        //var muxer = FFmpegMuxer.Create(camera);
        //await muxer.OpenAsync(fs);
        //muxer.Start();
        //await Task.Delay(5000);
        //await muxer.CloseAsync();
    }

    [Test]
    public async Task TestFFmpegAudioMuxer()
    {
        var defaultMicrophone = deviceManagement.GetDefaultMicrophone();
        if (defaultMicrophone == null)
        {
            Assert.Fail("No audio recording device found.");
            return;
        }

        Trace.WriteLine("使用默认：" + defaultMicrophone.FriendlyName);

        var microphone = deviceManagement.CreateMicrophone(defaultMicrophone,
            new AudioFrameFormat(44100, 16, 1)).NotNull();
        BindErrorHandler(microphone);

        microphone.Modifier = new AudioResampleModifier(
            new AudioFrameFormat(microphone.Format.SampleRate, 16, 1)
            {
                IsFloat = false,
                IsPlanar = true
            });

        microphone.Encoder = new FFmpegAudioEncoder
        {
            Format = EncodedAudioFormat.Mp3,
            Bitrate = 128000
        };


        await using var fs = new FileStream(@".\ffmpeg.mp3", FileMode.Create);
        var muxer = new FFmpegMuxer();
        muxer.AddAudioEncoder(microphone.Encoder);
        muxer.Open(fs, "mp3");

        await microphone.OpenAsync();
        microphone.Start();
        await Task.Delay(5000);
        await microphone.CloseAsync();
    }

    [Test]
    public async Task TestFFmpegMuxer()
    {
        var videoDeviceInfos = deviceManagement.GetCameras().ToArray();
        if (videoDeviceInfos.Length == 0)
        {
            Assert.Fail("No video recording device found.");
            return;
        }

        Trace.WriteLine("视频设备：");
        foreach (var device in videoDeviceInfos)
        {
            Trace.WriteLine(device.FriendlyName);
        }

        var camera = deviceManagement.CreateCamera(videoDeviceInfos[^1], new VideoInputDevice.CreatePreference(1280, 720, 24, VideoFrameFormat.NV12));
        if (camera == null)
        {
            Assert.Fail("No video recording device meets CreatePreference.");
            return;
        }

        BindErrorHandler(camera);
        camera.Modifier = new AggregateVideoModifier(
            new ConvertColorSpaceModifier(VideoFrameFormat.RGBA32),
            new OpenCvShowModifier());
        camera.Encoder = new NvCodecVideoEncoder(new NvCodecVideoEncoder.CbrEncodeOptions { AverageBitRate = 1_000_000 });

        var defaultMicrophone = deviceManagement.GetDefaultMicrophone();
        if (defaultMicrophone == null)
        {
            Assert.Fail("No audio recording device found.");
            return;
        }

        Trace.WriteLine("使用默认：" + defaultMicrophone.FriendlyName);

        var microphone = deviceManagement.CreateMicrophone(defaultMicrophone, new AudioFrameFormat(44100, 32, 1)).NotNull();
        microphone.Encoder = new FFmpegAudioEncoder
        {
            Format = EncodedAudioFormat.Aac,
            Bitrate = 128000
        };
        BindErrorHandler(microphone);

        await using var fs = new FileStream(@".\ffmpeg.mp4", FileMode.Create);
        var muxer = new FFmpegMuxer();
        muxer.AddAudioEncoder(microphone.Encoder);
        muxer.AddVideoEncoder(camera.Encoder);
        muxer.Open(fs, "mp4");

        await camera.OpenAsync();
        await microphone.OpenAsync();

        camera.Start();
        microphone.Start();
        await Task.Delay(5000);

        await camera.CloseAsync();
        await microphone.CloseAsync();
    }

    [Test]
    public async Task TestMediaFoundationVideoOnlyMuxer()
    {
        var videoDeviceInfos = deviceManagement.GetCameras().ToArray();
        if (videoDeviceInfos.Length == 0)
        {
            Assert.Fail("No video recording device found.");
            return;
        }

        Trace.WriteLine("视频设备：");
        foreach (var device in videoDeviceInfos)
        {
            Trace.WriteLine(device.FriendlyName);
        }

        var camera = deviceManagement.CreateCamera(videoDeviceInfos[^1],
            new VideoInputDevice.CreatePreference(1280, 720, 24, VideoFrameFormat.NV12));
        if (camera == null)
        {
            Assert.Fail("No video recording device meets CreatePreference.");
            return;
        }

        BindErrorHandler(camera);
        camera.Modifier = new OpenCvShowModifier();

        var encoder = new MediaFoundationVideoEncoder
        {
            Format = EncodedVideoFormat.H264
        };
        camera.Encoder = encoder;

        var muxer = new MediaFoundationMuxer();
        muxer.AddVideoEncoder(encoder);
        muxer.Open(@".\FM_no_audio.mp4");

        await camera.OpenAsync();
        camera.Start();
        await Task.Delay(5000);
        await camera.CloseAsync();
    }

    [Test]
    public async Task TestMediaFoundationAudioOnlyMuxer()
    {
        var defaultMicrophone = deviceManagement.GetDefaultMicrophone();
        if (defaultMicrophone == null)
        {
            Assert.Fail("No audio recording device found.");
            return;
        }

        Trace.WriteLine("使用默认：" + defaultMicrophone.FriendlyName);

        var microphone = deviceManagement.CreateMicrophone(defaultMicrophone, new AudioFrameFormat(44100, 16, 1)).NotNull();
        BindErrorHandler(microphone);

        var encoder = new MediaFoundationAudioEncoder
        {
            Format = EncodedAudioFormat.Wma
        };
        microphone.Encoder = encoder;

        var muxer = new MediaFoundationMuxer();
        muxer.AddAudioEncoder(encoder);
        muxer.Open(@".\FM.wma");

        await microphone.OpenAsync();
        microphone.Start();
        await Task.Delay(5000);
        await microphone.CloseAsync();
    }

    [Test]
    public async Task TestFFmpegEncoder()
    {
        var videoDeviceInfos = deviceManagement.GetCameras().ToArray();
        if (videoDeviceInfos.Length == 0)
        {
            Assert.Fail("No video recording device found.");
            return;
        }

        Trace.WriteLine("视频设备：");
        foreach (var device in videoDeviceInfos)
        {
            Trace.WriteLine(device.FriendlyName);
        }

        var preference = new VideoInputDevice.CreatePreference(800, 600, 15, VideoFrameFormat.NV12);
        var camera = deviceManagement.CreateCamera(videoDeviceInfos[0], preference);
        if (camera == null)
        {
            Assert.Fail("No video recording device meets CreatePreference.");
            return;
        }

        BindErrorHandler(camera);
        //camera.Modifier = new CombinedModifier(new ConvertColorSpaceModifier(VideoFrameFormat.RGB32), new OpenCvShowModifier(false));
        camera.Encoder = new FFmpegVideoEncoder
        {
            Format = EncodedVideoFormat.H264
        };

        var fs = new FileStream(@".\FFmpeg.h264", FileMode.Create);
        var i = 0;
        camera.Encoder.FrameEncoded += p =>
        {
            fs.Write(p.AsSpan());
            Trace.WriteLine($"#{++i:0000}");
        };

        await camera.OpenAsync();
        camera.Start();
        await Task.Delay(5000);
        await camera.CloseAsync();
        Assert.That(fs, Has.Length.GreaterThan(1024));
        await fs.DisposeAsync();
    }

    [Test]
    public async Task TestNvCodecEncoder()
    {
        var videoDeviceInfos = deviceManagement.GetCameras().ToArray();
        if (videoDeviceInfos.Length == 0)
        {
            Assert.Fail("No video recording device found.");
            return;
        }

        Trace.WriteLine("视频设备：");
        foreach (var device in videoDeviceInfos)
        {
            Trace.WriteLine(device.FriendlyName);
        }

        var preference = new VideoInputDevice.CreatePreference(800, 600, 15, VideoFrameFormat.NV12);
        var camera = deviceManagement.CreateCamera(videoDeviceInfos[0], preference);
        if (camera == null)
        {
            Assert.Fail("No video recording device meets CreatePreference.");
            return;
        }

        BindErrorHandler(camera);
        camera.Modifier = new AggregateVideoModifier(new ConvertColorSpaceModifier(VideoFrameFormat.RGBA32), new OpenCvShowModifier());
        camera.Encoder = new NvCodecVideoEncoder(new NvCodecVideoEncoder.CbrEncodeOptions{ AverageBitRate = 1_000_000 });

        var fs = new FileStream(@".\NvCodec.h264", FileMode.Create);
        var i = 0;
        camera.Encoder.FrameEncoded += p =>
        {
            fs.Write(p.AsSpan());
            Trace.WriteLine($"#{++i:0000}");
        };

        await camera.OpenAsync();
        camera.Start();
        await Task.Delay(5000);
        await camera.CloseAsync();
        Assert.That(fs, Has.Length.GreaterThan(1024));
        await fs.DisposeAsync();
    }

    [Test]
    public async Task TestAacEncoder()
    {
        var ms = deviceManagement.GetMicrophones().ToArray();
        if (ms.Length == 0) { Assert.Fail("None found"); }
        var info = ms[0];
        var microphone = deviceManagement.CreateMicrophone(info, new AudioFrameFormat(44100, 16, 2)).NotNull();
        BindErrorHandler(microphone);
        microphone.Modifier = new AudioResampleModifier(new AudioFrameFormat(44100, 32, 2)
        {
            IsFloat = true, IsPlanar = true
        });
        var fs = new FileStream(@".\ffmpeg.aac", FileMode.Create);
        microphone.Encoder = new FFmpegAudioEncoder
        {
            Format = EncodedAudioFormat.Aac,
            Bitrate = 256000
        };
        microphone.Encoder.FrameEncoded += (p) => fs.Write(p.AsSpan());
        await microphone.OpenAsync();
        microphone.Start();
        await TimeSpan.FromMilliseconds(5000);
        await microphone.CloseAsync();
        Assert.That(fs, Has.Length.GreaterThan(1024));
        await fs.DisposeAsync();
    }

    [Test]
    public async Task TestVirtualBackground()
    {
        Trace.Listeners.Add(new ConsoleTraceListener());

        if (!File.Exists(@".\Resources\Images\OfficeBackground.png"))
        {
            Assert.Fail("无法找到虚拟背景图片");
            return;
        }
        var background = Cv2.ImRead(@".\Resources\Images\OfficeBackground.png");

        var videoDeviceInfos = deviceManagement.GetCameras().ToArray();
        if (videoDeviceInfos.Length == 0)
        {
            Assert.Fail("No video recording device found.");
            return;
        }

        var preference = new VideoInputDevice.CreatePreference(640, 480, 30, VideoFrameFormat.NV12);
        var camera = deviceManagement.CreateCamera(videoDeviceInfos[^1], preference);
        if (camera == null)
        {
            Assert.Fail("No video recording device meets CreatePreference.");
            return;
        }

        BindErrorHandler(camera);
        camera.Modifier = new AggregateVideoModifier(new ConvertColorSpaceModifier(VideoFrameFormat.RGB24),
            new VirtualBackgroundModifier
            {
                Background = background
            },
            new OpenCvShowModifier()
        );

        await camera.OpenAsync();
        camera.Start();
        await Task.Delay(10000);
        await camera.CloseAsync();
    }

    [Test]
    public async Task TestStillVirtualBackground()
    {
        if (!File.Exists(@".\Resources\Images\OfficeBackground.png"))
        {
            Assert.Fail("无法找到虚拟背景图片");
            return;
        }

        if (!File.Exists(@".\Resources\Images\iKun.png"))
        {
            Assert.Fail("无法找到虚拟背景图片");
            return;
        }

        var background = Cv2.ImRead(@".\Resources\Images\OfficeBackground.png");

        var device = new StillVideoInputDevice(@".\Resources\Images\iKun.png", 1)
        {
            Modifier = new AggregateVideoModifier(
                new ConvertColorSpaceModifier(VideoFrameFormat.RGB24),
                new VirtualBackgroundModifier
                {
                    Background = background,
                    IsEnabled = true
                },
                new OpenCvShowModifier()
            )
        };

        await device.OpenAsync();
        device.Start();
        await Task.Delay(5000);
        await device.CloseAsync();
    }

    [Test]
    public async Task TestWavAudioModifier()
    {
        var defaultMicrophone = deviceManagement.GetDefaultMicrophone();
        if (defaultMicrophone == null)
        {
            Assert.Fail("No audio recording device found.");
            return;
        }

        Trace.WriteLine("使用默认：" + defaultMicrophone.FriendlyName);

        var device = deviceManagement.CreateMicrophone(defaultMicrophone, new AudioFrameFormat(44100, 16, 1)).NotNull();
        BindErrorHandler(device);

        var volumeMeter = new VolumeMeteringModifier();
        device.Modifier = volumeMeter;

        await device.OpenAsync();

        var timer = new Timer(_ =>
                Trace.WriteLine("Volume: " + volumeMeter.Volume),
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(0.2));

        device.Start();
        await Task.Delay(5000);
        device.Pause();
        Trace.WriteLine("暂停");
        await Task.Delay(5000);
        Trace.WriteLine("继续");
        device.Start();
        await Task.Delay(5000);
        await timer.DisposeAsync();
        await device.CloseAsync();
    }

    [Test]
    public async Task TestAudioDecoder()
    {
        var device = deviceManagement.CreateMicrophone(deviceManagement.GetDefaultMicrophone()!,
            new AudioFrameFormat(44100, 16, 1))!;
        device.Encoder = new FFmpegAudioEncoder
        {
            Format = EncodedAudioFormat.G722
        };
        var stream = new FileStream("./G722.g722", FileMode.Create);
        device.Encoder.FrameEncoded += packet =>
        {
            stream.Write(packet.AsSpan());
        };
        await device.OpenAsync();
        device.Start();
        await Task.Delay(5000);
        await device.CloseAsync();
        await stream.DisposeAsync();
    }

    [Test]
    public async Task TestDirectSoundAudioInputDevice()
    {
        var info = deviceManagement.GetDefaultMicrophone().NotNull();
        var microphone = deviceManagement.CreateMicrophone(
            info,
            new AudioFrameFormat(44100, 16, 1)).NotNull();

        Trace.WriteLine(info.FriendlyName);

        await using var fs = File.OpenWrite("./44100_16_1.pcm");
        var writer = new DirectWriteToStreamModifier(fs);
        microphone.Modifier = writer;

        await microphone.OpenAsync();
        microphone.Start();
        await Task.Delay(5000);
        await microphone.CloseAsync();
    }

    private static void BindErrorHandler(InputDevice device)
    {
        device.ErrorOccurred += (inputDevice, exception) =>
        {
            if (exception is FFmpegException ffmpegException)
            {
                exception = ffmpegException.ToDetailed();
            }

            Assert.Fail($"Device {inputDevice} raised a RecordError\n{exception}");
        };
    }
}