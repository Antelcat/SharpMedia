using System.Diagnostics;
using System.Runtime.InteropServices;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;
using Lennox.NvEncSharp;
using CuVideoFrame = Antelcat.Media.Windows.Abstractions.CuVideoFrame;

namespace Antelcat.Media.Windows.Decoders.Video;

/// <summary>
/// 基于NvCodec Cuda的视频解码器，解码成RGBA32
/// </summary>
public class NvCodecVideoDecoder : IVideoDecoder
{
    public VideoFrameFormat FrameFormat => VideoFrameFormat.RGBA32;
    public TimeSpan CurrentTime { get; private set; }
    public event Action<RawVideoFrame>? FrameDecoded;

    private readonly NvCodecVideoDecoderContext context;
    private readonly bool useHostMemory;
    private readonly int desiredWidth, desiredHeight;

    private CuVideoDecoder decoder;
    private CuVideoDecodeCreateInfo info;
    private CuVideoParser parser;

    /// <summary>
    /// 基于NvCodec Cuda的视频解码器
    /// </summary>
    /// <param name="context"></param>
    /// <param name="useHostMemory">如果为true，解码的帧将被存储到CPU，否则保留在GPU</param>
    /// <param name="desiredWidth">如果需要缩放输出，指定希望的大小</param>
    /// <param name="desiredHeight">如果需要缩放输出，指定希望的大小</param>
    public NvCodecVideoDecoder(NvCodecVideoDecoderContext context, bool useHostMemory = true, int desiredWidth = 0, int desiredHeight = 0)
    {
        this.context = context;
        this.useHostMemory = useHostMemory;
        this.desiredWidth = desiredWidth;
        this.desiredHeight = desiredHeight;

        var parserParams = new CuVideoParserParams
        {
            CodecType = context.PacketFormat switch
            {
                EncodedVideoFormat.H264 => CuVideoCodec.H264,
                EncodedVideoFormat.Hevc => CuVideoCodec.HEVC,
                EncodedVideoFormat.Vp8 => CuVideoCodec.VP8,
                EncodedVideoFormat.Vp9 => CuVideoCodec.VP9,
                _ => throw new NotSupportedException()
            },
            MaxNumDecodeSurfaces = 1,
            MaxDisplayDelay = 0,
            ErrorThreshold = 100,
            UserData = IntPtr.Zero,
            DisplayPicture = VideoDisplayCallback,
            DecodePicture = DecodePictureCallback,
            SequenceCallback = SequenceCallback
        };
        parser = CuVideoParser.Create(ref parserParams);
    }

