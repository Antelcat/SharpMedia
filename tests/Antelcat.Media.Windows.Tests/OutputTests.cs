using System.Diagnostics;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;
using Antelcat.Media.Abstractions.Extensions;
using Antelcat.Media.Decoders;
using Antelcat.Media.Extensions;
using Antelcat.Media.Modifiers;
using Antelcat.Media.Windows.Modifiers.Audio;
using OpenCvSharp;
using Sdcb.FFmpeg.Common;
using Sdcb.FFmpeg.Raw;
using LogLevel = Sdcb.FFmpeg.Raw.LogLevel;

namespace Antelcat.Media.Windows.Tests;

public class OutputTests
{
    private readonly IInputDeviceManager deviceManager = new InputDeviceManager();

    [SetUp]
    public void Setup()
    {
        Trace.Listeners.Add(new ConsoleTraceListener());
    }

    [Test]
    public void TestGetSpeaker()
    {
        Trace.WriteLine("Speakers:");
        Trace.WriteLine(string.Join("\n", deviceManager.GetSpeakers()));

        Trace.WriteLine("Default Speaker:");
        Trace.WriteLine(deviceManager.GetDefaultSpeaker());
    }

    [Test]
    public async Task TestWasapiOutputAudioModifier()
    {
        var speakerInfo = deviceManager.GetDefaultSpeaker().NotNull();
        Trace.WriteLine($"Using speaker: {speakerInfo}");
        var microphoneInfo = deviceManager.GetDefaultMicrophone().NotNull();
        Trace.WriteLine($"Using microphone: {microphoneInfo}");

        var modifier = new WasapiOutputAudioModifier(speakerInfo)
        {
            IsPlaying = true
        };
        var microphone = deviceManager.CreateMicrophone(microphoneInfo,
            new AudioFrameFormat(
                44100,
                16,
                1)).NotNull();
        microphone.Modifier = modifier;

        await microphone.OpenAsync();
        microphone.Start();
        await Task.Delay(5000);
        await microphone.CloseAsync();
    }

    [Test]
    [TestCase("./Resources/Audios/Hanser - 恋爱吧 魔法少女 hanser.mp3")]
    [TestCase("./Resources/Audios/鸟爷ToriSama,洛天依 - 一半一半.wav")]
    public async Task TestPlaySpeedChangedAudioFile(string path)
    {
        var speakerInfo = deviceManager.GetDefaultSpeaker().NotNull();
        Trace.WriteLine($"Using speaker: {speakerInfo}");

        var output = new WasapiOutputAudioModifier(speakerInfo)
        {
            IsPlaying = true,
            Volume = 0.3f
        };
        var changer = new AudioSpeedModifier();
        var context = new FFmpegUrlDecoderContext(path, AVMediaType.Audio);
        var format = new AudioFrameFormat(48000, 16, 1);
        var decoder = new FFmpegAudioDecoder(context, format);
        var input = new DecodedAudioInputDevice(decoder)
        {
            Modifier = new AggregateAudioModifier(new AudioResampleModifier(format with
                {
                    BitsPerSample = 32, IsFloat = true
                }),
                changer,
                new AudioResampleModifier(format),
                output)
        };
        BindErrorHandler(input);

        await input.OpenAsync();
        input.Start();
        context.Seek(TimeSpan.FromSeconds(3));
        output.Flush();

        // 速度变化，0.5~2，sin函数，10s一个周期，1s改变一次
        for (var i = 0; i < 100; i++)
        {
            var speed = 1 + Math.Sin(i * Math.PI / 10) * 0.5;
            changer.Tempo = speed;
            await Task.Delay(500);
        }

        await input.CloseAsync();
    }

