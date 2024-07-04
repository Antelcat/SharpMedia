using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using EasyPathology.Abstractions.DataTypes;
using Antelcat.Media.Abstractions;
using Antelcat.Media.Abstractions.Enums;
using Sdcb.FFmpeg.Codecs;
using Sdcb.FFmpeg.Common;
using Sdcb.FFmpeg.Raw;
using Sdcb.FFmpeg.Swresamples;
using Sdcb.FFmpeg.Swscales;
using Sdcb.FFmpeg.Utils;
using static Sdcb.FFmpeg.Raw.ffmpeg;

namespace Antelcat.Media.Extensions;

public static class FFmpegExtension
{
    public enum AvLogLevel
    {
        Quiet = -8,
        Panic = 0,
        Fatal = 8,
        Error = 16,
        Warning = 24,
        Info = 32,
        Verbose = 40,
        Debug = 48,
        Trace = 56,
    }

    private delegate void AvLogCallbackHandler(IntPtr p0, AvLogLevel level, IntPtr format, IntPtr args);

    // 防止GC回收
    // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
    private static readonly AvLogCallbackHandler LogCallbackDelegate;

    private static string? avLastError;

    static FFmpegExtension()
    {
        LogCallbackDelegate = AvLogCallback;
        var ptr = Marshal.GetFunctionPointerForDelegate(LogCallbackDelegate);
        av_log_set_callback(new av_log_set_callback_callback_func(ptr));
    }

