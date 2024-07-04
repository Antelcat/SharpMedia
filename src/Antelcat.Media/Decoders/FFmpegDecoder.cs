using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;
using Antelcat.Media.Extensions;
using Sdcb.FFmpeg.Codecs;
using Sdcb.FFmpeg.Common;
using Sdcb.FFmpeg.Formats;
using Sdcb.FFmpeg.Raw;
using Sdcb.FFmpeg.Swresamples;
using Sdcb.FFmpeg.Swscales;
using Sdcb.FFmpeg.Utils;

namespace Antelcat.Media.Decoders;

internal sealed class FFmpegDecoder(
    FFmpegDecoderContext decoderContext, 
    FFmpegFrameConverter frameConverter, 
    Action<Frame> frameDecoded)
    : IDisposable
{
    public TimeSpan CurrentTime { get; private set; } = TimeSpan.MinValue;

    public DecodeResult Decode(CancellationToken cancellationToken)
    {
        if (decoderContext.CodecContext == null) throw new InvalidOperationException("Not opened");

        using var packet = new Packet();
        using var frame = new Frame();
        while (true)
        {
            var result = decoderContext.ReadPacket(out var rawPacket, cancellationToken);
            try
            {
                if (result == DecodeResult.Again)
                {
                    continue;
                }
                if (result != DecodeResult.Success || rawPacket == null)
                {
                    return result;
                }
                
                packet.Data = new DataPointer(rawPacket.Data, rawPacket.Length);
                lock (decoderContext.CodecContext)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return DecodeResult.Cancelled;
                    }

                    unsafe
                    {
                        _ = ffmpeg.avcodec_send_packet(decoderContext.CodecContext, packet); // 不处理错误
                    }
                }
            }
            finally
            {
                rawPacket?.Dispose();
            }

            break;
        }
        
        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return DecodeResult.Cancelled;
            }

            CodecResult ret;
            lock (decoderContext.CodecContext)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return DecodeResult.Cancelled;
                }

                ret = decoderContext.CodecContext.ReceiveFrame(frame);
            }
            if (ret == CodecResult.Again)
            {
                break;
            }
            switch (ret)
            {
                case CodecResult.EOF: // 没有更多的帧了
                {
                    return DecodeResult.Eof;
                }
                case < 0:
                {
                    throw new Exception("Decode error: " + ret);
                }
            }

            CurrentTime = frame.Pts.ToTimeSpan(frame.TimeBase);
            frameDecoded(frameConverter.ConvertFrame(frame));
        }

        return DecodeResult.Success;
    }

    public void Dispose()
    {
        CurrentTime = TimeSpan.MinValue;
        frameConverter.Dispose();
        decoderContext.Dispose();
    }
}

public abstract class FFmpegDecoderContext : IDecoderContext, IDisposable
{
    public CodecContext? CodecContext { get; protected set; }
    public Codec Codec { get; protected set; }
    public virtual AVRational TimeBase
    {
        get
        {
            if (CodecContext != null && CodecContext.TimeBase.Den != 0 && CodecContext.TimeBase.Num != 0)
            {
                return CodecContext.TimeBase;
            }

            return new AVRational(0, 1);
        }
    }

    public abstract DecodeResult ReadPacket(out RawPacket? rawPacket, CancellationToken cancellationToken);