    [Test]
    [TestCase("./Resources/Audios/Hanser - 恋爱吧 魔法少女 hanser.mp3")]
    [TestCase("./Resources/Audios/鸟爷ToriSama,洛天依 - 一半一半.wav")]
    public async Task TestPlayAudioFile(string path)
    {
        var speakerInfo = deviceManager.GetDefaultSpeaker().NotNull();
        Trace.WriteLine($"Using speaker: {speakerInfo}");

        var output = new WasapiOutputAudioModifier(speakerInfo)
        {
            IsPlaying = true
        };
        var context = new FFmpegUrlDecoderContext(path, AVMediaType.Audio);
        var decoder = new FFmpegAudioDecoder(context, new AudioFrameFormat(48000, 32, 1));
        var input = new DecodedAudioInputDevice(decoder)
        {
            Modifier = new AggregateAudioModifier(new AudioResampleModifier(new AudioFrameFormat(48000, 16, 1)), output)
        };
        BindErrorHandler(input);

        await input.OpenAsync();
        input.Start();
        await Task.Delay(5000);
        Trace.WriteLine("即将跳转到中间");
        context.Seek(context.Duration / 2);
        output.Flush();
        await Task.Delay(5000);
        Trace.WriteLine("即将跳转到末尾");
        context.Seek(context.Duration - TimeSpan.FromSeconds(3));
        output.Flush();
        await Task.Delay(5000);
        // 现在已经到了EOF
        Trace.WriteLine("即将跳转到开头");
        context.Seek(TimeSpan.FromSeconds(3));
        output.Flush();
        // 但是还应该支持Seek
        await Task.Delay(5000);
        await input.CloseAsync();
    }

    [Test]
    public async Task TestPlayMutedVideoFile()
    {
        var stopwatch = new Stopwatch();

        var source = new FFmpegUrlDecoderContext("./Resources/Videos/谷歌翻译20遍【如来来没来】配音.mp4", AVMediaType.Video);
        var decoder = new FFmpegVideoDecoder(source, VideoFrameFormat.RGB24);
        var input = new DecodeVideoInputDevice(decoder)
        {
            Modifier = new SynchronousPlayVideoModifier(
                () => stopwatch.Elapsed,
                new OpenCvShowModifier())
        };
        BindErrorHandler(input);

        await input.OpenAsync();
        input.Start();
        stopwatch.Start();
        await Task.Delay(30000);
        await input.CloseAsync();
    }

    [Test]
    public async Task TestPlayVideoFile()
    {
        var speakerInfo = deviceManager.GetDefaultSpeaker().NotNull();
        Trace.WriteLine($"Using speaker: {speakerInfo}");

        var audioOutput = new WasapiOutputAudioModifier(speakerInfo)
        {
            IsPlaying = true
        };
        var audioContext = new FFmpegUrlDecoderContext("./Resources/Videos/谷歌翻译20遍【如来来没来】配音.mp4", AVMediaType.Audio);
        var audioDecoder = new FFmpegAudioDecoder(audioContext, new AudioFrameFormat(48000, 16, 1));
        var audio = new DecodedAudioInputDevice(audioDecoder)
        {
            Modifier = audioOutput
        };
        BindErrorHandler(audio);

        var mat = Cv2.ImRead(
            @"G:\Source\CSharp\EasyPathology\Desktop\Recording\Resources\Images\OfficeBackground.jpg");
        var videoContext = new FFmpegUrlDecoderContext("./Resources/Videos/谷歌翻译20遍【如来来没来】配音.mp4", AVMediaType.Video);
        var videoDecoder = new FFmpegVideoDecoder(videoContext, VideoFrameFormat.RGB24);
        var video = new DecodeVideoInputDevice(videoDecoder)
        {
            Modifier = new SynchronousPlayVideoModifier(
                () => audioDecoder.CurrentTime, // 视频根据音频同步
                new AggregateVideoModifier(
                    new MirrorVideoModifier
                    {
                        VerticalMirrored = true,
                        HorizontalMirrored = true
                    },
                    new OpenCvShowModifier()))
        };
        BindErrorHandler(video);

        await video.OpenAsync();
        video.Start();
        await audio.OpenAsync();
        audio.Start();
        await Task.Delay(5000);
        Trace.WriteLine("即将跳转到中间");
        Seek(videoContext.Duration / 2);
        await Task.Delay(5000);
        Trace.WriteLine("即将跳转到末尾");
        Seek(videoContext.Duration - TimeSpan.FromSeconds(3));
        await Task.Delay(5000);
        // 现在已经到了EOF
        Trace.WriteLine("即将跳转到开头");
        Seek(TimeSpan.FromSeconds(3));
        // 但是还应该支持Seek
        await Task.Delay(5000);

        await video.CloseAsync();
        await audio.CloseAsync();

        void Seek(TimeSpan position)
        {
            audioContext.Seek(position);
            audioOutput.Flush();
            videoContext.Seek(position);
        }
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