    private unsafe CuCallbackResult VideoDisplayCallback(IntPtr data, IntPtr infoPtr)
    {
        using var _ = context.CuContext.Push();

        if (CuVideoParseDisplayInfo.IsFinalFrame(infoPtr, out var displayInfo))
        {
            return CuCallbackResult.Success;
        }

        var processingParam = new CuVideoProcParams
        {
            ProgressiveFrame = displayInfo.ProgressiveFrame,
            SecondField = displayInfo.RepeatFirstField + 1,
            TopFieldFirst = displayInfo.TopFieldFirst,
            UnpairedField = displayInfo.RepeatFirstField < 0 ? 1 : 0
        };

        using var frame = decoder.MapVideoFrame(
            displayInfo.PictureIndex,
            ref processingParam,
            out var pitch);

        var status = decoder.GetDecodeStatus(displayInfo.PictureIndex);
        if (status != CuVideoDecodeStatus.Success)
        {
            // TODO: Determine what to do in this situation. This condition
            // is non-exceptional but may require different handling?
        }

        // 下面要进行缩放，并转换成RGBA32
        var width = info.Width;
        var height = info.Height;
        const int RgbBpp = 4;
        var rgbBytesSize = width * height * RgbBpp;

        var source = frame.Handle;
        var hasNewSource = false;

        CuVideoFrame cuVideoFrame;

        try
        {
            // This code path does not appear to properly resize the
            // window.
            if (desiredWidth > 0 && desiredHeight > 0)
            {
                // This buffer size allocation is incorrect but should be
                // oversized enough to be fine.
                source = CuDeviceMemory.Allocate(rgbBytesSize = desiredWidth * desiredHeight * RgbBpp);
                hasNewSource = true;

                LibCudaLibrary.ResizeNv12(
                    new CuDevicePtr(source),
                    desiredWidth,
                    desiredWidth,
                    desiredHeight,
                    new CuDevicePtr(frame.Handle),
                    pitch,
                    width,
                    height,
                    CuDevicePtr.Empty);

                width = desiredWidth;
                height = desiredHeight;
            }

            if (useHostMemory)
            {
                using var rgb32Ptr = CuDeviceMemory.Allocate(rgbBytesSize);
                LibCudaLibrary.Nv12ToBGRA32(source, pitch, rgb32Ptr, width * RgbBpp, width, height);
                cuVideoFrame = new CuVideoFrame(width, height, rgbBytesSize, VideoFrameFormat.RGBA32, CuMemoryType.Host, width * RgbBpp)
                {
                    Time = TimeSpan.FromTicks(displayInfo.Timestamp),
                };
                rgb32Ptr.CopyToHost((byte*)cuVideoFrame.Data, rgbBytesSize);
            }
            else
            {
                cuVideoFrame = new CuVideoFrame(width, height, rgbBytesSize, VideoFrameFormat.RGBA32, CuMemoryType.Device, width * RgbBpp)
                {
                    Time = TimeSpan.FromTicks(displayInfo.Timestamp),
                };
                LibCudaLibrary.Nv12ToBGRA32(source, pitch, cuVideoFrame.Data, width * RgbBpp, width, height);
            }
        }
        finally
        {
            if (hasNewSource)
            {
                // source.Dispose();
                LibCuda.MemFree(new CuDevicePtr(source));
            }
        }

        CurrentTime = cuVideoFrame.Time;
        FrameDecoded?.Invoke(cuVideoFrame);
        Debug.WriteLine($"V [{vIndex++:0000}] {DateTime.Now:O}");
        return CuCallbackResult.Success;
    }

    private CuCallbackResult DecodePictureCallback(IntPtr data, ref CuVideoPicParams param)
    {
        decoder.DecodePicture(ref param);
        Debug.WriteLine($"D [{dIndex++:0000}] {DateTime.Now:O}");
        return CuCallbackResult.Success;
    }

