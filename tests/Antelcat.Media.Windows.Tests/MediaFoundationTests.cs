using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Extensions;
using SharpDX.MediaFoundation;

namespace Antelcat.Media.Windows.Tests;

[StructLayout(LayoutKind.Sequential)]
public struct WaveFormatEx
{
    public ushort wFormatTag;
    public ushort nChannels;
    public uint nSamplesPerSec;
    public uint nAvgBytesPerSec;
    public ushort nBlockAlign;
    public ushort wBitsPerSample;
    public ushort cbSize;
}

[StructLayout(LayoutKind.Sequential)]
public struct MpegLayer3WaveFormat
{
    public WaveFormatEx WaveFormatEx;
    public ushort wID;
    public uint fdwFlags;
    public ushort nBlockSize;
    public ushort nFramesPerBlock;
    public ushort nCodecDelay;
}

internal class MediaFoundationTests
{
    [SetUp]
    public void SetUp()
    {
        Trace.Listeners.Add(new ConsoleTraceListener());
    }

    [Test]
    public void Mp3SinkWriterTest()
    {
        MediaManager.Startup();

        var manager = new InputDeviceManager();
        var microphoneInfo = manager.GetDefaultMicrophone().NotNull();

        var collector = new DisposeCollector();
        var attributes = collector.Add(new MediaAttributes());
        attributes.Set(CaptureDeviceAttributeKeys.SourceType, CaptureDeviceAttributeKeys.SourceTypeAudioCapture.Guid);
        attributes.Set(CaptureDeviceAttributeKeys.SourceTypeAudcapEndpointId, microphoneInfo.Uid);

        MediaManager.Startup();
        MediaFactory.CreateDeviceSource(attributes, out var mediaSource);
        collector.Add(mediaSource);

        mediaSource.CreatePresentationDescriptor(out var pDesc);
        collector.Add(pDesc);
        pDesc.GetStreamDescriptorByIndex(0, out _, out var sDesc);
        collector.Add(sDesc);

        var handler = collector.Add(sDesc.MediaTypeHandler);
        // Trace.WriteLine("Input media type:");
        // handler.CurrentMediaType.Dump();

        var microphone = new MediaFoundationMediaSourceWrapper(microphoneInfo.Uid, handler.CurrentMediaType, MediaDeviceType.Microphone);
        microphone.Open();

        var inputMediaType = collector.Add(new MediaType());
        handler.CurrentMediaType.CopyAllItems(inputMediaType);
        inputMediaType.Set(MediaTypeAttributeKeys.Subtype, AudioFormatGuids.Pcm);
        inputMediaType.Set(MediaTypeAttributeKeys.AudioBitsPerSample, 16);

        var reader = collector.Add(new SourceReader(microphone.MediaSource));
        reader.SetStreamSelection(SourceReaderIndex.FirstAudioStream, true);
        reader.SetCurrentMediaType(SourceReaderIndex.FirstAudioStream, inputMediaType);

        var outputMediaType = collector.Add(new MediaType());
        inputMediaType.CopyAllItems(outputMediaType);
        outputMediaType.Set(MediaTypeAttributeKeys.Subtype, AudioFormatGuids.Mp3);
        outputMediaType.Set(MediaTypeAttributeKeys.AvgBitrate, 128000);

        var userData = new MpegLayer3WaveFormat
        {
            WaveFormatEx = new WaveFormatEx
            {
                wFormatTag = 0x0055,
                nChannels = 2,
                nSamplesPerSec = 44100,
                nAvgBytesPerSec = 128000,
                nBlockAlign = 1,
                wBitsPerSample = 16,
                cbSize = 12
            },
            wID = 1,
            fdwFlags = 0,
            nBlockSize = 417,
            nFramesPerBlock = 1,
            nCodecDelay = 1393
        };
        // 将UserData转成byte[]
        var userDataBytes = new byte[Marshal.SizeOf(userData)];
        var userDataPtr = Marshal.AllocHGlobal(userDataBytes.Length);
        Marshal.StructureToPtr(userData, userDataPtr, false);
        Marshal.Copy(userDataPtr, userDataBytes, 0, userDataBytes.Length);
        outputMediaType.Set(MediaTypeAttributeKeys.UserData, userDataBytes);

        // Trace.WriteLine("Output media type:");
        // outputMediaType.Dump();

        // 44100/48000 16/PCM 1/2 channels
        var fs = collector.Add(new FileStream(@".\Mp3SinkWriterTest.mp3", FileMode.Create));
        var bs = collector.Add(new ByteStream(fs));
        MediaFactory.CreateMP3MediaSink(bs, out var mediaSink);
        MediaFactory.CreateSinkWriterFromMediaSink(mediaSink, outputMediaType, out var sinkWriter);
        //var sinkWriter = collector.Collect(MediaFactory.CreateSinkWriterFromURL(null, bs, outputMediaType));

        // sinkWriter.AddStream(outputMediaType, out var streamIndex);
        sinkWriter.BeginWriting();

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(5))
        {
            using var sample = reader.ReadSample(SourceReaderIndex.AnyStream, SourceReaderControlFlags.None, out _, out _, out _);
            if (sample == null)
            {
                continue;
            }

            sinkWriter.WriteSample(0, sample);
        }

        sinkWriter.Finalize();
        microphone.Close();

        collector.DisposeAndClear();

        MediaManager.Shutdown();
    }
}