    public virtual void Dispose()
    {
        if (CodecContext != null)
        {
            lock (CodecContext)
            {
                CodecContext.Dispose();
            }
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 解码FormatContext，这往往是一个媒体文件
/// </summary>
public class FFmpegUrlDecoderContext : FFmpegDecoderContext
{
    public static bool HasMediaType(string url, AVMediaType mediaType)
    {
        try
        {
            using var formatContext = FormatContext.OpenInputUrl(url);
            foreach (var stream in formatContext.Streams)
            {
                if (stream.Codecpar == null || stream.Codecpar.CodecType != mediaType) continue;
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public override AVRational TimeBase
    {
        get
        {
            if (MediaStream.TimeBase.Den != 0 && MediaStream.TimeBase.Num != 0)
            {
                return MediaStream.TimeBase;
            }

            return base.TimeBase;
        }
    }

    public TimeSpan Duration
    {
        get
        {
            if (duration.HasValue) return duration.Value;

            TimeSpan GetDuration()
            {
                if (formatContext == null) throw new InvalidOperationException("Decoder not opened");

                if (MediaStream.Duration != ffmpeg.AV_NOPTS_VALUE)
                {
                    return MediaStream.Duration.ToTimeSpan(MediaStream.TimeBase);
                }

                // 音频流没有Duration，使用文件的Duration
                if (formatContext.Duration != ffmpeg.AV_NOPTS_VALUE)
                {
                    return TimeSpan.FromSeconds((double)formatContext.Duration / ffmpeg.AV_TIME_BASE);
                }

                TimeSpan ExtractDuration()
                {
                    var maxPts = -1L;
                    using Packet packet = new();
                    while (true)
                    {
                        var result = formatContext.ReadFrame(packet);
                        if (result == CodecResult.EOF) break;

                        if (packet.StreamIndex != MediaStream.Index)
                        {
                            packet.Unref();
                        }
                        else
                        {
                            maxPts = Math.Max(maxPts, packet.Pts);
                        }
                    }

                    return maxPts.ToTimeSpan(MediaStream.TimeBase);
                }

                // 仍然没有Duration，遍历
                var result = ExtractDuration();
                formatContext.SeekFrame(0);

                return result;
            }

            duration = GetDuration();
            return duration.Value;
        }
    }

    private TimeSpan? duration;

    public MediaStream MediaStream { get; }

    protected readonly FormatContext formatContext;

    public FFmpegUrlDecoderContext(string url, AVMediaType mediaType)
    {
        formatContext = FormatContext.OpenInputUrl(url);
        var hasMediaStream = false;
        foreach (var stream in formatContext.Streams)
        {
            if (stream.Codecpar == null || stream.Codecpar.CodecType != mediaType) continue;
            MediaStream = stream;
            hasMediaStream = true;
            break;
        }
        if (!hasMediaStream)
        {
            throw new InvalidDataException("No media stream found.");
        }

        var codecPar = MediaStream.Codecpar!;
        var codecId = codecPar.CodecId;
        Codec = Codec.FindDecoderById(codecId);
        CodecContext = new CodecContext(Codec);
        CodecContext.FillParameters(codecPar);
        CodecContext.Open(Codec);
    }

    public override DecodeResult ReadPacket(out RawPacket? rawPacket, CancellationToken cancellationToken)
    {
        if (formatContext == null)
        {
            throw new NullReferenceException(nameof(formatContext));
        }

        using var packet = new Packet();
        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                rawPacket = null;
                return DecodeResult.Cancelled;
            }

            switch (formatContext.ReadFrame(packet))
            {
                case CodecResult.Again:
                {
                    packet.Unref();
                    continue;
                }
                case CodecResult.EOF:
                {
                    rawPacket = null;
                    return DecodeResult.Eof;
                }
                case CodecResult.Success when packet.StreamIndex != MediaStream.Index:
                {
                    rawPacket = null;
                    return DecodeResult.Again;
                }
                default:
                {
                    rawPacket = new RawPacket(packet.Data.Pointer, packet.Data.Length)
                    {
                        Dts = packet.Dts.ToTimeSpan(packet.TimeBase),
                        Pts = packet.Pts.ToTimeSpan(packet.TimeBase),
                        Duration = packet.Duration.ToTimeSpan(packet.TimeBase),
                    };
                    return DecodeResult.Success;
                }
            }
        }
    }

    public void Seek(TimeSpan position)
    {
        if (CodecContext == null)
        {
            throw new NullReferenceException(nameof(CodecContext));
        }

        if (formatContext == null)
        {
            throw new NullReferenceException(nameof(formatContext));
        }

        var timestamp = position.ToTimestamp(TimeBase);
        formatContext.SeekFrame(timestamp, MediaStream.Index);
        unsafe
        {
            lock (CodecContext)
            {
                ffmpeg.avcodec_flush_buffers(CodecContext);
            }
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        formatContext.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 解码没有FormatContext的流，会使用ffmpeg自动从流中拆分出Packet，如果packet已经拆分好了，请使用<see cref="FFmpegInstantDecoderContext"/>
/// </summary>
public class FFmpegStreamingDecoderContext : FFmpegDecoderContext
{
    private readonly Stream stream;
    private readonly CodecParserContext codecParserContext;

    public FFmpegStreamingDecoderContext(Stream stream, AVCodecID codecID)
    {
        if (codecParserContext != null) throw new InvalidOperationException("Already opened");

        this.stream = stream;
        Codec = Codec.FindDecoderById(codecID);
        codecParserContext = new CodecParserContext(codecID);
        CodecContext = new CodecContext(Codec);
        CodecContext.Open(Codec);
    }

    private IEnumerator<DataPointer>? parserEnumerator;
    private DecodeResult parserState;

    private int framerIndex;

    public override DecodeResult ReadPacket(out RawPacket? rawPacket, CancellationToken cancellationToken)
    {
        parserEnumerator ??= ParserEnumerator(cancellationToken);
        if (parserEnumerator.MoveNext())
        {
            rawPacket = new RawPacket(parserEnumerator.Current.Pointer, parserEnumerator.Current.Length);
            Debug.WriteLine($"F [{framerIndex++:0000}] {DateTime.Now:O}");
        }
        else
        {
            rawPacket = null;
            parserEnumerator = null;
        }

        return parserState;
    }

    private IEnumerator<DataPointer> ParserEnumerator(CancellationToken cancellationToken)
    {
        if (stream == null || codecParserContext == null || CodecContext == null) throw new InvalidOperationException("Not opened");

        const int bufferSize = 4092;
        var data = new byte[bufferSize + ffmpeg.AV_INPUT_BUFFER_PADDING_SIZE];
        var dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    parserState = DecodeResult.Cancelled;
                    yield break;
                }

                var thisBufferLength = stream.Read(data, 0, bufferSize);
                if (thisBufferLength == 0)
                {
                    parserState = DecodeResult.Eof;
                    yield break;
                }

                var thisBuffer = new DataPointer(dataHandle.AddrOfPinnedObject(), thisBufferLength);
                while (thisBuffer.Length > 0)
                {
                    var offset = codecParserContext.Parse(CodecContext, thisBuffer, out var dataPointer);
                    if (dataPointer.Length > 0)
                    {
                        parserState = DecodeResult.Success;
                        yield return dataPointer;
                    }
                    else
                    {
                        parserState = DecodeResult.Again;
                    }
                    thisBuffer = thisBuffer[offset..];
                }
            }
        }
        finally
        {
            dataHandle.Free();
            parserState = DecodeResult.Eof;
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        codecParserContext.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class FFmpegInstantDecoderContext : FFmpegDecoderContext
{
    private RawPacket? packetCache;
    private readonly object syncLock = new();

    public FFmpegInstantDecoderContext(AVCodecID codecID)
    {
        Codec = Codec.FindDecoderById(codecID);
        CodecContext = new CodecContext(Codec);
        CodecContext.Open(Codec);
    }

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

/// <summary>
/// 用于将FFmpeg的Frame转换为期望的格式，这个类会自己管理内存，防止频繁分配
/// </summary>
public abstract class FFmpegFrameConverter : IDisposable
{
    protected Frame? convertedFrame;

    public abstract Frame ConvertFrame(Frame decodedFrame);

    public virtual void Dispose()
    {
        convertedFrame?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class FFmpegVideoFrameConverter(VideoFrameFormat dstFrameFormat) : FFmpegFrameConverter
{
    private VideoFrameConverter? converter;

    public override Frame ConvertFrame(Frame decodedFrame)
    {
        var dstPixelFormat = (int)dstFrameFormat.ToAvPixelFormat();
        // 首先确保convertedFrame和期望的FrameFormat一致
        if (convertedFrame == null || convertedFrame.Format != dstPixelFormat)
        {
            convertedFrame?.Dispose();
            convertedFrame = new Frame
            {
                Width = decodedFrame.Width,
                Height = decodedFrame.Height
            };
            convertedFrame.Format = dstPixelFormat;
            convertedFrame.EnsureBuffer();
        }

        // 如果convertedFrame和decodedFrame的格式不一致，需要进行转换
        if (convertedFrame.Format != decodedFrame.Format)
        {
            converter ??= new VideoFrameConverter();
            converter.ConvertFrame(decodedFrame, convertedFrame);

            convertedFrame.TimeBase = decodedFrame.TimeBase;
            convertedFrame.PktDts = decodedFrame.PktDts;
            convertedFrame.Duration = decodedFrame.Duration;
            convertedFrame.Pts = decodedFrame.Pts;
        }
        else
        {
            unsafe
            {
                ffmpeg.av_frame_copy(convertedFrame, decodedFrame);
            }
        }

        return convertedFrame;
    }

    public override void Dispose()
    {
        base.Dispose();
        converter?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class FFmpegAudioFrameConverter(AudioFrameFormat dstFrameFormat) : FFmpegFrameConverter
{
    private SampleConverter? resampler;
    private int actualConvertedFrameNbSamples;

    public override Frame ConvertFrame(Frame decodedFrame)
    {
        var avSampleFormat = (int)dstFrameFormat.ToAvSampleFormat();
        // 首先确保convertedFrame和期望的FrameFormat一致
        if (convertedFrame == null ||
            convertedFrame.ChLayout.nb_channels != dstFrameFormat.ChannelCount ||
            convertedFrame.SampleRate != dstFrameFormat.SampleRate ||
            convertedFrame.Format != avSampleFormat)
        {
            convertedFrame?.Dispose();
            convertedFrame = new Frame
            {
                NbSamples = actualConvertedFrameNbSamples = decodedFrame.NbSamples,
                TimeBase = decodedFrame.TimeBase
            };
            convertedFrame.ChLayout = dstFrameFormat.ToAvChannelLayout();
            convertedFrame.SampleRate = dstFrameFormat.SampleRate;
            convertedFrame.Format = avSampleFormat;
            convertedFrame.EnsureBuffer();
        }

        unsafe bool AreChannelLayoutsEqual(AVChannelLayout a, AVChannelLayout b)
        {
            return ffmpeg.av_channel_layout_compare(&a, &b) == 0;
        }

        // 如果convertedFrame和decodedFrame的格式不一致，需要进行转换
        if (!AreChannelLayoutsEqual(convertedFrame.ChLayout, decodedFrame.ChLayout) ||
            convertedFrame.SampleRate != decodedFrame.SampleRate ||
            convertedFrame.Format != decodedFrame.Format)
        {
            if (resampler == null)
            {
                resampler = new SampleConverter();
                resampler.Reset(
                    convertedFrame.ChLayout,
                    (AVSampleFormat)convertedFrame.Format,
                    convertedFrame.SampleRate,
                    decodedFrame.ChLayout,
                    (AVSampleFormat)decodedFrame.Format,
                    decodedFrame.SampleRate);
                resampler.Initialize();
            }

            var dstNbSamples = (int)ffmpeg.av_rescale_rnd(
                resampler.GetDelay(decodedFrame.SampleRate) +
                decodedFrame.NbSamples,
                convertedFrame.SampleRate,
                decodedFrame.SampleRate,
                AVRounding.Up);
            if (actualConvertedFrameNbSamples < dstNbSamples)
            {
                convertedFrame.Dispose();
                convertedFrame = new Frame
                {
                    NbSamples = actualConvertedFrameNbSamples = dstNbSamples,
                    TimeBase = decodedFrame.TimeBase
                };
                convertedFrame.ChLayout = dstFrameFormat.ToAvChannelLayout();
                convertedFrame.SampleRate = dstFrameFormat.SampleRate;
                convertedFrame.Format = avSampleFormat;
                convertedFrame.EnsureBuffer();
            }

            var convertedSamples = resampler.Convert(
                convertedFrame.Data,
                dstNbSamples,
                decodedFrame.Data,
                decodedFrame.NbSamples);

            convertedFrame.NbSamples = convertedSamples;
            convertedFrame.TimeBase = decodedFrame.TimeBase;
            convertedFrame.PktDts = decodedFrame.PktDts;
            convertedFrame.Duration = decodedFrame.Duration;
            convertedFrame.Pts = decodedFrame.Pts;
        }
        else
        {
            unsafe
            {
                ffmpeg.av_frame_copy(convertedFrame, decodedFrame);
            }
        }

        return convertedFrame;
    }

    public override void Dispose()
    {
        base.Dispose();
        resampler?.Dispose();
        GC.SuppressFinalize(this);
    }
}