    [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "_vscprintf")]
    private static extern int VscPrintf(IntPtr format, IntPtr ptr);

    [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "vsprintf")]
    public static extern int VsPrintf(IntPtr buffer, IntPtr format, IntPtr args);

    private static void AvLogCallback(IntPtr p0, AvLogLevel level, IntPtr format, IntPtr args)
    {
        if (level >= AvLogLevel.Warning)
        {
            return;
        }

        var byteLength = VscPrintf(format, args) + 1;
        var utf8Buffer = Marshal.AllocHGlobal(byteLength);
        if (VsPrintf(utf8Buffer, format, args) != byteLength - 1)
        {
            return;
        }

        avLastError = Marshal.PtrToStringUTF8(utf8Buffer);
        if (avLastError != null)
        {
            avLastError = $"[ffmpeg {level}] {Marshal.PtrToStringUTF8(utf8Buffer)}";
            Debug.WriteLine(avLastError);
        }

        Marshal.FreeHGlobal(utf8Buffer);
    }

    public static Exception ToDetailed(this FFmpegException exception)
    {
        if (avLastError == null)
        {
            return exception;
        }

        return new Exception(avLastError, exception);
    }

    public static int CheckFFmpeg(this int errorCode, string? message = null)
    {
        if (errorCode < 0)
        {
            throw FFmpegException.FromErrorCode(errorCode, message);
        }

        return errorCode;
    }

    public static void SetByWaveFormat(this CodecContext context, AudioFrameFormat format, long bitRate)
    {
        context.ChLayout = format.ToAvChannelLayout();
        context.SampleFormat = format.ToAvSampleFormat();
        context.SampleRate = format.SampleRate;
        context.BitRate = bitRate;
    }

    public static void SetByVideoInput(
        this CodecContext context,
        VideoInputDevice device,
        AVPixelFormat pixelFormat,
        long bitRate)
    {

        context.Height = (int)device.FrameHeight;
        context.Width = (int)device.FrameWidth;
        context.TimeBase = device.FrameRate.ToRational().Inverse();
        context.BitRate = bitRate;
        context.PixelFormat = pixelFormat;
        context.Framerate = device.FrameRate.ToRational();
        context.Flags = (AV_CODEC_FLAG)((int)context.Flags | (int)AVFMT.Globalheader);

        throw new NotImplementedException();
        switch (context.CodecId)
        {
            case AVCodecID.H264:
            {
                unsafe
                {
                    av_opt_set((void*)context.PrivateData, "preset", "veryfast", 0);
                    // AVDictionary* svtAv1Opts = null;
                    // av_dict_set_int(ref svtAv1Opts, "rc", 1, 0);  // Rate Control设置为1: VBR（动态码率）
                    // av_opt_set_dict_val((void*)context.PrivateData, "svtav1_opts", svtAv1Opts, 0);
                    // av_dict_free(ref svtAv1Opts);
                    context.GopSize = 10;
                    context.MaxBFrames = 1;
                    context.Qcompress = 0.6f;
                }
                break;
            }
            case AVCodecID.Mpeg4:
            {
                context.MaxBFrames = 2;
                unsafe
                {
                    av_opt_set(context, "preset", "slow", 0);
                    av_opt_set(context, "tune", "film", 0);
                }
                break;
            }
            case AVCodecID.Vp8:
            {
                break;
            }
            default:
            {
                throw new ArgumentException("Unsupported CodecId");
            }
        }
    }

    public static void SetByVideoInput(this CodecContext context, VideoInputDevice device)
    {
        context.Height = (int)device.FrameHeight;
        context.Width = (int)device.FrameWidth;
        context.Flags |= AV_CODEC_FLAG.GlobalHeader;
    }

    public static void Convert(this PixelConverter converter, IntPtr data, Frame receiver, AVPixelFormat srcFormat, CodecContext context)
    {
        switch (srcFormat)
        {
            case AVPixelFormat.Nv12:
                var size = context.Width * context.Height;
                var inAddress = new int_array8()
                {
                    [0] = context.Width,
                    [1] = context.Width,
                    [2] = 0
                };
                var outAddress = new int_array8()
                {
                    [0] = context.Width,
                    [1] = context.Width / 2,
                    [2] = context.Width / 2,
                };
                var cache = new IntPtr();
                var dst = new byte_ptrArray8 { [0] = cache, [1] = cache + size, [2] = cache + size + size / 4 };
                converter.Convert(
                    new byte_ptrArray8 { [0] = data, [1] = data + size, [2] = IntPtr.Zero },
                    inAddress,
                    context.Height,
                    dst,
                    outAddress);
                receiver.Data.UpdateFrom(dst.ToArray());
                return;
        }
    }

    public static void Convert(this SampleConverter converter, IntPtr data, int dataLength, Frame receiver, CodecContext context)
    {
        converter.Convert(receiver.Data,
            context.FrameSize,
            new byte_ptrArray8 { [0] = data },
            dataLength);
    }

    public static AVCodecID ToCodecId(this EncodedAudioFormat format) =>
        format switch
        {
            EncodedAudioFormat.Aac => AVCodecID.Aac,
            EncodedAudioFormat.Mp3 => AVCodecID.Mp3,
            EncodedAudioFormat.Wma => AVCodecID.Wmalossless,
            EncodedAudioFormat.Opus => AVCodecID.Opus,
            EncodedAudioFormat.Pcma => AVCodecID.PcmAlaw,
            EncodedAudioFormat.Pcmu => AVCodecID.PcmMulaw,
            EncodedAudioFormat.PcmS16Le => AVCodecID.PcmS16le,
            EncodedAudioFormat.G729 => AVCodecID.G729,
            _ => AVCodecID.None
        };

    public static EncodedAudioFormat ToEncodedAudioFormat(this AVCodecID codecId) =>
        codecId switch
        {
            AVCodecID.Aac => EncodedAudioFormat.Aac,
            AVCodecID.Mp3 => EncodedAudioFormat.Mp3,
            AVCodecID.Wmalossless => EncodedAudioFormat.Wma,
            AVCodecID.Opus => EncodedAudioFormat.Opus,
            AVCodecID.PcmAlaw => EncodedAudioFormat.Pcma,
            AVCodecID.PcmMulaw => EncodedAudioFormat.Pcmu,
            AVCodecID.PcmS16le => EncodedAudioFormat.PcmS16Le,
            AVCodecID.G729 => EncodedAudioFormat.G729,
            _ => EncodedAudioFormat.Unset
        };

    public static AVCodecID ToCodecId(this EncodedVideoFormat format) =>
        format switch
        {
            EncodedVideoFormat.H264 => AVCodecID.H264,
            EncodedVideoFormat.Hevc => AVCodecID.Hevc,
            EncodedVideoFormat.Vp8 => AVCodecID.Vp8,
            EncodedVideoFormat.Vp9 => AVCodecID.Vp9,
            _ => AVCodecID.None
        };

    public static EncodedVideoFormat ToEncodedVideoFormat(this AVCodecID codecId) =>
        codecId switch
        {
            AVCodecID.H264 => EncodedVideoFormat.H264,
            AVCodecID.Hevc => EncodedVideoFormat.Hevc,
            AVCodecID.Vp8 => EncodedVideoFormat.Vp8,
            AVCodecID.Vp9 => EncodedVideoFormat.Vp9,
            _ => EncodedVideoFormat.Unset
        };

    public static AVRational ToRational(this Fraction fraction) => new((int)fraction.Number, (int)fraction.Denominator);

    public static AVPixelFormat ToAvPixelFormat(this VideoFrameFormat format) =>
        format switch
        {
            VideoFrameFormat.RGB24 => AVPixelFormat.Bgr24,
            VideoFrameFormat.RGBA32 => AVPixelFormat.Bgr0,
            VideoFrameFormat.NV12 => AVPixelFormat.Nv12,
            VideoFrameFormat.Yv12 => AVPixelFormat.Yuv420p,
            VideoFrameFormat.YUY2 => AVPixelFormat.Yuyv422,
            _ => throw new NotSupportedException(format.ToString())
        };

    public static VideoFrameFormat ToVideoFrameFormat(this AVPixelFormat format) =>
        format switch
        {
            AVPixelFormat.Rgb24 => VideoFrameFormat.RGB24,
            AVPixelFormat.Rgb0 => VideoFrameFormat.RGBA32,
            AVPixelFormat.Rgba => VideoFrameFormat.RGBA32,
            AVPixelFormat.Nv12 => VideoFrameFormat.NV12,
            AVPixelFormat.Yuv420p => VideoFrameFormat.Yv12,
            AVPixelFormat.Yuyv422 => VideoFrameFormat.YUY2,
            _ => VideoFrameFormat.Unset
        };

    public static AVSampleFormat ToAvSampleFormat(this AudioFrameFormat format) =>
        format.BitsPerSample switch
        {
            8 when format is { IsFloat: false, IsPlanar: true } => AVSampleFormat.U8p,
            8 when format is { IsFloat: false, IsPlanar: false } => AVSampleFormat.U8,
            16 when format is { IsFloat: false, IsPlanar: true } => AVSampleFormat.S16p,
            16 when format is { IsFloat: false, IsPlanar: false } => AVSampleFormat.S16,
            32 when format is { IsFloat: false, IsPlanar: true } => AVSampleFormat.S32p,
            32 when format is { IsFloat: false, IsPlanar: false } => AVSampleFormat.S32,
            32 when format is { IsFloat: true, IsPlanar: true } => AVSampleFormat.Fltp,
            32 when format is { IsFloat: true, IsPlanar: false } => AVSampleFormat.Flt,
            64 when format is { IsFloat: false, IsPlanar: true } => AVSampleFormat.S64p,
            64 when format is { IsFloat: false, IsPlanar: false } => AVSampleFormat.S64,
            64 when format is { IsFloat: true, IsPlanar: true } => AVSampleFormat.Dblp,
            64 when format is { IsFloat: true, IsPlanar: false } => AVSampleFormat.Dbl,
            _ => throw new NotSupportedException()
        };

    public static string ToAvSampleFormatName(this AudioFrameFormat format) =>
        av_get_sample_fmt_name(ToAvSampleFormat(format));

    public static AudioFrameFormat ToAudioFrameFormat(this AVSampleFormat format, int sampleRate, int channelCount) =>
        format switch
        {
            AVSampleFormat.U8p => new AudioFrameFormat(sampleRate, 8, channelCount) { IsFloat = false, IsPlanar = true },
            AVSampleFormat.U8 => new AudioFrameFormat(sampleRate, 8, channelCount) { IsFloat = false, IsPlanar = false },
            AVSampleFormat.S16p => new AudioFrameFormat(sampleRate, 16, channelCount) { IsFloat = false, IsPlanar = true },
            AVSampleFormat.S16 => new AudioFrameFormat(sampleRate, 16, channelCount) { IsFloat = false, IsPlanar = false },
            AVSampleFormat.S32p => new AudioFrameFormat(sampleRate, 32, channelCount) { IsFloat = false, IsPlanar = true },
            AVSampleFormat.S32 => new AudioFrameFormat(sampleRate, 32, channelCount) { IsFloat = false, IsPlanar = false },
            AVSampleFormat.Fltp => new AudioFrameFormat(sampleRate, 32, channelCount) { IsFloat = true, IsPlanar = true },
            AVSampleFormat.Flt => new AudioFrameFormat(sampleRate, 32, channelCount) { IsFloat = true, IsPlanar = false },
            AVSampleFormat.S64p => new AudioFrameFormat(sampleRate, 64, channelCount) { IsFloat = false, IsPlanar = true },
            AVSampleFormat.S64 => new AudioFrameFormat(sampleRate, 64, channelCount) { IsFloat = false, IsPlanar = false },
            AVSampleFormat.Dblp => new AudioFrameFormat(sampleRate, 64, channelCount) { IsFloat = true, IsPlanar = true },
            AVSampleFormat.Dbl => new AudioFrameFormat(sampleRate, 64, channelCount) { IsFloat = true, IsPlanar = false },
            _ => throw new NotSupportedException()
        };

    public static unsafe AVChannelLayout ToAvChannelLayout(this AudioFrameFormat format)
    {
        var chLayout = new AVChannelLayout();
        av_channel_layout_default(&chLayout, format.ChannelCount);
        return chLayout;
    }

    public static unsafe string ToAvChannelLayoutDescribe(this AudioFrameFormat format)
    {
        var chLayout = new AVChannelLayout();
        av_channel_layout_default(&chLayout, format.ChannelCount);
        const int bufSize = 32;
        var buf = Marshal.AllocHGlobal(bufSize);
        var count = av_channel_layout_describe(&chLayout, (byte*)buf.ToPointer(), bufSize);
        var describe = Marshal.PtrToStringAnsi(buf, count);
        Marshal.FreeHGlobal(buf);
        return describe;
    }

    public static TimeSpan ToTimeSpan(this long timestamp, AVRational timeBase)
    {
        if (timestamp < 0)
        {
            return TimeSpan.MinValue;
        }

        if (timeBase.Num <= 0 || timeBase.Den <= 0)
        {
            return TimeSpan.FromSeconds((double)timestamp / AV_TIME_BASE);
        }

        return TimeSpan.FromTicks(av_rescale_q(timestamp,
            timeBase,
            new AVRational(1, (int)TimeSpan.TicksPerSecond)));
    }

    public static long ToTimestamp(this TimeSpan timeSpan, AVRational timeBase)
    {
        if (timeSpan == TimeSpan.MinValue)
        {
            return AV_NOPTS_VALUE;
        }

        return av_rescale_q(timeSpan.Ticks,
            new AVRational(1, (int)TimeSpan.TicksPerSecond),
            timeBase);
    }

    public static unsafe void CopyToAvFrame(this RawFrame rawFrame, Frame avFrame)
    {
        var src = (byte*)rawFrame.Data.ToPointer();
        var srcLength = (long)rawFrame.Length;
        for (var i = 0; i < 8; i++)
        {
            if (srcLength <= 0 || avFrame.Linesize[i] == 0)
            {
                continue;
            }

            Buffer.MemoryCopy(
                src,
                avFrame.Data[i].ToPointer(),
                avFrame.Linesize[i],
                Math.Min(srcLength, avFrame.Linesize[i]));

            src += avFrame.Linesize[i];
            srcLength -= avFrame.Linesize[i];
        }
    }

    public static unsafe AVFrame* ToAvFrame(this RawAudioFrame frame)
    {
        var avFrame = av_frame_alloc();
        if (avFrame == null)
        {
            throw new OutOfMemoryException("Cannot allocate frame.");
        }

        avFrame->sample_rate = frame.Format.SampleRate;
        avFrame->format = (int)frame.Format.ToAvSampleFormat();
        avFrame->ch_layout = frame.Format.ToAvChannelLayout();
        avFrame->nb_samples = frame.SampleCount;
        var ret = av_frame_get_buffer(avFrame, 1);
        if (ret < 0)
        {
            av_frame_free(&avFrame);
            throw FFmpegException.FromErrorCode(ret, "Cannot allocate frame buffer.");
        }

        var src = (byte*)frame.Data.ToPointer();
        var srcLength = (long)frame.Length;
        for (var i = 0; i < frame.Format.ChannelCount; i++)
        {
            Buffer.MemoryCopy(
                src,
                avFrame->data[i].ToPointer(),
                avFrame->linesize[i],
                Math.Min(srcLength, avFrame->linesize[i]));

            src += avFrame->linesize[i];
            srcLength -= avFrame->linesize[i];
        }

        return avFrame;
    }

    public static int GetTotalLineSize(this Frame frame)
    {
        var totalSize = 0;
        for (var i = 0; i < 8; i++)
        {
            totalSize += frame.Linesize[i];
        }

        return totalSize;
    }

    public static Fraction ToFraction(this AVRational rational)
    {
        if (rational.Num <= 0 || rational.Den <= 0)
        {
            return new Fraction(0, 0);
        }
        
        return new Fraction((uint)rational.Num, (uint)rational.Den);   
    }
}