    private CuCallbackResult SequenceCallback(IntPtr data, ref CuVideoFormat format)
    {
        using var _ = context.CuContext.Push();

        // PrintInformation("CuVideoFormat",
        //     new Dictionary<string, object>
        //     {
        //         ["Codec"] = format.Codec,
        //         ["Bitrate"] = format.Bitrate,
        //         ["CodedWidth"] = format.CodedWidth,
        //         ["CodedHeight"] = format.CodedHeight,
        //         ["Framerate"] = format.FrameRateNumerator / format.FrameRateDenominator,
        //     });

#if DEBUG
        if (!format.IsSupportedByDecoder(out var error, out var caps))
        {
            Debug.WriteLine(error);
            Debug.WriteLine(caps);
            Debugger.Break();
#else
        if (!format.IsSupportedByDecoder(out var _, out var _))
        {
#endif
            return CuCallbackResult.Failure;
        }

        // PrintInformation("CuVideoDecodeCaps",
        //     new Dictionary<string, object>
        //     {
        //         ["MaxWidth"] = caps.MaxWidth,
        //         ["MaxHeight"] = caps.MaxHeight,
        //     });

        if (!decoder.IsEmpty)
        {
            decoder.Reconfigure(ref format);
            Debug.WriteLine($"Q [{qIndex++:0000}] {DateTime.Now:O}");
            return CuCallbackResult.Success;
        }

        info = new CuVideoDecodeCreateInfo
        {
            CodecType = format.Codec,
            ChromaFormat = format.ChromaFormat,
            OutputFormat = format.GetSurfaceFormat(),
            BitDepthMinus8 = format.BitDepthLumaMinus8,
            DeinterlaceMode = format.ProgressiveSequence ? CuVideoDeinterlaceMode.Weave : CuVideoDeinterlaceMode.Adaptive,
            NumOutputSurfaces = 2,
            CreationFlags = CuVideoCreateFlags.PreferCUVID,
            NumDecodeSurfaces = format.MinNumDecodeSurfaces,
            VideoLock = context.contextLock,
            Width = format.CodedWidth,
            Height = format.CodedHeight,
            MaxWidth = format.CodedWidth,
            MaxHeight = format.CodedHeight,
            TargetWidth = format.CodedWidth,
            TargetHeight = format.CodedHeight
        };

        decoder = CuVideoDecoder.Create(ref info);
        Debug.WriteLine($"Q [{qIndex++:0000}] {DateTime.Now:O}");
        return (CuCallbackResult)format.MinNumDecodeSurfaces;
    }

    private int rIndex, pIndex, vIndex, qIndex, dIndex;

    public DecodeResult Decode(CancellationToken cancellationToken)
    {
        var result = DecodeResult.Again;
        
        while (context.ReadPacket(out var packet, cancellationToken) == DecodeResult.Success && packet != null)
        {
            Debug.WriteLine($"R [{rIndex++:0000}] {DateTime.Now:O}");
            parser.ParseVideoData(packet.AsSpan());
            Debug.WriteLine($"P [{pIndex++:0000}] {DateTime.Now:O}");
            result = DecodeResult.Success;
        }

        return result;
    }

    public void Dispose()
    {
        if (!parser.IsEmpty)
        {
            parser.SendEndOfStream();
            DisposeCollector.DisposeToDefault(ref parser);
        }

        if (!decoder.IsEmpty) DisposeCollector.DisposeToDefault(ref decoder);

        GC.SuppressFinalize(this);
    }
}

public abstract class NvCodecVideoDecoderContext : IDecoderContext, IDisposable
{
    public CuContext CuContext => cuContext;

    public EncodedVideoFormat PacketFormat { get; }

    private CuContext cuContext;
    internal CuVideoContextLock contextLock;

    protected NvCodecVideoDecoderContext(EncodedVideoFormat packetFormat)
    {
        PacketFormat = packetFormat;

        LibCuda.Initialize();
        
        var descriptions = CuDevice.GetDescriptions().ToArray();
        if (descriptions.Length == 0)
        {
            throw new DriveNotFoundException("No CUDA devices found.");
        }

        var device = descriptions[0].Device;
        cuContext = device.CreateContext(CuContextFlags.SchedBlockingSync);
        contextLock = CuContext.CreateLock();
    }

    public abstract DecodeResult ReadPacket(out RawPacket? rawPacket, CancellationToken cancellationToken);

    public virtual void Dispose()
    {
        DisposeCollector.DisposeToDefault(ref cuContext);
        DisposeCollector.DisposeToDefault(ref contextLock);

        GC.SuppressFinalize(this);
    }
}

public class NvCodecInstantVideoDecoderContext(EncodedVideoFormat packetFormat) : NvCodecVideoDecoderContext(packetFormat)
{
    private RawPacket? packetCache;
    private readonly object syncLock = new();

    public override DecodeResult ReadPacket(out RawPacket? rawPacket, CancellationToken cancellationToken)
    {
        lock (syncLock)
        {
            if (packetCache == null)
            {
                rawPacket = null;
                return DecodeResult.Again;
            }
            
            rawPacket = packetCache;
            packetCache = null;
            return DecodeResult.Success;
        }
    }

    public void SetPacket(RawPacket rawPacket)
    {
        lock (syncLock)
        {
            packetCache?.Dispose();
            packetCache = rawPacket;
        }
    }

    public void SetPacket(byte[] packet)
    {
        var rawPacket = new RawPacket(packet.Length);
        Marshal.Copy(packet, 0, rawPacket.Data, packet.Length);
        SetPacket(rawPacket);
    }
}